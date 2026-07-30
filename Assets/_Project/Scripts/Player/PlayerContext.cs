using System;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Combat.Vitals;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Player.Controls;
using AdaptiveBossArena.Player.Movement;
using UnityEngine;

namespace AdaptiveBossArena.Player
{
    /// <summary>
    /// Shared state and services handed to every player state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// States are stateless behaviours; everything that persists across a transition lives here.
    /// That keeps a state safe to re-enter and means the whole player can be inspected from one
    /// object in the debugger rather than pieced together from whichever state is active.
    /// </para>
    /// <para>
    /// The mutable properties are the handshake between states. A state does not reach into another
    /// state to change it; it writes here, and a transition reads it.
    /// </para>
    /// </remarks>
    public sealed class PlayerContext
    {
        /// <summary>Creates a context.</summary>
        /// <param name="config">Character tuning values.</param>
        /// <param name="motor">Movement driver.</param>
        /// <param name="health">Health pool.</param>
        /// <param name="stamina">Stamina pool.</param>
        /// <param name="input">Control source.</param>
        /// <param name="inputBuffer">Buffer of recent presses.</param>
        /// <param name="time">Time service supplying the combat clock.</param>
        /// <exception cref="ArgumentNullException">Thrown when any dependency is missing.</exception>
        public PlayerContext(
            PlayerConfig config,
            PlayerMotor motor,
            HealthPool health,
            StaminaPool stamina,
            IPlayerInput input,
            InputBuffer inputBuffer,
            ITimeService time,
            AttackExecutor attacks,
            ICombatEventBus events)
        {
            Config = config != null ? config : throw new ArgumentNullException(nameof(config));
            Motor = motor ?? throw new ArgumentNullException(nameof(motor));
            Health = health ?? throw new ArgumentNullException(nameof(health));
            Stamina = stamina ?? throw new ArgumentNullException(nameof(stamina));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            InputBuffer = inputBuffer ?? throw new ArgumentNullException(nameof(inputBuffer));
            Time = time ?? throw new ArgumentNullException(nameof(time));
            Attacks = attacks ?? throw new ArgumentNullException(nameof(attacks));
            Events = events ?? throw new ArgumentNullException(nameof(events));

            HealChargesRemaining = config.HealCharges;
        }

        /// <summary>Character tuning values.</summary>
        public PlayerConfig Config { get; }

        /// <summary>Movement driver.</summary>
        public PlayerMotor Motor { get; }

        /// <summary>Health pool.</summary>
        public HealthPool Health { get; }

        /// <summary>Stamina pool.</summary>
        public StaminaPool Stamina { get; }

        /// <summary>Control source.</summary>
        public IPlayerInput Input { get; }

        /// <summary>Buffer of recent action presses.</summary>
        public InputBuffer InputBuffer { get; }

        /// <summary>Time service supplying the combat clock.</summary>
        public ITimeService Time { get; }

        /// <summary>Runs the player's attacks and resolves what they hit.</summary>
        public AttackExecutor Attacks { get; }

        /// <summary>Bus that witnessed occurrences are published to.</summary>
        public ICombatEventBus Events { get; }

        /// <summary>The attack the next entry into the attack state should perform.</summary>
        public AttackDefinition PendingAttack { get; set; }

        /// <summary>Index reached in the light attack chain, reset when the chain ends.</summary>
        public int ComboIndex { get; set; }

        /// <summary>Seconds remaining before the special ability may be used again.</summary>
        public float SpecialCooldownRemaining { get; set; }

        /// <summary>Heals still available this attempt.</summary>
        public int HealChargesRemaining { get; set; }

        /// <summary>
        /// Combat-clock time the current dash began.
        /// </summary>
        /// <remarks>
        /// Perfect dodges are judged from this. A hit negated shortly after a dash started means the
        /// player committed at the last possible moment and has earned the reward; one negated later
        /// in the same dash was simply a dodge that happened to be in progress.
        /// </remarks>
        public float DashStartedAt { get; set; } = float.NegativeInfinity;

