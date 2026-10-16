using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Utilities.Collections;

namespace AdaptiveBossArena.Learning
{
    /// <summary>
    /// The boss's recollection of what it has seen the player do.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A bounded ring buffer of witnessed occurrences plus a few running counts. Bounded because a
    /// fight of any length must cost the same memory and the same per-frame work, and because a boss
    /// that remembered the entire encounter equally would never let go of a habit the player
    /// abandoned five minutes ago.
    /// </para>
    /// <para>
    /// This type only records. It draws no conclusions; that is
    /// <see cref="PatternRecognizer"/>'s job. Keeping the two apart means the recording can run
    /// every frame while the interpretation runs every few seconds, which is precisely the
    /// separation that stops the boss reacting to individual actions.
    /// </para>
    /// </remarks>
    public sealed class CombatMemory
    {
        /// <summary>Roughly the last few hundred noteworthy occurrences of a fight.</summary>
        public const int DefaultCapacity = 256;

        private readonly CircularBuffer<CombatEvent> _events;

        /// <summary>
        /// A dodge made within this share of a wind-up was made before the blade could be judged.
        /// </summary>
        /// <remarks>
        /// Under half the wind-up, after the boss's own perception delay and a human reaction, is a guess at
        /// the timing rather than a read of it. Measured against each swing's own length, so a fast jab
        /// dodged as soon as it is seen never counts as early.
        /// </remarks>
        public const float EarlyDodgeShare = 0.45f;

        /// <summary>How long after a wind-up begins a dodge still counts as an answer to it.</summary>
        private const float SwingAnswerWindowSeconds = 1.6f;

        private readonly float[] _recent = new float[System.Enum.GetValues(typeof(CombatTally)).Length];

        private float _bossSwingStartedAt = float.NegativeInfinity;
        private float _bossSwingWindup;
        private bool _bossSwingResolved = true;

        /// <summary>Creates a memory with a fixed recall depth.</summary>
        /// <param name="capacity">Number of occurrences retained.</param>
        public CombatMemory(int capacity = DefaultCapacity) =>
            _events = new CircularBuffer<CombatEvent>(capacity);

        /// <summary>Occurrences currently retained, oldest first.</summary>
        public CircularBuffer<CombatEvent> Events => _events;

        /// <summary>Number of occurrences retained.</summary>
        public int Count => _events.Count;

        /// <summary>Total player attacks witnessed since the fight began.</summary>
        public int PlayerAttacksThrown { get; private set; }

        /// <summary>Player attacks that were heavy.</summary>
        public int PlayerHeavyAttacksThrown { get; private set; }

        /// <summary>Player attacks that touched nothing.</summary>
        public int PlayerAttacksWhiffed { get; private set; }

        /// <summary>Dodges witnessed.</summary>
        public int PlayerDodges { get; private set; }

        /// <summary>Dodges timed precisely enough to negate an attack outright.</summary>
        public int PlayerPerfectDodges { get; private set; }

        /// <summary>Heals the player has begun.</summary>
        public int PlayerHealsStarted { get; private set; }

        /// <summary>Times the player raised their guard.</summary>
        public int PlayerGuardsRaised { get; private set; }

        /// <summary>Attacks the player turned aside on the exact beat.</summary>
        public int PlayerDeflects { get; private set; }

        /// <summary>Attacks the player caught late on the guard, taking chip and posture.</summary>
        public int PlayerBlocks { get; private set; }

        /// <summary>Broken guards the player punished with a riposte.</summary>
        public int PlayerRipostes { get; private set; }

        /// <summary>Boss attacks that connected.</summary>
        public int BossAttacksLanded { get; private set; }

        /// <summary>Boss attacks that the player avoided.</summary>
        public int BossAttacksEvaded { get; private set; }

        /// <summary>Boss attacks that touched nothing.</summary>
        public int BossAttacksWhiffed { get; private set; }

