using AdaptiveBossArena.AI.States;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Combat.Movement;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Combat.Vitals;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Core.StateMachine;
using AdaptiveBossArena.Learning;
using UnityEngine;

namespace AdaptiveBossArena.AI
{
    /// <summary>
    /// Assembles the boss and drives its decision making.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Worth stating plainly, because it is the point of the whole project: this class has no
    /// reference to the player and cannot obtain one. Its assembly does not reference the player's
    /// assembly or the input package. It resolves an <see cref="IObservablePlayer"/> — an interface
    /// declared in Core that exposes only what is visible on screen — and then wraps even that in a
    /// <see cref="DelayedPerceptionSource"/> so it is reading a snapshot from a fraction of a second
    /// ago.
    /// </para>
    /// <para>
    /// Sampling is pushed from here on a fixed cadence rather than pulled by the states, so no
    /// amount of eagerness deeper in the AI can obtain fresher information than the boss is
    /// entitled to.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class BossController : MonoBehaviour, IDamageable, IPushBody
    {
        private const int DeathTransitionPriority = 1000;
        private const int StaggerTransitionPriority = 500;

        /// <summary>Above a normal attack so the set-piece takes over, below stagger/death so those interrupt it.</summary>
        private const int SetPieceTransitionPriority = 300;

        private const int ParryTransitionPriority = 200;

        /// <summary>Stagger applied to the boss when its poise breaks.</summary>
        /// <remarks>
        /// Long enough to walk in and take the punish: the riposte is now the player pressing attack inside this
        /// window rather than something that fires itself the instant the guard breaks.
        /// </remarks>
        private const float PoiseBreakStaggerSeconds = 1.6f;

        /// <summary>
        /// How far two observed swing start times may differ and still count as the same swing.
        /// </summary>
        /// <remarks>
        /// The start time is derived by subtraction from a delayed observation, so it carries a
        /// little float noise across frames. Well under the shortest gap between two real swings.
        /// </remarks>
        private const float SameSwingToleranceSeconds = 0.01f;

        /// <summary>Camera trauma accompanying an adaptation, as a physical cue that something changed.</summary>
        private const float AdaptationTellTrauma = 0.25f;

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Boss tuning values, phases and counter-strategy catalogue. Required.")]
        private BossConfig _config;

        [Header("Event Channels")]
        [SerializeField]
        [Tooltip("Broadcasts normalised boss health for the health bar.")]
        private FloatEventChannel _healthChannel;

        [SerializeField]
        [Tooltip("Broadcasts the zero-based phase index when the boss escalates.")]
        private IntEventChannel _phaseChannel;

        [SerializeField]
        [Tooltip("Raised once when the boss is defeated.")]
        private VoidEventChannel _defeatChannel;

        [SerializeField]
        [Tooltip("Carries the tell for a newly adopted counter-strategy, so the player can perceive " +
                 "that the boss has changed rather than suspecting it of cheating.")]
        private StringEventChannel _adaptationChannel;

        [SerializeField]
        [Tooltip("Broadcasts normalised boss posture. Emptying it opens the riposte.")]
        private FloatEventChannel _postureChannel;

        [SerializeField]
        [Tooltip("Raised when a committed boss swing whiffs hard enough to overbalance it, so " +
                 "presentation can show the stumble and the opening it leaves.")]
        private VoidEventChannel _overbalanceChannel;

        [SerializeField]
        [Tooltip("Raised when the boss parries a swing. The encounter director relays it into " +
                 "posture damage on the player.")]
        private VoidEventChannel _parrySucceededChannel;

        private CharacterController _characterController;
        private HitFlash _hitFlash;
        private CharacterAnimator _animator;

        /// <summary>Drives the rigged skeleton, and swaps it to the frenzy's bare-handed form.</summary>
        private CharacterAnimationBridge _bridge;

        /// <summary>The rig's physical body, woken on death. Null for a primitive body.</summary>
        private RagdollActivator _ragdoll;
        private PhaseAura _phaseAura;
        private CorruptionDisplay _corruption;

        /// <summary>
        /// How long the boss shrugs off hits while transforming into a new phase.
        /// </summary>
        /// <remarks>
        /// Heavily telegraphed — the roar clip, a flare of the aura — so it reads as the boss powering up
        /// rather than as a hit that failed to register. It stops the player from simply bursting through
        /// the escalation the moment it begins. 1.3 s rather than 0.6: long enough for the roar to be
        /// seen as a roar, which at 0.6 s was a twitch. The boss throws nothing else while it lasts.
        /// </remarks>
        private const float PhaseTransitionInvulnSeconds = 1.3f;

        /// <summary>The phase in which the brute throws its sword away and fights bare-handed.</summary>
        /// <remarks>
        /// The last phase of the configured list, whichever that is, so a config with three phases frenzies
        /// in its third rather than never frenzying at all.
        /// </remarks>
        private int FrenzyPhaseIndex =>
            _config != null && _config.Phases != null && _config.Phases.Length > 0
                ? _config.Phases.Length - 1
                : int.MaxValue;

        /// <summary>How long the death clip plays before physics takes the body.</summary>
        /// <remarks>
        /// Long enough to see the brute go down on its own terms, short enough that the corpse still falls
        /// while the outcome screen is fading in.
        /// </remarks>
        private const float DeathClipSeconds = 0.9f;

        private float _phaseTransitionInvulnRemaining;

        /// <summary>Seconds of death animation left before the ragdoll takes over, or zero when not dying.</summary>
        private float _deathClipRemaining;

        /// <summary>The killing blow, held until the death clip has played.</summary>
        private Vector3 _pendingDeathThrow;

        /// <summary>Set when a phase escalation should erupt into the set-piece shockwave, once free.</summary>
        private bool _phaseAttackQueued;

        /// <summary>Set once an outcome has been decided, standing the boss down.</summary>
        private bool _fightConcluded;

        /// <summary>Run-modifier multiplier on the adaptation rate. One is the ordinary fight.</summary>
        private float _adaptationRateScale = 1f;

        private ITimeService _time;
        private ICombatEventBus _events;
        private DelayedPerceptionSource _perception;

        private HealthPool _health;
        private PoisePool _poise;
        private BossContext _context;
        private BossAttackSelector _selector;
        private AdaptationManager _adaptation;
        private BossResolve _resolve;
        private IRandomProvider _random;
        private OverbalanceEvaluator _overbalance;
        private IScreenShake _screenShake;
        private StateMachine<BossContext> _machine;

        private BossIdleState _idleState;
        private BossObserveState _observeState;
        private BossApproachState _approachState;
        private BossAttackState _attackState;
        private BossRecoverState _recoverState;
        private BossParryState _parryState;

        /// <summary>When the swing the boss last made a parry decision about began.</summary>
        private float _lastConsideredSwingStart = float.NegativeInfinity;

        /// <summary>The decision made about that swing, held so it is not re-rolled every frame.</summary>
        private bool _parryThisSwing;
        private BossStaggerState _staggerState;
        private BossDeadState _deadState;

        private bool _isInitialised;

        /// <inheritdoc />
        public CombatantTeam Team => CombatantTeam.Boss;

        /// <inheritdoc />
        public bool IsAlive => _health != null && _health.IsAlive;

        /// <summary>Health pool, exposed read-only for the heads-up display.</summary>
        public IHealth Health => _health;

        /// <summary>Poise pool, exposed read-only for debug tooling.</summary>
        public IPoise Poise => _poise;

        /// <summary>Behaviour numbers the learning system adjusts. Exposed for the debug overlay.</summary>
        public BossTuning Tuning => _context?.Tuning;

        /// <summary>Zero-based index of the phase currently in effect.</summary>
        public int PhaseIndex => _context?.PhaseIndex ?? 0;

        /// <summary>A phase's name as configured, for its title as the boss turns into it.</summary>
        /// <param name="index">Zero-based phase.</param>
        /// <returns>The name, or empty when there is no such phase.</returns>
        public string PhaseNameOf(int index) =>
            _config != null && _config.Phases != null && index >= 0 && index < _config.Phases.Length
                ? _config.Phases[index].Name ?? string.Empty
                : string.Empty;

        /// <summary>Name of the state the boss currently occupies. For debug tooling.</summary>
        public string CurrentStateName => _machine?.CurrentState?.Name ?? "Uninitialised";

        /// <summary>
        /// Whether construction finished. Exposed because the failure is otherwise silent.
        /// </summary>
        /// <remarks>
        /// The last three checks in <c>ValidateSetup</c> return false without logging, so a missing
        /// service disables the boss quietly: it stops attacking and ignores every hit, which reads
        /// from the outside like a passive boss rather than a broken one.
        /// </remarks>
        public bool IsInitialised => _isInitialised;

        /// <summary>Seconds of phase-transition invulnerability remaining. For debug tooling.</summary>
        public float PhaseTransitionInvulnRemaining => _phaseTransitionInvulnRemaining;

        /// <summary>
        /// True while the boss is down on one knee after the blow that should have killed him, waiting for the
        /// knight's last one.
        /// </summary>
        public bool AwaitingFinalBlow => _awaitingFinalBlow;

        /// <summary>How long he kneels for the last blow before he falls on his own.</summary>
        /// <remarks>
        /// Long enough to walk in from anywhere near and press attack; short enough that a player who does not
        /// know the finisher is coming is never left wondering why the fight has not ended.
        /// </remarks>
        public const float FinalKneelSeconds = 4f;

        /// <summary>The health the kneel leaves him, so the last blow is the one that kills.</summary>
        private const float FinalBlowHealthFloor = 1f;

        private bool _awaitingFinalBlow;
        private float _finalBlowRemaining;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _hitFlash = GetComponentInChildren<HitFlash>();
            _animator = GetComponentInChildren<CharacterAnimator>();
            _bridge = GetComponentInChildren<CharacterAnimationBridge>();
            _ragdoll = GetComponentInChildren<RagdollActivator>();
            _phaseAura = GetComponentInChildren<PhaseAura>();
            _corruption = GetComponentInChildren<CorruptionDisplay>();
        }

