using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.StateMachine;
using UnityEngine;

namespace AdaptiveBossArena.Player.States
{
    /// <summary>
    /// Reeling from a hit that broke through.
    /// </summary>
    /// <remarks>
    /// The stagger duration is carried on the context rather than fixed here, because how long a hit
    /// interrupts for is a property of the hit. A light poke and a boss slam both stagger; only one
    /// of them should cost the player their next several decisions.
    /// </remarks>
    public sealed class PlayerStaggerState : StateBase<PlayerContext>
    {
        private float _remainingSeconds;

        /// <summary>True once the interruption has run its course.</summary>
        /// <param name="context">The player context.</param>
        /// <returns>Whether the player may act again.</returns>
        public bool IsComplete(PlayerContext context) => _remainingSeconds <= 0f;

        /// <inheritdoc />
        protected override void OnEnter(PlayerContext context)
        {
            context.SetObservableState(ObservableActionState.Staggered);

            _remainingSeconds = context.RequestedStaggerSeconds;
            context.StaggerRequested = false;

            // Buffered presses from before the hit would otherwise fire the instant control returns,
            // spending an attack or a dash the player no longer wants.
            context.InputBuffer.Clear();
        }

        /// <inheritdoc />
        protected override void OnTick(PlayerContext context, float deltaTime)
        {
            // A hit landing mid-stagger cannot re-enter this state, because the machine refuses to
            // transition into the state it is already in. It is absorbed here instead, extending the
            // interruption rather than being silently discarded.
            if (context.StaggerRequested)
            {
                _remainingSeconds = Mathf.Max(_remainingSeconds, context.RequestedStaggerSeconds);
                context.StaggerRequested = false;
            }

            _remainingSeconds -= deltaTime;

            context.Motor.Decelerate(deltaTime);
            context.Motor.Tick(deltaTime);
        }
    }

    /// <summary>
    /// Defeated. A terminal state with no outgoing transitions.
    /// </summary>
    /// <remarks>
    /// Input is suspended on entry rather than merely ignored, so that presses made during the death
    /// animation cannot be buffered and replayed if the fight is restarted.
    /// </remarks>
    public sealed class PlayerDeadState : StateBase<PlayerContext>
    {
        /// <inheritdoc />
        protected override void OnEnter(PlayerContext context)
        {
            context.SetObservableState(ObservableActionState.Dead);

            context.IsInvulnerable = true;
            context.InputBuffer.Clear();
            context.Input.SetEnabled(false);
            context.Motor.Halt();
        }

        /// <inheritdoc />
        protected override void OnTick(PlayerContext context, float deltaTime)
        {
            context.Motor.Tick(deltaTime);
        }
    }
}
