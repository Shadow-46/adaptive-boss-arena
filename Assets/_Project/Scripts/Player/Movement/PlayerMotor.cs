using System;
using UnityEngine;

namespace AdaptiveBossArena.Player.Movement
{
    /// <summary>
    /// Turns movement intent into character motion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain class driven by whichever state currently owns the character, rather than a component
    /// that decides for itself. Movement during a dash and movement under player control differ only
    /// in who sets the velocity, so both share one integration and collision path and cannot drift
    /// apart.
    /// </para>
    /// <para>
    /// Acceleration uses <see cref="Vector3.MoveTowards"/> against a rate derived from the
    /// configured time-to-top-speed. That makes the configuration value literal — setting
    /// acceleration to 0.06 seconds really does mean the character reaches full speed in 60
    /// milliseconds — where an eased approach would only ever approximate it and would leave a
    /// designer tuning a number that does not mean what it says.
    /// </para>
    /// <para>
    /// The arena is enclosed by real colliders, so containment is handled by the character
    /// controller's own collision response. There is no positional clamping here, and deliberately
    /// no dependency on the arena's configuration.
    /// </para>
    /// </remarks>
    public sealed class PlayerMotor
    {
        /// <summary>
        /// Constant downward velocity applied to keep the controller in contact with the floor.
        /// </summary>
        /// <remarks>
        /// Not simulated gravity. The arena is flat and there is no jumping, so all this needs to do
        /// is stop <see cref="CharacterController.isGrounded"/> from flickering, which would make
        /// ground-dependent logic unreliable.
        /// </remarks>
        private const float GroundingSpeed = -2f;

        /// <summary>Below this speed the character is treated as stationary.</summary>
        private const float StationarySpeedThreshold = 0.05f;

        private readonly CharacterController _controller;
        private readonly Transform _transform;
        private readonly PlayerConfig _config;
        private readonly Transform _cameraTransform;

        private Vector3 _planarVelocity;

        /// <summary>Creates a motor bound to a character controller.</summary>
        /// <param name="controller">The controller that performs movement and collision.</param>
        /// <param name="config">Movement tuning values.</param>
        /// <param name="cameraTransform">
        /// Camera used to interpret movement input. May be null, in which case input is treated as
        /// world-relative.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when the controller or config is missing.</exception>
        public PlayerMotor(CharacterController controller, PlayerConfig config, Transform cameraTransform)
        {
            _controller = controller != null
                ? controller
                : throw new ArgumentNullException(nameof(controller));

            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _transform = controller.transform;
            _cameraTransform = cameraTransform;
        }

        /// <summary>Current horizontal velocity in world units per second.</summary>
        public Vector3 PlanarVelocity => _planarVelocity;

        /// <summary>Current horizontal speed in world units per second.</summary>
        public float Speed => _planarVelocity.magnitude;

        /// <summary>Current speed as a fraction of the configured top speed.</summary>
        public float NormalizedSpeed =>
            _config.MoveSpeed <= 0f ? 0f : Mathf.Clamp01(Speed / _config.MoveSpeed);

        /// <summary>True while the character is effectively stationary.</summary>
        public bool IsStationary => Speed < StationarySpeedThreshold;

        /// <summary>World position of the character.</summary>
        public Vector3 Position => _transform.position;

        /// <summary>Unit vector the character is facing.</summary>
        public Vector3 Facing => _transform.forward;

        /// <summary>
        /// Accelerates toward the speed requested by movement input.
        /// </summary>
        /// <param name="moveInput">Movement axis, at most unit length.</param>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        public void ApplyMoveInput(Vector2 moveInput, float deltaTime)
        {
            Vector3 desiredDirection = ToWorldDirection(moveInput);
            Vector3 targetVelocity = desiredDirection * _config.MoveSpeed;

            bool isAccelerating = desiredDirection.sqrMagnitude > 0f;
            float durationSeconds = isAccelerating
                ? _config.AccelerationSeconds
                : _config.DecelerationSeconds;

            _planarVelocity = Vector3.MoveTowards(
                _planarVelocity, targetVelocity, RateFor(durationSeconds) * deltaTime);
        }

        /// <summary>Brings the character to rest at the configured deceleration rate.</summary>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        public void Decelerate(float deltaTime) => ApplyMoveInput(Vector2.zero, deltaTime);

        /// <summary>
        /// Overrides horizontal velocity outright, bypassing acceleration.
        /// </summary>
        /// <remarks>Used by the dash and by knockback, both of which are imposed rather than steered.</remarks>
        /// <param name="velocity">Horizontal velocity to adopt. The vertical component is discarded.</param>
        public void SetPlanarVelocity(Vector3 velocity) =>
            _planarVelocity = new Vector3(velocity.x, 0f, velocity.z);

        /// <summary>Stops the character immediately.</summary>
        public void Halt() => _planarVelocity = Vector3.zero;

        /// <summary>Integrates the current velocity and resolves collisions.</summary>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 motion = _planarVelocity;
            motion.y = GroundingSpeed;

            _controller.Move(motion * deltaTime);
        }

        /// <summary>Rotates toward the direction of travel.</summary>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        public void FaceTravelDirection(float deltaTime)
        {
            if (IsStationary)
            {
                return;
            }

            FaceDirection(_planarVelocity, deltaTime);
        }

        /// <summary>Rotates toward a world-space direction at the configured turn rate.</summary>
        /// <param name="direction">Direction to face. The vertical component is ignored.</param>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        public void FaceDirection(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            _transform.rotation = Quaternion.RotateTowards(
                _transform.rotation, target, _config.TurnSpeedDegreesPerSecond * deltaTime);
        }

        /// <summary>Faces a direction immediately, used when committing to a dash.</summary>
        /// <param name="direction">Direction to face.</param>
        public void SnapToDirection(Vector3 direction)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            _transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        /// <summary>Teleports the character, disabling collision resolution for the move.</summary>
        /// <param name="position">Destination.</param>
        public void Teleport(Vector3 position)
        {
            // The controller caches its position internally, so it has to be disabled across the
            // move or it will snap straight back on the next step.
            _controller.enabled = false;
            _transform.position = position;
            _controller.enabled = true;

            _planarVelocity = Vector3.zero;
        }

        /// <summary>
        /// Converts a movement axis into a world direction on the camera's horizontal plane.
        /// </summary>
        /// <remarks>
        /// The generated camera has no yaw, so this currently resolves to an identity mapping. It is
        /// written camera-relatively anyway because the alternative fails silently and confusingly
        /// the first time the camera is rotated for framing.
        /// </remarks>
        /// <param name="moveInput">Movement axis, at most unit length.</param>
        /// <returns>The corresponding world-space direction on the horizontal plane.</returns>
        public Vector3 ToWorldDirection(Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude < Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            var input = new Vector3(moveInput.x, 0f, moveInput.y);

            if (_cameraTransform == null)
            {
                return input;
            }

            // Only the camera's yaw is meaningful; its pitch would otherwise tilt movement into the
            // floor.
            float cameraYaw = _cameraTransform.eulerAngles.y;
            return Quaternion.Euler(0f, cameraYaw, 0f) * input;
        }

        /// <summary>Converts a time-to-top-speed into a velocity change rate.</summary>
        private float RateFor(float durationSeconds) =>
            durationSeconds <= 0f ? float.MaxValue : _config.MoveSpeed / durationSeconds;
    }
}