        private void Start()
        {
            if (!ValidateSetup())
            {
                enabled = false;
                return;
            }

            _time = ServiceRegistry.Current.Get<ITimeService>();
            _animator?.SetTime(_time);
            _events = ServiceRegistry.Current.Get<ICombatEventBus>();
            var random = ServiceRegistry.Current.Get<IRandomProvider>();
            ServiceRegistry.Current.TryGet(out _screenShake);

            var observablePlayer = ServiceRegistry.Current.Get<IObservablePlayer>();
            _perception = new DelayedPerceptionSource(
                observablePlayer, _config.ReactionProfile.PerceptionLatencySeconds);

            var motor = new BossMotor(_characterController, _config);
            _health = new HealthPool(_config.MaxHealth, CombatantTeam.Boss);
            _poise = new PoisePool(_config.MaxPoise, _config.PoiseRegenPerSecond, PoiseBreakStaggerSeconds);

            // The hazard field is optional and lives in the scene, so a boss in a test scene without
            // one simply leaves no scars. Only the boss is handed it, which is what keeps ground
            // hazards to the boss's side of the fight.
            var hazardField = FindAnyObjectByType<Combat.HazardField>();

            var attacks = new AttackExecutor(
                transform, CombatantTeam.Boss, Layers.BossAttackMask, _time, _events, _screenShake,
                GetComponentInChildren<Combat.Feel.AttackVisualizer>(), hazardField);

            // The blade, fists, foot and body its blows land with. Inactive ones included: the cleaver is
            // hidden in the frenzy and returns on a retry.
            attacks.SetStrikers(GetComponentsInChildren<StrikeVolume>(includeInactive: true));

            var tuning = new BossTuning(
                _config.BaselinePreferredRange, _config.BaselineAggression, _config.BaselineParryChance);

            _context = new BossContext(
                _config, transform, motor, _health, _poise, attacks,
                _perception, tuning, _time, random, _events);

            attacks.Parried += OnOwnAttackParried;

            _selector = new BossAttackSelector(random);

            BuildLearning(random);

            // A fight entered from the menu after a loss is still the same rivalry.
            SessionMemory.TryRecall(_adaptation);
            BuildStateMachine();
            SubscribeToVitals();
            ApplyPhase(0);

            _isInitialised = true;
            _healthChannel?.Raise(_health.Normalized);
        }

        /// <summary>
        /// Assembles the learning loop and subscribes it to the fight.
        /// </summary>
        /// <remarks>
        /// The subscription is to the combat event bus, not to the player. The boss finds out what
        /// happened the same way a spectator would, from occurrences that have already taken place.
        /// </remarks>
        private void BuildLearning(IRandomProvider random)
        {
            var memory = new CombatMemory();
            var tracker = new BehaviorTracker(memory, _config.MemoryHalfLifeSeconds);

            _adaptation = new AdaptationManager(
                memory,
                tracker,
                new BehaviorProfile(),
                _context.Tuning,
                _config.CounterStrategies,
                random,
                _config.AnalysisIntervalSeconds,
                _config.MinSecondsBetweenAdaptations,
                _config.MemoryHalfLifeSeconds);

            _resolve = new BossResolve(
                _config.ResolveThreshold, _config.ResolveBuildPerSecond, _config.ResolveBuildPerWhiff);

            _random = random;
            _overbalance = new OverbalanceEvaluator(
                _config.OverbalanceMaxChance,
                _config.OverbalanceIntensityThreshold,
                _config.OverbalanceRecoverySeconds,
                _config.OverbalancePoiseMultiplier);

            _adaptation.StrategyAdopted += OnStrategyAdopted;
            _events.EventRecorded += OnCombatEventRecorded;
        }

        /// <summary>
        /// Feeds a witnessed occurrence into the learning loop, and into Resolve when it is one of the
        /// boss's own attacks being dodged.
        /// </summary>
        private void OnCombatEventRecorded(CombatEvent combatEvent)
        {
            _adaptation.OnCombatEvent(combatEvent);

            // A whiffed boss attack means the player slipped it; that frustration winds up the gambit.
            // Only the boss's own whiffs count, and only as a statistic — never as a reaction.
            if (combatEvent.Kind == CombatEventKind.AttackWhiffed && combatEvent.Actor == CombatantTeam.Boss)
            {
                _resolve?.RegisterBossWhiff();
                TryOverbalance();
            }
        }

