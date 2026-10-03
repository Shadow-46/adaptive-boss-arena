using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.Constants;
using UnityEngine;

namespace AdaptiveBossArena.AI
{
    /// <summary>
    /// Bends the boss's walk around standing stone in its path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The boss steers straight at the knight. With a pillar exactly between them, a body walking dead into a
    /// round collider gets no sideways push at all and stops - and a knight could stand behind stone and never be
    /// reached. Any opponent would simply walk round, so the brute does: looking a short way ahead, it turns along
    /// the face of a pillar it is about to meet, more sharply the nearer it is.
    /// </para>
    /// <para>
    /// Only breakable stone counts. The wall ring is not avoided, or the brute would veer off a knight backed
    /// against it. And only walking is steered: a lunge is committed and does not bend, which is exactly why a
    /// charge baited into a pillar breaks the pillar and staggers the brute.
    /// </para>
    /// <para>
    /// What it reads is the room's geometry, which a player sees as plainly as the boss does; nothing of the
    /// knight's state enters it.
    /// </para>
    /// </remarks>
    public static class PillarAvoidance
    {
        /// <summary>How far ahead stone is looked for, in metres: a stride or two.</summary>
        public const float LookAhead = 2.5f;

        /// <summary>Height above the boss's feet the path is tested at.</summary>
        private const float ProbeHeight = 1f;

        /// <summary>The probe's width against the body's, a little narrower so grazing stone is left to sliding.</summary>
        private const float ProbeWidth = 0.8f;

        /// <summary>Turns a walking direction along the face of any standing stone just ahead.</summary>
        /// <param name="position">The boss's feet.</param>
        /// <param name="direction">Where it means to walk; its length is the fraction of top speed.</param>
        /// <param name="bodyRadius">The boss's body radius.</param>
        /// <returns>The direction to walk instead, the same length as the one given.</returns>
        public static Vector3 Steer(Vector3 position, Vector3 direction, float bodyRadius)
        {
            direction.y = 0f;
            float length = direction.magnitude;

            if (length < Mathf.Epsilon)
            {
                return direction;
            }

            Vector3 heading = direction / length;
            Vector3 origin = position + Vector3.up * ProbeHeight;

            if (!Physics.SphereCast(origin, bodyRadius * ProbeWidth, heading, out RaycastHit hit, LookAhead,
                    Layers.ArenaMask, QueryTriggerInteraction.Ignore) ||
                hit.collider.GetComponent<Destructible>() == null)
            {
                return direction;
            }

            Vector3 away = hit.normal;
            away.y = 0f;

            if (away.sqrMagnitude < Mathf.Epsilon)
            {
                return direction;
            }

            away.Normalize();

            // Along the stone's face, on whichever side the heading already leans to, so it rounds the near side.
            var along = new Vector3(-away.z, 0f, away.x);

            if (Vector3.Dot(along, heading) < 0f)
            {
                along = -along;
            }

            float nearness = 1f - Mathf.Clamp01(hit.distance / LookAhead);
            Vector3 steered = Vector3.Slerp(heading, along, nearness);

            return steered.normalized * length;
        }
    }
}
