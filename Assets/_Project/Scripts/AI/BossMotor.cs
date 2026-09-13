using System;
using AdaptiveBossArena.Combat.Movement;
using AdaptiveBossArena.Core.Constants;
using UnityEngine;

namespace AdaptiveBossArena.AI
{
    /// <summary>
    /// Turns the boss's movement intent into motion.
    /// </summary>
    /// <remarks>
    /// A near-twin of the player's motor but deliberately a separate type. The boss turns slowly and
    /// accelerates bluntly, and those are combat properties rather than incidental differences: a
    /// slow turn rate is precisely what makes circling the boss a viable tactic, and therefore what
    /// its dodge-prediction adaptation exists to answer. Sharing one motor would put both characters'
    /// handling behind the same tuning values and quietly couple two things that should be tuned
    /// against each other.
    /// </remarks>
    public sealed class BossMotor
    {
        /// <summary>Below this speed the boss is treated as stationary.</summary>
        private const float StationarySpeedThreshold = 0.05f;

        private readonly CharacterController _controller;
        private readonly Transform _transform;
        private readonly BossConfig _config;

        private Vector3 _planarVelocity;
        private float _speedMultiplier = 1f;

        /// <summary>Carries imposed motion separately from steering. See the player's motor.</summary>
        private readonly MotionIntegrator _motion =
            new MotionIntegrator(GameplayConstants.Gravity, GameplayConstants.GroundedSpeed);

        /// <summary>Creates a motor bound to a character controller.</summary>
        /// <param name="controller">The controller performing movement and collision.</param>
        /// <param name="config">Boss movement values.</param>
        /// <exception cref="ArgumentNullException">Thrown when the controller or config is missing.</exception>
        public BossMotor(CharacterController controller, BossConfig config)
        {
            _controller = controller != null
                ? controller
                : throw new ArgumentNullException(nameof(controller));

            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _transform = controller.transform;
        }

        /// <summary>Current horizontal velocity.</summary>
        public Vector3 PlanarVelocity => _planarVelocity;

        /// <summary>The imposed shove, such as knockback, still playing out on top of the character's own movement.</summary>
        public Vector3 ImpulseVelocity => _motion.Impulse;

        /// <summary>Current horizontal speed.</summary>
        public float Speed => _planarVelocity.magnitude;

        /// <summary>True while the boss is effectively stationary.</summary>
        public bool IsStationary => Speed < StationarySpeedThreshold;

        /// <summary>World position of the boss.</summary>
        public Vector3 Position => _transform.position;

        /// <summary>Unit vector the boss is facing.</summary>
        public Vector3 Facing => _transform.forward;

        /// <summary>Top speed after the current phase's multiplier.</summary>
        public float CurrentTopSpeed => _config.MoveSpeed * _speedMultiplier;

        /// <summary>Applies the movement multiplier for the phase now in effect.</summary>
        /// <param name="multiplier">Multiplier on base movement speed.</param>
        public void SetSpeedMultiplier(float multiplier) => _speedMultiplier = Mathf.Max(0f, multiplier);

        /// <summary>Accelerates toward a direction at full speed.</summary>
        /// <param name="direction">World direction to travel. Need not be normalised.</param>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        public void MoveInDirection(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;

            Vector3 target = direction.sqrMagnitude > Mathf.Epsilon
                ? direction.normalized * CurrentTopSpeed
                : Vector3.zero;

            float rate = _config.AccelerationSeconds <= 0f
                ? float.MaxValue
                : CurrentTopSpeed / _config.AccelerationSeconds;

            _planarVelocity = Vector3.MoveTowards(_planarVelocity, target, rate * deltaTime);
        }

