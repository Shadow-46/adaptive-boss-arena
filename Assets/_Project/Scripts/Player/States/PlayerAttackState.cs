using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.StateMachine;
using AdaptiveBossArena.Player.Controls;
using UnityEngine;

namespace AdaptiveBossArena.Player.States
{
    /// <summary>
    /// Performing an attack, including chaining into the next link of a combo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One state covers every attack the player has. What distinguishes a light from a heavy is
    /// entirely data on the <see cref="AttackDefinition"/>, so a state per attack would be five
    /// copies of the same logic differing only in which asset they read.
    /// </para>
    /// <para>
    /// Chaining happens inside the state rather than through a transition out and back. Leaving and
    /// re-entering would reset the state's own timing and lose the combo index, and it would let
    /// another transition slip in between two links of a combo the player has already committed to.
    /// </para>
    /// <para>
    /// Movement is surrendered for the duration. The only motion an attack produces is its lunge,
    /// which is what makes a committed swing a real decision about position as well as timing.
    /// </para>
    /// </remarks>
    public sealed class PlayerAttackState : StateBase<PlayerContext>
    {
        /// <summary>Fraction of normal speed available while winding up, for aiming the swing.</summary>
        private const float StartupControlFraction = 0.35f;

        /// <summary>True once the attack, and any combo it chained into, has finished.</summary>
        /// <param name="context">The player context.</param>
        /// <returns>Whether the player may act again.</returns>
        public bool IsComplete(PlayerContext context) => !context.Attacks.IsRunning;

        /// <inheritdoc />
        protected override void OnEnter(PlayerContext context)
        {
            BeginAttack(context, context.PendingAttack);
        }

        /// <inheritdoc />
        protected override void OnTick(PlayerContext context, float deltaTime)
        {
            ApplyAttackMovement(context, deltaTime);

            context.Motor.Tick(deltaTime);
            context.Attacks.Tick(deltaTime);

            TryChainCombo(context);
        }

        /// <summary>
        /// True once a dash may cancel out of the attack.
        /// </summary>
        /// <remarks>
        /// Cancelling into a dash during recovery is what stops a committed attack from feeling like
        /// a punishment for having pressed the button. It is deliberately unavailable until the
        /// hitbox has closed, so the commitment is still real.
        /// </remarks>
        /// <param name="context">The player context.</param>
        /// <returns>Whether a dash may interrupt the attack.</returns>
        public bool CanDashCancel(PlayerContext context) =>
            context.Attacks.Phase == AttackPhase.Recovery;

        /// <inheritdoc />
        protected override void OnExit(PlayerContext context)
        {
            // Leaving mid-swing means something interrupted the player. The attack must be torn down
            // or its hitbox would still be considered open by the executor on the next attack.
            if (context.Attacks.IsRunning)
            {
                context.Attacks.Cancel();
            }

            context.ComboIndex = 0;
        }

        /// <summary>Starts one attack and pays for it.</summary>
        private static void BeginAttack(PlayerContext context, AttackDefinition attack)
        {
            if (attack == null)
            {
                return;
            }

            context.SetObservableState(ObservableStateFor(attack.DamageType));
            context.Stamina.TrySpend(attack.StaminaCost);

            // Facing is committed at the moment of the swing. Allowing it to keep tracking would let
            // the player turn to follow a dodging opponent mid-attack, which removes the cost of
            // choosing the wrong moment.
            if (context.HasMoveInput)
            {
                context.Motor.SnapToDirection(context.Motor.ToWorldDirection(context.Input.MoveDirection));
            }

            context.Attacks.Begin(attack);
        }

        /// <summary>
        /// Moves the character during an attack: lunge, then commitment, then recovery of control.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Freezing the character for the whole attack was wrong, and badly so on the heavy, which
        /// runs almost a full second. With no animation to justify it the character simply appears
        /// to stop responding, which reads as the game hanging rather than as a committed swing.
        /// </para>
        /// <para>
        /// Control now returns progressively. Startup allows a fraction of normal speed so a swing
        /// can be aimed; the active window is fully committed, because that is the moment the
        /// decision is being paid for; and control returns during the back half of recovery. The
        /// attack still costs real position, but the character never stops answering the stick.
        /// </para>
        /// </remarks>
        private static void ApplyAttackMovement(PlayerContext context, float deltaTime)
        {
            AttackDefinition attack = context.Attacks.CurrentAttack;

            if (attack == null)
            {
                context.Motor.Decelerate(deltaTime);
                return;
            }

            AttackPhase phase = context.Attacks.Phase;

            if (phase == AttackPhase.Startup && attack.LungeSpeed > 0f)
            {
                context.Motor.SetPlanarVelocity(context.Motor.Facing * attack.LungeSpeed);
                return;
            }

            float control = ResolveControlFraction(context, attack, phase);

            if (control <= 0f || !context.HasMoveInput)
            {
                context.Motor.Decelerate(deltaTime);
                return;
            }

            context.Motor.ApplyMoveInput(context.Input.MoveDirection * control, deltaTime);
        }

