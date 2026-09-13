using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// Whether an attacker may still turn to follow its target.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The boss used to turn toward its aim for the whole of every wind-up, and re-apply its lunge
    /// along that facing every frame, so a charge homed in right up to the moment it struck. A late
    /// sidestep could never work, which is the opposite of how the genre's committed swings read: the
    /// attacker tracks early, then commits, and the committed part is what a well-timed dodge beats.
    /// </para>
    /// <para>
    /// Tracking runs for the opening fraction of the wind-up and locks from there until the swing is
    /// over. The lock covers the rest of the wind-up too, because a swing that could still turn in
    /// its last frames of wind-up would follow a dodge into the strike.
    /// </para>
    /// </remarks>
    public static class AttackTracking
    {
        /// <summary>Whether the attacker may still turn toward its target.</summary>
        /// <param name="phase">The swing's current phase.</param>
        /// <param name="elapsedSeconds">Time since the swing began.</param>
        /// <param name="startupSeconds">Length of the wind-up.</param>
        /// <param name="trackingWindowFraction">Fraction of the wind-up during which tracking is allowed.</param>
        /// <returns>True while tracking is allowed.</returns>
        public static bool CanTrack(
            AttackPhase phase,
            float elapsedSeconds,
            float startupSeconds,
            float trackingWindowFraction)
        {
            if (phase != AttackPhase.Startup)
            {
                return false;
            }

            return elapsedSeconds < startupSeconds * Mathf.Clamp01(trackingWindowFraction);
        }
    }
}
