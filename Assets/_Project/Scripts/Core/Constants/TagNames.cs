namespace AdaptiveBossArena.Core.Constants
{
    /// <summary>Scene object tags the project relies on.</summary>
    /// <remarks>
    /// Tags are used sparingly and only where a scene-authored marker is genuinely the clearest
    /// mechanism. Anything queried during combat is resolved through an interface reference obtained
    /// at initialisation, never by searching for a tag each frame.
    /// </remarks>
    public static class TagNames
    {
        /// <summary>Root object of the player character.</summary>
        public const string Player = "Player";

        /// <summary>Root object of the boss.</summary>
        public const string Boss = "Boss";

        /// <summary>Marker for the point combatants respawn at when a fight restarts.</summary>
        public const string ArenaSpawnPoint = "ArenaSpawnPoint";

        /// <summary>Every custom tag the project configurator creates.</summary>
        public static readonly string[] All =
        {
            Boss,
            ArenaSpawnPoint
        };
    }

    /// <summary>Keys identifying persisted records.</summary>
    /// <remarks>
    /// Centralised because a typo in a save key is silent: the write succeeds, the read finds
    /// nothing, and the game quietly falls back to defaults every launch.
    /// </remarks>
    public static class SaveKeys
    {
        /// <summary>Audio, video, input and accessibility settings.</summary>
        public const string Settings = "settings";

        /// <summary>Best results achieved against the boss.</summary>
        public const string Records = "records";

        /// <summary>Challenge modifiers chosen for the run.</summary>
        public const string RunModifiers = "run-modifiers";
    }
}
