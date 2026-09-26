using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// A part of a body that can strike: a capsule from a base to a tip along this transform's forward.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mounted on the bone or weapon it belongs to, so it moves with exactly what the player sees - the blade
    /// the clip swings, the fist it throws. What hurts is what touched you, rather than a wedge in front of the
    /// attacker that decided the blow whatever the body did.
    /// </para>
    /// <para>
    /// The span along forward is the one the weapon trail already samples, so the smear the player sees and the
    /// capsule the hit is decided on are one line. Presentation never feeds back into it: this only answers
    /// where the part is.
    /// </para>
    /// </remarks>
    public sealed class StrikeVolume : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Which part of the body this is.")]
        private StrikerParts _part = StrikerParts.Weapon;

        [SerializeField]
        [Tooltip("Distance along this transform's forward where the striking part begins.")]
        private float _baseDistance;

        [SerializeField]
        [Tooltip("Distance along this transform's forward where it ends: the tip of the blade, the knuckles.")]
        private float _tipDistance = 0.5f;

        [SerializeField]
        [Tooltip("Thickness of the part, in metres.")]
        private float _radius = 0.1f;

        /// <summary>Which part of the body this is.</summary>
        public StrikerParts Part => _part;

        /// <summary>Thickness of the part.</summary>
        public float Radius => _radius;

        /// <summary>
        /// Whether the part is there to strike with.
        /// </summary>
        /// <remarks>
        /// False while its object is hidden: the brute's cleaver in the frenzy, after it has thrown the sword away.
        /// </remarks>
        public bool IsAvailable => isActiveAndEnabled;

        /// <summary>Shapes the part. Used by the prefab generators.</summary>
        /// <param name="part">Which part of the body this is.</param>
        /// <param name="baseDistance">Where it begins along forward.</param>
        /// <param name="tipDistance">Where it ends along forward.</param>
        /// <param name="radius">Its thickness.</param>
        public void Configure(StrikerParts part, float baseDistance, float tipDistance, float radius)
        {
            _part = part;
            _baseDistance = baseDistance;
            _tipDistance = Mathf.Max(baseDistance, tipDistance);
            _radius = Mathf.Max(0.01f, radius);
        }

        /// <summary>Where the part is right now, as the two ends of its capsule.</summary>
        /// <param name="start">The base, in world space.</param>
        /// <param name="end">The tip, in world space.</param>
        public void Segment(out Vector3 start, out Vector3 end)
        {
            Vector3 origin = transform.position;
            Vector3 along = transform.forward;
            start = origin + along * _baseDistance;
            end = origin + along * _tipDistance;
        }

        private void OnDrawGizmosSelected()
        {
            Segment(out Vector3 start, out Vector3 end);
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(start, _radius);
            Gizmos.DrawWireSphere(end, _radius);
            Gizmos.DrawLine(start, end);
        }
    }
}