        /// <summary>
        /// Considers whether the committed swing that just missed overbalances the boss into an open
        /// stumble.
        /// </summary>
        /// <remarks>
        /// This is a reaction to the boss's <em>own</em> action and its <em>own</em> commitment — never
        /// to anything about the player — so it needs no perception delay to stay honest: the boss is
        /// paying for how hard it leaned into a read, exactly when the player stops feeding that read.
        /// The chance and severity scale with commitment, and are zero when the boss has adapted
        /// nothing, so a fight it learns nothing in is untouched.
        /// </remarks>
        private void TryOverbalance()
        {
            if (_context.IsOverbalanced)
            {
                return;
            }

            float commitment = _context.Tuning.CommitmentIntensity;

            if (!_overbalance.ShouldOverbalance(
                    _context.LastAttackWasCommitted, commitment, _random.NextFloat01()))
            {
                return;
            }

            _context.OverbalanceSeconds = _overbalance.ExtraRecoverySeconds(commitment);
            _context.OverbalancePoiseMultiplier = _overbalance.PoiseVulnerabilityMultiplier(commitment);

            // A lurch forward in the direction it overcommitted, so the miss reads on the body as an
            // overextension rather than the boss simply pausing. Same presentation channel the aura
            // pulse uses on an adaptation, kept here beside the mechanic it accompanies.
            _animator?.Recoil(_context.Motor.Facing);

            // Announced so the directors can dust, sound and mark the opening; the mechanic itself is
            // already live in the context. Nothing here reads or reacts to the player.
            _overbalanceChannel?.Raise();
        }

        /// <summary>
        /// Announces a newly developed answer so the player can perceive that the boss has changed.
        /// </summary>
        /// <remarks>
        /// Required, not optional. An adaptation the player cannot notice is one they will read as
        /// the boss cheating, which defeats the point of the entire encounter.
        /// </remarks>
        private void OnStrategyAdopted(CounterStrategy strategy)
        {
            if (strategy == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(strategy.TellMessage))
            {
                _adaptationChannel?.Raise(strategy.TellMessage);
            }

            _screenShake?.AddTrauma(AdaptationTellTrauma);

            // A flare on the boss itself the instant it adapts, so the change is felt on the
            // character and not only read in the banner at the top of the screen.
            _phaseAura?.Pulse();
        }

        /// <summary>
        /// Stands the boss down once the fight has been decided.
        /// </summary>
        /// <remarks>
        /// Without this the boss keeps swinging at a corpse. Its own death routes through the dead
        /// state, but the player's death is invisible to it — the firewall means the boss is never
        /// told anything about the player directly — so the director has to call it off. Animation
        /// keeps running, so the outcome beat and the loser's topple still play out.
        /// </remarks>
        /// <param name="concluded">True once an outcome has been decided.</param>
        public void SetFightConcluded(bool concluded) => _fightConcluded = concluded;

        private void Update()
        {
            if (!_isInitialised || _time.IsPaused)
            {
                return;
            }

            float deltaTime = _time.DeltaTime;

            // Dying: the clip plays, then the body is handed to physics carrying the killing blow. On game
            // time, so an execution's slow motion draws the fall out with everything else.
            if (_deathClipRemaining > 0f)
            {
                _deathClipRemaining -= deltaTime;

                if (_deathClipRemaining <= 0f)
                {
                    _ragdoll?.Activate(_pendingDeathThrow);
                }
            }

            // The fight is over: stop swinging, stop deciding, and coast to a halt. Animation still
            // runs below so the boss settles visibly instead of freezing mid-pose.
            if (_fightConcluded)
            {
                _context.Attacks.Cancel();
                _context.Motor.Decelerate(deltaTime);
                _context.Motor.Tick(deltaTime);
                DriveAnimation();
                return;
            }

            _phaseTransitionInvulnRemaining = Mathf.Max(0f, _phaseTransitionInvulnRemaining - deltaTime);
            TickFinalBlow(deltaTime);
            _context.OverbalanceSeconds = Mathf.Max(0f, _context.OverbalanceSeconds - deltaTime);

            // The boss's only window onto the player, refreshed once per frame and served back
            // delayed. Nothing downstream can ask for anything fresher.
            _perception.Sample(_time.CombatTime);

            _context.TickCooldowns(deltaTime);
            _poise.Tick(deltaTime);
            _machine.Tick(deltaTime);
            ResolveWallImpact();

            // Learning is advanced after the state machine, so the habits it derives describe a
            // frame that has already been played rather than one in progress.
            _adaptation.Tick(
                deltaTime, _time.CombatTime, transform.position, _context.Perceived, _context.PhaseIndex + 1);

            _context.HabitualCirclingSide = _adaptation.CirclingSide;
            _context.HasHabitualDodge = _adaptation.TryGetHabitualDodge(out Vector2 habitualDodge);
            _context.HabitualDodge = habitualDodge;

            WatchForOpenings();

            UpdatePhase();
            UpdateResolve(deltaTime);

            // Driven every frame rather than from the parry state's entry and exit, so the light can
            // never be left on by a state torn down mid-stance.
            _phaseAura?.SetParryWindow(_context.ParryWindowOpen);

            DriveAnimation();
        }

        /// <summary>
        /// Builds Resolve, shows it charging, and queues the gambit once it is full.
        /// </summary>
        /// <remarks>
        /// The gambit is delivered through the same path as a phase transition — the queued set-piece
        /// shockwave — so the two dangerous set-pieces share one telegraphed, dodgeable attack. Resolve
        /// is spent the instant it is queued, so a single fill cannot fire twice while the boss waits
        /// for an opening to throw it.
        /// </remarks>
        private void UpdateResolve(float deltaTime)
        {
            // A dead boss gathers no more resolve; it once kept charging until the encounter noticed the fight was over.
            if (_resolve == null || !_health.IsAlive)
            {
                return;
            }

            _resolve.Tick(deltaTime);
            _phaseAura?.SetCharge(_resolve.Normalized);

            if (_resolve.IsReady && _config.PhaseShockwave != null)
            {
                _phaseAttackQueued = true;
                _resolve.Spend();
            }
        }

        /// <summary>Pushes the frame's visible state to the procedural animator.</summary>
        /// <remarks>
        /// The boss keeps no observable-state field of its own — the player is the one the firewall
        /// forces to publish that — so its animation state is derived here, on the presentation side,
        /// from signals the boss already acts on. Nothing about this flows back into the AI.
        /// </remarks>
        private void DriveAnimation()
        {
            if (_animator == null)
            {
                return;
            }

            ObservableActionState state = ResolveAnimationState();

            float topSpeed = _context.Motor.CurrentTopSpeed;
            float speed01 = topSpeed > 0f ? _context.Motor.Speed / topSpeed : 0f;

            if (_context.TakeGesture(out string gesture, out float gestureSeconds))
            {
                _animator.Gesture(gesture, gestureSeconds);
            }

            _animator.SetTopSpeed(topSpeed);
            _animator.SetMotionState(
                state, _context.Attacks.Phase, speed01,
                _context.Attacks.CurrentAttack, _context.Attacks.ElapsedSeconds);
        }

