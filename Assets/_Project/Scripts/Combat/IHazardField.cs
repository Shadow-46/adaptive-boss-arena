using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// Spawns lingering ground hazards on demand, without the caller knowing how they are pooled.
    /// </summary>
    /// <remarks>
    /// Injected into the boss's <see cref="AttackExecutor"/> as an optional dependency. The player's
    /// executor is given none, which is the whole reason only the boss scars the ground: the seam is
    /// closed by absence rather than by a runtime check.
    /// </remarks>
    public interface IHazardField
    {
        /// <summary>Raises a hazard zone at a point on the ground.</summary>
        /// <param name="center">World-space centre, at floor height.</param>
        /// <param name="radius">Radius of the danger area.</param>
        /// <param name="damagePerTick">Damage dealt each tick to anything standing inside.</param>
        /// <param name="durationSeconds">How long the zone stays dangerous.</param>
        void Spawn(Vector3 center, float radius, float damagePerTick, float durationSeconds);
    }
}
