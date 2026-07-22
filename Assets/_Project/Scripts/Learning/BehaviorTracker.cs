using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Utilities;
using AdaptiveBossArena.Utilities.Statistics;
using UnityEngine;

namespace AdaptiveBossArena.Learning
{
    /// <summary>
    /// Watches the fight and maintains the running statistics behind every habit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Subscribes to the combat event bus and samples distance on a slow cadence. It records; it
    /// does not decide. Nothing here ever causes the boss to act, which is what allows it to run
    /// every frame without reintroducing instant reactions.
    /// </para>
    /// <para>
    /// Two mechanisms make its picture fade. Continuous quantities use exponentially weighted
    /// averages with half-lives, and the dash-direction histogram decays on the same schedule. A
    /// player who abandons a habit stops being countered for it, which is what makes changing
    /// tactics an actual answer rather than a hope.
    /// </para>
    /// </remarks>
    public sealed class BehaviorTracker
    {
        /// <summary>Window after a dodge within which an attack counts as a follow-up.</summary>
        private const float PostDodgeWindowSeconds = 0.6f;

        /// <summary>Attacks per second treated as the practical ceiling when normalising.</summary>
        private const float MaxSustainableAttacksPerSecond = 2.5f;

        /// <summary>Distance treated as the far end of the arena when normalising separation.</summary>
        private const float MaxMeaningfulDistance = 14f;

        /// <summary>How often distance is sampled. Coarse; it feeds a trend, not a reaction.</summary>
        private const float DistanceSampleIntervalSeconds = 0.2f;

        private readonly CombatMemory _memory;
        private readonly DirectionHistogram _dashDirections = new DirectionHistogram();

        private readonly Ewma _attackRate;
        private readonly Ewma _separation;
        private readonly Ewma _approachTendency;

        private readonly float _memoryHalfLifeSeconds;

        private float _lastDodgeAt = float.NegativeInfinity;
        private float _lastDistanceSampleAt;
        private float _lastDistance = -1f;
        private float _lastEventTime;

        private int _postDodgeAttacks;
        private int _dodgesFollowedByAnything;
        private int _distanceSamples;

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

            _attackRate = new Ewma(_memoryHalfLifeSeconds);
            _separation = new Ewma(_memoryHalfLifeSeconds);
            _approachTendency = new Ewma(_memoryHalfLifeSeconds);
        }

        /// <summary>Dash directions witnessed, decayed over time.</summary>
        public DirectionHistogram DashDirections => _dashDirections;

        /// <summary>Smoothed player attack rate, in attacks per second.</summary>
        public float AttacksPerSecond => _attackRate.Value;

        /// <summary>Smoothed separation between the combatants, in world units.</summary>
        public float AverageSeparation => _separation.Value;

        /// <summary>Smoothed tendency to close rather than retreat, from minus one to one.</summary>
        public float ApproachTendency => _approachTendency.Value;

        /// <summary>Dodges that were followed by an attack inside the follow-up window.</summary>
        public int PostDodgeAttacks => _postDodgeAttacks;

        /// <summary>Dodges that could have been followed by an attack.</summary>
        public int DodgeOpportunities => _dodgesFollowedByAnything;

        /// <summary>Distance samples taken.</summary>
        public int DistanceSampleCount => _distanceSamples;

        /// <summary>Records a witnessed occurrence.</summary>
        /// <param name="combatEvent">What happened.</param>
        public void OnCombatEvent(in CombatEvent combatEvent)
        {
            _memory.Record(combatEvent);

            float elapsed = Mathf.Max(0f, combatEvent.Timestamp - _lastEventTime);
            _lastEventTime = combatEvent.Timestamp;

            if (combatEvent.Actor != CombatantTeam.Player)
            {
                return;
            }

            switch (combatEvent.Kind)
            {
                case CombatEventKind.AttackStarted:
                    RecordPlayerAttack(combatEvent, elapsed);
                    break;

                case CombatEventKind.DodgePerformed:
                    RecordPlayerDodge(combatEvent);
                    break;
            }
        }

        /// <summary>Samples continuous quantities and decays the picture toward neutral.</summary>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        /// <param name="timeNow">Current combat-clock time.</param>
        /// <param name="separationDistance">Current distance between the combatants.</param>
        public void Tick(float deltaTime, float timeNow, float separationDistance)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            _dashDirections.Decay(_memoryHalfLifeSeconds, deltaTime);

            // Attack rate decays toward zero when nothing is happening, so a player who stops
            // pressuring stops being read as aggressive.
            _attackRate.DecayToward(0f, deltaTime);

            if (timeNow - _lastDistanceSampleAt < DistanceSampleIntervalSeconds)
            {
                return;
            }

            float sampleInterval = timeNow - _lastDistanceSampleAt;
            _lastDistanceSampleAt = timeNow;

            SampleSeparation(separationDistance, sampleInterval);
        }

        /// <summary>Folds a distance reading into the separation and approach averages.</summary>
        private void SampleSeparation(float separationDistance, float sampleInterval)
        {
            if (separationDistance <= 0f || float.IsInfinity(separationDistance))
            {
                return;
            }

            _separation.AddSample(separationDistance, sampleInterval);
            _distanceSamples++;

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
        private void RecordPlayerAttack(in CombatEvent combatEvent, float elapsed)
        {
            float instantaneousRate = elapsed > 0f ? 1f / elapsed : MaxSustainableAttacksPerSecond;
            _attackRate.AddSample(
                Mathf.Min(instantaneousRate, MaxSustainableAttacksPerSecond), elapsed);

            if (combatEvent.Timestamp - _lastDodgeAt <= PostDodgeWindowSeconds)
            {
                _postDodgeAttacks++;

                // Consumed so one dodge cannot be credited with several follow-ups.
                _lastDodgeAt = float.NegativeInfinity;
            }
        }

        /// <summary>Records a dodge and its direction.</summary>
        private void RecordPlayerDodge(in CombatEvent combatEvent)
        {
            _lastDodgeAt = combatEvent.Timestamp;
            _dodgesFollowedByAnything++;

            _dashDirections.Add(MathUtil.FlattenToPlane(combatEvent.Direction));
        }

        /// <summary>Normalises the attack rate against the practical ceiling.</summary>
        /// <returns>Attack frequency from zero to one.</returns>
        public float NormalizedAttackFrequency() =>
            Mathf.Clamp01(_attackRate.Value / MaxSustainableAttacksPerSecond);

        /// <summary>Normalises average separation against the arena's usable span.</summary>
        /// <returns>Preferred distance from zero, meaning in melee, to one, meaning kiting.</returns>
        public float NormalizedPreferredDistance() =>
            Mathf.Clamp01(_separation.Value / MaxMeaningfulDistance);

        /// <summary>Maps the approach tendency onto the zero-to-one range.</summary>
        /// <returns>Aggression from zero, meaning retreating, to one, meaning pressuring.</returns>
        public float NormalizedAggression() => Mathf.Clamp01(_approachTendency.Value * 0.5f + 0.5f);

        /// <summary>Clears every statistic, for use when a fight restarts.</summary>
        public void Reset()
        {
            _dashDirections.Reset();
            _attackRate.Reset();
            _separation.Reset();
            _approachTendency.Reset();

            _lastDodgeAt = float.NegativeInfinity;
            _lastDistanceSampleAt = 0f;
            _lastDistance = -1f;
            _lastEventTime = 0f;

            _postDodgeAttacks = 0;
            _dodgesFollowedByAnything = 0;
            _distanceSamples = 0;
        }
    }
}
