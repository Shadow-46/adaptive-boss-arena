using System.Collections.Generic;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Utilities.Statistics;
using UnityEngine;

namespace AdaptiveBossArena.Learning
{
    /// <summary>
    /// Watches the fight and maintains the running statistics behind every habit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fed by the combat event bus and by what the boss perceives of the player each frame, through the
    /// same delayed perception every behaviour uses. It records; it does not decide. Nothing here ever causes
    /// the boss to act, which is what allows it to run every frame without reintroducing instant reactions.
    /// </para>
    /// <para>
    /// Every count and duration it keeps fades with the memory half-life, so the picture is of how the player
    /// fights now. A player who abandons a habit stops being countered for it, which is what makes changing
    /// tactics an actual answer rather than a hope.
    /// </para>
    /// <para>
    /// Movement is read relative to the boss, not the world. Rolling "left" means to the boss's left, so a
    /// player who always rolls the same way around it reads as predictable whichever way the arena faces -
    /// measured in world directions, a player circling the boss and always rolling the same way read as
    /// random.
    /// </para>
    /// </remarks>
    public sealed class BehaviorTracker
    {
        /// <summary>Window after a dodge within which an attack counts as a follow-up.</summary>
        private const float PostDodgeWindowSeconds = 0.6f;

        /// <summary>How far back the attack rate looks.</summary>
        /// <remarks>
        /// Long enough to span a few exchanges, so a player who attacks in bursts between dodges reads as
        /// the attacker they are; short enough that someone who stops swinging stops reading as one within
        /// the half-minute.
        /// </remarks>
        public const float AttackWindowSeconds = 20f;

        /// <summary>Attacks per second treated as relentless pressure when normalising.</summary>
        /// <remarks>
        /// One a second, averaged over the window, is a player swinging through nearly every opening. The old
        /// ceiling of two and a half was a button-mashing rate nobody sustains, which read real attackers as
        /// passive.
        /// </remarks>
        private const float RelentlessAttacksPerSecond = 1f;

        /// <summary>Inside this distance the player is pressed against the boss.</summary>
        public const float CrowdingDistance = 2.5f;

        /// <summary>Beyond this distance the player is keeping away rather than fighting.</summary>
        public const float KitingDistance = 6f;

        /// <summary>Within this distance a raised guard is a choice rather than a stray input.</summary>
        public const float GuardingDistance = 5f;

        /// <summary>Within this distance a heal is begun under the boss's nose.</summary>
        public const float PressureHealDistance = 4f;

        /// <summary>Sideways speed, around the boss, that counts as deliberately circling it.</summary>
        private const float CirclingSpeed = 1.5f;

        /// <summary>How often distance is sampled. Coarse; it feeds a trend, not a reaction.</summary>
        private const float DistanceSampleIntervalSeconds = 0.2f;

        private readonly CombatMemory _memory;
        private readonly DirectionHistogram _dashDirections = new DirectionHistogram();
        private readonly Queue<float> _recentAttacks = new Queue<float>();
        private readonly Ewma _approachTendency;
        private readonly float _memoryHalfLifeSeconds;

        private float _lastDodgeAt = float.NegativeInfinity;
        private float _lastDistanceSampleAt;
        private float _lastDistance = -1f;
        private float _windowElapsed;
        private int _distanceSamples;

        private bool _hasPositions;
        private Vector3 _bossPosition;
        private Vector3 _playerPosition;

        /// <summary>Creates a tracker.</summary>
        /// <param name="memory">Where witnessed occurrences are recorded.</param>
        /// <param name="memoryHalfLifeSeconds">
        /// Time for an unreinforced habit's influence to halve. The single most important knob on how
        /// stubborn the boss's picture of the player is.
        /// </param>
        public BehaviorTracker(CombatMemory memory, float memoryHalfLifeSeconds)
        {
            _memory = memory;
            _memoryHalfLifeSeconds = Mathf.Max(1f, memoryHalfLifeSeconds);
            _approachTendency = new Ewma(_memoryHalfLifeSeconds);
        }

        /// <summary>Dodge directions witnessed, relative to the boss, decayed over time.</summary>
        /// <remarks>Across is the boss's right, along is away from it.</remarks>
        public DirectionHistogram DashDirections => _dashDirections;

        /// <summary>Player attacks per second over the last <see cref="AttackWindowSeconds"/>.</summary>
        public float AttacksPerSecond =>
            _windowElapsed > 0f ? _recentAttacks.Count / Mathf.Min(_windowElapsed, AttackWindowSeconds) : 0f;

