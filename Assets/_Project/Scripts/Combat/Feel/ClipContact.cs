namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Finds when in an attack clip the blow lands, from how fast the weapon hand moves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The clip warp maps an attack's live hitbox window onto the moment its clip connects, so the blade is
    /// seen striking while the hit is real. That only works if the moment is right for each clip. A swing is
    /// fastest where it strikes, so contact is the fraction of the clip at which the weapon hand's speed peaks.
    /// </para>
    /// <para>
    /// The first and last stretch of a clip are ignored: Mixamo clips often whip the hand back to guard in their
    /// closing frames or snap out of the previous pose in their opening ones, and neither is the strike.
    /// </para>
    /// </remarks>
    public static class ClipContact
    {
        /// <summary>The earliest a strike is believed to land, as a fraction of the clip.</summary>
        public const float EarliestContact = 0.15f;

        /// <summary>The latest a strike is believed to land, as a fraction of the clip.</summary>
        public const float LatestContact = 0.85f;

        /// <summary>The contact used when a clip cannot be measured.</summary>
        public const float DefaultContact = 0.45f;

        /// <summary>The fraction of the clip at which the hand is fastest, within the believable window.</summary>
        /// <param name="handSpeeds">Hand speed sampled evenly from the clip's start to its end.</param>
        /// <returns>Contact as a fraction of the clip's length.</returns>
        public static float PeakFraction(float[] handSpeeds)
        {
            if (handSpeeds == null || handSpeeds.Length < 5)
            {
                return DefaultContact;
            }

            int last = handSpeeds.Length - 1;
            int best = -1;
            float fastest = float.MinValue;

            for (int i = 0; i <= last; i++)
            {
                float fraction = i / (float)last;

                if (fraction < EarliestContact || fraction > LatestContact)
                {
                    continue;
                }

                if (handSpeeds[i] > fastest)
                {
                    fastest = handSpeeds[i];
                    best = i;
                }
            }

            return best < 0 ? DefaultContact : best / (float)last;
        }
    }
}
