using System;
using UnityEngine;

namespace AdaptiveBossArena.Utilities.Statistics
{
    /// <summary>
    /// Weighted histogram of movement directions binned into eight compass sectors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This answers the design requirement "if the player rolls left frequently, the boss predicts
    /// left". Every dash is folded into a sector, old entries decay on a half-life, and
    /// <see cref="Predictability"/> reports how lopsided the distribution has become.
    /// </para>
    /// <para>
    /// Predictability is what gates exploitation: a boss that leads its swing toward the player's
    /// favourite dodge direction only feels fair if the player really does have a favourite. When
    /// the distribution is close to uniform, predictability approaches zero and the counter-strategy
    /// that depends on it should not fire.
    /// </para>
    /// <para>
    /// Directions are supplied on the arena's horizontal plane as (x, z). Bin zero points along
    /// positive X and bins advance counter-clockwise in 45 degree steps.
    /// </para>
    /// </remarks>
    public sealed class DirectionHistogram
    {
        /// <summary>Number of compass sectors tracked.</summary>
        public const int BinCount = 8;

        private const float RadiansPerBin = 2f * Mathf.PI / BinCount;

        /// <summary>Below this total weight the distribution is treated as having no signal.</summary>
        private const float MinimumMeaningfulWeight = 0.0001f;

        /// <summary>Entropy of a perfectly uniform distribution, used to normalise predictability.</summary>
        private static readonly float MaxEntropy = Mathf.Log(BinCount);

        private readonly float[] _weights = new float[BinCount];
        private float _totalWeight;

        /// <summary>Sum of all bin weights after decay.</summary>
        public float TotalWeight => _totalWeight;

        /// <summary>True once enough weight has accumulated for the distribution to mean anything.</summary>
        public bool HasSignal => _totalWeight > MinimumMeaningfulWeight;

        /// <summary>
        /// Records a movement direction.
        /// </summary>
        /// <param name="direction">Direction on the horizontal plane. Magnitude is ignored; near-zero vectors are discarded.</param>
        /// <param name="weight">Relative importance of this observation. Must be positive.</param>
        public void Add(Vector2 direction, float weight = 1f)
        {
            if (weight <= 0f || direction.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            _weights[BinForDirection(direction)] += weight;
            _totalWeight += weight;
        }

        /// <summary>
        /// Fades every bin toward zero so that stale movement habits stop influencing the boss.
        /// </summary>
        /// <param name="halfLifeSeconds">Time for accumulated weight to halve. Must be positive.</param>
        /// <param name="deltaTimeSeconds">Elapsed time since the previous decay.</param>
        public void Decay(float halfLifeSeconds, float deltaTimeSeconds)
        {
            if (halfLifeSeconds <= 0f || deltaTimeSeconds <= 0f || _totalWeight <= 0f)
            {
                return;
            }

            float retained = (float)Math.Pow(0.5d, deltaTimeSeconds / halfLifeSeconds);

            for (int i = 0; i < BinCount; i++)
            {
                _weights[i] *= retained;
            }

            _totalWeight *= retained;
        }

        /// <summary>Fraction of total weight held by <paramref name="bin"/>, in the range zero to one.</summary>
        /// <param name="bin">Sector index in the range [0, <see cref="BinCount"/>).</param>
        /// <returns>Normalised weight, or zero when the histogram has no signal.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the bin index is out of range.</exception>
        public float NormalizedWeight(int bin)
        {
            if ((uint)bin >= BinCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bin), bin, $"Bin must be in the range [0, {BinCount}).");
            }

            return HasSignal ? _weights[bin] / _totalWeight : 0f;
        }

        /// <summary>Index of the most frequently observed sector, or -1 when there is no signal.</summary>
        public int DominantBin
        {
            get
            {
                if (!HasSignal)
                {
                    return -1;
                }

                int best = 0;
                for (int i = 1; i < BinCount; i++)
                {
                    if (_weights[i] > _weights[best])
                    {
                        best = i;
                    }
                }

                return best;
            }
        }

        /// <summary>
        /// How lopsided the distribution is, from zero (perfectly uniform) to one (a single sector).
        /// </summary>
        /// <remarks>
        /// Computed as normalised inverse Shannon entropy. Counter-strategies that exploit movement
        /// habits should require this to clear a threshold before engaging.
        /// </remarks>
        public float Predictability
        {
            get
            {
                if (!HasSignal)
                {
                    return 0f;
                }

                float entropy = 0f;
                for (int i = 0; i < BinCount; i++)
                {
                    float p = _weights[i] / _totalWeight;
                    if (p > 0f)
                    {
                        entropy -= p * Mathf.Log(p);
                    }
                }

                return Mathf.Clamp01(1f - entropy / MaxEntropy);
            }
        }

        /// <summary>Unit vector pointing along the centre of the given sector.</summary>
        /// <param name="bin">Sector index in the range [0, <see cref="BinCount"/>).</param>
        /// <returns>Unit direction on the horizontal plane.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the bin index is out of range.</exception>
        public static Vector2 BinCenterDirection(int bin)
        {
            if ((uint)bin >= BinCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bin), bin, $"Bin must be in the range [0, {BinCount}).");
            }

            float angle = bin * RadiansPerBin;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>Maps a direction onto its compass sector.</summary>
        /// <param name="direction">Direction on the horizontal plane.</param>
        /// <returns>Sector index in the range [0, <see cref="BinCount"/>).</returns>
        public static int BinForDirection(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x);
            int bin = Mathf.RoundToInt(angle / RadiansPerBin);

            // Atan2 returns a signed angle and rounding at the seam can reach BinCount, so wrap.
            return ((bin % BinCount) + BinCount) % BinCount;
        }

        /// <summary>Clears all accumulated weight.</summary>
        public void Reset()
        {
            Array.Clear(_weights, 0, BinCount);
            _totalWeight = 0f;
        }
    }
}
