using UnityEngine;

namespace AdaptiveBossArena.Combat.Movement
{
    /// <summary>
    /// A fighter's body, as seen by whatever keeps two bodies from occupying the same space.
    /// </summary>
    /// <remarks>
    /// Exists so the composition root can push the player and the boss apart without either
    /// assembly referencing the other. The firewall forbids the AI from seeing the player at all; this
    /// narrow surface lets something that already sees both — the encounter director — do the physics
    /// between them while exposing nothing either fighter could use to cheat.
    /// </remarks>
    public interface IPushBody
    {
        /// <summary>World position of the body's centre on the ground.</summary>
        Vector3 BodyPosition { get; }

        /// <summary>Horizontal radius of the body.</summary>
        float BodyRadius { get; }

        /// <summary>Relative mass. Heavier bodies give less ground in contact.</summary>
        float BodyMass { get; }

        /// <summary>Whether the body currently takes part in contact at all.</summary>
        bool IsSolid { get; }

        /// <summary>
        /// Moves the body by a horizontal offset, still colliding with the world.
        /// </summary>
        /// <param name="offset">Displacement to apply. The vertical component is ignored.</param>
        void Displace(Vector3 offset);
    }
}