        /// <summary>Maps the boss's current situation onto a presentation state for the animator.</summary>
        private ObservableActionState ResolveAnimationState()
        {
            if (!_health.IsAlive)
            {
                return ObservableActionState.Dead;
            }

            if (ReferenceEquals(_machine.CurrentState, _staggerState))
            {
                return ObservableActionState.Staggered;
            }

            // The window, not the whole stance. Holding the guard pose through the tail would show
            // the player a raised guard at the exact moment the guard has already been spent, which
            // is the one frame where the pose has to be honest.
            if (_context.ParryWindowOpen)
            {
                return ObservableActionState.Guarding;
            }

            // Boss swings are weighty, so they always read as the heavy pose — bigger anticipation
            // and a longer lunge than the player's light chain.
            if (_context.Attacks.IsRunning)
            {
                return ObservableActionState.HeavyAttacking;
            }

            return _context.Motor.IsStationary
                ? ObservableActionState.Idle
                : ObservableActionState.Moving;
        }

        /// <inheritdoc />
        public DamageResult TakeDamage(in DamageInfo damage)
        {
            if (!_isInitialised || !_health.IsAlive || damage.SourceTeam == Team)
            {
                return DamageResult.NoDamage(DamageOutcome.Ignored);
            }

            // Any blow that reached the boss, landed or turned aside, means the swing it came from was no whiff.
            _lastHurtAt = _time.CombatTime;

            // A successful parry refuses the hit and hands the punish window back to the boss. It is
            // only reachable inside the window the stance opens with - a moment later the same
            // stance is the boss standing still with its guard already spent, and the hit lands in
            // full. CanBlock is false because the boss has no guard to fall back on: there is no
            // chip, no cost, and no half-answer. It either timed it or it did not.
            if (DescribeDefence(damage) == DefenceOutcome.Deflected)
            {
                return ResolveParry();
            }

            // Mid-transformation the boss is briefly untouchable. The window is short and unmistakably
            // telegraphed, so it reads as the escalation rather than as a swallowed hit.
            if (_phaseTransitionInvulnRemaining > 0f && !damage.IgnoresInvulnerability)
            {
                return DamageResult.NoDamage(DamageOutcome.Invulnerable);
            }

            // The killing blow is always an execution. A blow that would end a standing boss brings him to
            // his knee instead, broken; the knight's next blow - the riposte the broken guard offers - ends it.
            if (!_awaitingFinalBlow && !_poise.IsBroken && damage.Amount >= _health.Current)
            {
                return KneelForFinalBlow(damage);
            }

            float applied = _health.Reduce(damage.Amount, damage);
            _hitFlash?.Play();
            _animator?.Recoil(damage.HitDirection);

            // While overbalanced the boss's stance is wide open, so a hit landed in the stumble rushes
            // the posture break that opens the execution — the concrete reward for baiting the read.
            if (_poise.ApplyPoiseDamage(damage.PoiseDamage * _context.IncomingPoiseMultiplier))
            {
                _context.RequestStagger(PoiseBreakStaggerSeconds, StaggerReason.PoiseBreak);
            }

            // After the poise damage, so the blow that breaks the stance is also the one that throws
            // the boss. The boss used to ignore knockback entirely.
            ApplyKnockback(damage);

            return DamageResult.Applied(applied, !_health.IsAlive);
        }

        /// <summary>
        /// Takes the blow that should have killed a standing boss, and brings him to his knee instead.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The big action games end their bosses on a finisher, not on a health bar reaching zero: the beaten
        /// body drops, the prompt comes up, and the player delivers the last blow in a slowed, framed moment.
        /// The execution was already here - a riposte on a broken guard, with the finisher camera and the
        /// deepest slow-motion - but most fights ended on an ordinary hit and never showed it.
        /// </para>
        /// <para>
        /// So the blow is taken down to one point of health, the stance is broken for the length of the kneel,
        /// and the riposte the broken guard offers is the end. It is nothing the boss does: he is beaten, and
        /// cannot act until the kneel ends, at which point he falls on his own.
        /// </para>
        /// </remarks>
        private DamageResult KneelForFinalBlow(in DamageInfo damage)
        {
            float applied = _health.Reduce(Mathf.Max(0f, _health.Current - FinalBlowHealthFloor), damage);
            _hitFlash?.Play();
            _animator?.Recoil(damage.HitDirection);

            _awaitingFinalBlow = true;
            _finalBlowRemaining = FinalKneelSeconds;
            _poise.ForceBreak(FinalKneelSeconds);
            _context.Attacks.Cancel();
            _context.RequestStagger(FinalKneelSeconds, StaggerReason.PoiseBreak);
            _context.RequestGesture(CharacterAnimatorParameters.KneelState, FinalKneelSeconds);

            return DamageResult.Applied(applied, false);
        }

        /// <summary>Counts down the kneel; unanswered, the boss falls on his own.</summary>
        private void TickFinalBlow(float deltaTime)
        {
            if (!_awaitingFinalBlow || !_health.IsAlive)
            {
                return;
            }

            _finalBlowRemaining -= deltaTime;

            if (_finalBlowRemaining > 0f)
            {
                return;
            }

            // Credited to the knight: it was their blow that beat him, the kneel only held the moment open.
            _awaitingFinalBlow = false;
            _health.Reduce(
                _health.Current,
                DamageInfo.Create(_health.Current, DamageType.Heavy, CombatantTeam.Player, 0));
        }

