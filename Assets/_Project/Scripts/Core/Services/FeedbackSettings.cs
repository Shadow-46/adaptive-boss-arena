namespace AdaptiveBossArena.Core.Services
{
    /// <summary>
    /// Global presentation preferences that any system may read without a reference to the menu.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A deliberate, and deliberately small, piece of global state. The accessibility settings it
    /// holds are genuinely global — reduced flashing has to apply to every hit flash in the scene,
    /// and there is no single object that owns them all — so routing them through an event channel or
    /// a per-object reference would be more machinery than the problem deserves. Two flags read in a
    /// couple of hot paths do not.
    /// </para>
    /// <para>
    /// It lives in Core so both the settings menu (which writes it) and the combat feedback that
    /// reads it can see it without either depending on the other. The menu writes these on load, so a
    /// domain reload clearing them back to their defaults is harmless: the next fight re-applies the
    /// saved values.
    /// </para>
    /// </remarks>
    public static class FeedbackSettings
    {
        /// <summary>
        /// When true, hit flashes are suppressed.
        /// </summary>
        /// <remarks>
        /// Rapid full-screen flashing is a photosensitivity risk, so it must be defeatable from one
        /// place that every flash honours.
        /// </remarks>
        public static bool ReducedFlashing { get; set; }

        /// <summary>
        /// Multiplier on how long an adaptation tell stays on screen.
        /// </summary>
        /// <remarks>
        /// Above one for players who need longer to read the boss's announcements. Defaults to one so
        /// the setting is invisible until deliberately changed.
        /// </remarks>
        public static float TellHoldMultiplier { get; set; } = 1f;
    }
}
