using System;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.ScriptableObjects;
using AdaptiveBossArena.Learning;
using UnityEngine;

namespace AdaptiveBossArena.AI
{
    /// <summary>Everything that changes about the boss when it enters a new phase.</summary>
    /// <remarks>
    /// Phases escalate by widening the repertoire and raising the adaptation rate, never by handing
    /// the boss information it did not have. A phase-three boss reacts faster and commits harder,
    /// but it still sees only what a phase-one boss saw.
    /// </remarks>
    [Serializable]
    public struct BossPhaseDefinition
    {
        [SerializeField]
        [Tooltip("Name shown in debug tooling.")]
        private string _name;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Health fraction at or below which this phase begins.")]
        private float _healthThreshold;

        [SerializeField]
        [Tooltip("Attacks available during this phase.")]
        private AttackDefinition[] _attacks;

        [SerializeField]
        [Tooltip("The phase's signature attack, weighted up in selection so each phase has a " +
                 "recognisable, readable threat. Optional; leave empty for no signature.")]
        private AttackDefinition _signatureAttack;

        [SerializeField]
        [Range(0.5f, 2f)]
        [Tooltip("Multiplier on movement speed for this phase.")]
        private float _moveSpeedMultiplier;

        [SerializeField]
        [Range(0f, 3f)]
        [Tooltip("Pause between attacks. Lower values make the phase more relentless.")]
        private float _attackCooldownSeconds;

        [SerializeField]
        [Range(0.5f, 3f)]
        [Tooltip("Multiplier on how quickly the boss adopts new counter-strategies in this phase.")]
        private float _adaptationRateMultiplier;

        /// <summary>Name shown in debug tooling.</summary>
        public string Name => _name;

        /// <summary>Health fraction at or below which this phase begins.</summary>
        public float HealthThreshold => _healthThreshold;

        /// <summary>Attacks available during this phase.</summary>
        public AttackDefinition[] Attacks => _attacks;

        /// <summary>The phase's signature attack, weighted up in selection, or null for none.</summary>
        public AttackDefinition SignatureAttack => _signatureAttack;

        /// <summary>Movement speed multiplier for this phase.</summary>
        public float MoveSpeedMultiplier => _moveSpeedMultiplier;

        /// <summary>Pause between attacks during this phase.</summary>
        public float AttackCooldownSeconds => _attackCooldownSeconds;

        /// <summary>Adaptation rate multiplier for this phase.</summary>
        public float AdaptationRateMultiplier => _adaptationRateMultiplier;
    }

