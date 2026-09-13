using UnityEngine;

namespace AdaptiveBossArena.Combat.Movement
{
    /// <summary>
    /// The speed of a dodge roll over its duration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dodge used to be a flat 25 m/s slide for 0.18 s: constant speed, instant start, instant
    /// stop. It covered ground but had no body in it, and it read as a teleport with a smear.
    /// </para>
    /// <para>
    /// A roll bursts out and slows as it settles. Speed falls away on a square curve, so most of the
    /// distance is covered early - which is where the invincibility is - and the tail is slow enough
    /// to be cancelled out of cleanly. The peak is derived from the distance and duration, so the
    /// roll always travels exactly as far as configured, however long it is tuned to last.
    /// </para>
    /// </remarks>
    public static class DodgeRollProfile
    {
        /// <summary>The roll's speed at a point in time.</summary>
        /// <param name="elapsedSeconds">Time since the roll began.</param>
        /// <param name="durationSeconds">Total length of the roll.</param>
        /// <param name="distance">Ground the roll covers over its duration.</param>
        /// <returns>Speed in metres per second; zero outside the roll.</returns>
        public static float SpeedAt(float elapsedSeconds, float durationSeconds, float distance)
        {
            if (durationSeconds <= 0f || elapsedSeconds < 0f || elapsedSeconds >= durationSeconds)
            {
                return 0f;
            }

            float remaining = 1f - elapsedSeconds / durationSeconds;

            return PeakSpeed(durationSeconds, distance) * remaining * remaining;
        }

        /// <summary>The roll's speed at its first instant.</summary>
        /// <remarks>The area under a square falloff is a third of its height, hence three times the average.</remarks>
        /// <param name="durationSeconds">Total length of the roll.</param>
        /// <param name="distance">Ground the roll covers.</param>
        /// <returns>Peak speed in metres per second.</returns>
        public static float PeakSpeed(float durationSeconds, float distance) =>
            durationSeconds <= 0f ? 0f : 3f * Mathf.Max(0f, distance) / durationSeconds;
    }
}
