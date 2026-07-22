using System;

namespace AdaptiveBossArena.Utilities.Statistics
{
    /// <summary>
    /// Time-aware exponentially weighted moving average with a configurable half-life.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This type is the mechanism behind two of the boss's design requirements at once:
    /// <em>adapt slowly</em> and <em>forget stale behavior</em>. The half-life states how long it
    /// takes for an old observation's influence to fall to half. A short half-life makes the boss
    /// twitchy and reactive; a long one makes it stubborn. Tuning adaptation feel is largely a
    /// matter of tuning half-lives.
    /// </para>
    /// <para>
    /// Weighting is driven by elapsed time rather than sample count, so a feature sampled
    /// irregularly (a dodge, a landed hit) decays at the same real-world rate as one sampled on a
    /// fixed cadence (distance to the player).
    /// </para>
    /// </remarks>
    public sealed class Ewma
    {
        private const float MinimumHalfLifeSeconds = 0.0001f;

        private readonly float _halfLifeSeconds;
        private float _value;
        private int _sampleCount;

        /// <summary>Creates an average that halves the weight of old samples every <paramref name="halfLifeSeconds"/>.</summary>
        /// <param name="halfLifeSeconds">Time for an observation's influence to decay by half. Must be positive.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the half-life is not positive.</exception>
        public Ewma(float halfLifeSeconds)
        {
            if (halfLifeSeconds < MinimumHalfLifeSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(halfLifeSeconds), halfLifeSeconds, "Half-life must be positive.");
            }

            _halfLifeSeconds = halfLifeSeconds;
        }

        /// <summary>Current smoothed value. Zero until the first sample arrives.</summary>
        public float Value => _value;

        /// <summary>Total number of samples fed in since construction or the last reset.</summary>
        public int SampleCount => _sampleCount;

        /// <summary>True once at least one sample has been recorded.</summary>
        public bool HasSamples => _sampleCount > 0;

        /// <summary>Configured half-life in seconds.</summary>
        public float HalfLifeSeconds => _halfLifeSeconds;

        /// <summary>
        /// Folds a new observation into the average, weighting it by how much time has passed.
        /// </summary>
        /// <param name="sample">The observed value.</param>
        /// <param name="deltaTimeSeconds">Time elapsed since the previous sample. Negative values are clamped to zero.</param>
        public void AddSample(float sample, float deltaTimeSeconds)
        {
            // The first sample defines the baseline outright. Blending it against the implicit zero
            // would otherwise bias every feature toward zero for the opening seconds of a fight.
            if (_sampleCount == 0)
            {
                _value = sample;
                _sampleCount = 1;
                return;
            }

            if (deltaTimeSeconds < 0f)
            {
                deltaTimeSeconds = 0f;
            }

            float alpha = AlphaForElapsed(deltaTimeSeconds);
            _value += (sample - _value) * alpha;
            _sampleCount++;
        }

        /// <summary>
        /// Decays the average toward <paramref name="restingValue"/> without recording a new observation.
        /// </summary>
        /// <remarks>
        /// Used when a behavior simply stops happening. Absence of evidence has to erode a feature,
        /// otherwise the boss would keep countering a tactic the player abandoned minutes ago.
        /// </remarks>
        /// <param name="restingValue">The value the average relaxes toward.</param>
        /// <param name="deltaTimeSeconds">Time elapsed since the previous update.</param>
        public void DecayToward(float restingValue, float deltaTimeSeconds)
        {
            if (_sampleCount == 0 || deltaTimeSeconds <= 0f)
            {
                return;
            }

            _value += (restingValue - _value) * AlphaForElapsed(deltaTimeSeconds);
        }

        /// <summary>Clears the average back to its unobserved state.</summary>
        public void Reset()
        {
            _value = 0f;
            _sampleCount = 0;
        }

        /// <summary>Overwrites the current value without disturbing the sample count.</summary>
        /// <param name="value">The value to force.</param>
        public void SetValue(float value) => _value = value;

        /// <summary>Blend weight for a sample observed after the given elapsed time.</summary>
        private float AlphaForElapsed(float deltaTimeSeconds)
        {
            // Derived from 0.5 ^ (dt / halfLife): the fraction of the old value that survives.
            double retained = Math.Pow(0.5d, deltaTimeSeconds / _halfLifeSeconds);
            return (float)(1d - retained);
        }
    }
}
