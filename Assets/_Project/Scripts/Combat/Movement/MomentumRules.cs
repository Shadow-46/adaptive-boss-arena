using UnityEngine;

namespace AdaptiveBossArena.Combat.Movement
{
    /// <summary>
    /// How much speed a moving body keeps when it asks to change direction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The knight used to reverse direction at full speed with no cost at all: it reached top speed
    /// in 0.06 s, turned at 1080 degrees a second, and velocity simply swung round to wherever the
    /// stick pointed. Nothing in that has mass, which is most of why movement read as floaty.
    /// </para>
    /// <para>
    /// A body carrying speed that asks to go somewhere else has to shed some of that speed first. The
    /// sharper the turn and the faster it was going, the more it loses; a gentle curve at a jog costs
    /// almost nothing, a full reversal at a sprint costs most of it. That single rule is what makes a
    /// character read as heavy without making it unresponsive, because starting from rest is untouched.
    /// </para>
    /// </remarks>
    public static class MomentumRules
    {
        /// <summary>Below this fraction of top speed a body turns freely.</summary>
        /// <remarks>
        /// A character standing still or barely moving has no momentum to fight, and charging it a turn
        /// cost would make starting to move in a new direction feel sluggish rather than weighty.
        /// </remarks>
        private const float FreeTurnSpeedFraction = 0.2f;

        /// <summary>
        /// The fraction of top speed a body may aim for when turning toward a new direction.
        /// </summary>
        /// <param name="currentVelocity">Current horizontal velocity.</param>
        /// <param name="desiredDirection">Direction the body wants to travel. Need not be normalised.</param>
        /// <param name="topSpeed">The body's top speed.</param>
        /// <param name="speedFloorOnReversal">
        /// Fraction of top speed kept when reversing outright at full speed.
        /// </param>
        /// <returns>A multiplier on top speed, from the floor up to one.</returns>
        public static float TargetSpeedFactor(
            Vector3 currentVelocity,
            Vector3 desiredDirection,
            float topSpeed,
            float speedFloorOnReversal)
        {
            currentVelocity.y = 0f;
            desiredDirection.y = 0f;

            if (topSpeed <= 0f || desiredDirection.sqrMagnitude < Mathf.Epsilon)
            {
                return 1f;
            }

            float speedFraction = Mathf.Clamp01(currentVelocity.magnitude / topSpeed);

            if (speedFraction <= FreeTurnSpeedFraction)
            {
                return 1f;
            }

            float turn01 = Vector3.Angle(currentVelocity, desiredDirection) / 180f;

            // Scaled by how far above the free-turn speed the body is, so the cost grows smoothly with
            // momentum instead of switching on at a threshold.
            float momentum01 = Mathf.InverseLerp(FreeTurnSpeedFraction, 1f, speedFraction);
            float cost = turn01 * momentum01;

            return Mathf.Lerp(1f, Mathf.Clamp01(speedFloorOnReversal), cost);
        }
    }
}
