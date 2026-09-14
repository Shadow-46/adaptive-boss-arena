using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Hands a rigged character's body to physics when it dies, and takes it back for a retry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bodies are built into the prefab by the editor, kinematic and with their colliders off, so
    /// while the character lives the Animator poses the bones and physics ignores them. Death flips that:
    /// the Animator and both presentation drivers stop, the bodies go dynamic, and the killing blow's
    /// direction is given to the hips and chest so the fall reads as caused by the hit.
    /// </para>
    /// <para>
    /// Presentation only. The character's root, its controller and its hurtbox stay where they were;
    /// the ragdoll's layer collides with the world and debris and nothing else, so a corpse can neither
    /// block the living nor take a hit.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RagdollActivator : MonoBehaviour
    {
        /// <summary>Speed added upward on death, so a blow from level ground still lifts the body off its feet.</summary>
        private const float LiftSpeed = 1.5f;

        /// <summary>Ceiling on the thrown speed. A huge knockback must not fire a corpse across the arena.</summary>
        private const float MaximumThrowSpeed = 9f;

        [SerializeField]
        [Tooltip("The physical bones, built by the editor. The first two are the hips and the chest.")]
        private Rigidbody[] _bodies = new Rigidbody[0];

        private Animator _animator;
        private CharacterAnimator _procedural;
        private CharacterAnimationBridge _bridge;

        /// <summary>Whether the body currently belongs to physics.</summary>
        public bool IsActive { get; private set; }

        /// <summary>The physical bones, hips first.</summary>
        public Rigidbody[] Bodies => _bodies;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>(true);
            _procedural = GetComponent<CharacterAnimator>();
            _bridge = GetComponent<CharacterAnimationBridge>();
        }

        /// <summary>Assigns the bones. Used by the prefab builder.</summary>
        /// <param name="bodies">The physical bones, hips and chest first.</param>
        public void Bind(Rigidbody[] bodies)
        {
            _bodies = bodies ?? new Rigidbody[0];
        }

        /// <summary>Lets the body fall, thrown along the blow that killed it.</summary>
        /// <param name="throwVelocity">Velocity to give the torso, typically the killing blow's knockback.</param>
        public void Activate(Vector3 throwVelocity)
        {
            if (IsActive || _bodies.Length == 0)
            {
                return;
            }

            IsActive = true;

            // Everything that writes the bones' transforms stops first, or it would fight the solver
            // every frame and the corpse would jitter standing up.
            if (_procedural != null)
            {
                _procedural.enabled = false;
            }

            if (_bridge != null)
            {
                _bridge.enabled = false;
            }

            if (_animator != null)
            {
                _animator.enabled = false;
            }

            Vector3 thrown = Vector3.ClampMagnitude(throwVelocity, MaximumThrowSpeed) + Vector3.up * LiftSpeed;

            for (int i = 0; i < _bodies.Length; i++)
            {
                Rigidbody body = _bodies[i];

                if (body == null)
                {
                    continue;
                }

                SetCollidersEnabled(body, true);
                body.isKinematic = false;

                // The torso takes the whole blow and the limbs a share of it, so the body folds around
                // the hit rather than sliding away rigid.
                body.linearVelocity = i < 2 ? thrown : thrown * 0.5f;
            }
        }

        /// <summary>Returns the body to the Animator, standing, for a new attempt.</summary>
        public void Restore()
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;

            foreach (Rigidbody body in _bodies)
            {
                if (body == null)
                {
                    continue;
                }

                // Velocities are cleared while still dynamic; setting them on a kinematic body is an error.
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                SetCollidersEnabled(body, false);
            }

            if (_animator != null)
            {
                _animator.enabled = true;

                // Snaps every bone back to the pose the Animator was bound with, before the first frame
                // of locomotion plays. Without it the retry would open on the corpse's last pose.
                _animator.Rebind();
                _animator.Update(0f);
            }

            if (_bridge != null)
            {
                _bridge.enabled = true;
            }

            if (_procedural != null)
            {
                _procedural.enabled = true;
            }
        }

        private static void SetCollidersEnabled(Rigidbody body, bool enabled)
        {
            // Only the bone's own collider: children are other bones with their own bodies.
            if (body.TryGetComponent(out Collider collider))
            {
                collider.enabled = enabled;
            }
        }
    }
}
