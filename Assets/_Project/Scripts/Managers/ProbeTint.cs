using UnityEngine;
using UnityEngine.Rendering;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Carries the phase mood into light that was baked in the opening phase's colours.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The light probes are baked once, under the cool opening ambience. Anything lit by them - the fighters
    /// and the stone - takes its ambient light from the probes instead of from the room's ambient colour, so
    /// without this the arena's slide into red would stop reaching exactly the things the eye is on.
    /// </para>
    /// <para>
    /// Scaling each colour channel of the baked light by how far the ambience has moved from the colours it
    /// was baked in keeps the bounce's shape - bright where the floor is sunlit, dim in the corners - while
    /// taking on the phase's hue. Pure, so the arithmetic is a test.
    /// </para>
    /// </remarks>
    public static class ProbeTint
    {
        /// <summary>Ceiling on the gain, so a near-black baked channel cannot blow up into a flare.</summary>
        public const float MaximumGain = 4f;

        /// <summary>Per-channel gain from the colour light was baked in to the colour it should now be.</summary>
        /// <param name="current">The ambience now.</param>
        /// <param name="baked">The ambience the probes were baked under.</param>
        /// <returns>A gain per channel, between zero and <see cref="MaximumGain"/>.</returns>
        public static Color Gain(Color current, Color baked) => new Color(
            Channel(current.r, baked.r),
            Channel(current.g, baked.g),
            Channel(current.b, baked.b),
            1f);

        /// <summary>Writes the baked probes, scaled by a gain, into a destination array of the same length.</summary>
        /// <param name="baked">The probes as baked. Never modified.</param>
        /// <param name="destination">Receives the tinted probes.</param>
        /// <param name="gain">Per-channel gain.</param>
        public static void Apply(SphericalHarmonicsL2[] baked, SphericalHarmonicsL2[] destination, Color gain)
        {
            for (int probe = 0; probe < baked.Length && probe < destination.Length; probe++)
            {
                // A struct: this is a copy, so the baked array is never written.
                SphericalHarmonicsL2 tinted = baked[probe];

                for (int coefficient = 0; coefficient < 9; coefficient++)
                {
                    tinted[0, coefficient] *= gain.r;
                    tinted[1, coefficient] *= gain.g;
                    tinted[2, coefficient] *= gain.b;
                }

                destination[probe] = tinted;
            }
        }

        private static float Channel(float current, float baked) =>
            Mathf.Clamp(current / Mathf.Max(0.001f, baked), 0f, MaximumGain);
    }
}