        /// <summary>Pushes the boss along a blow, by as much as its stance lets through.</summary>
        private void ApplyKnockback(in DamageInfo damage)
        {
            Vector3 direction = damage.HitDirection;
            direction.y = 0f;

            if (damage.KnockbackSpeed <= 0f || direction.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            float shove = KnockbackResistance.ShoveMultiplier(
                _poise.Normalized,
                _poise.IsBroken,
                _config.KnockbackResistanceAtFullPoise,
                _config.KnockbackResistanceWhenBroken);

            _context.Motor.AddImpulse(direction.normalized * (damage.KnockbackSpeed * shove));
        }

        /// <summary>
        /// Recoils from a swing the player deflected.
        /// </summary>
        /// <remarks>
        /// The boss's half of the same exchange, and deferred for the same reason as the player's:
        /// this runs inside the attack executor's phase-stepping loop, so the swing is torn down by
        /// the stagger it requests rather than cancelled here. Until now a deflect only moved the
        /// boss's posture; the swing itself carried on through the guard as though nothing had
        /// happened, which is most of why deflecting never felt like winning an exchange.
        /// </remarks>
        private void OnOwnAttackParried() =>
            _context.RequestStagger(
                _context.Attacks.LastParryWasStrike ? ParriedByStrikeStaggerSeconds : StaggerDurations.InterruptSeconds,
                StaggerReason.Parried);

        /// <summary>
        /// How long the boss reels from a deliberate parry, against the shorter recoil a deflect causes.
        /// </summary>
        /// <remarks>
        /// Long enough to swing at, which is what makes the parry worth its commitment; short enough that it is
        /// not a free riposte on its own.
        /// </remarks>
        private const float ParriedByStrikeStaggerSeconds = 0.6f;

        /// <summary>
        /// Asks the shared resolver how the boss's stance meets an incoming hit.
        /// </summary>
        /// <param name="damage">The incoming hit.</param>
        /// <returns>How the hit resolves against the stance.</returns>
        private DefenceOutcome DescribeDefence(in DamageInfo damage) =>
            DefenceResolver.ResolveDefence(new DefenceQuery
            {
                IsDefending = _context.IsParrying,
                CanDeflect = true,
                CanBlock = false,
                TimeInDefenceSeconds = _parryState.TimeInState,
                DeflectWindowSeconds = _config.ParryWindowSeconds,
                Unblockable = damage.Unblockable,
                Unparryable = damage.Unparryable
            });

        /// <summary>
        /// Refuses a hit the boss met on the beat.
        /// </summary>
        /// <remarks>
        /// Clearing the attack cooldown is the reward: having guessed right, the boss may answer
        /// immediately. Nothing is published from here on purpose - returning Deflected is what
        /// makes the player's own executor raise <see cref="CombatEventKind.Parried"/> with the boss
        /// named as the defender, and publishing a second event for the same instant would burst and
        /// ring twice on one exchange.
        /// </remarks>
        /// <returns>A result carrying no damage.</returns>
        private DamageResult ResolveParry()
        {
            _context.AttackCooldownRemaining = 0f;
            _parrySucceededChannel?.Raise();

            return DamageResult.NoDamage(DamageOutcome.Deflected);
        }

        /// <summary>Keeps a faded copy of what this fight taught the boss, for the next attempt.</summary>
        /// <remarks>Called by the encounter when the player falls or restarts; a win forgets instead.</remarks>
        public void RememberFight() => SessionMemory.Remember(_adaptation);

        /// <summary>Resets the boss for a new attempt, opening it with whatever it remembers of the player.</summary>
        /// <param name="spawnPosition">Where the boss starts.</param>
        public void ResetForNewAttempt(Vector3 spawnPosition)
        {
            if (!_isInitialised)
            {
                return;
            }

            _health.ResetToFull();
            _poise.ResetToFull();
            _perception.Reset();

            _context.Tuning.ResetToBaseline();
            _context.AttackCooldownRemaining = 0f;
            _phaseTransitionInvulnRemaining = 0f;
            _phaseAttackQueued = false;
            _fightConcluded = false;
            _context.OverbalanceSeconds = 0f;
            _context.OverbalancePoiseMultiplier = 1f;
            _context.LastAttackWasCommitted = false;
            _resolve?.Reset();
            _context.StaggerRequested = false;
            _context.IsParrying = false;
            _context.ParryWindowOpen = false;
            _context.ParryCooldownRemaining = 0f;
            _lastConsideredSwingStart = float.NegativeInfinity;
            _parryThisSwing = false;
            _context.PendingAttack = null;
            _context.RecentMoves.Clear();
            _awaitingFinalBlow = false;
            _finalBlowRemaining = 0f;
            _context.Attacks.Cancel();
            _context.Motor.Teleport(spawnPosition);
            _adaptation.Reset();

            // The same player, trying again: the boss opens the rematch remembering them, faded.
            SessionMemory.TryRecall(_adaptation);

            ApplyPhase(0);
            _machine.ForceState(_idleState);
            // The body goes back to the Animator before the pose is reset, or the reset would pose bones
            // that physics still owns.
            _ragdoll?.Restore();
            _animator?.ResetPose();
            _phaseAura?.ResetAura();
            _healthChannel?.Raise(_health.Normalized);
        }

        /// <summary>Wires the states and the conditions that move between them.</summary>
        private void BuildStateMachine()
        {
            _idleState = new BossIdleState();
            _observeState = new BossObserveState();
            _approachState = new BossApproachState();
            _attackState = new BossAttackState();
            _recoverState = new BossRecoverState();
            _parryState = new BossParryState();
            _staggerState = new BossStaggerState();
            _deadState = new BossDeadState();

            _machine = new StateMachine<BossContext>(_context, _idleState);

            _machine.AddTransition(_idleState, _observeState, HasPerceivedPlayer);

            // Committing to an attack picks one first, so the approach knows what reach to close to.
            _machine.AddTransition(_observeState, _attackState, WantsToAttackNow, priority: 100);
            _machine.AddTransition(_observeState, _approachState, WantsToApproach, priority: 90);

            _machine.AddTransition(_approachState, _attackState, HasArrived);
            _machine.AddTransition(_approachState, _observeState, LostSightOfPlayer);

            _machine.AddTransition(_attackState, _recoverState, AttackFinished);
            _machine.AddTransition(_recoverState, _observeState, RecoveryFinished);
            _machine.AddTransition(_parryState, _observeState, ParryFinished);

            _machine.AddGlobalTransition(_deadState, IsDefeated, DeathTransitionPriority);
            _machine.AddGlobalTransition(_staggerState, IsStaggerRequested, StaggerTransitionPriority);
            _machine.AddGlobalTransition(_attackState, WantsPhaseAttack, SetPieceTransitionPriority);
            _machine.AddGlobalTransition(_parryState, WantsToParry, ParryTransitionPriority);

            _machine.AddTransition(_staggerState, _observeState, StaggerFinished);
        }

        private static bool HasPerceivedPlayer(BossContext context) => context.HasPerceivedPlayer;

        private static bool LostSightOfPlayer(BossContext context) => !context.HasPerceivedPlayer;

        private static bool IsDefeated(BossContext context) => !context.Health.IsAlive;

        private static bool IsStaggerRequested(BossContext context) => context.StaggerRequested;

        /// <summary>
        /// Decides whether to attack, choosing which attack as a side effect.
        /// </summary>
        /// <remarks>
        /// The reaction gate is checked before anything else. However obvious an opening is, the boss
        /// cannot take it faster than its reaction profile permits, and it will sometimes decline it
        /// outright because a human would.
        /// </remarks>
        private bool WantsToAttackNow(BossContext context)
        {
            // Not while transforming: the roar is the beat, and a swing out of it would be thrown by a body
            // that is visibly still roaring. Checked before selection, so no random draw is spent on it.
            // Nor out of a hop or a turn: the body is in the air or halfway round, and a blow thrown from there
            // would have no wind-up the knight could read.
            if (_phaseTransitionInvulnRemaining > 0f || context.IsMidFootwork)
            {
                return false;
            }

            // Selection happens here and only here. The approach transition, evaluated immediately
            // afterwards, reads the result rather than choosing again, so one decision draws from
            // the random stream once and a seeded fight stays reproducible.
            if (!TryCommitToAttack(context))
            {
                return false;
            }

            AttackDefinition attack = context.PendingAttack;
            float reach = attack.EffectiveReach;

            return context.DistanceToPlayer <= reach;
        }

        /// <summary>
        /// Fires the queued phase-transition set-piece once the boss is free to throw it.
        /// </summary>
        /// <remarks>
        /// Waits out the rear-up's invulnerability so the shockwave lands <em>after</em> the
        /// transformation beat, and holds off while any ordinary attack is still committed, so the
        /// set-piece interrupts nothing mid-swing. It selects the attack as it commits, exactly like
        /// the ordinary attack path, so a seeded fight stays reproducible.
        /// </remarks>
        private bool WantsPhaseAttack(BossContext context)
        {
            // A set-piece waits its turn: never out of a stagger the player earned, never mid-swing, never from
            // the floor. It was a global transition that only checked the queue, so it could cut a stagger short.
            if (!IsFreeToAct() || !_health.IsAlive)
            {
                return false;
            }

            if (!_phaseAttackQueued ||
                _phaseTransitionInvulnRemaining > 0f ||
                context.IsOverbalanced ||
                context.PendingAttack != null ||
                _config.PhaseShockwave == null)
            {
                return false;
            }

            _phaseAttackQueued = false;
            context.PendingAttack = _config.PhaseShockwave;
            return true;
        }

        /// <summary>Closes the distance toward an attack the boss has already committed to.</summary>
        private static bool WantsToApproach(BossContext context) =>
            context.PendingAttack != null && !context.IsMidFootwork;

        /// <summary>
        /// Decides whether to commit to an attack, and picks one if so.
        /// </summary>
        /// <remarks>
        /// A failed opportunity roll starts a fresh reaction delay rather than simply returning
        /// false. Without that the boss would re-roll every frame and clear even a high miss chance
        /// almost immediately, which would quietly reduce its designed fallibility to nothing. Making
        /// a miss cost a full reaction window is also the more faithful model: a human who fails to
        /// take an opening does not get to reconsider it a sixtieth of a second later.
        /// </remarks>
        private bool TryCommitToAttack(BossContext context)
        {
            if (context.PendingAttack != null)
            {
                return true;
            }

            if (context.AttackCooldownRemaining > 0f ||
                context.IsOverbalanced ||
                !context.IsReactionReady ||
                !context.Perceived.IsValid)
            {
                return false;
            }

            if (context.Random.NextBool(MissedOpportunityChance(
                    context.Reaction.MissedOpportunityChance, context.Tuning.Get(BossTuningParameter.Aggression))))
            {
                context.BeginReactionDelay();
                return false;
            }

            AttackDefinition attack = _selector.Select(context);

            if (attack == null)
            {
                return false;
            }

            context.PendingAttack = attack;
            return true;
        }

        /// <summary>
        /// How often the boss lets a ready opening pass, after learned aggression.
        /// </summary>
        /// <remarks>
        /// A pressing boss hesitates less: at full aggression it lets only a third as many openings slip. The
        /// fallibility never reaches zero, so even a boss pressing hardest still sometimes waits.
        /// </remarks>
        /// <param name="baseChance">The reaction profile's own chance.</param>
        /// <param name="aggression">Learned aggression.</param>
        /// <returns>The chance to let this opening pass.</returns>
        public static float MissedOpportunityChance(float baseChance, float aggression) =>
            Mathf.Clamp01(baseChance) *
            Mathf.Lerp(1f, PressedMissShare, Mathf.InverseLerp(BossAttackState.NeutralAggression, 1f, aggression));

        /// <summary>The share of openings a boss at full aggression still lets pass.</summary>
        public const float PressedMissShare = 0.35f;

        /// <summary>How long after seeing a swing end on nothing the boss treats it as an opening.</summary>
        private const float WhiffOpeningSeconds = 0.6f;

        /// <summary>The fastest the boss answers an opening it took: still a person's reaction, never instant.</summary>
        private const float OpeningReactionSeconds = 0.12f;

        private ObservableActionState _lastPerceivedState;
        private float _lastPerceivedActionStart = float.NegativeInfinity;
        private float _openingDecidedFor = float.NegativeInfinity;
        private float _lastHurtAt = float.NegativeInfinity;

        /// <summary>
        /// Watches the delayed perception for an opening the player has given, and sometimes takes it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two openings: a heal begun within reach, and a swing seen to end on nothing. Both are things a
        /// spectator could see, seen as late as the perception delay allows. Each opening is decided once - one
        /// roll, as a person deciding whether to go - so a high chance is never compounded frame by frame.
        /// </para>
        /// <para>
        /// The chances start low and learning raises them against a player who keeps giving the same opening.
        /// Taken, the boss drops its cooldown, answers after a short human reaction and throws the quickest
        /// blow that reaches.
        /// </para>
        /// </remarks>
        private void WatchForOpenings()
        {
            PlayerObservation seen = _context.Perceived;

            if (!seen.IsValid)
            {
                return;
            }

            bool wasAttacking = _lastPerceivedState == ObservableActionState.LightAttacking ||
                                _lastPerceivedState == ObservableActionState.HeavyAttacking;
            float swingStartedAt = _lastPerceivedActionStart;

            _lastPerceivedState = seen.ActionState;
            _lastPerceivedActionStart = seen.ActionStartedAt;

            if (!CanTakeOpening())
            {
                // Interrupted - staggered, parrying, knocked about - before the opening was taken: it has passed.
                if (_context.IsTakingOpening && !IsFreeToAct() && _machine.CurrentState != _attackState)
                {
                    _context.IsTakingOpening = false;
                    _context.PendingAttack = null;
                }

                return;
            }

            if (seen.ActionState == ObservableActionState.Healing &&
                _context.DistanceToPlayer <= ReachForOpenings())
            {
                TryTakeOpening(seen.ActionStartedAt, BossTuningParameter.PunishHealChance);
                return;
            }

            bool swingJustEnded = wasAttacking && !seen.IsAttacking &&
                                  _time.CombatTime - seen.ActionStartedAt <= WhiffOpeningSeconds;
            bool swingTouchedNothing = _lastHurtAt < swingStartedAt;

            if (swingJustEnded && swingTouchedNothing && _context.DistanceToPlayer <= ReachForOpenings())
            {
                TryTakeOpening(swingStartedAt, BossTuningParameter.PunishWhiffChance);
            }
        }

        /// <summary>Whether the boss is free to take an opening: not mid-swing, staggered, parrying or done.</summary>
        private bool CanTakeOpening() =>
            IsFreeToAct() && !_context.IsOverbalanced && _context.PendingAttack == null;

        /// <summary>Whether the boss is stalking, closing in or recovering - the states an opening can come from.</summary>
        private bool IsFreeToAct()
        {
            IState<BossContext> state = _machine.CurrentState;
            return state == _observeState || state == _approachState || state == _recoverState;
        }

        /// <summary>The furthest an opening is worth taking: the longest reach this phase owns.</summary>
        private float ReachForOpenings()
        {
            float reach = 0f;

            foreach (AttackDefinition attack in _context.CurrentPhase.Attacks ?? new AttackDefinition[0])
            {
                if (attack != null && !attack.Unblockable)
                {
                    reach = Mathf.Max(reach, attack.EffectiveReach);
                }
            }

            return reach;
        }

        /// <summary>Rolls once for an opening, and on a hit commits to the quickest blow that reaches.</summary>
        private void TryTakeOpening(float openingStartedAt, BossTuningParameter chance)
        {
            if (Mathf.Approximately(openingStartedAt, _openingDecidedFor))
            {
                return;
            }

            _openingDecidedFor = openingStartedAt;

            if (!_context.Random.NextBool(Mathf.Clamp01(_context.Tuning.Get(chance))))
            {
                return;
            }

            AttackDefinition attack = _selector.SelectQuickestReaching(_context);

            if (attack == null)
            {
                return;
            }

            _context.PendingAttack = attack;
            _context.IsTakingOpening = true;
            _context.AttackCooldownRemaining = 0f;
            _context.ReactionReadyAt = Mathf.Min(_context.ReactionReadyAt, _time.CombatTime + OpeningReactionSeconds);
        }

        /// <summary>
        /// Decides whether to attempt a parry against a heavy attack the boss has seen begin.
        /// </summary>
        /// <remarks>
        /// Gated on the perceived, and therefore stale, action state. The boss is reacting to a
        /// wind-up it watched start, not to a decision the player has just made. Its willingness to
        /// try is the learned parry chance, which is zero until the player gives it a reason.
        /// </remarks>
        private bool WantsToParry(BossContext context)
        {
            // Only a boss with its hands free parries: not mid-swing, where the parry used to cancel its own blow,
            // not out of a stagger the player earned, and not after death. The cooldowns alone are zero in all of
            // those, which is how a global transition reached them.
            if (!IsFreeToAct() || !_health.IsAlive)
            {
                return false;
            }

            if (context.AttackCooldownRemaining > 0f || context.ParryCooldownRemaining > 0f)
            {
                return false;
            }

            float parryChance = context.Tuning.Get(BossTuningParameter.ParryChance);

            if (parryChance <= 0f)
            {
                return false;
            }

            PlayerObservation observation = context.Perceived;

            if (!observation.IsValid || observation.ActionState != ObservableActionState.HeavyAttacking)
            {
                return false;
            }

            // Only worth attempting if the player is close enough to actually be swinging at the boss.
            if (context.DistanceToPlayer > context.PreferredRange + 2f)
            {
                return false;
            }

            // Decided once per swing, not once per frame. This predicate is evaluated by the state
            // machine every frame the boss is eligible, so rolling here made the chance compound:
            // across the ~fifty frames a heavy swing is visible for, even a modest per-frame chance
            // becomes a near-certainty, and the boss would parry essentially every heavy thrown at
            // it. Identifying the swing by when the observed action state began - not by the
            // observation's own timestamp, which advances every frame - keeps one swing to one roll.
            if (Mathf.Abs(observation.ActionStartedAt - _lastConsideredSwingStart) >
                SameSwingToleranceSeconds)
            {
                _lastConsideredSwingStart = observation.ActionStartedAt;
                _parryThisSwing = context.Random.NextBool(parryChance);
            }

            return _parryThisSwing;
        }

        private bool HasArrived(BossContext context) => _approachState.HasArrived(context);

        private bool AttackFinished(BossContext context) => _attackState.IsComplete(context);

        private bool RecoveryFinished(BossContext context) => _recoverState.IsComplete(context);

        private bool ParryFinished(BossContext context) => _parryState.IsComplete(context);

        private bool StaggerFinished(BossContext context) => _staggerState.IsComplete(context);

        /// <summary>Escalates the boss when its health crosses a phase threshold.</summary>
        private void UpdatePhase()
        {
            // A beaten boss does not transform. The blow that brings him to his knee can cross every remaining
            // threshold at once, and the transformation it started made him invulnerable and roaring over the kneel,
            // so the last blow was swallowed - found by driving the shipped scene, not by any unit test.
            if (_awaitingFinalBlow)
            {
                return;
            }

            int resolved = _config.ResolvePhaseIndex(_health.Normalized);

            if (resolved != _context.PhaseIndex)
            {
                ApplyPhase(resolved);
            }
        }

        /// <summary>Applies a phase's movement multiplier and adaptation rate, then announces it.</summary>
        private void ApplyPhase(int phaseIndex)
        {
            // Captured before the assignment so a genuine escalation can be told apart from the
            // opening phase and from a reset back to phase zero, neither of which should erupt.
            bool isEscalation = phaseIndex > _context.PhaseIndex;

            _context.PhaseIndex = phaseIndex;
            _context.Motor.SetSpeedMultiplier(_context.CurrentPhase.MoveSpeedMultiplier);

            // Later phases learn faster. This is how the encounter escalates without the boss ever
            // gaining information it did not already have. The run-modifier scale (Fast Learner) folds
            // in here so it survives every phase transition rather than being overwritten by one.
            _adaptation?.SetAdaptationRateMultiplier(
                _context.CurrentPhase.AdaptationRateMultiplier * _adaptationRateScale);

            _phaseAura?.SetPhase(phaseIndex);

            // The corrupted knight's fire breaks out further with each phase, and dies back on a retry.
            _corruption?.SetPhase(phaseIndex);

            // The last phase is fought bare-handed: a different body, not only faster numbers.
            _bridge?.SetFrenzy(phaseIndex >= FrenzyPhaseIndex);

            if (isEscalation)
            {
                // The transformation beat: rear up, flare the aura, and shrug off hits briefly while
                // it happens. The roar and the arena-clearing shockwave are raised by the phase
                // channel's listeners; here we own only what belongs to the boss itself.
                _animator?.Flourish(PhaseTransitionInvulnSeconds);
                _phaseAura?.Pulse();
                _phaseTransitionInvulnRemaining = PhaseTransitionInvulnSeconds;

                // The transformation is now a threat, not just a light show: once the rear-up ends the
                // boss erupts into its set-piece shockwave, so entering a phase is a "survive this".
                _phaseAttackQueued = _config.PhaseShockwave != null;
            }

            _phaseChannel?.Raise(phaseIndex);
        }

        /// <summary>Relays pool changes onto the event channels the interface listens to.</summary>
        private void SubscribeToVitals()
        {
            _health.Changed += args => _healthChannel?.Raise(args.Normalized);
            _poise.Changed += args => _postureChannel?.Raise(args.Normalized);
            _health.Died += _ => _defeatChannel?.Raise();

            // The clip first, then the body. Handing the corpse to physics on the frame it died meant the
            // brute's death animation was never seen: it simply folded. The throw is remembered and applied
            // when the clip has played, so the fall still reads as caused by the killing blow.
            _health.Died += args =>
            {
                _pendingDeathThrow = args.FinalBlow.HitDirection.normalized * args.FinalBlow.KnockbackSpeed;
                _deathClipRemaining = DeathClipSeconds;
            };
        }

        /// <summary>Reports missing dependencies clearly rather than failing with a null reference.</summary>
        private bool ValidateSetup()
        {
            if (_config == null)
            {
                Debug.LogError(
                    "[Adaptive Boss Arena] BossController has no BossConfig assigned and has been disabled.",
                    this);
                return false;
            }

            if (_config.ReactionProfile == null)
            {
                // Without a reaction profile the boss would act on the frame it perceived something,
                // which is the behaviour this project exists to prevent. Refusing to run is correct.
                Debug.LogError(
                    "[Adaptive Boss Arena] BossConfig has no ReactionProfile. The boss has been " +
                    "disabled rather than allowed to react instantly.", this);
                return false;
            }

            if (!ServiceRegistry.Current.TryGet(out IObservablePlayer _))
            {
                Debug.LogError(
                    "[Adaptive Boss Arena] No IObservablePlayer is registered. The boss has nothing to " +
                    "perceive and has been disabled.", this);
                return false;
            }

            return ServiceRegistry.Current.TryGet(out ITimeService _) &&
                   ServiceRegistry.Current.TryGet(out ICombatEventBus _) &&
                   ServiceRegistry.Current.TryGet(out IRandomProvider _);
        }

        /// <summary>Assigns the configuration. Used by the prefab generator.</summary>
        /// <param name="config">Boss tuning values.</param>
        public void SetConfig(BossConfig config) => _config = config;

        /// <summary>
        /// Scales how fast the boss adapts, on top of its per-phase rate, for the Fast Learner run
        /// modifier.
        /// </summary>
        /// <remarks>
        /// A scale on the existing rate rather than a new capability: the boss reaches the same
        /// conclusions from the same evidence, only sooner. Reapplied on the next phase transition, so
        /// it holds for the whole fight.
        /// </remarks>
        /// <param name="scale">Multiplier on the adaptation rate. One leaves the fight unchanged.</param>
        public void SetAdaptationRateScale(float scale)
        {
            _adaptationRateScale = Mathf.Max(0.1f, scale);

            if (_adaptation != null && _context != null)
            {
                _adaptation.SetAdaptationRateMultiplier(
                    _context.CurrentPhase.AdaptationRateMultiplier * _adaptationRateScale);
            }
        }

        /// <summary>Assigns the event channels. Used by the prefab generator.</summary>
        /// <param name="health">Normalised boss health channel.</param>
        /// <param name="phase">Phase index channel.</param>
        /// <param name="defeat">Boss defeat channel.</param>
        /// <param name="adaptation">Adaptation tell channel.</param>
        /// <param name="posture">Normalised posture channel.</param>
        public void SetChannels(
            FloatEventChannel health,
            IntEventChannel phase,
            VoidEventChannel defeat,
            StringEventChannel adaptation,
            FloatEventChannel posture,
            VoidEventChannel overbalance,
            VoidEventChannel parried)
        {
            _healthChannel = health;
            _phaseChannel = phase;
            _defeatChannel = defeat;
            _adaptationChannel = adaptation;
            _postureChannel = posture;
            _overbalanceChannel = overbalance;
            _parrySucceededChannel = parried;
        }

        /// <summary>
        /// Applies posture damage from a deflected blow.
        /// </summary>
        /// <remarks>
        /// Called by the encounter director when the player deflects cleanly. This is the entire
        /// deflect economy in one method: every well-timed guard moves the boss closer to a break,
        /// which is what makes standing your ground feel like progress rather than mere survival.
        /// </remarks>
        /// <param name="postureDamage">Posture to remove.</param>
        public void ApplyDeflectPosture(float postureDamage)
        {
            if (!_isInitialised || _poise == null)
            {
                return;
            }

            if (_poise.ApplyPoiseDamage(postureDamage))
            {
                _context.RequestStagger(PoiseBreakStaggerSeconds, StaggerReason.PoiseBreak);
            }
        }

        /// <summary>Posture a successful boss parry deals to the player.</summary>
        /// <remarks>
        /// The exact mirror of <c>PlayerController.DeflectPostureDamage</c>, read by the encounter
        /// director for the same reason: neither combatant may hold a reference to the other, so the
        /// one that acted reports what it did and the director decides what that means for the other.
        /// </remarks>
        public float ParryPostureDamage => _config != null ? _config.ParryPostureDamage : 0f;

        /// <summary>The reach of a swing whose hitbox is open right now, or zero if none is.</summary>
        /// <remarks>
        /// Deliberately narrower than exposing the attack executor. The encounter director needs to
        /// know whether a live blade is out and how far it reaches in order to detect a clash, and
        /// nothing more; handing it the executor would hand it Cancel as well, and cancelling a swing
        /// from outside is exactly the thing Stage D's constraint exists to control.
        /// </remarks>
        public float LiveSwingReach =>
            _isInitialised &&
            _context.Attacks.IsRunning &&
            _context.Attacks.Phase == AttackPhase.Active &&
            _context.Attacks.CurrentAttack != null
                ? _context.Attacks.CurrentAttack.Range
                : 0f;

        /// <summary>
        /// Refuses the swing in progress because it met another blade.
        /// </summary>
        /// <remarks>
        /// Routed through the same stagger request a parry uses, and for the same reason: it defers
        /// to the next tick, where the attack state's exit tears the swing down. Called from the
        /// director's LateUpdate, which is outside any executor's phase-stepping loop, so a
        /// synchronous cancel would in fact be safe here - but there is no reason to have two ways of
        /// stopping a swing when the existing one already reads correctly on both fighters.
        /// </remarks>
        public void RecoilFromClash()
        {
            if (!_isInitialised)
            {
                return;
            }

            _context.RequestStagger(StaggerDurations.InterruptSeconds, StaggerReason.Parried);
        }

        /// <summary>
        /// Punishes a lunge that ended in a wall.
        /// </summary>
        /// <remarks>
        /// Only a lunge counts. The boss walking into a pillar is not a mistake anyone could exploit,
        /// but a charge the player sidestepped into the wall is exactly the read the fight rewards. The
        /// interruption goes through the deferred stagger request, as every other one does.
        /// </remarks>
        private void ResolveWallImpact()
        {
            AttackDefinition attack = _context.Attacks.CurrentAttack;

            if (_context.Motor.WallImpactSpeed < _config.WallImpactSpeed ||
                _context.StaggerRequested ||
                !_context.Attacks.IsRunning ||
                attack == null ||
                attack.LungeSpeed <= 0f)
            {
                return;
            }

            _context.PublishCombatEvent(CombatEventKind.WallImpact);

            bool broken = _poise.ApplyPoiseDamage(_config.WallImpactPoiseDamage);

            _context.RequestStagger(
                broken ? PoiseBreakStaggerSeconds : StaggerDurations.InterruptSeconds,
                broken ? StaggerReason.PoiseBreak : StaggerReason.Hit);
        }

        /// <inheritdoc />
        public Vector3 BodyPosition => transform.position;

        /// <inheritdoc />
        public float BodyRadius => _characterController != null
            ? _characterController.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z)
            : 0f;

        /// <inheritdoc />
        public float BodyMass => _config != null ? _config.BodyMass : 1f;

        /// <inheritdoc />
        public bool IsSolid => _isInitialised && _characterController != null && _characterController.enabled;

        /// <inheritdoc />
        public void Displace(Vector3 offset)
        {
            if (_isInitialised)
            {
                _context.Motor.Displace(offset);
            }
        }

        /// <summary>Exposes the learning loop so the debug overlay can show what the boss believes.</summary>
        /// <returns>The adaptation manager, or null before initialisation.</returns>
        public AdaptationManager GetAdaptation() => _adaptation;

        private void OnDestroy()
        {
            if (_events != null)
            {
                _events.EventRecorded -= OnCombatEventRecorded;
            }
        }

        /// <summary>Exposes the perception source so the learning system can share it.</summary>
        /// <returns>The boss's delayed view of the player.</returns>
        public IPerceptionSource GetPerceptionSource() => _perception;

        /// <summary>Exposes the context so the learning system can adjust tuning.</summary>
        /// <returns>The boss's context, or null before initialisation.</returns>
        public BossContext GetContext() => _context;
    }
}
