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

        /// <summary>Boss attacks that connected.</summary>
        public int BossAttacksLanded { get; private set; }

        /// <summary>Boss attacks that the player avoided.</summary>
        public int BossAttacksEvaded { get; private set; }

        /// <summary>Boss attacks that touched nothing.</summary>
        public int BossAttacksWhiffed { get; private set; }

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

                    if (combatEvent.DamageType == DamageType.Heavy)
                    {
                        PlayerHeavyAttacksThrown++;
                    }

                    break;

                case CombatEventKind.AttackWhiffed:
                    PlayerAttacksWhiffed++;
                    break;

                case CombatEventKind.DodgePerformed:
                    PlayerDodges++;
                    break;

                case CombatEventKind.PerfectDodge:
                    PlayerPerfectDodges++;
                    break;

                case CombatEventKind.HealStarted:
                    PlayerHealsStarted++;
                    break;
            }
        }

        private void RecordBossEvent(in CombatEvent combatEvent)
        {
            switch (combatEvent.Kind)
            {
                case CombatEventKind.AttackLanded:
                    BossAttacksLanded++;
                    break;

                case CombatEventKind.AttackEvaded:
                    BossAttacksEvaded++;
                    break;

                case CombatEventKind.AttackWhiffed:
                    BossAttacksWhiffed++;
                    break;
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

            PlayerAttacksThrown = 0;
            PlayerHeavyAttacksThrown = 0;
            PlayerAttacksWhiffed = 0;
            PlayerDodges = 0;
            PlayerPerfectDodges = 0;
            PlayerHealsStarted = 0;
            BossAttacksLanded = 0;
            BossAttacksEvaded = 0;
            BossAttacksWhiffed = 0;
        }
    }
}