        /// <summary>Player swings the boss met on the beat.</summary>
        /// <remarks>
        /// Statistics only, like everything here. Nothing the boss does may hang off this: the bus
        /// delivers immediately, so a behaviour wired to it would react without the perception delay
        /// that keeps the boss honest. If the boss should ever parry more against a player it keeps
        /// reading correctly, that has to ease a tuning parameter on the slow adaptation tick.
        /// </remarks>
        public int BossParries { get; private set; }

        /// <summary>A tally as it stands now, faded by how long ago each occurrence was.</summary>
        /// <param name="tally">Which tally.</param>
        /// <returns>The faded count or duration.</returns>
        public float Recent(CombatTally tally) => _recent[(int)tally];

        /// <summary>Adds to a faded tally. Used by the tracker for what it measures itself.</summary>
        /// <param name="tally">Which tally.</param>
        /// <param name="amount">How much, in occurrences or seconds.</param>
        public void Add(CombatTally tally, float amount = 1f) => _recent[(int)tally] += amount;

        /// <summary>A copy of every faded tally, scaled, for carrying into another attempt.</summary>
        /// <param name="share">The share to keep.</param>
        /// <returns>The scaled tallies.</returns>
        public float[] CopyTallies(float share)
        {
            var copy = new float[_recent.Length];

            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = _recent[i] * share;
            }

            return copy;
        }

        /// <summary>Adds remembered tallies to this fight's, at the start of an attempt.</summary>
        /// <param name="tallies">Tallies from <see cref="CopyTallies"/>.</param>
        public void Restore(float[] tallies)
        {
            for (int i = 0; i < _recent.Length && tallies != null && i < tallies.Length; i++)
            {
                _recent[i] += tallies[i];
            }
        }

        /// <summary>Fades every tally, so old habits weigh less than current ones.</summary>
        /// <param name="halfLifeSeconds">Time for an unreinforced tally to halve.</param>
        /// <param name="deltaTime">Elapsed time.</param>
        public void Fade(float halfLifeSeconds, float deltaTime)
        {
            if (halfLifeSeconds <= 0f || deltaTime <= 0f)
            {
                return;
            }

            float retained = UnityEngine.Mathf.Pow(0.5f, deltaTime / halfLifeSeconds);

            for (int i = 0; i < _recent.Length; i++)
            {
                _recent[i] *= retained;
            }
        }

        /// <summary>Records an occurrence and updates the running counts.</summary>
        /// <param name="combatEvent">The occurrence to remember.</param>
        public void Record(in CombatEvent combatEvent)
        {
            _events.Add(combatEvent);

            if (combatEvent.Actor == CombatantTeam.Player)
            {
                RecordPlayerEvent(combatEvent);
            }
            else if (combatEvent.Actor == CombatantTeam.Boss)
            {
                RecordBossEvent(combatEvent);
            }
        }

        private void RecordPlayerEvent(in CombatEvent combatEvent)
        {
            switch (combatEvent.Kind)
            {
                case CombatEventKind.AttackStarted:
                    PlayerAttacksThrown++;
                    Add(CombatTally.PlayerAttacks);

                    if (combatEvent.DamageType == DamageType.Heavy)
                    {
                        PlayerHeavyAttacksThrown++;
                        Add(CombatTally.PlayerHeavyAttacks);
                    }

                    break;

                case CombatEventKind.AttackWhiffed:
                    PlayerAttacksWhiffed++;
                    Add(CombatTally.PlayerWhiffs);
                    break;

                case CombatEventKind.DodgePerformed:
                    PlayerDodges++;
                    Add(CombatTally.PlayerDodges);
                    RecordDodgeTiming(combatEvent.Timestamp);
                    break;

                case CombatEventKind.PerfectDodge:
                    PlayerPerfectDodges++;
                    Add(CombatTally.PlayerPerfectDodges);
                    break;

                case CombatEventKind.HealStarted:
                    PlayerHealsStarted++;
                    Add(CombatTally.PlayerHeals);
                    break;

                // The defensive half of the fight. These four were published to the bus from the
                // start and counted by nothing, which left the boss unable to learn anything about
                // the deflect system the design calls its defining mechanic.
                case CombatEventKind.GuardRaised:
                    PlayerGuardsRaised++;
                    break;

                case CombatEventKind.Deflected:
                    PlayerDeflects++;
                    Add(CombatTally.PlayerDeflects);
                    _bossSwingResolved = true;
                    break;

                case CombatEventKind.Blocked:
                    PlayerBlocks++;
                    Add(CombatTally.PlayerBlocks);
                    _bossSwingResolved = true;
                    break;

                case CombatEventKind.Riposte:
                    PlayerRipostes++;
                    break;
            }
        }

