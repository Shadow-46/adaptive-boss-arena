using AdaptiveBossArena.Combat.Movement;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.StateMachine;
using UnityEngine;

namespace AdaptiveBossArena.Player.States
{
    /// <summary>
    /// The evasive dodge roll, including its invincibility frames.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Invincibility covers only the opening of the roll, not all of it. That single choice is what
    /// makes dodging a skill rather than a button: rolling early beats an attack, rolling late moves
    /// the player into it and still gets punished. The window is an absolute time, and deliberately
    /// the same 0.135 s the old slide had, because the boss's timing and its learned read of how this
    /// player dodges were built against it.
    /// </para>
    /// <para>
    /// The roll replaced a flat-speed slide. It bursts out on <see cref="DodgeRollProfile"/> and
    /// settles, covering most of its ground inside the invincibility; the stick can still steer it for
    /// the opening moment, and its slow tail can be cancelled into a buffered attack. It snaps facing
    /// on entry so the character visibly commits, which is what gives the boss's dodge-prediction
    /// adaptation something legible to read.
    /// </para>
    /// </remarks>
    public sealed class PlayerDashState : StateBase<PlayerContext>
    {
        /// <summary>True once the roll has run its configured duration.</summary>
        /// <param name="context">The player context.</param>
        /// <returns>Whether the roll has finished.</returns>
        public bool IsComplete(PlayerContext context) => TimeInState >= context.Config.DashDurationSeconds;

        /// <summary>True once the roll has slowed enough to be cancelled into an attack.</summary>
        /// <param name="context">The player context.</param>
        /// <returns>Whether a buffered attack may interrupt the roll.</returns>
        public bool CanCancel(PlayerContext context) =>
            TimeInState >= context.Config.DashDurationSeconds * context.Config.DashCancelFraction;

        /// <inheritdoc />
        protected override void OnEnter(PlayerContext context)
        {
            context.SetObservableState(ObservableActionState.Dashing);

            // Stamina is spent here rather than at the transition so that the cost and the movement
            // can never diverge; if the roll happens, it was paid for.
            context.Stamina.TrySpend(context.Config.DashStaminaCost);

            context.DashDirection = context.ResolveDashDirection();
            context.IsInvulnerable = true;
            context.DashStartedAt = context.Time.CombatTime;

            context.Motor.SnapToDirection(context.DashDirection);
            ApplyRollVelocity(context);

            // Published so the boss can build a picture of which way this player tends to roll. The
            // direction is legitimately observable; the boss watches the roll happen like anyone else.
            context.PublishCombatEvent(CombatEventKind.DodgePerformed, context.DashDirection);
        }

        /// <inheritdoc />
        protected override void OnTick(PlayerContext context, float deltaTime)
        {
            if (context.IsInvulnerable && TimeInState >= context.Config.InvulnerabilitySeconds)
            {
                context.IsInvulnerable = false;
            }

            SteerEarly(context, deltaTime);
            ApplyRollVelocity(context);

            context.Motor.Tick(deltaTime);
        }

        /// <inheritdoc />
        protected override void OnExit(PlayerContext context)
        {
            // The velocity is left as the roll left it. The curve has already slowed it, so a roll that
            // runs its course ends nearly still, and one cancelled into an attack keeps just the carry
            // it had - no separate exit fraction is needed to make it flow.
            context.IsInvulnerable = false;
            context.DashCooldownRemaining = context.Config.DashCooldownSeconds;
        }

        /// <summary>Lets the stick bend the roll during its opening moment only.</summary>
        /// <remarks>
        /// Steering for the whole roll would make the distance travelled unpredictable and quietly
        /// break the spacing the boss's range logic assumes. A short window is enough to correct a
        /// roll started a few degrees off, which is what steering is for.
        /// </remarks>
        private static void SteerEarly(PlayerContext context, float deltaTime)
        {
            float steerUntil = context.Config.DashDurationSeconds * context.Config.DashSteerFraction;

            if (!context.HasMoveInput || context.Motor == null)
            {
                return;
            }

            if (context.Time.CombatTime - context.DashStartedAt >= steerUntil)
            {
                return;
            }

            Vector3 wanted = context.Motor.ToWorldDirection(context.Input.MoveDirection);

            if (wanted.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            float maxRadians = context.Config.TurnSpeedDegreesPerSecond * Mathf.Deg2Rad * deltaTime;
            context.DashDirection = Vector3.RotateTowards(context.DashDirection, wanted.normalized, maxRadians, 0f);
            context.Motor.SnapToDirection(context.DashDirection);
        }

        private void ApplyRollVelocity(PlayerContext context)
        {
            float speed = DodgeRollProfile.SpeedAt(
                TimeInState, context.Config.DashDurationSeconds, context.Config.DashDistance);

            context.Motor.SetPlanarVelocity(context.DashDirection * speed);
        }
    }
}
