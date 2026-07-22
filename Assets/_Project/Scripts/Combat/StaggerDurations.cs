using AdaptiveBossArena.Core.Combat;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// How long each class of stagger takes control away for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shared by the player and the boss so that a poise break costs both of them a comparable
    /// window. Combat legibility depends on the player being able to learn one rule about what a
    /// stagger means rather than two.
    /// </para>
    /// <para>
    /// These are durations of lost control, so they are short by design. A stagger long enough to
    /// permit a full combo turns a single mistake into a whole health bar, which is the failure mode
    /// that makes an action game feel unfair rather than demanding.
    /// </para>
    /// </remarks>
    public static class StaggerDurations
    {
        /// <summary>A flinch does not interrupt; it only registers visually.</summary>
        public const float FlinchSeconds = 0.12f;

        /// <summary>An interrupt cancels the current action and costs a beat.</summary>
        public const float InterruptSeconds = 0.35f;

        /// <summary>A poise break opens a real punish window.</summary>
        public const float BreakSeconds = 0.9f;

        /// <summary>Resolves the control loss for a stagger class.</summary>
        /// <param name="strength">The stagger class.</param>
        /// <returns>Duration in seconds, zero when the hit does not interrupt.</returns>
        public static float For(StaggerStrength strength)
        {
            switch (strength)
            {
                case StaggerStrength.Flinch: return FlinchSeconds;
                case StaggerStrength.Interrupt: return InterruptSeconds;
                case StaggerStrength.Break: return BreakSeconds;
                default: return 0f;
            }
        }
    }
}
