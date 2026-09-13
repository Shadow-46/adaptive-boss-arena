using UnityEngine;

namespace AdaptiveBossArena.Combat.Vitals
{
    /// <summary>
    /// How much of a shove a combatant shrugs off, given how intact its stance is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The boss used to ignore knockback entirely, which made it read as a wall rather than a body:
    /// the heaviest blow in the game moved it exactly as far as the lightest. Taking the full shove
    /// would be the opposite mistake — a two-and-a-half-metre brute sliding like the knight does.
    /// </para>
    /// <para>
    /// Tying resistance to poise makes weight a thing the player earns through. A fresh boss absorbs
    /// most of a blow; one whose stance the player has been wearing down gives ground; a broken one
    /// is thrown. The same number the posture bar shows is the number that decides whether a hit
    /// pushes it, so the feedback and the mechanic can never disagree.
    /// </para>
    /// </remarks>
    public static class KnockbackResistance
    {
        /// <summary>The fraction of a shove that still lands, from zero (none) to one (all of it).</summary>
        /// <param name="poiseFraction">How intact the stance is, from zero to one.</param>
        /// <param name="isBroken">Whether the stance is currently broken.</param>
        /// <param name="resistanceAtFullPoise">Fraction shrugged off with a full stance.</param>
        /// <param name="resistanceWhenBroken">Fraction shrugged off while broken.</param>
        /// <returns>The multiplier to apply to the knockback speed.</returns>
        public static float ShoveMultiplier(
            float poiseFraction,
            bool isBroken,
            float resistanceAtFullPoise,
            float resistanceWhenBroken)
        {
            float resistance = isBroken
                ? resistanceWhenBroken
                : Mathf.Lerp(resistanceWhenBroken, resistanceAtFullPoise, Mathf.Clamp01(poiseFraction));

            return 1f - Mathf.Clamp01(resistance);
        }
    }
}
