using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.StateMachine;
using AdaptiveBossArena.Player.Controls;
using UnityEngine;

namespace AdaptiveBossArena.Player.States
{
    /// <summary>
    /// The knight's air game: a rising slash out of a roll, cuts thrown in the air, and a plunge that slams the floor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The big action games let the player leave the ground on their own terms. Here it starts from the heavy attack
    /// pressed out of a roll: the knight launches himself upward in a rising cut. In the air, a light attack throws a
    /// cut and hangs him a moment longer, twice at most; a heavy attack drives him straight down into a plunge that
    /// lands as a slam around him. Without a plunge he simply lands and recovers.
    /// </para>
    /// <para>
    /// Igris is never juggled - he is too big, and the leap is the knight's own - and every part of it is visible
    /// for what it is: the rise and the plunge read as heavy attacks, the air cuts as light ones, so the boss can
    /// answer them exactly as he answers anything else a watcher could see. A hard cap on time aloft means the
    /// state can never strand the knight in the air.
    /// </para>
    /// </remarks>
    public sealed class PlayerAerialState : StateBase<PlayerContext>
    {
        /// <summary>How fast the rising slash throws the knight upward.</summary>
        public const float LaunchSpeed = 7.5f;

        /// <summary>The small lift each air cut gives, so a combo hangs rather than drops.</summary>
        public const float AirCutLift = 2.5f;

        /// <summary>How fast the plunge drives the knight down.</summary>
        public const float PlungeSpeed = -18f;

        /// <summary>How many cuts the knight may throw in one leap.</summary>
        public const int MaximumAirCuts = 2;

        /// <summary>The longest a leap may last, landing or not.</summary>
        public const float MaximumAirSeconds = 1.8f;

        /// <summary>How long the knight takes to recover from a landing before he may act.</summary>
        public const float LandingRecoverySeconds = 0.3f;

        /// <summary>How far forward the leap carries him, in metres a second.</summary>
        private const float ForwardDrift = 2.5f;

        /// <summary>The time before a landing may count, so the leap's own first frame is not taken for one.</summary>
        private const float LiftOffSeconds = 0.12f;

        private enum Stage
        {
            Aloft,
            Plunging,
            Landed
        }

        private Stage _stage = Stage.Landed;
        private float _elapsed;
        private float _landedFor;
        private int _airCuts;

        /// <summary>True once the knight has landed and recovered.</summary>
        /// <param name="context">The player context.</param>
        /// <returns>Whether the knight may act again.</returns>
        public bool IsComplete(PlayerContext context) =>
            _stage == Stage.Landed && _landedFor >= LandingRecoverySeconds && !context.Attacks.IsRunning;

        /// <summary>Whether the knight is off the ground in a leap of his own. For tests and debug tooling.</summary>
        public bool IsAloft => _stage != Stage.Landed;

        /// <inheritdoc />
        protected override void OnEnter(PlayerContext context)
        {
            _stage = Stage.Aloft;
            _elapsed = 0f;
            _landedFor = 0f;
            _airCuts = 0;

            context.Attacks.Cancel();
            Strike(context, context.LauncherAttack, ObservableActionState.HeavyAttacking);

            context.Motor.SetPlanarVelocity(context.Motor.Facing * ForwardDrift);
            context.Motor.Launch(LaunchSpeed);
        }

        /// <inheritdoc />
        protected override void OnTick(PlayerContext context, float deltaTime)
        {
            _elapsed += deltaTime;
            context.Motor.Tick(deltaTime);
            context.Attacks.Tick(deltaTime * context.AttackSpeedMultiplier);

            if (_stage == Stage.Landed)
            {
                _landedFor += deltaTime;
                context.Motor.Decelerate(deltaTime);
                return;
            }

            float now = context.Time.CombatTime;

            if (_stage == Stage.Aloft && !context.Attacks.IsRunning)
            {
                if (context.InputBuffer.TryConsume(PlayerInputAction.HeavyAttack, now))
                {
                    _stage = Stage.Plunging;
                    context.SetObservableState(ObservableActionState.HeavyAttacking);
                    context.Motor.Halt();
                    context.Motor.Launch(PlungeSpeed);
                }
                else if (_airCuts < MaximumAirCuts && context.AirSlashAttack != null &&
                         context.InputBuffer.TryConsume(PlayerInputAction.LightAttack, now))
                {
                    _airCuts++;
                    Strike(context, context.AirSlashAttack, ObservableActionState.LightAttacking);
                    context.Motor.Launch(AirCutLift);
                }
            }

            bool landed = _elapsed >= LiftOffSeconds && context.Motor.IsGrounded && context.Motor.VerticalVelocity <= 0f;

            if (landed || _elapsed >= MaximumAirSeconds)
            {
                Land(context);
            }
        }

        /// <summary>Brings the knight down: a plunge lands as a slam, anything else as a plain landing.</summary>
        private void Land(PlayerContext context)
        {
            if (_stage == Stage.Plunging && context.PlungeAttack != null)
            {
                Strike(context, context.PlungeAttack, ObservableActionState.HeavyAttacking);
            }

            _stage = Stage.Landed;
            _landedFor = 0f;
            context.Motor.Halt();
        }

        /// <summary>Begins one of the leap's blows, paying its stamina and showing it for what it is.</summary>
        private static void Strike(PlayerContext context, AttackDefinition attack, ObservableActionState shown)
        {
            if (attack == null)
            {
                return;
            }

            context.SetObservableState(shown);
            context.Stamina.TrySpend(attack.StaminaCost);
            context.Attacks.Begin(attack);
        }
    }
}
