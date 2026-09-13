using UnityEngine;

namespace AdaptiveBossArena.Combat.Movement
{
    /// <summary>
    /// Pushes two overlapping bodies apart, the lighter one further.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fighters' character controllers used to collide with each other directly, which made them
    /// solid but not physical: walking into the boss stopped the knight dead, and a sixteen-metre-a-
    /// second charge into the knight stopped the boss dead too. Neither ever gave ground, so contact
    /// had no weight in either direction.
    /// </para>
    /// <para>
    /// Here overlap is resolved by moving both bodies, split by mass. A player leaning into the boss
    /// shoves it a little and is shoved back a lot; a charging boss drives the player before it. The
    /// correction is the whole overlap every frame, so bodies never visibly interpenetrate.
    /// </para>
    /// <para>
    /// One guard matters: with the controllers no longer blocking each other, a long enough frame
    /// could carry one body's centre past the other's, and a naive resolve would then push it out of
    /// the far side — through the boss. The side each body was on last frame is remembered, and a
    /// crossed pair is pushed back to it.
    /// </para>
    /// </remarks>
    public static class BodySeparation
    {
        /// <summary>The corrections needed to separate two bodies.</summary>
        public readonly struct Result
        {
            /// <summary>Whether the bodies overlapped at all.</summary>
            public bool Overlapped { get; init; }

            /// <summary>Horizontal displacement to apply to the first body.</summary>
            public Vector3 PushFirst { get; init; }

            /// <summary>Horizontal displacement to apply to the second body.</summary>
            public Vector3 PushSecond { get; init; }

            /// <summary>
            /// Unit direction from the first body to the second after resolution. Pass it back next
            /// frame as the previous direction, so a crossed pair is recognised.
            /// </summary>
            public Vector3 Direction { get; init; }
        }

        /// <summary>Resolves overlap between two upright bodies on the horizontal plane.</summary>
        /// <param name="first">Position of the first body.</param>
        /// <param name="firstRadius">Horizontal radius of the first body.</param>
        /// <param name="firstMass">Mass of the first body. Heavier bodies move less.</param>
        /// <param name="second">Position of the second body.</param>
        /// <param name="secondRadius">Horizontal radius of the second body.</param>
        /// <param name="secondMass">Mass of the second body.</param>
        /// <param name="previousDirection">
        /// Direction from first to second last frame, or zero when unknown.
        /// </param>
        /// <returns>The corrections, and the direction to remember.</returns>
        public static Result Resolve(
            Vector3 first,
            float firstRadius,
            float firstMass,
            Vector3 second,
            float secondRadius,
            float secondMass,
            Vector3 previousDirection)
        {
            Vector3 between = second - first;
            between.y = 0f;

            float distance = between.magnitude;
            float contact = Mathf.Max(0f, firstRadius) + Mathf.Max(0f, secondRadius);

            Vector3 direction = distance > Mathf.Epsilon
                ? between / distance
                : FallbackDirection(previousDirection);

            // The centres crossed since last frame: the bodies passed through each other. Resolving
            // along the new direction would complete the pass, so the old side wins, and the full
            // distance between them has to be undone as well as the contact gap.
            // Only while still within contact range, though. Bodies far apart on opposite sides from
            // last frame were moved deliberately — a retry teleports both — and must not be yanked
            // back toward each other.
            bool crossed = distance < contact &&
                           previousDirection.sqrMagnitude > Mathf.Epsilon &&
                           Vector3.Dot(direction, previousDirection) < 0f;

            if (crossed)
            {
                direction = previousDirection.normalized;
            }

            float overlap = crossed ? contact + distance : contact - distance;

            if (overlap <= 0f)
            {
                return new Result { Overlapped = false, Direction = direction };
            }

            float totalMass = Mathf.Max(Mathf.Epsilon, Mathf.Max(0f, firstMass) + Mathf.Max(0f, secondMass));

            // Each body moves by the other's share of the mass: the heavy one barely, the light one
            // almost all the way.
            float firstShare = Mathf.Max(0f, secondMass) / totalMass;

            return new Result
            {
                Overlapped = true,
                PushFirst = -direction * (overlap * firstShare),
                PushSecond = direction * (overlap * (1f - firstShare)),
                Direction = direction
            };
        }

        private static Vector3 FallbackDirection(Vector3 previousDirection)
        {
            previousDirection.y = 0f;

            // Exactly coincident with no history has no meaningful side; any consistent choice is
            // better than leaving the bodies stuck inside each other.
            return previousDirection.sqrMagnitude > Mathf.Epsilon
                ? previousDirection.normalized
                : Vector3.forward;
        }
    }
}
