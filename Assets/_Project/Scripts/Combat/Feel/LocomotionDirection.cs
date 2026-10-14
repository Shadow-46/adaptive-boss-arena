using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Which way a fighter is moving relative to where it faces, for the directional locomotion blend.
    /// </summary>
    /// <remarks>
    /// A single speed drove a walk and a run that always step forward, so a boss circling its opponent, or
    /// a knight backing off with the guard up, slid sideways and backwards on legs walking straight ahead.
    /// Splitting the movement into across and along the facing lets the blend pick strafes and back-steps.
    /// </remarks>
    public static class LocomotionDirection
    {
        /// <summary>Displacement below which a frame carries no reliable direction, in metres.</summary>
        public const float MinimumDisplacement = 0.0005f;

        /// <summary>The blend position for a frame's movement.</summary>
        /// <param name="worldDisplacement">How far the fighter moved this frame, in world space.</param>
        /// <param name="facing">The fighter's facing.</param>
        /// <param name="speed01">Planar speed as a fraction of top speed, which sets the distance from the centre.</param>
        /// <param name="previous">Last frame's result, kept when this frame's movement is too small to read.</param>
        /// <returns>Across (x) and along (y) the facing, scaled by speed.</returns>
        public static Vector2 BlendPosition(Vector3 worldDisplacement, Quaternion facing, float speed01, Vector2 previous)
        {
            speed01 = Mathf.Clamp01(speed01);
            var planar = new Vector3(worldDisplacement.x, 0f, worldDisplacement.z);

            if (planar.magnitude < MinimumDisplacement)
            {
                // Standing still, or a frozen frame: keep the direction so the blend eases out rather than snapping.
                return previous.sqrMagnitude > 1e-8f ? previous.normalized * speed01 : new Vector2(0f, speed01);
            }

            Vector3 local = Quaternion.Inverse(facing) * planar.normalized;
            return new Vector2(local.x, local.z).normalized * speed01;
        }

        /// <summary>Speed fraction by which the legs have fully taken the moving rate.</summary>
        private const float RateBlendInSpeed = 0.3f;

        /// <summary>The slowest and fastest the legs are played, as a multiple of their natural pace.</summary>
        private const float MinimumRate = 0.7f;
        private const float MaximumRate = 1.4f;

        /// <summary>
        /// How fast to play the locomotion clips so the feet keep pace with the ground.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each clip sits in the blend where it travels, as a fraction of the fastest clip, so at any position the
        /// blend walks the body at that fraction of the fastest clip's pace. The body really moves at that fraction
        /// of its own top speed. The two differ by one number, top speed over the clip's pace, and played at that
        /// rate the feet stop sliding - on the brute's run, the knight's sprint and the frenzy's faster legs alike.
        /// </para>
        /// <para>
        /// Faded in from a standstill, so the idle never fidgets at a run's tempo, and clamped, because legs at
        /// half speed read as slow motion and at double as a cartoon: past that, a little slide is the lesser evil.
        /// </para>
        /// </remarks>
        /// <param name="topSpeed">The body's top speed now, in metres per second.</param>
        /// <param name="clipTopSpeed">The fastest locomotion clip's own pace on this body, in metres per second.</param>
        /// <param name="speed01">Speed as a fraction of the top speed.</param>
        /// <returns>The playback rate, one when either speed is unknown.</returns>
        public static float PlaybackRate(float topSpeed, float clipTopSpeed, float speed01)
        {
            if (topSpeed <= 0f || clipTopSpeed <= 0f)
            {
                return 1f;
            }

            float moving = Mathf.Clamp(topSpeed / clipTopSpeed, MinimumRate, MaximumRate);
            return Mathf.Lerp(1f, moving, Mathf.Clamp01(speed01 / RateBlendInSpeed));
        }
    }
}