        private void RecordBossEvent(in CombatEvent combatEvent)
        {
            switch (combatEvent.Kind)
            {
                case CombatEventKind.AttackStarted:
                    _bossSwingStartedAt = combatEvent.Timestamp;
                    _bossSwingWindup = combatEvent.WindupSeconds;
                    _bossSwingResolved = false;
                    break;

                // A swing is resolved once, however many events it raises. A dodged swing also ends
                // touching nothing and so reports a whiff; counted as both, every dodge twice inflated
                // the boss's read on how hard the player is to hit.
                case CombatEventKind.AttackLanded:
                    BossAttacksLanded++;
                    Add(CombatTally.BossLanded);
                    _bossSwingResolved = true;
                    break;

                case CombatEventKind.AttackEvaded:
                    BossAttacksEvaded++;
                    Add(CombatTally.BossEvaded);
                    _bossSwingResolved = true;
                    break;

                case CombatEventKind.AttackWhiffed:
                    if (!_bossSwingResolved)
                    {
                        BossAttacksWhiffed++;
                        Add(CombatTally.BossWhiffed);
                    }

                    _bossSwingResolved = true;
                    break;

                // Published by the attacker's executor with the defender as actor, so a swing the
                // boss parried arrives here rather than in the player's tally. Getting that backwards
                // would credit a parried player with a deflect and feed the deflect-mastery read a
                // number that never happened.
                case CombatEventKind.Parried:
                    BossParries++;
                    break;
            }
        }

        /// <summary>Judges a dodge against the wind-up it answered, if it answered one.</summary>
        private void RecordDodgeTiming(float dodgedAt)
        {
            float lead = dodgedAt - _bossSwingStartedAt;

            if (lead < 0f || lead > SwingAnswerWindowSeconds || _bossSwingWindup <= 0f)
            {
                return;
            }

            Add(CombatTally.DodgesIntoSwings);

            if (lead < _bossSwingWindup * EarlyDodgeShare)
            {
                Add(CombatTally.EarlyDodges);
            }
        }

        /// <summary>
        /// Finds the most recent occurrence of a kind by a given actor.
        /// </summary>
        /// <param name="kind">What to look for.</param>
        /// <param name="actor">Whose action to look for.</param>
        /// <param name="found">Receives the occurrence when one exists.</param>
        /// <returns>True when a matching occurrence was retained.</returns>
        public bool TryFindMostRecent(CombatEventKind kind, CombatantTeam actor, out CombatEvent found)
        {
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                CombatEvent candidate = _events[i];

                if (candidate.Kind == kind && candidate.Actor == actor)
                {
                    found = candidate;
                    return true;
                }
            }

            found = default;
            return false;
        }

        /// <summary>Clears every recollection, for use when a fight restarts.</summary>
        public void Clear()
        {
            _events.Clear();
            System.Array.Clear(_recent, 0, _recent.Length);
            _bossSwingStartedAt = float.NegativeInfinity;
            _bossSwingWindup = 0f;
            _bossSwingResolved = true;

            PlayerAttacksThrown = 0;
            PlayerHeavyAttacksThrown = 0;
            PlayerAttacksWhiffed = 0;
            PlayerDodges = 0;
            PlayerPerfectDodges = 0;
            PlayerHealsStarted = 0;
            PlayerGuardsRaised = 0;
            PlayerDeflects = 0;
            PlayerBlocks = 0;
            PlayerRipostes = 0;
            BossAttacksLanded = 0;
            BossAttacksEvaded = 0;
            BossAttacksWhiffed = 0;
            BossParries = 0;
        }
    }
}
