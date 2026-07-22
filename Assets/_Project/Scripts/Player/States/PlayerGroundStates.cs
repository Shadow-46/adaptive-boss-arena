using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.StateMachine;

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
            context.Motor.ApplyMoveInput(context.Input.MoveDirection, deltaTime);
            context.Motor.FaceTravelDirection(deltaTime);
            context.Motor.Tick(deltaTime);
        }
    }
}
