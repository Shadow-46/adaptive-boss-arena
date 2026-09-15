using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// The velocity a bone was moving at, from two of its animated poses.
    /// </summary>
    /// <remarks>
    /// Physics starts a ragdoll's bodies at rest unless told otherwise, so a fighter killed mid-swing stopped
    /// dead and dropped straight down, as if the animation had been unplugged. Handing each body the velocity
    /// its bone had in the last animated frames carries the swing, the stagger or the lunge into the fall.
    /// </remarks>
    public static class PoseVelocity
    {
        /// <summary>Ceiling on an inherited speed, so one bad frame's pop cannot fling a limb.</summary>
        public const float MaximumLinearSpeed = 8f;

        /// <summary>Ceiling on an inherited spin, in radians per second, for the same reason.</summary>
        public const float MaximumAngularSpeed = 20f;

        /// <summary>Linear velocity between two positions.</summary>
        /// <param name="previous">Position in the earlier pose.</param>
        /// <param name="current">Position in the later pose.</param>
        /// <param name="seconds">Time between the poses. Zero, as in a frozen frame, gives no velocity.</param>
        /// <returns>The velocity, capped at <see cref="MaximumLinearSpeed"/>.</returns>
        public static Vector3 Linear(Vector3 previous, Vector3 current, float seconds)
        {
            if (seconds <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            return Vector3.ClampMagnitude((current - previous) / seconds, MaximumLinearSpeed);
        }

        /// <summary>Angular velocity between two rotations, in radians per second about a world axis.</summary>
        /// <param name="previous">Rotation in the earlier pose.</param>
        /// <param name="current">Rotation in the later pose.</param>
        /// <param name="seconds">Time between the poses. Zero gives no velocity.</param>
        /// <returns>The angular velocity, capped at <see cref="MaximumAngularSpeed"/>.</returns>
        public static Vector3 Angular(Quaternion previous, Quaternion current, float seconds)
        {
            if (seconds <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            Quaternion delta = current * Quaternion.Inverse(previous);
            delta.ToAngleAxis(out float degrees, out Vector3 axis);

            // ToAngleAxis reports a small turn backwards as nearly a whole turn forwards; take the short way.
            if (degrees > 180f)
            {
                degrees -= 360f;
            }

            if (float.IsNaN(axis.x) || float.IsInfinity(axis.x) || Mathf.Approximately(degrees, 0f))
            {
                return Vector3.zero;
            }

            return Vector3.ClampMagnitude(axis.normalized * (degrees * Mathf.Deg2Rad / seconds), MaximumAngularSpeed);
        }
    }
}
