using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// Decides whether two live swings have met each other rather than their targets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A clash is the third outcome a duel needs. A parry is one fighter reading the other; a trade
    /// is both simply connecting; a clash is neither winning, and without it two simultaneous swings
    /// resolve as a damage race in which the fighter with the bigger number is always right.
    /// </para>
    /// <para>
    /// Pure, and separate from where it is detected, because the decision is a rule and rules in this
    /// project are pinned by edit-mode tests. The detection has to live somewhere that can see both
    /// combatants at once, which is the encounter director.
    /// </para>
    /// </remarks>
    public static class ClashResolver
    {
        /// <summary>
        /// How squarely the two must be facing each other, as a dot product.
        /// </summary>
        /// <remarks>
        /// Roughly forty-five degrees. Blades meet when the swings are aimed at one another; a
        /// fighter struck from the side has been caught out, not matched, and turning that into a
        /// clash would rob them of a hit they had already earned.
        /// </remarks>
        private const float MinimumFacingDot = 0.7f;

        /// <summary>
        /// Whether two swings should bounce off each other instead of landing.
        /// </summary>
        /// <param name="firstIsSwinging">Whether the first fighter's hitbox is currently open.</param>
        /// <param name="secondIsSwinging">Whether the second fighter's hitbox is currently open.</param>
        /// <param name="separation">Distance between the two fighters.</param>
        /// <param name="combinedReach">The two live attacks' reaches added together.</param>
        /// <param name="facingDot">
        /// How squarely the two face each other: the smaller of the two fighters' alignment with the
        /// line between them. Taking the smaller of the pair means one fighter looking away is
        /// enough to make it a hit rather than a clash, which is the fair reading.
        /// </param>
        /// <returns>True when both swings should be refused.</returns>
        public static bool ShouldClash(
            bool firstIsSwinging,
            bool secondIsSwinging,
            float separation,
            float combinedReach,
            float facingDot)
        {
            if (!firstIsSwinging || !secondIsSwinging)
            {
                return false;
            }

            // Half the combined reach, not the whole of it: two attacks whose ranges merely overlap
            // somewhere are not two blades in the same place. Requiring the fighters to be inside
            // the average of the two reaches is what keeps a clash to swings that genuinely met.
            if (separation > combinedReach * 0.5f)
            {
                return false;
            }

            return facingDot >= MinimumFacingDot;
        }

        /// <summary>
        /// How squarely two fighters face each other, for <see cref="ShouldClash"/>.
        /// </summary>
        /// <remarks>
        /// Measured on the horizontal plane only, matching how the hit arcs are measured: a fighter
        /// standing slightly higher is not facing any less squarely.
        /// </remarks>
        /// <param name="firstPosition">Where the first fighter stands.</param>
        /// <param name="firstForward">Where the first fighter is looking.</param>
        /// <param name="secondPosition">Where the second fighter stands.</param>
        /// <param name="secondForward">Where the second fighter is looking.</param>
        /// <returns>The smaller of the two fighters' alignment with the line between them.</returns>
        public static float FacingDot(
            Vector3 firstPosition,
            Vector3 firstForward,
            Vector3 secondPosition,
            Vector3 secondForward)
        {
            Vector3 between = secondPosition - firstPosition;
            between.y = 0f;

            if (between.sqrMagnitude < Mathf.Epsilon)
            {
                return 1f;
            }

            between.Normalize();

            return Mathf.Min(
                Vector3.Dot(Flatten(firstForward), between),
                Vector3.Dot(Flatten(secondForward), -between));
        }

        /// <summary>Drops the vertical component and renormalises, or returns forward if degenerate.</summary>
        private static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;

            return direction.sqrMagnitude < Mathf.Epsilon ? Vector3.forward : direction.normalized;
        }
    }
}
