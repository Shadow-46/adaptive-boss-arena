using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.StateMachine;
using UnityEngine;

namespace AdaptiveBossArena.Player.States
{
    /// <summary>
    /// Standing still.
    /// </summary>
    /// <remarks>
    /// Kept distinct from <see cref="PlayerMoveState"/> rather than folded into a single grounded
    /// state. The separation costs almost nothing and gives idle-specific presentation — breathing
    /// animation, stance, a different footfall on the first step — somewhere to live without a
    /// speed check scattered through the movement code.
    /// </remarks>
    public sealed class PlayerIdleState : StateBase<PlayerContext>
    {
        /// <inheritdoc />
        protected override void OnEnter(PlayerContext context)
        {
            context.SetObservableState(ObservableActionState.Idle);
        }

        /// <inheritdoc />
        protected override void OnTick(PlayerContext context, float deltaTime)
        {
            // Still decelerating: a character entering idle while sliding out of a dash should coast
            // to a halt rather than stop dead.
            context.Motor.Decelerate(deltaTime);

            // Locked on, a standing knight keeps squaring up to the boss as it circles.
            if (context.IsLockedOn)
            {
                context.Motor.FaceDirection(context.FacingTowardThreat, deltaTime);
            }

            context.Motor.Tick(deltaTime);
        }
    }

    /// <summary>Running under player control.</summary>
    public sealed class PlayerMoveState : StateBase<PlayerContext>
    {
        /// <inheritdoc />
        protected override void OnEnter(PlayerContext context)
        {
            context.SetObservableState(ObservableActionState.Moving);
        }

        /// <inheritdoc />
        protected override void OnTick(PlayerContext context, float deltaTime)
        {
            Vector2 input = context.Input.MoveDirection;
            context.Motor.ApplyMoveInput(input, deltaTime);

            // Facing follows the stick, not the velocity. Following velocity meant a reversal turned the knight
            // only as fast as momentum bled off, so he visibly lagged where he was pointed. Locked on, he keeps
            // facing the boss and the directional blend plays strafes and back-steps.
            Vector3 facing = context.IsLockedOn
                ? context.FacingTowardThreat
                : context.Motor.ToWorldDirection(input);

            context.Motor.FaceDirection(facing, deltaTime);
            context.Motor.Tick(deltaTime);
        }
    }
}
