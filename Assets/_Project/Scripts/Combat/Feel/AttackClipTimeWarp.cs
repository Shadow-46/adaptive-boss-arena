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
    /// <para>
    /// The pieces are joined by a monotone cubic rather than straight lines. Straight segments change the clip's
    /// playback rate in a step at every joint - a wind-up crawling at half speed, then the contact snapping past
    /// at three times - which read as a hitch at the start of every swing. The cubic passes through the same
    /// points, so the blade lands at the same instant, but the rate eases from one piece into the next, and it
    /// can never run the clip backwards.
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
            // The warp's joints: the swing's start, the contact opening, its middle, its close, and the clip's end.
            // A joint with no time before it (an attack without a wind-up, say) takes the later clip time, as the
            // straight pieces did.
            System.Span<float> x = stackalloc float[KnotCount];
            System.Span<float> y = stackalloc float[KnotCount];
            int count = 0;

            float activeEnd = StartupSeconds + ActiveSeconds;
            AddKnot(x, y, ref count, 0f, 0f);
            AddKnot(x, y, ref count, StartupSeconds, ContactStart);
            AddKnot(x, y, ref count, StartupSeconds + ActiveSeconds * 0.5f, (ContactStart + ContactEnd) * 0.5f);
            AddKnot(x, y, ref count, activeEnd, ContactEnd);
            AddKnot(x, y, ref count, activeEnd + RecoverySeconds, ClipLengthSeconds);

            float t = Mathf.Max(0f, attackElapsedSeconds);

            if (count < 2)
            {
                return y[0];
            }

            if (t >= x[count - 1])
            {
                return y[count - 1];
            }

            int k = 0;

            while (t > x[k + 1])
            {
                k++;
            }

            float h = x[k + 1] - x[k];
            float s = (t - x[k]) / h;
            float m0 = Slope(x, y, count, k);
            float m1 = Slope(x, y, count, k + 1);

            // Cubic Hermite between the two joints, with the joints' own rates as tangents.
            float s2 = s * s;
            float s3 = s2 * s;
            float value = (2f * s3 - 3f * s2 + 1f) * y[k] + (s3 - 2f * s2 + s) * h * m0 +
                          (-2f * s3 + 3f * s2) * y[k + 1] + (s3 - s2) * h * m1;

            return Mathf.Clamp(value, y[k], y[k + 1]);
        }

        /// <summary>The same position as a fraction of the clip, as an Animator's normalised time takes it.</summary>
        /// <param name="attackElapsedSeconds">Time since the attack began.</param>
        /// <returns>Normalised clip time, from zero to one.</returns>
        public float NormalizedTimeAt(float attackElapsedSeconds) =>
            Mathf.Clamp01(ClipTimeAt(attackElapsedSeconds) / ClipLengthSeconds);

        /// <summary>The warp's joints: the start, the contact opening, middle and close, and the clip's end.</summary>
        private const int KnotCount = 5;

        /// <summary>Time below which two joints are one.</summary>
        private const float JointEpsilon = 1e-5f;

        private static void AddKnot(System.Span<float> x, System.Span<float> y, ref int count, float time, float clipTime)
        {
            if (count > 0 && time <= x[count - 1] + JointEpsilon)
            {
                y[count - 1] = clipTime;
                return;
            }

            x[count] = time;
            y[count] = clipTime;
            count++;
        }

        /// <summary>
        /// The playback rate at a joint, chosen so the curve never overshoots: the weighted harmonic mean of the
        /// rates either side (Fritsch and Butland), zero where the clip holds still on one side, and the one-sided
        /// rate at the ends.
        /// </summary>
        private static float Slope(System.Span<float> x, System.Span<float> y, int count, int k)
        {
            if (k == 0)
            {
                return (y[1] - y[0]) / (x[1] - x[0]);
            }

            if (k == count - 1)
            {
                return (y[k] - y[k - 1]) / (x[k] - x[k - 1]);
            }

            float h0 = x[k] - x[k - 1];
            float h1 = x[k + 1] - x[k];
            float d0 = (y[k] - y[k - 1]) / h0;
            float d1 = (y[k + 1] - y[k]) / h1;

            if (d0 <= 0f || d1 <= 0f)
            {
                return 0f;
            }

            float w0 = 2f * h1 + h0;
            float w1 = h1 + 2f * h0;
            return (w0 + w1) / (w0 / d0 + w1 / d1);
        }
    }
}