        /// <summary>True while a guard is raised.</summary>
        public bool IsGuarding { get; set; }

        /// <summary>True once the current posture break has been spent on a riposte.</summary>
        /// <remarks>Prevents one break being cashed in repeatedly during a single stagger.</remarks>
        public bool RiposteConsumed { get; set; }

        /// <summary>
        /// Direction the player should turn to face the threat.
        /// </summary>
        /// <remarks>
        /// Supplied by the controller from the boss's position, because guarding and riposting must
        /// orient toward the opponent and the player is not steering during either. Falls back to
        /// current facing when no opponent is known.
        /// </remarks>
        public Vector3 FacingTowardThreat { get; set; } = Vector3.forward;

        /// <summary>Posture the boss has accumulated, exposed so a riposte can be offered.</summary>
        public bool IsThreatPostureBroken { get; set; }

        /// <summary>The weapon currently drawn, or null when fighting unarmed from the fallback set.</summary>
        public WeaponDefinition Weapon { get; set; }

        /// <summary>
        /// The light attack chain for the drawn weapon.
        /// </summary>
        /// <remarks>
        /// Every moveset lookup goes through these properties rather than reading the configuration
        /// directly, so equipping a weapon genuinely changes how the character fights and the
        /// fallback path stays exercised rather than rotting.
        /// </remarks>
        public AttackDefinition[] LightChain =>
            Weapon != null && Weapon.LightComboChain != null && Weapon.LightComboChain.Length > 0
                ? Weapon.LightComboChain
                : Config.LightComboChain;

        /// <summary>The heavy attack for the drawn weapon.</summary>
        public AttackDefinition HeavyAttack => Weapon?.HeavyAttack ?? Config.HeavyAttack;

        /// <summary>The special attack for the drawn weapon.</summary>
        public AttackDefinition SpecialAttack => Weapon?.SpecialAttack ?? Config.SpecialAttack;

        /// <summary>
        /// The stronger special to throw when focus is full, falling back to the ordinary special so a
        /// charged meter is never spent on nothing.
        /// </summary>
        public AttackDefinition EmpoweredSpecialAttack =>
            Weapon?.EmpoweredSpecialAttack ?? Config.EmpoweredSpecialAttack ?? SpecialAttack;

        /// <summary>The riposte for the drawn weapon.</summary>
        public AttackDefinition RiposteAttack =>
            Weapon?.RiposteAttack ?? Config.RiposteAttack ?? HeavyAttack;

        /// <summary>The execution thrown into a broken guard, falling back to the ordinary riposte.</summary>
        public AttackDefinition ExecutionAttack => Config.ExecutionAttack ?? RiposteAttack;

        /// <summary>Deflect window for the drawn weapon.</summary>
        public float DeflectWindowSeconds =>
            Weapon != null ? Weapon.DeflectWindowSeconds : States.PlayerParryState.DeflectWindowSeconds;

        /// <summary>Posture a clean deflect deals with the drawn weapon.</summary>
        public float DeflectPostureDamage =>
            Weapon != null ? Weapon.DeflectPostureDamage : Config.DeflectPostureDamage;

        /// <summary>True when the drawn weapon can deflect at all.</summary>
        public bool CanDeflect => Weapon == null || Weapon.CanDeflect;

        /// <summary>
        /// True while incoming damage should be ignored.
        /// </summary>
        /// <remarks>Owned by the dash state, which sets it on entry and clears it partway through.</remarks>
        public bool IsInvulnerable { get; set; }

        /// <summary>World direction the current dash is travelling.</summary>
        public Vector3 DashDirection { get; set; } = Vector3.forward;

        /// <summary>Seconds remaining before another dash may begin.</summary>
        public float DashCooldownRemaining { get; set; }

