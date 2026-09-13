using UnityEngine;

namespace AdaptiveBossArena.Combat.Movement
{
    /// <summary>
    /// Combines steered movement, external impulses and gravity into one velocity per frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both motors used to hold a single planar velocity that anything could overwrite. Steering,
    /// lunges and knockback all wrote the same field, so the last writer won: a knockback applied on
    /// the frame of impact was replaced by the victim's movement input on the next, and the boss never
    /// honoured knockback at all. A blow that cannot move anyone reads as weightless.
    /// </para>
    /// <para>
    /// This keeps what is <em>imposed</em> on a character apart from what the character is
    /// <em>doing</em>. State code keeps setting steered velocity exactly as before. Impulses are only
    /// ever added, decay on their own half-life, and survive any number of steering writes — so a shove
    /// plays out whether or not the victim is holding a direction. Vertical velocity is real gravity,
    /// held at a small downward speed while grounded so the controller's ground contact stays stable.
    /// </para>
    /// <para>
    /// Pure C# with no engine calls, shared by the player's and the boss's motors from the Combat
    /// assembly both already see. The two motors stay separate types, because their handling is
    /// tuned against each other; only this arithmetic is common.
    /// </para>
    /// </remarks>
    public sealed class MotionIntegrator
    {
        /// <summary>Below this speed an impulse is treated as spent and cleared.</summary>
        private const float SpentImpulseSpeed = 0.01f;

        private readonly float _gravity;
        private readonly float _groundedSpeed;

        private Vector3 _impulse;
        private float _verticalVelocity;

        /// <summary>Creates an integrator.</summary>
        /// <param name="gravity">Downward acceleration, as a positive number, in metres per second squared.</param>
        /// <param name="groundedSpeed">
        /// Downward speed held while grounded, as a negative number. Keeps the controller pressed onto
        /// the floor without accumulating speed a fall would then start from.
        /// </param>
        public MotionIntegrator(float gravity, float groundedSpeed)
        {
            _gravity = Mathf.Max(0f, gravity);
            _groundedSpeed = Mathf.Min(0f, groundedSpeed);
            _verticalVelocity = _groundedSpeed;
        }

        /// <summary>The imposed horizontal velocity still playing out.</summary>
        public Vector3 Impulse => _impulse;

        /// <summary>Current vertical velocity.</summary>
        public float VerticalVelocity => _verticalVelocity;

        /// <summary>
        /// Adds an imposed horizontal velocity, such as knockback.
        /// </summary>
        /// <remarks>
        /// Added rather than assigned, so two blows landing close together push further than one.
        /// The vertical component is discarded; launches are a separate, deliberate act.
        /// </remarks>
        /// <param name="velocity">Velocity to add.</param>
        public void AddImpulse(Vector3 velocity)
        {
            _impulse.x += velocity.x;
            _impulse.z += velocity.z;
        }

        /// <summary>Throws the body upward, replacing whatever vertical speed it had.</summary>
        /// <remarks>
        /// Replaces rather than adds: a body standing on the floor carries the small downward contact
        /// speed, and adding to that would make the same launch a little lower every time.
        /// </remarks>
        /// <param name="upwardSpeed">Initial upward speed, in metres per second.</param>
        public void Launch(float upwardSpeed)
        {
            _verticalVelocity = Mathf.Max(0f, upwardSpeed);
        }

        /// <summary>Discards the imposed shove, as when a wall stops the body it was carrying.</summary>
        public void StopImpulse()
        {
            _impulse = Vector3.zero;
        }

        /// <summary>Clears every imposed velocity, for a teleport or a retry.</summary>
        public void Reset()
        {
            _impulse = Vector3.zero;
            _verticalVelocity = _groundedSpeed;
        }

        /// <summary>
        /// Advances the imposed motion and returns the full velocity to move by this frame.
        /// </summary>
        /// <param name="steeredVelocity">What the character is doing on its own. Its vertical component is ignored.</param>
        /// <param name="impulseHalfLifeSeconds">
        /// How long an impulse takes to lose half its speed. Heavier bodies shed a shove faster.
        /// </param>
        /// <param name="isGrounded">Whether the character was on the ground after its last move.</param>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        /// <returns>The combined velocity.</returns>
        public Vector3 Step(Vector3 steeredVelocity, float impulseHalfLifeSeconds, bool isGrounded, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return new Vector3(steeredVelocity.x + _impulse.x, 0f, steeredVelocity.z + _impulse.z);
            }

            // Sampled before decay so a shove moves its victim on the very frame it lands.
            Vector3 imposed = _impulse;
            DecayImpulse(impulseHalfLifeSeconds, deltaTime);
            AdvanceVertical(isGrounded, deltaTime);

            return new Vector3(
                steeredVelocity.x + imposed.x,
                _verticalVelocity,
                steeredVelocity.z + imposed.z);
        }

        /// <summary>Exponential decay by half-life, so the result does not depend on frame rate.</summary>
        private void DecayImpulse(float halfLifeSeconds, float deltaTime)
        {
            if (halfLifeSeconds <= 0f)
            {
                _impulse = Vector3.zero;
                return;
            }

            _impulse *= Mathf.Pow(0.5f, deltaTime / halfLifeSeconds);

            if (_impulse.sqrMagnitude < SpentImpulseSpeed * SpentImpulseSpeed)
            {
                _impulse = Vector3.zero;
            }
        }

        private void AdvanceVertical(bool isGrounded, float deltaTime)
        {
            // Grounded and not rising: hold the small contact speed rather than accumulating gravity.
            // A character that has been standing still for a minute must not fall off a ledge at
            // terminal velocity on the first frame it leaves the ground.
            if (isGrounded && _verticalVelocity <= 0f)
            {
                _verticalVelocity = _groundedSpeed;
                return;
            }

            _verticalVelocity -= _gravity * deltaTime;
        }
    }
}
