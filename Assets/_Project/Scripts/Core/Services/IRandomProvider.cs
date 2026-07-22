using UnityEngine;

namespace AdaptiveBossArena.Core.Services
{
    /// <summary>
    /// Seedable source of randomness for every gameplay decision that involves chance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing in the project may call <c>UnityEngine.Random</c> directly. The boss's
    /// counter-strategy selection is deliberately probabilistic, so that identical player behaviour
    /// does not produce an identical, learnable response. That same property would make the AI
    /// impossible to test if the randomness were not controllable.
    /// </para>
    /// <para>
    /// Injecting this interface lets tests pin a seed and assert that a given behaviour profile
    /// produces a given adaptation, and lets a bug report carry a seed that reproduces the fight.
    /// </para>
    /// </remarks>
    public interface IRandomProvider
    {
        /// <summary>Seed the sequence was started from.</summary>
        uint Seed { get; }

        /// <summary>Returns a value in the half-open range zero to one.</summary>
        /// <returns>A uniformly distributed value.</returns>
        float NextFloat01();

        /// <summary>Returns a value in the half-open range from <paramref name="min"/> to <paramref name="max"/>.</summary>
        /// <param name="min">Inclusive lower bound.</param>
        /// <param name="max">Exclusive upper bound.</param>
        /// <returns>A uniformly distributed value.</returns>
        float NextFloat(float min, float max);

        /// <summary>Returns an integer in the half-open range from <paramref name="minInclusive"/> to <paramref name="maxExclusive"/>.</summary>
        /// <param name="minInclusive">Inclusive lower bound.</param>
        /// <param name="maxExclusive">Exclusive upper bound.</param>
        /// <returns>A uniformly distributed integer.</returns>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Returns true with the supplied probability.</summary>
        /// <param name="probability">Chance of returning true, from zero to one.</param>
        /// <returns>The rolled outcome.</returns>
        bool NextBool(float probability = 0.5f);

        /// <summary>Returns a uniformly distributed unit vector on the arena's horizontal plane.</summary>
        /// <returns>A unit direction.</returns>
        Vector2 NextDirectionOnPlane();

        /// <summary>Restarts the sequence from a new seed.</summary>
        /// <param name="seed">The seed to restart from. Zero is remapped, as it is a degenerate state.</param>
        void Reseed(uint seed);
    }
}
