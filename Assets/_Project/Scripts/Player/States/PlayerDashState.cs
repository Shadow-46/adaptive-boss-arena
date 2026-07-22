using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.StateMachine;

namespace AdaptiveBossArena.Player.States
{
    /// <summary>
    /// The evasive dash, including its invincibility frames.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Invincibility covers only the opening fraction of the dash, not all of it. That single choice
    /// is what makes dodging a skill rather than a button: dashing early beats an attack, dashing
    /// late moves the player into it and still gets punished. A dash invincible for its whole
    /// duration would reduce every boss attack to a reaction test with no timing component.
    /// </para>
    /// <para>
    /// The dash also drives direction, not just speed. It snaps facing on entry so the character
    /// visibly commits, which is what lets the boss's dodge-prediction adaptation have something
    /// legible to read.
    /// </para>
    /// <para>
    /// Movement input is ignored for the whole dash. Steering mid-dash would make the distance
    /// travelled unpredictable and would quietly break the spacing the boss's range logic assumes.
    /// </para>
    /// </remarks>
    public sealed class PlayerDashState : StateBase<PlayerContext>
    {
        /// <summary>True once the dash has run its configured duration.</summary>
        /// <param name="context">The player context.</param>
        /// <returns>Whether the dash has finished.</returns>
        public bool IsComplete(PlayerContext context) => TimeInState >= context.Config.DashDurationSeconds;

        /// <inheritdoc />
        protected override void OnEnter(PlayerContext context)
        {
            context.SetObservableState(ObservableActionState.Dashing);

            // Stamina is spent here rather than at the transition so that the cost and the movement
            // can never diverge; if the dash happens, it was paid for.
            context.Stamina.TrySpend(context.Config.DashStaminaCost);

            context.DashDirection = context.ResolveDashDirection();
            context.IsInvulnerable = true;
            context.DashStartedAt = context.Time.CombatTime;

            context.Motor.SnapToDirection(context.DashDirection);
            context.Motor.SetPlanarVelocity(context.DashDirection * context.Config.DashSpeed);

            // Published so the boss can build a picture of which way this player tends to roll. The
            // direction is legitimately observable; the boss watches the dash happen like anyone else.
            context.PublishCombatEvent(CombatEventKind.DodgePerformed, context.DashDirection);
        }

        /// <inheritdoc />
        protected override void OnTick(PlayerContext context, float deltaTime)
        {
            if (context.IsInvulnerable && TimeInState >= context.Config.InvulnerabilitySeconds)
            {
                context.IsInvulnerable = false;
            }

            context.Motor.Tick(deltaTime);
        }

        /// <inheritdoc />
        protected override void OnExit(PlayerContext context)
        {
            context.IsInvulnerable = false;
            context.DashCooldownRemaining = context.Config.DashCooldownSeconds;

            // Retaining part of the dash speed keeps the character flowing into whatever comes next.
            // Dropping straight to zero reads as the dash being interrupted rather than completed.
            context.Motor.SetPlanarVelocity(
                context.DashDirection * (context.Config.DashSpeed * context.Config.DashExitSpeedFraction));
        }
    }
}
