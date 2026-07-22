using System;
using System.Collections.Generic;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Learning
{
    /// <summary>
    /// Owns the whole learning loop and decides when the boss is allowed to change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pacing authority. Analysis runs on an interval, adaptations are separated by a cooldown,
    /// and each one eases in over several seconds. Those three limits exist for the same reason: an
    /// adaptive boss that changes quickly is indistinguishable from one that is cheating, and the
    /// player has to be given room to notice a change and answer it before the next arrives.
    /// </para>
    /// <para>
    /// Every adopted strategy announces itself through <see cref="StrategyAdopted"/>. This is not
    /// presentation polish; it is what makes the mechanic legible. An adaptation the player cannot
    /// perceive is one they will attribute to the boss reading their input.
    /// </para>
    /// <para>
    /// The class is deliberately free of engine types beyond <c>Mathf</c>, so a whole fight's worth
    /// of adaptation can be simulated in an edit-mode test by feeding it synthetic events and
    /// stepping its clock.
    /// </para>
    /// </remarks>
    public sealed class AdaptationManager
    {
        private readonly CombatMemory _memory;
        private readonly BehaviorTracker _tracker;
        private readonly PatternRecognizer _recognizer;
        private readonly CounterSelector _selector;
        private readonly BehaviorProfile _profile;
        private readonly BossTuning _tuning;

        private readonly IReadOnlyList<CounterStrategy> _catalogue;
        private readonly HashSet<CounterStrategy> _adopted = new HashSet<CounterStrategy>();

        private readonly float _analysisIntervalSeconds;
        private readonly float _minSecondsBetweenAdaptations;
        private readonly float _forgetHalfLifeSeconds;

        private float _nextAnalysisAt;
        private float _nextAdaptationAllowedAt;
        private float _adaptationRateMultiplier = 1f;
        private float _currentEaseHalfLife = DefaultEaseHalfLifeSeconds;

        /// <summary>Ease half-life used before any strategy has been adopted.</summary>
        private const float DefaultEaseHalfLifeSeconds = 3f;

        /// <summary>Creates a manager wired to the boss's learning components.</summary>
        /// <param name="memory">Where witnessed occurrences are recorded.</param>
        /// <param name="tracker">Running statistics.</param>
        /// <param name="profile">The habits derived from those statistics.</param>
        /// <param name="tuning">Behaviour numbers this manager adjusts.</param>
        /// <param name="catalogue">Every strategy the boss could develop.</param>
        /// <param name="random">Seedable randomness for strategy selection.</param>
        /// <param name="analysisIntervalSeconds">How often habits are recomputed.</param>
        /// <param name="minSecondsBetweenAdaptations">Shortest gap between adaptations taking effect.</param>
        /// <param name="forgetHalfLifeSeconds">Time for an unreinforced adjustment to half-decay.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is missing.</exception>
        public AdaptationManager(
            CombatMemory memory,
            BehaviorTracker tracker,
            BehaviorProfile profile,
            BossTuning tuning,
            IReadOnlyList<CounterStrategy> catalogue,
            IRandomProvider random,
            float analysisIntervalSeconds,
            float minSecondsBetweenAdaptations,
            float forgetHalfLifeSeconds)
        {
            _memory = memory ?? throw new ArgumentNullException(nameof(memory));
            _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _catalogue = catalogue;

            _recognizer = new PatternRecognizer();
            _selector = new CounterSelector(random ?? throw new ArgumentNullException(nameof(random)));

            _analysisIntervalSeconds = Mathf.Max(0.5f, analysisIntervalSeconds);
            _minSecondsBetweenAdaptations = Mathf.Max(0f, minSecondsBetweenAdaptations);
            _forgetHalfLifeSeconds = Mathf.Max(1f, forgetHalfLifeSeconds);

            // The boss must not adapt in the opening seconds, before it has seen anything worth
            // adapting to.
            _nextAnalysisAt = _analysisIntervalSeconds;
            _nextAdaptationAllowedAt = _minSecondsBetweenAdaptations;
        }

        /// <summary>The boss's current picture of the player.</summary>
        public BehaviorProfile Profile => _profile;

        /// <summary>Strategies the boss has developed so far this fight.</summary>
        public IReadOnlyCollection<CounterStrategy> AdoptedStrategies => _adopted;

        /// <summary>Combat-clock time habits were last recomputed.</summary>
        public float LastAnalysisAt => _profile.LastUpdatedAt;

        /// <summary>Raised when the boss develops a new answer, carrying its player-facing tell.</summary>
        public event Action<CounterStrategy> StrategyAdopted;

        /// <summary>Scales how readily the boss adapts, raised by later phases.</summary>
        /// <param name="multiplier">Multiplier on the adaptation rate.</param>
        public void SetAdaptationRateMultiplier(float multiplier) =>
            _adaptationRateMultiplier = Mathf.Max(0.1f, multiplier);

        /// <summary>Records a witnessed occurrence.</summary>
        /// <param name="combatEvent">What happened.</param>
        public void OnCombatEvent(in Core.Combat.CombatEvent combatEvent) =>
            _tracker.OnCombatEvent(combatEvent);

        /// <summary>
        /// Advances the learning loop.
        /// </summary>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        /// <param name="timeNow">Current combat-clock time.</param>
        /// <param name="separationDistance">Current distance between the combatants.</param>
        /// <param name="currentPhase">One-based phase currently in effect.</param>
        public void Tick(float deltaTime, float timeNow, float separationDistance, int currentPhase)
        {
            _tracker.Tick(deltaTime, timeNow, separationDistance);

            // Eased every frame regardless of whether an adaptation happened, so an adjustment
            // arrives gradually and an unreinforced one drains away just as gradually.
            _tuning.Tick(deltaTime, _currentEaseHalfLife, _forgetHalfLifeSeconds);

            if (timeNow < _nextAnalysisAt)
            {
                return;
            }

            _nextAnalysisAt = timeNow + _analysisIntervalSeconds / _adaptationRateMultiplier;
            _recognizer.Analyse(_tracker, _memory, _profile, timeNow);

            TryAdapt(timeNow, currentPhase);
        }

        /// <summary>Adopts a new strategy if the cooldown has elapsed and one is eligible.</summary>
        private void TryAdapt(float timeNow, int currentPhase)
        {
            if (timeNow < _nextAdaptationAllowedAt)
            {
                return;
            }

            CounterStrategy chosen = _selector.Select(_catalogue, _profile, currentPhase, _adopted);

            if (chosen == null)
            {
                return;
            }

            Apply(chosen);

            _adopted.Add(chosen);
            _nextAdaptationAllowedAt =
                timeNow + _minSecondsBetweenAdaptations / _adaptationRateMultiplier;

            StrategyAdopted?.Invoke(chosen);
        }

        /// <summary>Points the affected tuning values at their new targets.</summary>
        /// <remarks>
        /// Targets are set, not values. The boss does not become different at this instant; it
        /// begins becoming different, over the strategy's own ease-in time.
        /// </remarks>
        private void Apply(CounterStrategy strategy)
        {
            _currentEaseHalfLife = Mathf.Max(0.5f, strategy.EaseInSeconds * 0.5f);

            TuningAdjustment[] adjustments = strategy.Adjustments;

            if (adjustments == null)
            {
                return;
            }

            foreach (TuningAdjustment adjustment in adjustments)
            {
                _tuning.SetTarget(adjustment.Parameter, adjustment.TargetValue);
            }
        }

        /// <summary>Clears everything learned, for use when a fight restarts.</summary>
        public void Reset()
        {
            _memory.Clear();
            _tracker.Reset();
            _profile.Clear();
            _tuning.ResetToBaseline();
            _adopted.Clear();

            _nextAnalysisAt = _analysisIntervalSeconds;
            _nextAdaptationAllowedAt = _minSecondsBetweenAdaptations;
            _currentEaseHalfLife = DefaultEaseHalfLifeSeconds;
        }
    }
}
