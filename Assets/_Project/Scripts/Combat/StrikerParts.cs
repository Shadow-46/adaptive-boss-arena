using System;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// The parts of a body an attack strikes with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A flag set, because some blows land with more than one part: a grab closes with both hands, a charge
    /// drives the blade and the body in together.
    /// </para>
    /// <para>
    /// <see cref="None"/> keeps the attack on its authored volume - an arc, sphere or box around the attacker.
    /// That is right for anything whose area is drawn on the floor (a slam's ring, a shockwave), and it is the
    /// fallback for a body with no rig, where there are no limbs to strike with.
    /// </para>
    /// </remarks>
    [Flags]
    public enum StrikerParts
    {
        /// <summary>No part: the attack is decided by its authored volume.</summary>
        None = 0,

        /// <summary>The weapon in the right hand, base to tip.</summary>
        Weapon = 1,

        /// <summary>The right fist.</summary>
        RightHand = 2,

        /// <summary>The left fist.</summary>
        LeftHand = 4,

        /// <summary>The right foot, for a kick.</summary>
        RightFoot = 8,

        /// <summary>The whole body, for a leap or a charge that lands with its weight.</summary>
        Body = 16
    }
}