        /// <summary>Seconds of the attack window watched so far, which is how much the rate can be trusted.</summary>
        public float AttackWindowWatched => Mathf.Min(_windowElapsed, AttackWindowSeconds);

        /// <summary>Smoothed tendency to close rather than retreat, from minus one to one.</summary>
        public float ApproachTendency => _approachTendency.Value;

        /// <summary>Distance samples taken.</summary>
        public int DistanceSampleCount => _distanceSamples;

        /// <summary>
        /// Which way the player habitually circles the boss: one to its right, minus one to its left, zero
        /// for neither.
        /// </summary>
        public float CirclingSide
        {
            get
            {
                float right = _memory.Recent(CombatTally.CirclingRightSeconds);
                float left = _memory.Recent(CombatTally.CirclingLeftSeconds);
                return right + left > 0f ? Mathf.Clamp((right - left) / (right + left), -1f, 1f) : 0f;
            }
        }

        /// <summary>
        /// The way the player most often rolls, relative to the boss: across is its right, along is away.
        /// </summary>
        /// <param name="direction">Receives the direction, unit length.</param>
        /// <returns>False when there are too few rolls to say.</returns>
        public bool TryGetHabitualDodge(out Vector2 direction)
        {
            int bin = _dashDirections.DominantBin;

            if (bin < 0)
            {
                direction = Vector2.zero;
                return false;
            }

            float angle = bin * Mathf.PI * 2f / DirectionHistogram.BinCount;
            direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            return true;
        }

        /// <summary>A copy of the roll-direction bins, scaled, for carrying into another attempt.</summary>
        /// <param name="share">The share to keep.</param>
        /// <returns>The scaled weights.</returns>
        public float[] CopyDodgeWeights(float share)
        {
            var weights = new float[DirectionHistogram.BinCount];

            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = _dashDirections.Weight(i) * share;
            }

            return weights;
        }

        /// <summary>Adds remembered roll directions to this fight's.</summary>
        /// <param name="weights">Weights from <see cref="CopyDodgeWeights"/>.</param>
        public void RestoreDodgeWeights(float[] weights)
        {
            for (int i = 0; weights != null && i < weights.Length && i < DirectionHistogram.BinCount; i++)
            {
                _dashDirections.Add(DirectionHistogram.DirectionOfBin(i), weights[i]);
            }
        }

        /// <summary>Records a witnessed occurrence.</summary>
        /// <param name="combatEvent">What happened.</param>
        public void OnCombatEvent(in CombatEvent combatEvent)
        {
            _memory.Record(combatEvent);

            if (combatEvent.Actor != CombatantTeam.Player)
            {
                return;
            }

            switch (combatEvent.Kind)
            {
                case CombatEventKind.AttackStarted:
                    RecordPlayerAttack(combatEvent);
                    break;

                case CombatEventKind.DodgePerformed:
                    RecordPlayerDodge(combatEvent);
                    break;

                case CombatEventKind.HealStarted:
                    if (_lastDistance >= 0f && _lastDistance <= PressureHealDistance)
                    {
                        _memory.Add(CombatTally.PlayerHealsUnderPressure);
                    }

                    break;
            }
        }

        /// <summary>
        /// Takes in what the boss perceives of the player this frame: where they are, how they move, what
        /// they are doing.
        /// </summary>
        /// <remarks>
        /// The observation is the delayed one every behaviour sees, so nothing learned here is fresher than
        /// what the boss could act on anyway.
        /// </remarks>
        /// <param name="bossPosition">Where the boss stands.</param>
        /// <param name="observed">The player as the boss perceives them.</param>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        public void Observe(Vector3 bossPosition, in PlayerObservation observed, float deltaTime)
        {
            if (!observed.IsValid || deltaTime <= 0f)
            {
                return;
            }

            _hasPositions = true;
            _bossPosition = bossPosition;
            _playerPosition = observed.Position;

            Vector3 away = Flat(observed.Position - bossPosition);
            float distance = away.magnitude;

            if (distance < 0.01f)
            {
                return;
            }

            away /= distance;
            Vector3 right = Vector3.Cross(Vector3.up, away);

            if (distance <= GuardingDistance)
            {
                _memory.Add(CombatTally.NearSeconds, deltaTime);

                if (observed.ActionState == ObservableActionState.Guarding)
                {
                    _memory.Add(CombatTally.GuardingSeconds, deltaTime);
                }
            }

            float sideways = Vector3.Dot(Flat(observed.Velocity), right);

            if (distance <= KitingDistance && Mathf.Abs(sideways) >= CirclingSpeed)
            {
                _memory.Add(sideways > 0f ? CombatTally.CirclingRightSeconds : CombatTally.CirclingLeftSeconds, deltaTime);
            }
        }