        /// <summary>How much of normal movement speed the character may use in the current phase.</summary>
        private static float ResolveControlFraction(
            PlayerContext context,
            AttackDefinition attack,
            AttackPhase phase)
        {
            switch (phase)
            {
                case AttackPhase.Startup:
                    // Enough to adjust a swing, not enough to reposition with it.
                    return StartupControlFraction;

                case AttackPhase.Recovery:
                    // Control returns over the back half of recovery, so a long heavy recovery is
                    // felt as weight rather than as a dropped input.
                    float intoRecovery = context.Attacks.ElapsedSeconds - attack.ActiveEndSeconds;
                    float fraction = attack.RecoverySeconds <= 0f
                        ? 1f
                        : Mathf.Clamp01(intoRecovery / attack.RecoverySeconds);

                    return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1f, fraction));

                default:
                    return 0f;
            }
        }

        /// <summary>Advances to the next link of the light combo when the window allows it.</summary>
        private static void TryChainCombo(PlayerContext context)
        {
            if (!context.Attacks.IsInComboWindow)
            {
                return;
            }

            AttackDefinition[] chain = context.LightChain;
            int nextIndex = context.ComboIndex + 1;

            if (chain == null || nextIndex >= chain.Length || chain[nextIndex] == null)
            {
                return;
            }

            if (!context.InputBuffer.TryConsume(PlayerInputAction.LightAttack, context.Time.CombatTime))
            {
                return;
            }

            context.ComboIndex = nextIndex;
            context.PendingAttack = chain[nextIndex];

            BeginAttack(context, chain[nextIndex]);
        }

        /// <summary>Maps an attack's category onto what an onlooker would see.</summary>
        private static ObservableActionState ObservableStateFor(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Heavy: return ObservableActionState.HeavyAttacking;
                case DamageType.Special: return ObservableActionState.UsingAbility;
                default: return ObservableActionState.LightAttacking;
            }
        }
    }

    /// <summary>
    /// Channelling a heal.
    /// </summary>
    /// <remarks>
    /// The channel is long and the state is conspicuous on purpose. Healing has to be a decision the
    /// boss can legitimately punish, otherwise the player can top up whenever they please and the
    /// fight loses its pressure. It is also one of the habits the boss profiles, so a player who
    /// always heals at the same moment will find that moment stops being safe.
    /// </remarks>
    public sealed class PlayerHealState : StateBase<PlayerContext>
    {
        private bool _wasInterrupted;

        /// <summary>True once the channel has run its course.</summary>
        /// <param name="context">The player context.</param>
        /// <returns>Whether the player may act again.</returns>
        public bool IsComplete(PlayerContext context) =>
            _wasInterrupted || TimeInState >= context.Config.HealChannelSeconds;

        /// <inheritdoc />
        protected override void OnEnter(PlayerContext context)
        {
            context.SetObservableState(ObservableActionState.Healing);

            _wasInterrupted = false;
            context.HealChargesRemaining = Mathf.Max(0, context.HealChargesRemaining - 1);

            context.PublishCombatEvent(CombatEventKind.HealStarted);
        }

        /// <inheritdoc />
        protected override void OnTick(PlayerContext context, float deltaTime)
        {
            context.Motor.Decelerate(deltaTime);
            context.Motor.Tick(deltaTime);

            // A stagger request arriving mid-channel means a hit landed. The charge is already spent,
            // so being interrupted costs the player the heal outright.
            if (context.StaggerRequested)
            {
                _wasInterrupted = true;
            }
        }

        /// <inheritdoc />
        protected override void OnExit(PlayerContext context)
        {
            if (_wasInterrupted)
            {
                return;
            }

            context.Health.Heal(context.Config.HealAmount);
            context.PublishCombatEvent(CombatEventKind.HealCompleted);
        }
    }
}