        /// <summary>Sheds speed at a fixed rate, for stopping a lunge that has outrun normal movement.</summary>
        /// <remarks>
        /// Normal deceleration is scaled to walking speed, so a lunge travelling three times faster than
        /// the boss walks slid metres through its own strike before it stopped.
        /// </remarks>
        /// <param name="deceleration">Speed lost per second.</param>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        public void Brake(float deceleration, float deltaTime) =>
            _planarVelocity = Vector3.MoveTowards(
                _planarVelocity, Vector3.zero, Mathf.Max(0f, deceleration) * deltaTime);

        /// <summary>Brings the boss to rest.</summary>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        public void Decelerate(float deltaTime) => MoveInDirection(Vector3.zero, deltaTime);

        /// <summary>Overrides the boss's own velocity outright, used by lunging attacks.</summary>
        /// <param name="velocity">Horizontal velocity to adopt.</param>
        public void SetPlanarVelocity(Vector3 velocity) =>
            _planarVelocity = new Vector3(velocity.x, 0f, velocity.z);

        /// <summary>Stops the boss's own movement immediately. Imposed shoves still play out.</summary>
        public void Halt() => _planarVelocity = Vector3.zero;

        /// <summary>Imposes a shove, such as knockback, that steering cannot overwrite.</summary>
        /// <param name="velocity">Horizontal velocity to add.</param>
        public void AddImpulse(Vector3 velocity) => _motion.AddImpulse(velocity);

        /// <summary>Integrates velocity and resolves collisions.</summary>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 velocity = _motion.Step(
                _planarVelocity, _config.ImpulseHalfLifeSeconds, _controller.isGrounded, deltaTime);

            Vector3 horizontal = _planarVelocity + _motion.Impulse;
            horizontal.y = 0f;

            CollisionFlags flags = _controller.Move(velocity * deltaTime);

            // Speed into a wall is read before the wall takes it away, so an impact can be judged by
            // how hard it was rather than by the zero speed left afterwards.
            WallImpactSpeed = (flags & CollisionFlags.Sides) != 0 ? horizontal.magnitude : 0f;

            if (WallImpactSpeed > 0f)
            {
                _motion.StopImpulse();
            }
        }

        /// <summary>
        /// Horizontal speed of everything the body was carrying when the last step ran into a wall, or zero.
        /// </summary>
        /// <remarks>
        /// Reported rather than acted on. What a hard wall impact means - a stagger, lost poise - is a
        /// combat decision, and the motor only moves bodies.
        /// </remarks>
        public float WallImpactSpeed { get; private set; }

        /// <summary>Whether the body was standing on something after its last step.</summary>
        public bool IsGrounded => _controller.isGrounded;

        /// <summary>Upward or downward speed, in metres per second.</summary>
        public float VerticalVelocity => _motion.VerticalVelocity;

        /// <summary>
        /// Rotates toward a direction at the configured turn rate.
        /// </summary>
        /// <remarks>
        /// The rate is capped low on purpose. It is what lets a player who keeps moving get behind
        /// the boss, which is the pressure the dodge-prediction counter-strategy is designed to
        /// relieve.
        /// </remarks>
        /// <param name="direction">World direction to face.</param>
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

        /// <summary>
        /// Moves the body by an offset imposed from outside, such as being pushed by another body.
        /// </summary>
        /// <remarks>
        /// Goes through the controller rather than setting the position, so a body shoved toward a
        /// wall stops at the wall instead of being pushed through it.
        /// </remarks>
        /// <param name="offset">Displacement to apply. The vertical component is ignored.</param>
        public void Displace(Vector3 offset)
        {
            offset.y = 0f;

            if (offset.sqrMagnitude > 0f && _controller.enabled)
            {
                _controller.Move(offset);
            }
        }

        /// <summary>Teleports the boss, for use when a fight restarts.</summary>
        /// <param name="position">Destination.</param>
        public void Teleport(Vector3 position)
        {
            _controller.enabled = false;
            _transform.position = position;
            _controller.enabled = true;

            _planarVelocity = Vector3.zero;
            _motion.Reset();
        }
    }
}
