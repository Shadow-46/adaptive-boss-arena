using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Combat.Vitals;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Core.StateMachine;
using AdaptiveBossArena.Player.Controls;
using AdaptiveBossArena.Player.Movement;
using AdaptiveBossArena.Player.States;
using UnityEngine;

namespace AdaptiveBossArena.Player
{
    /// <summary>
    /// Assembles the player character and drives its state machine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The character is one component that owns several plain C# collaborators, rather than a
    /// cluster of components that find each other at runtime. Movement, health, stamina and input
    /// buffering are all testable without a scene, and the prefab stays simple enough to generate
    /// from code.
    /// </para>
    /// <para>
    /// This type implements both halves of the character's contract with the rest of the game:
    /// <see cref="IDamageable"/>, which decides whether an incoming hit lands, and
    /// <see cref="IObservablePlayer"/>, which is the sole channel through which the boss may learn
    /// anything about the player. Everything the boss will ever know passes through
    /// <see cref="CaptureObservation"/>, so that method is the place to scrutinise when judging
    /// whether the boss is playing fair.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour, IDamageable, IObservablePlayer
    {
        /// <summary>Transition priority for death, which must beat every other condition.</summary>
        private const int DeathTransitionPriority = 1000;

        /// <summary>Transition priority for being staggered.</summary>
        private const int StaggerTransitionPriority = 500;

        /// <summary>Transition priority for beginning a dash.</summary>
        private const int DashTransitionPriority = 100;

        /// <summary>Transition priority for beginning an attack, below dash by design.</summary>
        private const int AttackTransitionPriority = 90;

        /// <summary>Transition priority for beginning a heal, the least urgent action.</summary>
        private const int HealTransitionPriority = 80;

        /// <summary>Time scale granted as the perfect-dodge reward.</summary>
        /// <remarks>
        /// Slow rather than frozen. The player needs to see the opening and act inside it, which
        /// means the window has to remain playable, not merely dramatic.
        /// </remarks>
        private const float PerfectDodgeTimeScale = 0.35f;

        /// <summary>Freeze on a clean deflect. Longer than a normal hit — this is the moment to sell.</summary>
        private const float DeflectHitStopSeconds = 0.12f;

        /// <summary>Camera trauma on a clean deflect.</summary>
        private const float DeflectTrauma = 0.42f;

        /// <summary>Transition priority for raising a guard, above attacking but below dashing.</summary>
        private const int GuardTransitionPriority = 95;

        /// <summary>Transition priority for a riposte, which outranks every ordinary action.</summary>
        private const int RiposteTransitionPriority = 300;

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Character tuning values. Required.")]
        private PlayerConfig _config;

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("Camera used to interpret movement input. Falls back to the main camera when empty.")]
        private Transform _cameraTransform;

        [Header("Event Channels")]
        [SerializeField]
        [Tooltip("Broadcasts normalised health for the heads-up display.")]
        private FloatEventChannel _healthChannel;

        [SerializeField]
        [Tooltip("Broadcasts normalised stamina for the heads-up display.")]
        private FloatEventChannel _staminaChannel;

        [SerializeField]
        [Tooltip("Raised once when the player is defeated.")]
        private VoidEventChannel _deathChannel;

        [SerializeField]
        [Tooltip("Raised on a perfect dodge, for the flourish that tells the player they nailed it.")]
        private VoidEventChannel _perfectDodgeChannel;

        [SerializeField]
        [Tooltip("Raised on a clean deflect, driving the metallic ring and spark.")]
        private VoidEventChannel _deflectChannel;

        [SerializeField]
        [Tooltip("Broadcasts normalised player posture for the guard bar.")]
        private FloatEventChannel _postureChannel;

        [SerializeField]
        [Tooltip("Broadcasts normalised focus for the focus bar.")]
        private FloatEventChannel _focusChannel;

        [SerializeField]
        [Tooltip("Carries the name of a newly drawn weapon, shown briefly on screen.")]
        private StringEventChannel _weaponChannel;

        /// <summary>Health a Training-mode player is never allowed to fall below.</summary>
        private const float TrainingHealthFloor = 1f;

        private CharacterController _characterController;
        private HitFlash _hitFlash;
        private CharacterAnimator _animator;
        private WeaponSocket _weaponSocket;

        // Run-modifier state, applied by the encounter director at the start of a run. Defaults leave
        // the ordinary fight untouched.
        private float _incomingDamageMultiplier = 1f;
        private bool _immortal;
        private IPlayerInput _input;
        private ITimeService _time;
        private ICombatEventBus _events;

