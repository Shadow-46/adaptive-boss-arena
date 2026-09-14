using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// The colour of the air in the cathedral: the room's own light, never darker than a floor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fog was tried once before and removed because it "fell to black beyond the near fighters". The
    /// cause is not in history, but the shape of the failure fits a fog colour taken from the dark
    /// ambient ground band - or left at its default black - blending every distant surface toward
    /// nothing. Haze should do the opposite: distance should lift toward the light in the air.
    /// </para>
    /// <para>
    /// So the colour is built from what lights the room, the ambient horizon plus a share of the sun,
    /// and then held above a luminance floor. Whatever the phase palette does, distant stone fades
    /// toward a visible haze. Pure, so the floor is a test rather than a hope.
    /// </para>
    /// </remarks>
    public static class HazeColor
    {
        /// <summary>The darkest the haze may be, as relative luminance.</summary>
        public const float MinimumLuminance = 0.08f;

        /// <summary>How much of the sun's colour scatters into the haze.</summary>
        private const float SunScatter = 0.12f;

        /// <summary>Derives the fog colour from the room's light.</summary>
        /// <param name="ambientEquator">The ambient horizon colour.</param>
        /// <param name="sunColour">The directional light's colour.</param>
        /// <param name="sunIntensity">The directional light's intensity.</param>
        /// <returns>An opaque colour whose luminance is at least <see cref="MinimumLuminance"/>.</returns>
        public static Color Derive(Color ambientEquator, Color sunColour, float sunIntensity)
        {
            Color haze = ambientEquator + sunColour * (Mathf.Max(0f, sunIntensity) * SunScatter);
            haze.a = 1f;

            float luminance = Luminance(haze);

            if (luminance >= MinimumLuminance)
            {
                return haze;
            }

            // Raised along its own hue, so a red phase stays a red haze; a colour with no light at all
            // has no hue to keep and becomes a neutral grey at the floor.
            if (luminance <= 0.0001f)
            {
                return new Color(MinimumLuminance, MinimumLuminance, MinimumLuminance, 1f);
            }

            Color lifted = haze * (MinimumLuminance / luminance);
            lifted.a = 1f;

            return lifted;
        }

        /// <summary>Relative luminance of a linear colour.</summary>
        /// <param name="colour">The colour.</param>
        /// <returns>Luminance, from zero upward.</returns>
        public static float Luminance(Color colour) =>
            0.2126f * colour.r + 0.7152f * colour.g + 0.0722f * colour.b;
    }
}
