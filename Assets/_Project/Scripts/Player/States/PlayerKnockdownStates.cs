using System;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.StateMachine;

namespace AdaptiveBossArena.Player.States
{
    /// <summary>
    /// Thrown into the air by a launch, with no control until landing.
    /// </summary>
    /// <remarks>
    /// Landing is read from the controller, not predicted from the launch speed, so a player thrown
    /// onto a slab or off an edge lands when they actually touch down. The configured ceiling still
    /// ends the state, because the anti-juggle cap is only a cap if every stage of it is bounded.
    /// </remarks>
    public sealed class PlayerAirborneState : StateBase<PlayerContext>
    {
        /// <summary>
        /// Time before the ground can count as landed. The body is still on the floor it was launched
        /// from for the first step, and reading that as a landing would cancel every launch.
        /// </summary>
        private const float LiftOffSeconds = 0.08f;

        private float _elapsed;

        /// <summary>True once the thrown body is back on the ground, or has been in the air too long.</summary>
        /// <param name="context">The player context.</param>
        /// <returns>Whether to move on to lying on the floor.</returns>
        public bool HasLanded(PlayerContext context) =>
            _elapsed >= context.Config.MaximumAirborneSeconds ||
            (_elapsed >= LiftOffSeconds && context.Motor.IsGrounded && context.Motor.VerticalVelocity <= 0f);

        /// <inheritdoc />
        protected override void OnEnter(PlayerContext context)
        {
            context.SetObservableState(ObservableActionState.Airborne);
            context.RequestedReaction = ImpactReaction.None;
            _elapsed = 0f;

            LoseControl(context);
            context.Motor.Launch(context.Config.LaunchUpwardSpeed);
        }

        /// <inheritdoc />
        protected override void OnTick(PlayerContext context, float deltaTime)
        {
            _elapsed += deltaTime;
            context.StaggerRequested = false;
            context.Motor.Tick(deltaTime);
        }

        /// <summary>What every reaction state does on entry: the player's plans no longer apply.</summary>
        /// <param name="context">The player context.</param>
        internal static void LoseControl(PlayerContext context)
        {
            context.Attacks.Cancel();
            context.IsGuarding = false;
            context.IsInvulnerable = false;
            context.StaggerRequested = false;
            context.InputBuffer.Clear();
            context.Motor.Halt();
        }
    }

    /// <summary>
    /// On the floor after a knockdown or a landing. Cannot be hurt.
    /// </summary>
    /// <remarks>
    /// Invulnerability here is the reaction gate's doing, not the dodge's flag, so a hit on a body on
    /// the floor is simply ignored rather than counted as a dodge the player never made.
    /// </remarks>
    public sealed class PlayerKnockedDownState : StateBase<PlayerContext>
    {
        private float _remainingSeconds;

        /// <summary>True once the player has lain on the floor for the configured time.</summary>
        /// <param name="context">The player context.</param>
        /// <returns>Whether to start getting up.</returns>
        public bool IsComplete(PlayerContext context) => _remainingSeconds <= 0f;

        /// <inheritdoc />
        protected override void OnEnter(PlayerContext context)
        {
            context.SetObservableState(ObservableActionState.KnockedDown);
            context.RequestedReaction = ImpactReaction.None;
            context.Reactions.Land();
            _remainingSeconds = context.Config.KnockdownSeconds;

            PlayerAirborneState.LoseControl(context);
        }

        /// <inheritdoc />
        protected override void OnTick(PlayerContext context, float deltaTime)
        {
            _remainingSeconds -= deltaTime;
            context.StaggerRequested = false;

            context.Motor.Decelerate(deltaTime);
            context.Motor.Tick(deltaTime);
        }
    }

    /// <summary>
    /// Rising from the floor. Fully invulnerable, and ends with posture refilled.
    /// </summary>
    /// <remarks>
    /// Observed as still knocked down: rising is part of the same visible event, and a separate state
    /// would tell the boss the exact moment control returns, which a watcher can only judge by eye.
    /// </remarks>
    public sealed class PlayerGetUpState : StateBase<PlayerContext>
    {
        private readonly Action _onStood;
        private float _elapsed;

        /// <summary>Creates the state.</summary>
        /// <param name="onStood">
        /// Called once the player is back on their feet. The controller refills posture here, which the
        /// context does not own.
        /// </param>
        public PlayerGetUpState(Action onStood)
        {
            _onStood = onStood;
        }

        /// <summary>How far through rising the player is, from zero to one. Drives the get-up clip.</summary>
        /// <param name="context">The player context.</param>
        /// <returns>The fraction of the rise completed.</returns>
        public float Progress(PlayerContext context) =>
            context.Config.GetUpSeconds > 0f ? Math.Min(1f, _elapsed / context.Config.GetUpSeconds) : 1f;

        /// <summary>True once the player is standing.</summary>
        /// <param name="context">The player context.</param>
        /// <returns>Whether control returns.</returns>
        public bool IsComplete(PlayerContext context) => _elapsed >= context.Config.GetUpSeconds;

        /// <inheritdoc />
        protected override void OnEnter(PlayerContext context)
        {
            _elapsed = 0f;
            context.Reactions.BeginGettingUp();
        }

        /// <inheritdoc />
        protected override void OnTick(PlayerContext context, float deltaTime)
        {
            _elapsed += deltaTime;
            context.StaggerRequested = false;

            context.Motor.Decelerate(deltaTime);
            context.Motor.Tick(deltaTime);
        }

        /// <inheritdoc />
        protected override void OnExit(PlayerContext context)
        {
            context.Reactions.FinishGettingUp(context.Time.CombatTime);

            // Presses made while on the floor are not intentions for after standing up.
            context.InputBuffer.Clear();
            _onStood?.Invoke();
        }
    }
}
