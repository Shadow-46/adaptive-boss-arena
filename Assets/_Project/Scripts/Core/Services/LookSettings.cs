namespace AdaptiveBossArena.Core.Services
{
    /// <summary>
    /// The player's own camera settings: how fast the view turns, and which way.
    /// </summary>
    /// <remarks>
    /// In Core for the same reason as <see cref="FeedbackSettings"/>: the settings menu writes it and the camera
    /// reads it, and neither may depend on the other. The menu applies the saved values on load, so a domain
    /// reload resetting these to their defaults is harmless.
    /// </remarks>
    public static class LookSettings
    {
        /// <summary>The sensitivity a fresh save starts with.</summary>
        public const float DefaultSensitivity = 1f;

        /// <summary>Lowest sensitivity the menu offers.</summary>
        public const float MinimumSensitivity = 0.25f;

        /// <summary>Highest sensitivity the menu offers.</summary>
        public const float MaximumSensitivity = 2.5f;

        /// <summary>Multiplier on how far the view turns per unit of mouse or stick movement.</summary>
        public static float Sensitivity { get; set; } = DefaultSensitivity;

        /// <summary>Whether moving the mouse up looks down.</summary>
        public static bool InvertY { get; set; }
    }
}
