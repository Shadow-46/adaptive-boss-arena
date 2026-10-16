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

        /// <summary>The counters in force now, and how hard each is applied.</summary>
        private readonly Dictionary<CounterStrategy, float> _active = new Dictionary<CounterStrategy, float>();

        /// <summary>When each active counter's habit was last seen.</summary>
        private readonly Dictionary<CounterStrategy, float> _lastEligibleAt = new Dictionary<CounterStrategy, float>();

        private readonly List<CounterStrategy> _expired = new List<CounterStrategy>();
        private readonly List<CounterStrategy> _refreshed = new List<CounterStrategy>();
        private readonly float[] _strongestPull = new float[Enum.GetValues(typeof(BossTuningParameter)).Length];
        private readonly float[] _otherPulls = new float[Enum.GetValues(typeof(BossTuningParameter)).Length];

        /// <summary>
        /// How long a counter stays in force after its habit was last seen.
        /// </summary>
        /// <remarks>
        /// Long enough that one fight's worth of a habit is not forgotten in a lull; short enough that a player
        /// who has truly changed sees the counter go within the half-minute - which is the whole promise of
        /// "change your habits and it changes its answer".
        /// </remarks>
        public const float GraceSeconds = 20f;

        /// <summary>
        /// The share of every counter but the strongest that a shared parameter takes on.
        /// </summary>
        /// <remarks>
        /// Two counters raising aggression press harder than one, but not twice as hard: the strongest sets
        /// the pace and the rest add a quarter each, so stacking reads as escalation rather than a jump.
        /// </remarks>
        public const float StackShare = 0.25f;

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

        /// <summary>Which way the player habitually circles the boss: one to its right, minus one to its left.</summary>
        public float CirclingSide => _tracker.CirclingSide;

        /// <summary>The way the player most often rolls, relative to the boss: across its right, along away.</summary>
        /// <param name="direction">Receives the direction.</param>
        /// <returns>False when there are too few rolls to say.</returns>
        public bool TryGetHabitualDodge(out Vector2 direction) => _tracker.TryGetHabitualDodge(out direction);

        /// <summary>Strategies the boss has developed so far this fight.</summary>
        public IReadOnlyCollection<CounterStrategy> AdoptedStrategies => _adopted;

        /// <summary>The counters in force now: adopted, and their habit still seen within the grace.</summary>
        public IReadOnlyCollection<CounterStrategy> ActiveStrategies => _active.Keys;

        /// <summary>How hard an active counter is applied now, or zero when it is not in force.</summary>
        /// <param name="strategy">The counter.</param>
        /// <returns>Its strength, zero to one.</returns>
        public float StrengthOf(CounterStrategy strategy) =>
            strategy != null && _active.TryGetValue(strategy, out float strength) ? strength : 0f;

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
        /// Advances the learning loop from what the boss perceives of the player this frame.
        /// </summary>
        /// <remarks>
        /// The observation is the delayed one every behaviour sees: learning is never fresher than what the
        /// boss could act on.
        /// </remarks>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        /// <param name="timeNow">Current combat-clock time.</param>
        /// <param name="bossPosition">Where the boss stands.</param>
        /// <param name="observed">The player as the boss perceives them.</param>
        /// <param name="currentPhase">One-based phase currently in effect.</param>
        public void Tick(
            float deltaTime,
            float timeNow,
            Vector3 bossPosition,
            in Core.Perception.PlayerObservation observed,
            int currentPhase)
        {
            _tracker.Observe(bossPosition, observed, deltaTime);

            float separation = observed.IsValid
                ? Vector3.ProjectOnPlane(observed.Position - bossPosition, Vector3.up).magnitude
                : 0f;

            Tick(deltaTime, timeNow, separation, currentPhase);
        }

        /// <summary>
        /// Advances the learning loop from distance alone.
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

            RefreshActive(timeNow, currentPhase);
            TryAdapt(timeNow, currentPhase);
            ApplyTargets();
        }

        /// <summary>
        /// Keeps each active counter in force while its habit is still seen, at the strength the habit now
        /// earns, and lets it go once the habit has been gone for the grace.
        /// </summary>
        /// <remarks>
        /// Counters used to be adopted once and then drain away with the memory half-life, so the boss forgot
        /// its own answer while the player kept the habit. Now the answer holds exactly as long as the habit,
        /// and through a lull it keeps the strength it last had.
        /// </remarks>
        private void RefreshActive(float timeNow, int currentPhase)
        {
            _expired.Clear();
            _refreshed.Clear();

            foreach (CounterStrategy strategy in _active.Keys)
            {
                FeatureReading reading = _profile.Get(strategy.TriggerFeature);

                if (strategy.IsEligible(reading.Value, reading.Confidence, currentPhase))
                {
                    _refreshed.Add(strategy);
                }
                else if (timeNow - _lastEligibleAt[strategy] > GraceSeconds)
                {
                    _expired.Add(strategy);
                }
            }

            foreach (CounterStrategy strategy in _expired)
            {
                _active.Remove(strategy);
                _lastEligibleAt.Remove(strategy);
            }

            foreach (CounterStrategy strategy in _refreshed)
            {
                FeatureReading reading = _profile.Get(strategy.TriggerFeature);
                _active[strategy] = strategy.Strength(reading.Value, reading.Confidence);
                _lastEligibleAt[strategy] = timeNow;
            }
        }

        /// <summary>
        /// Points every tuning value at the target the counters in force add up to.
        /// </summary>
        /// <remarks>
        /// Recomputed from all of them each analysis, so the order counters were adopted in never matters and
        /// one never overwrites another: the strongest pull on a parameter sets it, the rest add a share.
        /// Targets are set, not values; the boss eases toward them.
        /// </remarks>
        private void ApplyTargets()
        {
            Array.Clear(_strongestPull, 0, _strongestPull.Length);
            Array.Clear(_otherPulls, 0, _otherPulls.Length);

            foreach (KeyValuePair<CounterStrategy, float> active in _active)
            {
                if (active.Key.Adjustments == null)
                {
                    continue;
                }

                foreach (TuningAdjustment adjustment in active.Key.Adjustments)
                {
                    int index = (int)adjustment.Parameter;
                    float pull = (adjustment.TargetValue - _tuning.GetBaseline(adjustment.Parameter)) * active.Value;

                    if (Mathf.Abs(pull) > Mathf.Abs(_strongestPull[index]))
                    {
                        _otherPulls[index] += _strongestPull[index];
                        _strongestPull[index] = pull;
                    }
                    else
                    {
                        _otherPulls[index] += pull;
                    }
                }
            }

            for (int i = 0; i < _strongestPull.Length; i++)
            {
                var parameter = (BossTuningParameter)i;
                _tuning.SetTarget(parameter, _tuning.GetBaseline(parameter) + _strongestPull[i] + _otherPulls[i] * StackShare);
            }
        }

        /// <summary>Adopts a new strategy if the cooldown has elapsed and one is eligible.</summary>
        private void TryAdapt(float timeNow, int currentPhase)
        {
            if (timeNow < _nextAdaptationAllowedAt)
            {
                return;
            }

            // Only counters not already in force are candidates: one that lapsed when its habit did can be
            // adopted again if the habit returns, and says so again.
            CounterStrategy chosen = _selector.Select(_catalogue, _profile, currentPhase, _active.Keys);

            if (chosen == null)
            {
                return;
            }

            FeatureReading reading = _profile.Get(chosen.TriggerFeature);
            _active[chosen] = chosen.Strength(reading.Value, reading.Confidence);
            _lastEligibleAt[chosen] = timeNow;
            _currentEaseHalfLife = Mathf.Max(0.5f, chosen.EaseInSeconds * 0.5f);

            _adopted.Add(chosen);
            _nextAdaptationAllowedAt =
                timeNow + _minSecondsBetweenAdaptations / _adaptationRateMultiplier;

            StrategyAdopted?.Invoke(chosen);
        }

        /// <summary>A faded copy of everything this fight taught, for <see cref="SessionMemory"/>.</summary>
        /// <param name="share">The share to keep.</param>
        /// <returns>The snapshot.</returns>
        public AdaptationSnapshot TakeSnapshot(float share)
        {
            var strengths = new Dictionary<CounterStrategy, float>();
            var ordered = new List<KeyValuePair<CounterStrategy, float>>(_active);
            ordered.Sort((a, b) => b.Value.CompareTo(a.Value));
            var habits = new List<BehaviorFeature>();

            foreach (KeyValuePair<CounterStrategy, float> active in ordered)
            {
                strengths[active.Key] = active.Value * share;

                if (!habits.Contains(active.Key.TriggerFeature))
                {
                    habits.Add(active.Key.TriggerFeature);
                }
            }

            return new AdaptationSnapshot(
                _memory.CopyTallies(share), _tracker.CopyDodgeWeights(share), strengths, habits);
        }

        /// <summary>
        /// Opens a freshly reset fight with a remembered one: its read on the player and its answers, faded.
        /// </summary>
        /// <remarks>
        /// The remembered answers start in force, at their faded strength, and are held to the same rule as
        /// any other: seen again, they strengthen; not seen within the grace, they lapse. The boss is ready
        /// from the first second, but the player can still change its mind.
        /// </remarks>
        /// <param name="snapshot">What was remembered.</param>
        public void Recall(AdaptationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            _memory.Restore(snapshot.Tallies);
            _tracker.RestoreDodgeWeights(snapshot.DodgeWeights);
            _recognizer.Analyse(_tracker, _memory, _profile, 0f);

            foreach (KeyValuePair<CounterStrategy, float> remembered in snapshot.Strengths)
            {
                if (remembered.Key == null)
                {
                    continue;
                }

                _active[remembered.Key] = remembered.Value;
                _lastEligibleAt[remembered.Key] = 0f;
            }

            ApplyTargets();
            _tuning.SnapToTargets();
        }

        /// <summary>Clears everything learned, for use when a fight restarts.</summary>
        public void Reset()
        {
            _memory.Clear();
            _tracker.Reset();
            _profile.Clear();
            _tuning.ResetToBaseline();
            _adopted.Clear();
            _active.Clear();
            _lastEligibleAt.Clear();

            _nextAnalysisAt = _analysisIntervalSeconds;
            _nextAdaptationAllowedAt = _minSecondsBetweenAdaptations;
            _currentEaseHalfLife = DefaultEaseHalfLifeSeconds;
        }
    }
}