        private HealthPool _health;
        private StaminaPool _stamina;
        private PlayerContext _context;
        private StateMachine<PlayerContext> _machine;

        private PlayerIdleState _idleState;
        private PlayerMoveState _moveState;
        private PlayerDashState _dashState;
        private PlayerAttackState _attackState;
        private PlayerHealState _healState;
        private PlayerParryState _parryState;
        private PlayerRiposteState _riposteState;
        private PlayerStaggerState _staggerState;
        private PlayerDeadState _deadState;

        private PoisePool _posture;
        private FocusMeter _focus;
        private IScreenShake _screenShake;

        /// <summary>
        /// The opponent, used only to orient guards and riposts.
        /// </summary>
        /// <remarks>
        /// A transform and nothing more. The player may look at the boss; the reverse relationship
        /// is what the whole architecture forbids, and it stays forbidden.
        /// </remarks>
        private Transform _threat;

        /// <summary>Index into the configured weapon list.</summary>
        private int _weaponIndex;

        private bool _isInitialised;

        /// <inheritdoc />
        public CombatantTeam Team => CombatantTeam.Player;

        /// <inheritdoc />
        public bool IsAlive => _health != null && _health.IsAlive;

        /// <summary>Health pool, exposed read-only for the heads-up display.</summary>
        public IHealth Health => _health;

        /// <summary>Stamina pool, exposed read-only for the heads-up display.</summary>
        public IStaminaPool Stamina => _stamina;

        /// <summary>Name of the state the character currently occupies. For debug tooling.</summary>
        public string CurrentStateName => _machine?.CurrentState?.Name ?? "Uninitialised";

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
            _hitFlash = GetComponentInChildren<HitFlash>();
            _animator = GetComponentInChildren<CharacterAnimator>();
            _weaponSocket = GetComponentInChildren<WeaponSocket>();

