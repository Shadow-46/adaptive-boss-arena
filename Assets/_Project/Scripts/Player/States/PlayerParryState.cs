using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.StateMachine;
using UnityEngine;

namespace AdaptiveBossArena.Player.States
{
    /// <summary>
    /// Holding a guard, and within the opening moments of it, a deflect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The defining mechanic of the fight. Pressing guard opens a brief deflect window; a hit
    /// arriving inside it is turned aside entirely, costs the attacker posture, and gives the
    /// unmistakable metallic clang and freeze that makes the timing worth learning. Press too early
    /// or hold too long and the same input becomes an ordinary block: the hit is survived, but it
    /// costs chip damage and the defender's own posture.
    /// </para>
    /// <para>
    /// Deflecting is deliberately the aggressive option rather than the safe one. Blocking bleeds
    /// your posture until it breaks; deflecting bleeds theirs. That inversion is what stops the
    /// fight becoming a turtle, and it is what makes the boss's feints matter once it learns you
    /// deflect on a rhythm.
    /// </para>
    /// <para>
    /// The state exposes its window rather than resolving damage itself. The player's
    /// <see cref="IDamageable"/> implementation is the only place that decides what happens to an
    /// incoming hit, so all the timing knowledge lives here and all the resolution lives there.
    /// </para>
    /// </remarks>
    public sealed class PlayerParryState : StateBase<PlayerContext>
    {
        /// <summary>How long after pressing guard a hit is deflected rather than blocked.</summary>
        public const float DeflectWindowSeconds = 0.2f;

        /// <summary>Shortest time the guard is held, so a tap still reads as a deliberate action.</summary>
        private const float MinimumGuardSeconds = 0.2f;

        /// <summary>Longest the guard may be held before the player is forced to drop it.</summary>
        private const float MaximumGuardSeconds = 1.6f;

        /// <summary>Stamina drained per second while guarding, so holding it is not free.</summary>
        private const float GuardStaminaDrainPerSecond = 12f;

        /// <summary>True while a hit arriving now would be deflected outright.</summary>
        public bool IsInDeflectWindow(PlayerContext context) =>
            context.CanDeflect && TimeInState <= context.DeflectWindowSeconds;

        /// <summary>True once the guard may be released.</summary>
        /// <param name="context">The player context.</param>
        /// <returns>Whether the player may act again.</returns>
        public bool IsComplete(PlayerContext context)
        {
            if (TimeInState < MinimumGuardSeconds)
            {
                return false;
            }

            // Released early if the button is let go, dropped automatically if held too long or if
            // stamina runs dry. A guard that could be held forever would remove the decision.
            bool stillHeld = context.Input.IsHeld(Controls.PlayerInputAction.Guard);

            return !stillHeld ||
                   TimeInState >= MaximumGuardSeconds ||
                   context.Stamina.Current <= 0f;
        }

        /// <inheritdoc />
        protected override void OnEnter(PlayerContext context)
        {
            context.SetObservableState(ObservableActionState.Guarding);
            context.IsGuarding = true;

            context.PublishCombatEvent(CombatEventKind.GuardRaised);
        }

        /// <inheritdoc />
        protected override void OnTick(PlayerContext context, float deltaTime)
        {
            // Guarding roots the player. Being able to walk while guarding would make it strictly
            // better than moving, and the fight would stop having spacing in it.
            context.Motor.Decelerate(deltaTime);
            context.Motor.Tick(deltaTime);

            context.Motor.FaceDirection(context.FacingTowardThreat, deltaTime);

            // Only the sustained portion costs stamina. Charging for the deflect window itself would
            // punish the precise timing the whole mechanic is trying to encourage.
            if (!IsInDeflectWindow(context))
            {
                context.Stamina.TrySpend(GuardStaminaDrainPerSecond * deltaTime);
            }
        }

        /// <inheritdoc />
        protected override void OnExit(PlayerContext context)
        {
            context.IsGuarding = false;
        }
    }

    /// <summary>
    /// The riposte: a single devastating strike into a broken guard.
    /// </summary>
    /// <remarks>
    /// Only reachable when the boss's posture has broken, and only for a short window. It is the
    /// entire payoff of the deflect system — every deflect is a small deposit toward this, which is
    /// what makes standing your ground feel like progress rather than mere survival.
    /// </remarks>
    public sealed class PlayerRiposteState : StateBase<PlayerContext>
    {
        /// <summary>True once the riposte has finished.</summary>
        /// <param name="context">The player context.</param>
        /// <returns>Whether the player may act again.</returns>
        public bool IsComplete(PlayerContext context) => !context.Attacks.IsRunning;

        /// <inheritdoc />
        protected override void OnEnter(PlayerContext context)
        {
            context.SetObservableState(ObservableActionState.HeavyAttacking);
            context.RiposteConsumed = true;

            // The execution: a heavier, multi-hit answer to a broken guard, falling back to the
            // ordinary riposte when none is authored.
            AttackDefinition execution = context.ExecutionAttack;

            if (execution == null)
            {
                return;
            }

            // Snapped toward the target rather than eased, because a riposte that missed because the
            // character was still turning would feel like the game refusing an earned reward.
            context.Motor.SnapToDirection(context.FacingTowardThreat);
            context.Attacks.Begin(execution);

            context.PublishCombatEvent(CombatEventKind.Riposte);
        }

        /// <inheritdoc />
        protected override void OnTick(PlayerContext context, float deltaTime)
        {
            context.Motor.Decelerate(deltaTime);
            context.Motor.Tick(deltaTime);
            context.Attacks.Tick(deltaTime);
        }

        /// <inheritdoc />
        protected override void OnExit(PlayerContext context)
        {
            if (context.Attacks.IsRunning)
            {
                context.Attacks.Cancel();
            }
        }
    }
}