        /// <summary>Samples continuous quantities and fades the picture toward neutral.</summary>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        /// <param name="timeNow">Current combat-clock time.</param>
        /// <param name="separationDistance">Current distance between the combatants.</param>
        public void Tick(float deltaTime, float timeNow, float separationDistance)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            _windowElapsed += deltaTime;
            _dashDirections.Decay(_memoryHalfLifeSeconds, deltaTime);
            _memory.Fade(_memoryHalfLifeSeconds, deltaTime);

            while (_recentAttacks.Count > 0 && timeNow - _recentAttacks.Peek() > AttackWindowSeconds)
            {
                _recentAttacks.Dequeue();
            }

            if (timeNow - _lastDistanceSampleAt < DistanceSampleIntervalSeconds)
            {
                return;
            }

            float sampleInterval = timeNow - _lastDistanceSampleAt;
            _lastDistanceSampleAt = timeNow;

            SampleSeparation(separationDistance, sampleInterval);
        }

        /// <summary>Folds a distance reading into where the player chooses to stand.</summary>
        private void SampleSeparation(float separationDistance, float sampleInterval)
        {
            if (separationDistance <= 0f || float.IsInfinity(separationDistance))
            {
                return;
            }

            _distanceSamples++;
            _memory.Add(CombatTally.ObservedSeconds, sampleInterval);

            if (separationDistance <= CrowdingDistance)
            {
                _memory.Add(CombatTally.CrowdingSeconds, sampleInterval);
            }
            else if (separationDistance >= KitingDistance)
            {
                _memory.Add(CombatTally.KitingSeconds, sampleInterval);
            }

            if (_lastDistance >= 0f)
            {
                // Positive when the gap is closing. This is the observable basis for calling a player
                // aggressive: not that they attack often, but that they choose to be close.
                float closing = Mathf.Clamp(_lastDistance - separationDistance, -1f, 1f);
                _approachTendency.AddSample(closing, sampleInterval);
            }

            _lastDistance = separationDistance;
        }

        /// <summary>Records an attack, noting whether it followed a dodge.</summary>
        private void RecordPlayerAttack(in CombatEvent combatEvent)
        {
            _recentAttacks.Enqueue(combatEvent.Timestamp);

            if (combatEvent.Timestamp - _lastDodgeAt <= PostDodgeWindowSeconds)
            {
                _memory.Add(CombatTally.PostDodgeAttacks);

                // Consumed so one dodge cannot be credited with several follow-ups.
                _lastDodgeAt = float.NegativeInfinity;
            }
        }

        /// <summary>Records a dodge and its direction relative to the boss.</summary>
        private void RecordPlayerDodge(in CombatEvent combatEvent)
        {
            _lastDodgeAt = combatEvent.Timestamp;

            Vector3 dodge = Flat(combatEvent.Direction);

            if (dodge.sqrMagnitude < 1e-6f)
            {
                return;
            }

            if (!_hasPositions)
            {
                // Nothing perceived yet to measure against: fall back to the world frame.
                _dashDirections.Add(new Vector2(dodge.x, dodge.z));
                return;
            }

            Vector3 from = combatEvent.Position.sqrMagnitude > 0f ? combatEvent.Position : _playerPosition;
            Vector3 away = Flat(from - _bossPosition);

            if (away.sqrMagnitude < 1e-4f)
            {
                return;
            }

            away.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, away);
            _dashDirections.Add(new Vector2(Vector3.Dot(dodge, right), Vector3.Dot(dodge, away)));
        }

        /// <summary>Normalises the attack rate against relentless pressure.</summary>
        /// <returns>Attack frequency from zero to one.</returns>
        public float NormalizedAttackFrequency() => Mathf.Clamp01(AttacksPerSecond / RelentlessAttacksPerSecond);

        /// <summary>Maps the approach tendency onto the zero-to-one range.</summary>
        /// <returns>Aggression from zero, meaning retreating, to one, meaning pressuring.</returns>
        public float NormalizedAggression() => Mathf.Clamp01(_approachTendency.Value * 0.5f + 0.5f);

        /// <summary>Clears every statistic, for use when a fight restarts.</summary>
        public void Reset()
        {
            _dashDirections.Reset();
            _recentAttacks.Clear();
            _approachTendency.Reset();

            _lastDodgeAt = float.NegativeInfinity;
            _lastDistanceSampleAt = 0f;
            _lastDistance = -1f;
            _windowElapsed = 0f;
            _distanceSamples = 0;
            _hasPositions = false;
        }

        private static Vector3 Flat(Vector3 vector) => new Vector3(vector.x, 0f, vector.z);
    }
}