            // Registered during Awake so the boss can resolve it in Start. This registration is the
            // entire surface the boss can reach the player through, and it is an interface exposing
            // only what is visible on screen.
            ServiceRegistry.Current.RegisterOrReplace<IObservablePlayer>(this);
        }

        /// <summary>
        /// Builds the character once every other object's <c>Awake</c> has run.
        /// </summary>
        /// <remarks>
        /// Deferred to <c>Start</c> because the time service is registered by the game bootstrapper
        /// during <c>Awake</c>, and Unity guarantees every <c>Awake</c> precedes every <c>Start</c>.
        /// </remarks>
        private void Start()
        {
            if (!ValidateSetup())
            {
                enabled = false;
                return;
            }

            _time = ServiceRegistry.Current.Get<ITimeService>();
            _events = ServiceRegistry.Current.Get<ICombatEventBus>();
            ServiceRegistry.Current.TryGet(out _screenShake);
            IScreenShake screenShake = _screenShake;

            var motor = new PlayerMotor(_characterController, _config, ResolveCameraTransform());
            _health = new HealthPool(_config.MaxHealth, CombatantTeam.Player);
            _stamina = new StaminaPool(
                _config.MaxStamina, _config.StaminaRegenPerSecond, _config.StaminaRegenDelaySeconds);

            // Reuses the boss's poise implementation. Posture and poise are the same mechanic seen
            // from two sides, so sharing it keeps a guard break and a stagger behaving identically.
            _posture = new PoisePool(
                _config.MaxPosture, _config.PostureRegenPerSecond, StaggerDurations.BreakSeconds);

            // Earned by defending well, spent to empower a special. Starts empty; it is a reward for
            // reading the boss within the fight, not a resource granted at its start.
            _focus = new FocusMeter(
                _config.MaxFocus, _config.FocusPerDeflect, _config.FocusPerPerfectDodge);

            var attacks = new AttackExecutor(
                transform, CombatantTeam.Player, Layers.PlayerAttackMask, _time, _events, screenShake,
                GetComponentInChildren<AttackVisualizer>());

            _context = new PlayerContext(
                _config, motor, _health, _stamina, _input, new InputBuffer(), _time, attacks, _events);

            BuildStateMachine();
            SubscribeToVitals();

            if (_config.Weapons != null && _config.Weapons.Length > 0)
            {
                EquipWeapon(_config.Weapons[0]);
            }

            _isInitialised = true;
            PublishVitals();
        }

        private void Update()
        {
            if (!_isInitialised)
            {
                return;
            }

            // While paused the combat clock is frozen, so a press recorded now would never age out
            // of the buffer and would fire the instant play resumed. Skipping the update entirely
            // is simpler and safer than trying to reason about a stopped clock.
            if (_time.IsPaused)
            {
                return;
            }

            float deltaTime = _time.DeltaTime;

            // Presses are recorded before the machine runs so that an input made this frame can be
            // acted on this frame. Recording afterwards would cost every action a frame of latency.
            _context.InputBuffer.RecordPressesFrom(_input, _time.CombatTime);

            _context.TickCooldowns(deltaTime);
            _stamina.Tick(deltaTime);

            // Posture only recovers while the guard is down, so holding block indefinitely is not a
            // way to wait out pressure.
            if (!_context.IsGuarding)
            {
                _posture.Tick(deltaTime);
            }

            UpdateThreatFacing();
            TrySwapWeapon();
            _machine.Tick(deltaTime);

            DriveAnimation();
        }

        /// <summary>Pushes the frame's visible state to the procedural animator.</summary>
        /// <remarks>
        /// Presentation only, and fed from exactly the same observable state the boss is allowed to
        /// see — the action and the attack phase — plus movement speed. The animator turns a hitbox
        /// timeline into a swing without ever touching the timeline.
        /// </remarks>
        private void DriveAnimation()
        {
            if (_animator == null)
            {
                return;
            }

            float speed01 = _config.MoveSpeed > 0f
                ? _context.Motor.PlanarVelocity.magnitude / _config.MoveSpeed
                : 0f;

            _animator.SetMotionState(_context.ObservableState, _context.Attacks.Phase, speed01);
        }

        /// <inheritdoc />
        public DamageResult TakeDamage(in DamageInfo damage)
        {
            if (!_isInitialised || !_health.IsAlive)
            {
                return DamageResult.NoDamage(DamageOutcome.Ignored);
            }

            // Friendly fire is rejected outright, independently of the physics layer setup, so a
            // mis-assigned prefab layer cannot make the player damage themselves.
            if (damage.SourceTeam == Team)
            {
                return DamageResult.NoDamage(DamageOutcome.Ignored);
            }

            if (_context.IsInvulnerable && !damage.IgnoresInvulnerability)
            {
                return ResolveEvasion();
            }

            // A perilous (unblockable) attack slips past a raised guard entirely and must be dodged;
            // the invulnerability check above still lets a dash avoid it.
            if (DefenceResolver.GuardStopsHit(_context.IsGuarding, damage.Unblockable))
            {
                return ResolveGuard(damage);
            }

            // Run modifiers scale incoming damage (Fragile) and, in Training, hold the player one hit
            // above death so a practice fight never ends. Neither changes what the hit does otherwise:
            // the flash, recoil, knockback and stagger below all still play, so training still teaches.
            float amount = damage.Amount * _incomingDamageMultiplier;

            if (_immortal)
            {
                amount = Mathf.Min(amount, Mathf.Max(0f, _health.Current - TrainingHealthFloor));
            }

            float applied = _health.Reduce(amount, damage);

            _hitFlash?.Play();
            _animator?.Recoil(damage.HitDirection);
            ApplyKnockback(damage);

            // Hyper-armour lets a committed swing carry through the hit: the damage lands, but the
            // interruption is refused, so a greatsword's heavy is not stopped by chip damage.
            float staggerSeconds = StaggerDurations.For(damage.Stagger);
            if (staggerSeconds > 0f && !ResistsIncomingStagger())
            {
                _context.RequestStagger(staggerSeconds);

                // Composure broken: the focus built from clean defence is lost with it.
                _focus?.Reset();
            }

            return DamageResult.Applied(applied, !_health.IsAlive);
        }

        /// <summary>True when the equipped weapon's hyper-armour carries the current swing through a hit.</summary>
        private bool ResistsIncomingStagger()
        {
            WeaponDefinition weapon = _context.Weapon;

            return weapon != null &&
                   _context.Attacks.IsRunning &&
                   DefenceResolver.ResistsStagger(weapon.Defence, _context.Attacks.Phase);
        }

        /// <summary>
        /// Captures what an onlooker could perceive about the player right now.
        /// </summary>
        /// <remarks>
        /// The entire anti-cheat guarantee narrows to this method. Every field assigned below
        /// corresponds to something visible on screen. Nothing here reads input, stamina, cooldowns
        /// or the invincibility flag, and nothing may be added that does.
        /// </remarks>
        /// <param name="timestamp">Combat-clock time to stamp the snapshot with.</param>
        /// <returns>A populated snapshot.</returns>
        public PlayerObservation CaptureObservation(float timestamp)
        {
            if (!_isInitialised)
            {
                return default;
            }

            return new PlayerObservation
            {
                Timestamp = timestamp,
                Position = transform.position,
                Velocity = _context.Motor.PlanarVelocity,
                Facing = transform.forward,
                ActionState = _context.ObservableState,
                TimeInActionState = _time.CombatTime - _context.ObservableStateEnteredAt,
                NormalizedHealth = _health.Normalized,
                IsValid = true
            };
        }

        /// <summary>Restores the character to its starting condition for a retry.</summary>
        /// <param name="spawnPosition">Where to place the character.</param>
        public void ResetForNewAttempt(Vector3 spawnPosition)
        {
            if (!_isInitialised)
            {
                return;
            }

            _health.ResetToFull();
            _stamina.ResetToFull();

            _context.IsInvulnerable = false;
            _context.DashCooldownRemaining = 0f;
            _context.SpecialCooldownRemaining = 0f;
            _context.HealChargesRemaining = _config.HealCharges;
            _context.ComboIndex = 0;
            _context.DashStartedAt = float.NegativeInfinity;
            _context.StaggerRequested = false;
            _context.IsGuarding = false;
            _context.RiposteConsumed = false;
            _context.IsThreatPostureBroken = false;
            _posture.ResetToFull();
            _focus.Reset();
            _context.Attacks.Cancel();
            _context.InputBuffer.Clear();
            _context.Motor.Teleport(spawnPosition);

            _input.SetEnabled(true);
            _machine.ForceState(_idleState);
            _animator?.ResetPose();

            PublishVitals();
        }

        /// <summary>Wires the states and the conditions that move between them.</summary>
        private void BuildStateMachine()
        {
            _idleState = new PlayerIdleState();
            _moveState = new PlayerMoveState();
            _dashState = new PlayerDashState();
            _attackState = new PlayerAttackState();
            _parryState = new PlayerParryState();
            _riposteState = new PlayerRiposteState();
            _healState = new PlayerHealState();
            _staggerState = new PlayerStaggerState();
            _deadState = new PlayerDeadState();

            _machine = new StateMachine<PlayerContext>(_context, _idleState);

            _machine.AddTransition(_idleState, _moveState, HasMoveInput);
            _machine.AddTransition(_moveState, _idleState, IsStandingStill);

            // Dash and attacks are offered from the grounded states only. Registering them globally
            // would let either one cancel a stagger, erasing the cost of being hit.
            AddGroundedTransitions(_idleState);
            AddGroundedTransitions(_moveState);

            _machine.AddTransition(_dashState, _moveState, DashFinishedWithInput);
            _machine.AddTransition(_dashState, _idleState, DashFinished);

            // Dash outranks the natural exit, so a player holding dash through recovery escapes the
            // moment they are allowed to rather than after the attack fully resolves.
            _machine.AddTransition(_attackState, _dashState, WantsToDashCancel, DashTransitionPriority);
            _machine.AddTransition(_attackState, _moveState, AttackFinishedWithInput);
            _machine.AddTransition(_attackState, _idleState, AttackFinished);

            _machine.AddTransition(_healState, _moveState, HealFinishedWithInput);
            _machine.AddTransition(_healState, _idleState, HealFinished);

            _machine.AddTransition(_parryState, _moveState, GuardDroppedWithInput);
            _machine.AddTransition(_parryState, _idleState, GuardDropped);

            _machine.AddTransition(_riposteState, _moveState, RiposteFinishedWithInput);
            _machine.AddTransition(_riposteState, _idleState, RiposteFinished);

            // A riposte outranks everything: an earned punish must never be swallowed by another
            // action the player happened to have buffered.
            _machine.AddGlobalTransition(_riposteState, CanRiposte, RiposteTransitionPriority);

            _machine.AddTransition(_staggerState, _moveState, StaggerFinishedWithInput);
            _machine.AddTransition(_staggerState, _idleState, StaggerFinished);

            _machine.AddGlobalTransition(_deadState, IsDefeated, DeathTransitionPriority);
            _machine.AddGlobalTransition(_staggerState, IsStaggerRequested, StaggerTransitionPriority);
        }

        /// <summary>
        /// Registers the actions available from a state in which the player has full control.
        /// </summary>
        /// <remarks>
        /// Ordering here is the character's priority of intent. Dash outranks attacking because a
        /// player who presses both under pressure meant to escape, and an attack that came out
        /// instead would feel like the game overrode them.
        /// </remarks>
        private void AddGroundedTransitions(IState<PlayerContext> from)
        {
            _machine.AddTransition(from, _dashState, WantsToDash, DashTransitionPriority);
            _machine.AddTransition(from, _parryState, WantsToGuard, GuardTransitionPriority);
            _machine.AddTransition(from, _attackState, WantsToAttack, AttackTransitionPriority);
            _machine.AddTransition(from, _healState, WantsToHeal, HealTransitionPriority);
        }

        private static bool HasMoveInput(PlayerContext context) => context.HasMoveInput;

        private static bool IsStandingStill(PlayerContext context) =>
            !context.HasMoveInput && context.Motor.IsStationary;

        /// <summary>
        /// Consumes a buffered dash press when a dash is actually possible.
        /// </summary>
        /// <remarks>
        /// The eligibility check is evaluated first on purpose. Consuming the press before knowing
        /// whether the dash can happen would swallow it, and the player would experience a dash that
        /// simply never came. Left buffered, it fires the moment stamina or the cooldown allows,
        /// which is precisely what buffering exists to do.
        /// </remarks>
        private bool WantsToDash(PlayerContext context) =>
            context.CanBeginDash &&
            context.InputBuffer.TryConsume(PlayerInputAction.Dash, _time.CombatTime);

        private bool DashFinished(PlayerContext context) => _dashState.IsComplete(context);

        private bool DashFinishedWithInput(PlayerContext context) =>
            _dashState.IsComplete(context) && context.HasMoveInput;

        /// <summary>
        /// Selects and consumes whichever attack input is buffered and currently affordable.
        /// </summary>
        /// <remarks>
        /// Heavy is tested before light so that a player pressing both, or rolling from one to the
        /// other, gets the committed option they were reaching for rather than the poke.
        /// </remarks>
        private bool WantsToAttack(PlayerContext context)
        {
            float now = _time.CombatTime;

            if (TryStartAttack(context, PlayerInputAction.HeavyAttack, context.HeavyAttack, now))
            {
                return true;
            }

            if (context.CanUseSpecial &&
                context.InputBuffer.TryConsume(PlayerInputAction.Special, now))
            {
                // A full focus meter turns the special into its empowered form and is spent doing so.
                // The empowered attack falls back to the ordinary one, so a charged meter is never
                // wasted even on a weapon without a distinct empowered version.
                bool empowered = _focus != null && _focus.TryConsumeFull();

                context.PendingAttack = empowered ? context.EmpoweredSpecialAttack : context.SpecialAttack;
                context.ComboIndex = 0;
                context.SpecialCooldownRemaining = context.Config.SpecialCooldownSeconds;
                return true;
            }

            AttackDefinition[] chain = context.LightChain;
            if (chain != null && chain.Length > 0 &&
                TryStartAttack(context, PlayerInputAction.LightAttack, chain[0], now))
            {
                return true;
            }

            return false;
        }

        /// <summary>Consumes a buffered press for one attack when it is affordable.</summary>
        private static bool TryStartAttack(
            PlayerContext context,
            PlayerInputAction action,
            AttackDefinition attack,
            float now)
        {
            // Affordability is checked before consuming, for the same reason as the dash: swallowing
            // a press the character could not act on makes the game look like it dropped an input.
            if (attack == null || !context.Stamina.CanSpend(attack.StaminaCost))
            {
                return false;
            }

            if (!context.InputBuffer.TryConsume(action, now))
            {
                return false;
            }

            context.PendingAttack = attack;
            context.ComboIndex = 0;
            return true;
        }

        /// <summary>
        /// Raises the guard while the control is held.
        /// </summary>
        /// <remarks>
        /// Tested against the held state rather than a buffered press, because the deflect window
        /// opens the instant the guard rises. Consuming a buffered press could open that window a
        /// frame or two after the player pressed, which is enough to turn a deflect into a block and
        /// would feel like the game stealing the input.
        /// </remarks>
        private static bool WantsToGuard(PlayerContext context) =>
            context.Input.IsHeld(PlayerInputAction.Guard);

        private bool GuardDropped(PlayerContext context) => _parryState.IsComplete(context);

        private bool GuardDroppedWithInput(PlayerContext context) =>
            _parryState.IsComplete(context) && context.HasMoveInput;

        /// <summary>True when the boss's guard is broken and the punish has not been spent.</summary>
        private static bool CanRiposte(PlayerContext context) =>
            context.IsThreatPostureBroken && !context.RiposteConsumed;

        private bool RiposteFinished(PlayerContext context) => _riposteState.IsComplete(context);

        private bool RiposteFinishedWithInput(PlayerContext context) =>
            _riposteState.IsComplete(context) && context.HasMoveInput;

        /// <summary>
        /// Cycles to the next weapon when the player asks and is free to do so.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Refused mid-action on purpose. Swapping out of a swing would let the player cancel any
        /// commitment for free, which would undo the entire point of attacks having recovery.
        /// </para>
        /// <para>
        /// The swap is published as a combat occurrence, so the boss can observe which weapon the
        /// player favours. That is legitimately visible — the weapon is in their hands — and it turns
        /// the weapon system into something the adaptation feeds on rather than something that
        /// dilutes it.
        /// </para>
        /// </remarks>
        private void TrySwapWeapon()
        {
            WeaponDefinition[] weapons = _config.Weapons;

            if (weapons == null || weapons.Length < 2)
            {
                return;
            }

            bool isFree = _machine.CurrentState == _idleState || _machine.CurrentState == _moveState;

            if (!isFree || !_context.InputBuffer.TryConsume(PlayerInputAction.SwapWeapon, _time.CombatTime))
            {
                return;
            }

            _weaponIndex = (_weaponIndex + 1) % weapons.Length;
            EquipWeapon(weapons[_weaponIndex]);
        }

        /// <summary>Draws a weapon and announces it.</summary>
        private void EquipWeapon(WeaponDefinition weapon)
        {
            if (weapon == null)
            {
                return;
            }

            _context.Weapon = weapon;
            _context.ComboIndex = 0;

            // Swaps the held model to match. Null model today, so this clears the socket and the
            // weapon still reads through its trail and swing colour.
            _weaponSocket?.Equip(weapon.ModelPrefab);

            _weaponChannel?.Raise(weapon.DisplayName);
            _context.PublishCombatEvent(CombatEventKind.WeaponDrawn);
        }

        /// <summary>Points the context at the boss so guarding and riposting face the right way.</summary>
        private void UpdateThreatFacing()
        {
            if (_threat == null)
            {
                return;
            }

            Vector3 toThreat = _threat.position - transform.position;
            toThreat.y = 0f;

            if (toThreat.sqrMagnitude > Mathf.Epsilon)
            {
                _context.FacingTowardThreat = toThreat.normalized;
            }
        }

        private bool WantsToHeal(PlayerContext context) =>
            context.CanHeal && context.InputBuffer.TryConsume(PlayerInputAction.Heal, _time.CombatTime);

        /// <summary>Consumes a buffered dash when the attack has reached a cancellable phase.</summary>
        private bool WantsToDashCancel(PlayerContext context) =>
            _attackState.CanDashCancel(context) && WantsToDash(context);

        private bool AttackFinished(PlayerContext context) => _attackState.IsComplete(context);

        private bool AttackFinishedWithInput(PlayerContext context) =>
            _attackState.IsComplete(context) && context.HasMoveInput;

        private bool HealFinished(PlayerContext context) => _healState.IsComplete(context);

        private bool HealFinishedWithInput(PlayerContext context) =>
            _healState.IsComplete(context) && context.HasMoveInput;

        private bool StaggerFinished(PlayerContext context) => _staggerState.IsComplete(context);

        private bool StaggerFinishedWithInput(PlayerContext context) =>
            _staggerState.IsComplete(context) && context.HasMoveInput;

        private static bool IsDefeated(PlayerContext context) => !context.Health.IsAlive;

        private static bool IsStaggerRequested(PlayerContext context) => context.StaggerRequested;

        /// <summary>
        /// Decides whether an avoided hit was merely dodged or dodged perfectly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The distinction is timing, and only timing. A hit negated within a short window of the
        /// dash beginning means the player read the attack and committed at the last moment; the
        /// same hit negated later in the same dash means they happened to already be moving. Both
        /// avoid damage, but only the first is a read worth rewarding.
        /// </para>
        /// <para>
        /// The reward is slow-motion rather than damage or resources, because what a perfect dodge
        /// should buy is the clarity to act on the opening it just created.
        /// </para>
        /// </remarks>
        private DamageResult ResolveEvasion()
        {
            float sinceDashBegan = _time.CombatTime - _context.DashStartedAt;

            if (sinceDashBegan > GameplayConstants.PerfectDodgeWindowSeconds)
            {
                return DamageResult.NoDamage(DamageOutcome.Invulnerable);
            }

            _time.RequestSlowMotion(
                PerfectDodgeTimeScale, GameplayConstants.PerfectDodgeSlowMotionSeconds);

            _context.PublishCombatEvent(CombatEventKind.PerfectDodge, _context.DashDirection);
            _perfectDodgeChannel?.Raise();
            _focus?.AddFromPerfectDodge();

            return DamageResult.NoDamage(DamageOutcome.PerfectDodged);
        }

        /// <summary>
        /// Decides whether a guarded hit was deflected on the beat or merely blocked late.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The whole rhythm of the fight lives in this one branch. A deflect negates the hit
        /// entirely, costs the attacker posture, and rewards the player with hit-stop and a
        /// metallic ring — standing your ground made progress. A late block survives the hit but
        /// pays chip damage and the player's own posture, so turtling loses slowly.
        /// </para>
        /// <para>
        /// Note that this is timing alone. There is no leniency scaling with difficulty and no
        /// hidden assistance; the window is the window, which is what makes learning it feel like
        /// the player improved rather than the game relented.
        /// </para>
        /// </remarks>
        private DamageResult ResolveGuard(in DamageInfo damage)
        {
            if (_parryState.IsInDeflectWindow(_context))
            {
                _time.RequestHitStop(DeflectHitStopSeconds);
                _screenShake?.AddTrauma(DeflectTrauma);
                _screenShake?.Punch(DeflectTrauma);

                // Carries the incoming direction so the sparks are thrown back the way the blow
                // came from rather than in an undirected puff.
                _context.PublishCombatEvent(CombatEventKind.Deflected, damage.HitDirection);
                _deflectChannel?.Raise();
                _focus?.AddFromDeflect();

                return DamageResult.NoDamage(DamageOutcome.Blocked);
            }

            // Late. The hit is survived but it costs, and enough of these break the guard outright. A
            // sustained guard bleeds slower here, which is what lets it be held rather than only timed.
            float chip = damage.Amount * _config.BlockChipFraction;
            float applied = _health.Reduce(chip, damage);

            float postureCost = _config.BlockPostureCost *
                                (_context.Weapon != null
                                    ? DefenceResolver.BlockPostureMultiplier(_context.Weapon.Defence)
                                    : 1f);

            if (_posture.ApplyPoiseDamage(postureCost))
            {
                _context.RequestStagger(StaggerDurations.BreakSeconds);
                _focus?.Reset();
            }

            _hitFlash?.Play();
            _animator?.Recoil(damage.HitDirection);
            _context.PublishCombatEvent(CombatEventKind.Blocked, damage.HitDirection);

            return DamageResult.Applied(applied, !_health.IsAlive);
        }

        /// <summary>Pushes the character away from an impact.</summary>
        private void ApplyKnockback(in DamageInfo damage)
        {
            if (damage.KnockbackSpeed <= 0f)
            {
                return;
            }

            Vector3 direction = damage.HitDirection;
            direction.y = 0f;

            if (direction.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            _context.Motor.SetPlanarVelocity(direction.normalized * damage.KnockbackSpeed);
        }

        /// <summary>Relays pool changes onto the event channels the interface listens to.</summary>
        private void SubscribeToVitals()
        {
            _health.Changed += args => _healthChannel?.Raise(args.Normalized);
            _stamina.Changed += args => _staminaChannel?.Raise(args.Normalized);
            _posture.Changed += args => _postureChannel?.Raise(args.Normalized);
            _focus.Changed += args => _focusChannel?.Raise(args.Normalized);
            _health.Died += _ => _deathChannel?.Raise();
        }

        /// <summary>Publishes the starting values so bars are correct before the first change.</summary>
        private void PublishVitals()
        {
            _healthChannel?.Raise(_health.Normalized);
            _staminaChannel?.Raise(_stamina.Normalized);
            _focusChannel?.Raise(_focus.Normalized);
        }

        /// <summary>Resolves the camera to interpret input against.</summary>
        private Transform ResolveCameraTransform()
        {
            if (_cameraTransform != null)
            {
                return _cameraTransform;
            }

            Camera main = Camera.main;
            return main != null ? main.transform : null;
        }

        /// <summary>Reports missing dependencies clearly rather than failing with a null reference.</summary>
        private bool ValidateSetup()
        {
            if (_config == null)
            {
                Debug.LogError(
                    "[Adaptive Boss Arena] PlayerController has no PlayerConfig assigned and has been " +
                    "disabled.", this);
                return false;
            }

            if (_input == null)
            {
                Debug.LogError(
                    "[Adaptive Boss Arena] PlayerController requires a PlayerInputReader on the same " +
                    "object and has been disabled.", this);
                return false;
            }

            if (!ServiceRegistry.Current.TryGet(out ITimeService _))
            {
                Debug.LogError(
                    "[Adaptive Boss Arena] No ITimeService is registered. Add a GameBootstrapper to the " +
                    "scene; it must run before the player initialises.", this);
                return false;
            }

            return true;
        }

        /// <summary>Assigns the configuration. Used by the prefab generator.</summary>
        /// <param name="config">Character tuning values.</param>
        public void SetConfig(PlayerConfig config) => _config = config;

        /// <summary>
        /// Tells the player where the opponent is, and whether its guard is currently broken.
        /// </summary>
        /// <remarks>
        /// Pushed in by the encounter director rather than pulled, because the player must not hold
        /// a reference to the boss any more than the boss holds one to the player. A transform and a
        /// flag are the whole of what crosses.
        /// </remarks>
        /// <param name="threat">The opponent's transform.</param>
        public void SetThreat(Transform threat) => _threat = threat;

        /// <summary>
        /// Applies the run's challenge modifiers to the player.
        /// </summary>
        /// <remarks>
        /// Called once by the encounter director at the start of a run. Every value defaults to the
        /// ordinary fight, so an unmodified run passes the defaults straight through and nothing here
        /// changes how the player plays.
        /// </remarks>
        /// <param name="healingEnabled">Whether the player may heal.</param>
        /// <param name="incomingDamageMultiplier">Multiplier on damage the player takes.</param>
        /// <param name="immortal">When true, the player is held one hit above death (Training).</param>
        public void ApplyRunModifiers(bool healingEnabled, float incomingDamageMultiplier, bool immortal)
        {
            _incomingDamageMultiplier = Mathf.Max(0f, incomingDamageMultiplier);
            _immortal = immortal;

            if (_context != null)
            {
                _context.HealingEnabled = healingEnabled;
            }
        }

        /// <summary>Reports whether the opponent's guard is broken, opening a riposte.</summary>
        /// <param name="isBroken">True while the opponent is staggered by a posture break.</param>
        public void SetThreatPostureBroken(bool isBroken)
        {
            if (_context == null)
            {
                return;
            }

            // Leaving the break resets the spent flag, so the next break offers a fresh punish.
            if (!isBroken)
            {
                _context.RiposteConsumed = false;
            }

            _context.IsThreatPostureBroken = isBroken;
        }

        /// <summary>Player posture, exposed for the guard bar.</summary>
        public IPoise Posture => _posture;

        /// <summary>Posture a clean deflect deals to the attacker.</summary>
        public float DeflectPostureDamage => _config != null ? _config.DeflectPostureDamage : 0f;

        /// <summary>Assigns the event channels. Used by the prefab generator.</summary>
        /// <param name="health">Normalised health channel.</param>
        /// <param name="stamina">Normalised stamina channel.</param>
        /// <param name="death">Player death channel.</param>
        /// <param name="perfectDodge">Perfect dodge channel.</param>
        /// <param name="deflect">Clean deflect channel.</param>
        /// <param name="posture">Normalised posture channel.</param>
        /// <param name="focus">Normalised focus channel.</param>
        public void SetChannels(
            FloatEventChannel health,
            FloatEventChannel stamina,
            VoidEventChannel death,
            VoidEventChannel perfectDodge,
            VoidEventChannel deflect,
            FloatEventChannel posture,
            StringEventChannel weapon,
            FloatEventChannel focus)
        {
            _healthChannel = health;
            _staminaChannel = stamina;
            _deathChannel = death;
            _perfectDodgeChannel = perfectDodge;
            _deflectChannel = deflect;
            _postureChannel = posture;
            _weaponChannel = weapon;
            _focusChannel = focus;
        }
    }
}
