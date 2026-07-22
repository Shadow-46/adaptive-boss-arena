using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// Finds the hurtboxes an attack's volume currently overlaps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Detection is a query run while the attack is active rather than a trigger callback. Trigger
    /// events fire on physics steps that need not align with the frames an attack's active window
    /// spans, which makes short active windows unreliable and near impossible to test. Querying puts
    /// the timing entirely under the attack's control.
    /// </para>
    /// <para>
    /// Buffers are allocated once and reused. The alternative allocates during exactly the frames
    /// where a garbage collection would be most noticeable.
    /// </para>
    /// </remarks>
    public sealed class AttackHitDetector
    {
        /// <summary>Ceiling on colliders considered per query. Far above a duel's realistic needs.</summary>
        public const int MaxOverlaps = 16;

        private readonly Collider[] _overlapBuffer = new Collider[MaxOverlaps];
        private readonly Hurtbox[] _resultBuffer = new Hurtbox[MaxOverlaps];

        /// <summary>Hurtboxes found by the most recent query, valid up to the returned count.</summary>
        public Hurtbox[] Results => _resultBuffer;

        /// <summary>
        /// Queries for hurtboxes inside an attack's volume.
        /// </summary>
        /// <param name="attack">Attack supplying the volume shape and dimensions.</param>
        /// <param name="origin">Transform the volume is positioned and oriented relative to.</param>
        /// <param name="layerMask">Layers to test against.</param>
        /// <returns>Number of hurtboxes written into <see cref="Results"/>.</returns>
        public int Query(AttackDefinition attack, Transform origin, int layerMask)
        {
            int overlapCount = QueryColliders(attack, origin, layerMask);
            int resultCount = 0;

            for (int i = 0; i < overlapCount; i++)
            {
                var hurtbox = _overlapBuffer[i].GetComponent<Hurtbox>();

                if (hurtbox == null || !hurtbox.IsValid)
                {
                    continue;
                }

                if (attack.Shape == AttackShape.Arc && !IsInsideArc(attack, origin, _overlapBuffer[i]))
                {
                    continue;
                }

                _resultBuffer[resultCount++] = hurtbox;
            }

            return resultCount;
        }

        /// <summary>Runs the shape-appropriate physics overlap.</summary>
        private int QueryColliders(AttackDefinition attack, Transform origin, int layerMask)
        {
            Vector3 center = origin.TransformPoint(attack.Offset);

            switch (attack.Shape)
            {
                case AttackShape.Box:
                    return Physics.OverlapBoxNonAlloc(
                        center,
                        attack.BoxHalfExtents,
                        _overlapBuffer,
                        origin.rotation,
                        layerMask,
                        QueryTriggerInteraction.Collide);

                case AttackShape.Arc:
                    // An arc is a sphere centred on the attacker, narrowed afterwards by angle.
                    // Centring it on the offset instead would place the wedge's apex in front of the
                    // character and let attacks reach around behind them.
                    return Physics.OverlapSphereNonAlloc(
                        origin.position + Vector3.up * attack.Offset.y,
                        attack.Range,
                        _overlapBuffer,
                        layerMask,
                        QueryTriggerInteraction.Collide);

                default:
                    return Physics.OverlapSphereNonAlloc(
                        center,
                        attack.Range,
                        _overlapBuffer,
                        layerMask,
                        QueryTriggerInteraction.Collide);
            }
        }

        /// <summary>Tests whether a collider falls within an arc's angular width.</summary>
        /// <remarks>
        /// Measured on the horizontal plane only. Including height would make a swing miss a target
        /// standing slightly above or below the attacker, which reads as the attack passing through
        /// them.
        /// </remarks>
        private static bool IsInsideArc(AttackDefinition attack, Transform origin, Collider target)
        {
            Vector3 toTarget = target.bounds.center - origin.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < Mathf.Epsilon)
            {
                // Standing exactly on top of the attacker counts as hit; there is no meaningful
                // direction to reject, and a miss at point-blank range would look like a bug.
                return true;
            }

            Vector3 forward = origin.forward;
            forward.y = 0f;

            return Vector3.Angle(forward, toTarget) <= attack.ArcDegrees * 0.5f;
        }

        /// <summary>Draws the attack volume for debugging, in the editor only.</summary>
        /// <param name="attack">Attack supplying the volume.</param>
        /// <param name="origin">Transform the volume is relative to.</param>
        public static void DrawGizmo(AttackDefinition attack, Transform origin)
        {
#if UNITY_EDITOR
            if (attack == null || origin == null)
            {
                return;
            }

            Gizmos.color = attack.TelegraphColor;

            switch (attack.Shape)
            {
                case AttackShape.Box:
                    Gizmos.matrix = Matrix4x4.TRS(
                        origin.TransformPoint(attack.Offset), origin.rotation, Vector3.one);
                    Gizmos.DrawWireCube(Vector3.zero, attack.BoxHalfExtents * 2f);
                    Gizmos.matrix = Matrix4x4.identity;
                    break;

                default:
                    Gizmos.DrawWireSphere(origin.TransformPoint(attack.Offset), attack.Range);
                    break;
            }
#endif
        }
    }
}
