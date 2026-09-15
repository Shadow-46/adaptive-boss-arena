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
    }
}
