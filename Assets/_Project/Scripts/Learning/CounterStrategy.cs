using System;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.ScriptableObjects;
using UnityEngine;

namespace AdaptiveBossArena.Learning
{
    /// <summary>One adjustment a counter-strategy makes to the boss's behaviour.</summary>
    [Serializable]
    public struct TuningAdjustment
    {
        [SerializeField]
        [Tooltip("Behaviour to adjust.")]
        private BossTuningParameter _parameter;

        [SerializeField]
        [Tooltip("Value the parameter is eased toward while this strategy is active.")]
        private float _targetValue;

        /// <summary>Behaviour being adjusted.</summary>
        public BossTuningParameter Parameter => _parameter;

        /// <summary>Value the parameter is eased toward.</summary>
        public float TargetValue => _targetValue;
    }

    /// <summary>
    /// A single answer the boss can develop to a habit it has observed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Strategies are authored as assets, which keeps the boss's repertoire a design surface rather
    /// than a code branch. Adding "starts feinting against players who dodge on a fixed rhythm" is a
    /// new asset, not a new <c>if</c> in the AI.
    /// </para>
    /// <para>
    /// Selection is weighted and probabilistic rather than deterministic. If the strongest habit
    /// always produced the same counter, the player would learn the boss's lookup table and the
    /// fight would collapse into memorisation, which is the exact failure this design exists to
    /// avoid.
    /// </para>
    /// <para>
    /// The player-facing tell is not decoration. An adaptation the player cannot perceive is
    /// indistinguishable from the boss cheating, so every strategy is required to announce itself.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "CounterStrategy",
        menuName = "Adaptive Boss Arena/Learning/Counter Strategy",
        order = 0)]
    public sealed class CounterStrategy : IdentifiableSO
    {
        [Header("Trigger")]
        [SerializeField]
        [Tooltip("Player habit this strategy answers.")]
        private BehaviorFeature _triggerFeature = BehaviorFeature.AttackFrequency;

        [SerializeField]
        [Tooltip("Direction of the comparison against the threshold.")]
        private FeatureComparison _comparison = FeatureComparison.AtOrAbove;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Feature value at which this strategy becomes eligible.")]
        private float _threshold = 0.6f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Confidence the feature must reach before this strategy may be chosen. Guards " +
                 "against the boss reacting to a coincidence.")]
        private float _minimumConfidence = 0.5f;

        [Header("Selection")]
        [SerializeField]
        [Range(0.1f, 5f)]
        [Tooltip("Relative likelihood of being chosen when several strategies are eligible.")]
        private float _selectionWeight = 1f;

        [SerializeField]
        [Range(1, GameplayConstants.BossPhaseCount)]
        [Tooltip("Earliest boss phase in which this strategy may be adopted. The ceiling is the " +
                 "phase count, so a strategy may be reserved for Last Stand.")]
        private int _minimumPhase = 1;

        [SerializeField]
        [Tooltip("Whether this strategy may be adopted more than once per fight.")]
        private bool _repeatable;

        [Header("Effect")]
        [SerializeField]
        [Tooltip("Behaviour adjustments applied while this strategy is active.")]
        private TuningAdjustment[] _adjustments = new TuningAdjustment[0];

        [SerializeField]
        [Range(1f, 20f)]
        [Tooltip("Time over which the adjustments are eased in. Instant changes read as cheating; " +
                 "gradual ones read as the boss working the player out.")]
        private float _easeInSeconds = 6f;

        [Header("Player-Facing Tell")]
        [SerializeField]
        [Tooltip("Short line shown when this strategy is adopted, so the player can perceive that " +
                 "the boss has changed. Example: 'It has stopped falling for your opening.'")]
        private string _tellMessage = string.Empty;

        [SerializeField]
        [Tooltip("Audio cue identifier played alongside the tell.")]
        private string _tellAudioCueId = string.Empty;

        /// <summary>Player habit this strategy answers.</summary>
        public BehaviorFeature TriggerFeature => _triggerFeature;

        /// <summary>Direction of the threshold comparison.</summary>
        public FeatureComparison Comparison => _comparison;

        /// <summary>Feature value at which this strategy becomes eligible.</summary>
        public float Threshold => _threshold;

        /// <summary>Confidence required before this strategy may be chosen.</summary>
        public float MinimumConfidence => _minimumConfidence;

        /// <summary>Relative likelihood of selection among eligible strategies.</summary>
        public float SelectionWeight => _selectionWeight;

        /// <summary>Earliest boss phase in which this strategy may be adopted.</summary>
        public int MinimumPhase => _minimumPhase;

        /// <summary>Whether this strategy may be adopted more than once per fight.</summary>
        public bool IsRepeatable => _repeatable;

        /// <summary>Behaviour adjustments applied while this strategy is active.</summary>
        public TuningAdjustment[] Adjustments => _adjustments;

        /// <summary>Time over which adjustments are eased in.</summary>
        public float EaseInSeconds => _easeInSeconds;

        /// <summary>Line shown to the player when this strategy is adopted.</summary>
        public string TellMessage => _tellMessage;

        /// <summary>Audio cue played when this strategy is adopted.</summary>
        public string TellAudioCueId => _tellAudioCueId;

        /// <summary>
        /// Tests whether an observed feature satisfies this strategy's trigger.
        /// </summary>
        /// <param name="featureValue">Observed value of the trigger feature, from zero to one.</param>
        /// <param name="confidence">Confidence in that observation, from zero to one.</param>
        /// <param name="currentPhase">Boss phase currently in effect.</param>
        /// <returns>True when this strategy is eligible for selection.</returns>
        public bool IsEligible(float featureValue, float confidence, int currentPhase)
        {
            if (currentPhase < _minimumPhase || confidence < _minimumConfidence)
            {
                return false;
            }

            return _comparison == FeatureComparison.AtOrAbove
                ? featureValue >= _threshold
                : featureValue <= _threshold;
        }
    }
}