    /// <summary>
    /// Every tunable value describing the boss, its phases and its capacity to learn.
    /// </summary>
    /// <remarks>
    /// The baseline tuning values are the boss's behaviour before it has learned anything. The
    /// adaptation system only ever eases these toward new targets, so this asset also defines what
    /// the boss reverts to when a habit it was countering disappears.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "BossConfig",
        menuName = "Adaptive Boss Arena/AI/Boss Config",
        order = 1)]
    public sealed class BossConfig : IdentifiableSO
    {
        [Header("Vitals")]
        [SerializeField]
        [Tooltip("Maximum health.")]
        private float _maxHealth = 650f;

        [SerializeField]
        [Tooltip("Poise capacity. Enough accumulated poise damage staggers the boss and opens a punish.")]
        private float _maxPoise = 100f;

        [SerializeField]
        [Tooltip("Poise restored per second while not being hit.")]
        private float _poiseRegenPerSecond = 12f;

        [Header("Movement")]
        [SerializeField]
        [Tooltip("Base movement speed in world units per second.")]
        private float _moveSpeed = 5f;

        [SerializeField]
        [Tooltip("Turn rate in degrees per second. The main lever on how easily the boss is circled.")]
        private float _turnSpeedDegreesPerSecond = 220f;

        [Header("Perception")]
        [SerializeField]
        [Tooltip("Reaction and perception limits. Required; without it the boss would act on the " +
                 "current frame and read as a cheat.")]
        private ReactionProfile _reactionProfile;

        [Header("Phases")]
        [SerializeField]
        [Tooltip("Phase definitions in order, from full health downward.")]
        private BossPhaseDefinition[] _phases = new BossPhaseDefinition[0];

        [Header("Set Pieces")]
        [SerializeField]
        [Tooltip("The telegraphed unblockable shockwave the boss unleashes on a phase transition and " +
                 "as its Resolve gambit. Optional; without it those beats are skipped.")]
        private AttackDefinition _phaseShockwave;

        [Header("Learning")]
        [SerializeField]
        [Tooltip("Counter-strategies the boss may develop over the course of a fight.")]
        private CounterStrategy[] _counterStrategies = new CounterStrategy[0];

        [SerializeField]
        [Range(2f, 60f)]
        [Tooltip("How often the behaviour profile is recomputed. Coarse on purpose, so the boss " +
                 "responds to trends rather than to individual actions.")]
        private float _analysisIntervalSeconds = 2.5f;

        [SerializeField]
        [Range(5f, 60f)]
        [Tooltip("Minimum gap between adaptations taking effect, giving the player time to notice " +
                 "and respond to each one.")]
        private float _minSecondsBetweenAdaptations = 12f;

        [SerializeField]
        [Range(10f, 180f)]
        [Tooltip("Half-life of the boss's memory. Habits the player abandons fade over this period, " +
                 "which is what makes switching tactics a viable answer to being countered.")]
        private float _memoryHalfLifeSeconds = 45f;

        [Header("Resolve (Gambit)")]
        [SerializeField]
        [Tooltip("Resolve the boss must build before it unleashes its gambit set-piece.")]
        private float _resolveThreshold = 100f;

        [SerializeField]
        [Tooltip("Resolve gained per second of fighting.")]
        private float _resolveBuildPerSecond = 3.2f;

        [SerializeField]
        [Tooltip("Resolve gained each time one of the boss's own attacks whiffs — an evasive player " +
                 "winds the gambit up faster.")]
        private float _resolveBuildPerWhiff = 14f;

        [Header("Baseline Tuning")]
        [SerializeField]
        [Tooltip("Distance the boss prefers to hold before it has learned anything.")]
        private float _baselinePreferredRange = 3f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Willingness to press the attack before it has learned anything.")]
        private float _baselineAggression = 0.5f;

        /// <summary>Maximum health.</summary>
        public float MaxHealth => _maxHealth;

        /// <summary>Poise capacity.</summary>
        public float MaxPoise => _maxPoise;

        /// <summary>Poise restored per second.</summary>
        public float PoiseRegenPerSecond => _poiseRegenPerSecond;

        /// <summary>Base movement speed.</summary>
        public float MoveSpeed => _moveSpeed;

        /// <summary>Turn rate in degrees per second.</summary>
        public float TurnSpeedDegreesPerSecond => _turnSpeedDegreesPerSecond;

        /// <summary>Reaction and perception limits.</summary>
        public ReactionProfile ReactionProfile => _reactionProfile;

        /// <summary>Phase definitions in order.</summary>
        public BossPhaseDefinition[] Phases => _phases;

        /// <summary>The telegraphed unblockable shockwave for phase transitions and the gambit, or null.</summary>
        public AttackDefinition PhaseShockwave => _phaseShockwave;

        /// <summary>Counter-strategies available to the boss.</summary>
        public CounterStrategy[] CounterStrategies => _counterStrategies;

        /// <summary>How often the behaviour profile is recomputed.</summary>
        public float AnalysisIntervalSeconds => _analysisIntervalSeconds;

        /// <summary>Minimum gap between adaptations.</summary>
        public float MinSecondsBetweenAdaptations => _minSecondsBetweenAdaptations;

        /// <summary>Half-life of the boss's behavioural memory.</summary>
        public float MemoryHalfLifeSeconds => _memoryHalfLifeSeconds;

        /// <summary>Resolve required before the gambit is ready.</summary>
        public float ResolveThreshold => _resolveThreshold;

        /// <summary>Resolve gained per second of fighting.</summary>
        public float ResolveBuildPerSecond => _resolveBuildPerSecond;

        /// <summary>Resolve gained each time one of the boss's attacks whiffs.</summary>
        public float ResolveBuildPerWhiff => _resolveBuildPerWhiff;

        /// <summary>Preferred range before adaptation.</summary>
        public float BaselinePreferredRange => _baselinePreferredRange;

        /// <summary>Aggression before adaptation.</summary>
        public float BaselineAggression => _baselineAggression;

        /// <summary>
        /// Resolves which phase applies at a given health fraction.
        /// </summary>
        /// <param name="normalizedHealth">Boss health as a fraction of maximum.</param>
        /// <returns>Zero-based phase index, clamped to the configured range.</returns>
        public int ResolvePhaseIndex(float normalizedHealth)
        {
            if (_phases == null || _phases.Length == 0)
            {
                return 0;
            }

            // Phases are ordered from full health downward, so the last one whose threshold has been
            // crossed is the active one.
            int index = 0;
            for (int i = 0; i < _phases.Length; i++)
            {
                if (normalizedHealth <= _phases[i].HealthThreshold)
                {
                    index = i;
                }
            }

            return index;
        }
    }
}
