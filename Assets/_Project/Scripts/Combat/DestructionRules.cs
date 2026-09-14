using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// What breaks, and which way the pieces fly - the decisions behind breaking stone.
    /// </summary>
    /// <remarks>
    /// Pure, so a slam that should crack the parapet beside it and a slam that should not are both
    /// tests. Every random choice takes an <see cref="IRandomProvider"/>, never <c>UnityEngine.Random</c>,
    /// so a seeded run breaks the same stone the same way.
    /// </remarks>
    public static class DestructionRules
    {
        /// <summary>Whether an impact reaches something breakable, measured across the floor.</summary>
        /// <remarks>
        /// Horizontal only. A slam lands on the floor and the stone it breaks stands a metre above it; a
        /// straight-line distance would make the same slam break less the taller the thing beside it.
        /// </remarks>
        /// <param name="impact">Where the blow landed.</param>
        /// <param name="reach">How far from that point it breaks things.</param>
        /// <param name="target">The breakable's centre.</param>
        /// <returns>True when the target is within reach.</returns>
        public static bool Reaches(Vector3 impact, float reach, Vector3 target)
        {
            float dx = target.x - impact.x;
            float dz = target.z - impact.z;

            return dx * dx + dz * dz <= reach * reach;
        }

        /// <summary>The velocity one broken piece leaves with.</summary>
        /// <remarks>
        /// Away from the impact across the floor and upward, then scattered, so stone struck on one side
        /// sprays away from the blow instead of popping straight up like a firework.
        /// </remarks>
        /// <param name="random">The seeded source of the scatter.</param>
        /// <param name="impact">Where the blow landed.</param>
        /// <param name="piece">Where the piece starts.</param>
        /// <param name="speed">Typical launch speed, in metres per second.</param>
        /// <returns>The piece's initial velocity.</returns>
        public static Vector3 PieceVelocity(IRandomProvider random, Vector3 impact, Vector3 piece, float speed)
        {
            Vector3 away = piece - impact;
            away.y = 0f;

            if (away.sqrMagnitude < 0.0001f)
            {
                Vector2 any = random.NextDirectionOnPlane();
                away = new Vector3(any.x, 0f, any.y);
            }

            away.Normalize();

            Vector2 scatter = random.NextDirectionOnPlane() * random.NextFloat(0f, 0.45f);
            Vector3 horizontal = (away + new Vector3(scatter.x, 0f, scatter.y)).normalized;

            return horizontal * (speed * random.NextFloat(0.6f, 1f)) + Vector3.up * (speed * random.NextFloat(0.4f, 0.9f));
        }

        /// <summary>How many pieces are live at most, by platform tier.</summary>
        /// <param name="isWeb">Whether this is the WebGL build.</param>
        /// <param name="webCapacity">The WebGL ceiling.</param>
        /// <param name="desktopCapacity">The desktop ceiling.</param>
        /// <returns>The ceiling, never below one.</returns>
        public static int Capacity(bool isWeb, int webCapacity, int desktopCapacity) =>
            Mathf.Max(1, isWeb ? webCapacity : desktopCapacity);
    }
}
