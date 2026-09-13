using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Maps an attack's timeline onto a clip, so the animation strikes exactly when the hitbox is live.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hit detection is authored in the attack's own frame data and is the authority on when a swing
    /// connects. A clip has its own idea of when the blade lands, and it will not match: played at its
    /// natural speed the blade visibly arrives before or after the hitbox opens.
    /// </para>
    /// <para>
    /// So the clip is warped piecewise. Startup plays the wind-up up to just before the contact point,
    /// the active window plays the contact, and recovery plays the rest. One clip serves a quick light
    /// swing and a slow heavy one, each landing inside its own active window. The input is the attack's
    /// elapsed time, which stops during hit-stop, so the pose freezes on the impact with everything else.
    /// </para>
    /// </remarks>
    public readonly struct AttackClipTimeWarp
    {
        /// <summary>Creates a warp for one attack and one clip.</summary>
        /// <param name="startupSeconds">The attack's wind-up.</param>
        /// <param name="activeSeconds">The attack's live window.</param>
        /// <param name="recoverySeconds">The attack's recovery.</param>
        /// <param name="clipLengthSeconds">The clip's natural length.</param>
        /// <param name="clipContactSeconds">When, in the clip's own time, the blade lands.</param>
        /// <param name="contactSpanSeconds">Clip time around the contact point the active window plays.</param>
        public AttackClipTimeWarp(
            float startupSeconds,
            float activeSeconds,
            float recoverySeconds,
            float clipLengthSeconds,
            float clipContactSeconds,
            float contactSpanSeconds)
        {
            StartupSeconds = Mathf.Max(0f, startupSeconds);
            ActiveSeconds = Mathf.Max(0f, activeSeconds);
            RecoverySeconds = Mathf.Max(0f, recoverySeconds);
            ClipLengthSeconds = Mathf.Max(Mathf.Epsilon, clipLengthSeconds);

            float halfSpan = Mathf.Max(0f, contactSpanSeconds) * 0.5f;
            float contact = Mathf.Clamp(clipContactSeconds, 0f, ClipLengthSeconds);

            ContactStart = Mathf.Clamp(contact - halfSpan, 0f, ClipLengthSeconds);
            ContactEnd = Mathf.Clamp(contact + halfSpan, ContactStart, ClipLengthSeconds);
        }

        /// <summary>The attack's wind-up.</summary>
        public float StartupSeconds { get; }

        /// <summary>The attack's live window.</summary>
        public float ActiveSeconds { get; }

        /// <summary>The attack's recovery.</summary>
        public float RecoverySeconds { get; }

        /// <summary>The clip's natural length.</summary>
        public float ClipLengthSeconds { get; }

        /// <summary>Clip time at which the active window begins.</summary>
        public float ContactStart { get; }

        /// <summary>Clip time at which the active window ends.</summary>
        public float ContactEnd { get; }

        /// <summary>The clip time to show at a point in the attack.</summary>
        /// <param name="attackElapsedSeconds">Time since the attack began.</param>
        /// <returns>Seconds into the clip.</returns>
        public float ClipTimeAt(float attackElapsedSeconds)
        {
            float t = Mathf.Max(0f, attackElapsedSeconds);

            if (t < StartupSeconds)
            {
                return Remap(t, 0f, StartupSeconds, 0f, ContactStart);
            }

            float activeEnd = StartupSeconds + ActiveSeconds;

            if (t < activeEnd)
            {
                return Remap(t, StartupSeconds, activeEnd, ContactStart, ContactEnd);
            }

            return Remap(t, activeEnd, activeEnd + RecoverySeconds, ContactEnd, ClipLengthSeconds);
        }

        /// <summary>The same position as a fraction of the clip, as an Animator's normalised time takes it.</summary>
        /// <param name="attackElapsedSeconds">Time since the attack began.</param>
        /// <returns>Normalised clip time, from zero to one.</returns>
        public float NormalizedTimeAt(float attackElapsedSeconds) =>
            Mathf.Clamp01(ClipTimeAt(attackElapsedSeconds) / ClipLengthSeconds);

        private static float Remap(float value, float fromStart, float fromEnd, float toStart, float toEnd)
        {
            if (fromEnd - fromStart <= Mathf.Epsilon)
            {
                return toEnd;
            }

            return Mathf.Lerp(toStart, toEnd, Mathf.Clamp01((value - fromStart) / (fromEnd - fromStart)));
        }
    }
}
