using System;
using UnityEngine;

namespace AdaptiveBossArena.Core.Services
{
    /// <summary>
    /// Deterministic xorshift128 random source.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implemented in-project rather than delegating to <see cref="System.Random"/> because the
    /// latter's algorithm is not contractually fixed and has changed between .NET runtimes. A seed
    /// captured in a bug report has to reproduce the same fight on every machine and every Unity
    /// version, which requires an algorithm we control.
    /// </para>
    /// <para>
    /// Xorshift128 is fast, allocation-free, and has a period far beyond anything a combat encounter
    /// could exhaust. It is not cryptographically secure and must never be used as if it were.
    /// </para>
    /// </remarks>
    public sealed class XorShiftRandomProvider : IRandomProvider
    {
        /// <summary>Substituted when a caller supplies a zero seed, which xorshift cannot escape.</summary>
        private const uint FallbackSeed = 0x9E3779B9u;

        /// <summary>Reciprocal of 2^32, converting a full-range uint into the zero-to-one range.</summary>
        private const double UintToUnitScale = 1.0d / 4294967296.0d;

        private uint _seed;
        private uint _x;
        private uint _y;
        private uint _z;
        private uint _w;

        /// <summary>Creates a generator from an explicit seed.</summary>
        /// <param name="seed">Starting seed. Zero is remapped to a fixed non-zero constant.</param>
        public XorShiftRandomProvider(uint seed) => Reseed(seed);

        /// <summary>Creates a generator seeded from the system clock.</summary>
        public XorShiftRandomProvider() => Reseed(unchecked((uint)Environment.TickCount));

        /// <inheritdoc />
        public uint Seed => _seed;

        /// <inheritdoc />
        public void Reseed(uint seed)
        {
            _seed = seed == 0u ? FallbackSeed : seed;

            // Decorrelate the four lanes from the seed so that neighbouring seeds do not produce
            // visibly similar opening sequences.
            _x = _seed;
            _y = _seed ^ 0x6C078965u;
            _z = _seed ^ 0x8F1BBCDCu;
            _w = _seed ^ 0xB504F333u;

            // Discard the first values, which are poorly mixed immediately after seeding.
            for (int i = 0; i < WarmUpIterations; i++)
            {
                NextUInt();
            }
        }

        /// <summary>Number of values discarded after seeding to let the state mix.</summary>
        private const int WarmUpIterations = 16;

        /// <inheritdoc />
        public float NextFloat01() => (float)(NextUInt() * UintToUnitScale);

        /// <inheritdoc />
        public float NextFloat(float min, float max) => min + NextFloat01() * (max - min);

        /// <inheritdoc />
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            uint range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % range);
        }

        /// <inheritdoc />
        public bool NextBool(float probability = 0.5f)
        {
            if (probability <= 0f)
            {
                return false;
            }

            return probability >= 1f || NextFloat01() < probability;
        }

        /// <inheritdoc />
        public Vector2 NextDirectionOnPlane()
        {
            float angle = NextFloat01() * 2f * Mathf.PI;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>Advances the generator and returns the next raw value.</summary>
        private uint NextUInt()
        {
            uint t = _x ^ (_x << 11);
            _x = _y;
            _y = _z;
            _z = _w;
            _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
            return _w;
        }
    }
}