        /// <summary>
        /// The action a watcher would see the player performing.
        /// </summary>
        /// <remarks>
        /// Set by each state on entry, and read only by the observation capture that feeds the
        /// boss. It is a deliberately coarse description, because a watcher's view is coarse.
        /// </remarks>
        public ObservableActionState ObservableState { get; set; } = ObservableActionState.Idle;

        /// <summary>Combat-clock time at which the current observable action began.</summary>
        public float ObservableStateEnteredAt { get; set; }

        /// <summary>Set when a landed hit should interrupt the player.</summary>
        public bool StaggerRequested { get; set; }

        /// <summary>Duration of the stagger being requested, in seconds.</summary>
        public float RequestedStaggerSeconds { get; set; }

        /// <summary>True when a dash could begin right now.</summary>
        public bool CanBeginDash =>
            DashCooldownRemaining <= 0f && Stamina.CanSpend(Config.DashStaminaCost);

        /// <summary>True when the movement axis is being deflected.</summary>
        public bool HasMoveInput => Input.MoveDirection.sqrMagnitude > 0f;

        /// <summary>
        /// Chooses the direction a dash should travel.
        /// </summary>
        /// <remarks>
        /// Falls back to the current facing when the stick is centred, so a dash always goes
        /// somewhere. A dash that silently fails because no direction was held is indistinguishable
        /// from a dropped input.
        /// </remarks>
        /// <returns>A unit direction on the horizontal plane.</returns>
        public Vector3 ResolveDashDirection()
        {
            Vector3 fromInput = Motor.ToWorldDirection(Input.MoveDirection);
            return fromInput.sqrMagnitude > Mathf.Epsilon ? fromInput.normalized : Motor.Facing;
        }

        /// <summary>Records that a stagger should interrupt the player.</summary>
        /// <param name="durationSeconds">How long the interruption should last.</param>
        public void RequestStagger(float durationSeconds)
        {
            StaggerRequested = true;
            RequestedStaggerSeconds = Mathf.Max(0f, durationSeconds);
        }

        /// <summary>Advances cooldown timers that run regardless of the active state.</summary>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        public void TickCooldowns(float deltaTime)
        {
            if (DashCooldownRemaining > 0f)
            {
                DashCooldownRemaining = Mathf.Max(0f, DashCooldownRemaining - deltaTime);
            }

            if (SpecialCooldownRemaining > 0f)
            {
                SpecialCooldownRemaining = Mathf.Max(0f, SpecialCooldownRemaining - deltaTime);
            }
        }

        /// <summary>True when the special ability is off cooldown and affordable.</summary>
        public bool CanUseSpecial =>
            SpecialAttack != null &&
            SpecialCooldownRemaining <= 0f &&
            Stamina.CanSpend(SpecialAttack.StaminaCost);

        /// <summary>Whether healing is permitted this run. Cleared by the No Healing modifier.</summary>
        public bool HealingEnabled { get; set; } = true;

        /// <summary>True when a heal charge remains and healing is allowed.</summary>
        public bool CanHeal => HealingEnabled && HealChargesRemaining > 0 && Health.Current < Health.Maximum;

        /// <summary>Publishes a simple occurrence positioned at the player.</summary>
        /// <param name="kind">What happened.</param>
        /// <param name="direction">Direction associated with the occurrence, where one applies.</param>
        public void PublishCombatEvent(CombatEventKind kind, Vector3 direction = default) =>
            Events.Publish(new CombatEvent
            {
                Kind = kind,
                Actor = CombatantTeam.Player,
                Timestamp = Time.CombatTime,
                Position = Motor.Position,
                Direction = direction
            });

        /// <summary>Records the observable action and the moment it began.</summary>
        /// <param name="state">The action a watcher would now see.</param>
        public void SetObservableState(ObservableActionState state)
        {
            ObservableState = state;
            ObservableStateEnteredAt = Time.CombatTime;
        }
    }
}
