using System;
using AdaptiveBossArena.Utilities;
using UnityEngine;

namespace AdaptiveBossArena.Learning
{
    /// <summary>One measured habit, together with how much the boss should trust the measurement.</summary>
    /// <remarks>
    /// Confidence is not decoration. A habit seen twice and a habit seen thirty times can produce
    /// the same value, and treating them alike would have the boss counter a coincidence. Every
    /// strategy multiplies its weight by this, so weak evidence nudges and strong evidence drives.
    /// </remarks>
    public readonly struct FeatureReading
    {
        /// <summary>Measured strength of the habit, from zero to one.</summary>
        public float Value { get; init; }

        /// <summary>How much evidence stands behind the measurement, from zero to one.</summary>
        public float Confidence { get; init; }

        /// <summary>Number of observations the measurement rests on.</summary>
        public int SampleCount { get; init; }

        /// <summary>Value weighted by confidence, useful for ranking habits against each other.</summary>
        public float Weighted => Value * Confidence;
    }

    /// <summary>
    /// The boss's working picture of how this player fights.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Recomputed on an interval rather than continuously, and read by the counter-strategy
    /// selector. Being a snapshot of trends rather than a live feed is what stops the boss reacting
    /// to any single thing the player did.
    /// </para>
    /// <para>
    /// Everything here is derived from occurrences the boss could legitimately have witnessed.
    /// There is nothing in this profile that a human opponent, watching carefully for a minute,
    /// could not also have worked out.
    /// </para>
    /// </remarks>
    public sealed class BehaviorProfile
    {
        private static readonly int FeatureCount = Enum.GetValues(typeof(BehaviorFeature)).Length;

        private readonly FeatureReading[] _readings;

        /// <summary>Creates an empty profile.</summary>
        public BehaviorProfile() => _readings = new FeatureReading[FeatureCount];

        /// <summary>Combat-clock time the profile was last recomputed.</summary>
        public float LastUpdatedAt { get; private set; }

        /// <summary>Reads a habit.</summary>
        /// <param name="feature">The habit to read.</param>
        /// <returns>Its most recent measurement.</returns>
        public FeatureReading Get(BehaviorFeature feature) => _readings[(int)feature];

        /// <summary>Records a measurement.</summary>
        /// <param name="feature">The habit measured.</param>
        /// <param name="value">Strength of the habit, clamped to zero through one.</param>
        /// <param name="sampleCount">Observations the measurement rests on.</param>
        /// <param name="halfConfidenceSampleCount">
        /// Observation count at which confidence reaches one half. Habits that need more evidence
        /// before acting on them use a larger number here.
        /// </param>
        public void Set(
            BehaviorFeature feature,
            float value,
            int sampleCount,
            int halfConfidenceSampleCount)
        {
            _readings[(int)feature] = new FeatureReading
            {
                Value = Mathf.Clamp01(value),
                Confidence = MathUtil.ConfidenceFromSampleCount(sampleCount, halfConfidenceSampleCount),
                SampleCount = sampleCount
            };
        }

        /// <summary>Marks the profile as freshly recomputed.</summary>
        /// <param name="timestamp">Combat-clock time of the recomputation.</param>
        public void MarkUpdated(float timestamp) => LastUpdatedAt = timestamp;

        /// <summary>
        /// Finds the habit the boss has the strongest evidence for.
        /// </summary>
        /// <remarks>Used by the debug overlay to explain, in one line, what the boss thinks it knows.</remarks>
        /// <returns>The habit with the highest confidence-weighted value.</returns>
        public BehaviorFeature StrongestFeature()
        {
            var strongest = BehaviorFeature.AttackFrequency;
            float best = -1f;

            for (int i = 0; i < FeatureCount; i++)
            {
                float weighted = _readings[i].Weighted;

                if (weighted > best)
                {
                    best = weighted;
                    strongest = (BehaviorFeature)i;
                }
            }

            return strongest;
        }

        /// <summary>Clears every measurement, for use when a fight restarts.</summary>
        public void Clear()
        {
            Array.Clear(_readings, 0, _readings.Length);
            LastUpdatedAt = 0f;
        }
    }
}
