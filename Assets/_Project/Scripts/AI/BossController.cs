using AdaptiveBossArena.AI.States;
using AdaptiveBossArena.Combat;
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
    public sealed class BossController : MonoBehaviour, IDamageable
    {
        private const int DeathTransitionPriority = 1000;
        private const int StaggerTransitionPriority = 500;

        /// <summary>Above a normal attack so the set-piece takes over, below stagger/death so those interrupt it.</summary>
        private const int SetPieceTransitionPriority = 300;

        private const int ParryTransitionPriority = 200;

        /// <summary>Stagger applied to the boss when its poise breaks.</summary>
        private const float PoiseBreakStaggerSeconds = 1.1f;

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

        private CharacterController _characterController;
        private HitFlash _hitFlash;
        private CharacterAnimator _animator;
        private PhaseAura _phaseAura;

        /// <summary>
        /// How long the boss shrugs off hits while transforming into a new phase.
        /// </summary>
        /// <remarks>
        /// Short and heavily telegraphed — a roar, a rear-up and a flare of the aura — so it reads as
        /// the boss powering up rather than as a hit that failed to register. It stops the player from
        /// simply bursting through the escalation the moment it begins.
        /// </remarks>
        private const float PhaseTransitionInvulnSeconds = 0.6f;

        private float _phaseTransitionInvulnRemaining;

        /// <summary>Set when a phase escalation should erupt into the set-piece shockwave, once free.</summary>
        private bool _phaseAttackQueued;

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

        /// <summary>Name of the state the boss currently occupies. For debug tooling.</summary>
        public string CurrentStateName => _machine?.CurrentState?.Name ?? "Uninitialised";

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _hitFlash = GetComponentInChildren<HitFlash>();
            _animator = GetComponentInChildren<CharacterAnimator>();
            _phaseAura = GetComponentInChildren<PhaseAura>();
        }

        private void Start()
        {
            if (!ValidateSetup())
            {
                enabled = false;
                return;
            }

            _time = ServiceRegistry.Current.Get<ITimeService>();
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

            var tuning = new BossTuning(_config.BaselinePreferredRange, _config.BaselineAggression);

            _context = new BossContext(
                _config, transform, motor, _health, _poise, attacks,
                _perception, tuning, _time, random, _events);

            _selector = new BossAttackSelector(random);

            BuildLearning(random);
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

            // Announced so presentation can stumble the boss and mark the opening; the mechanic itself
            // is already live in the context. Nothing here reads or reacts to the player.
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

        private void Update()
        {
            if (!_isInitialised || _time.IsPaused)
            {
                return;
            }

            float deltaTime = _time.DeltaTime;

            _phaseTransitionInvulnRemaining = Mathf.Max(0f, _phaseTransitionInvulnRemaining - deltaTime);
            _context.OverbalanceSeconds = Mathf.Max(0f, _context.OverbalanceSeconds - deltaTime);

            // The boss's only window onto the player, refreshed once per frame and served back
            // delayed. Nothing downstream can ask for anything fresher.
            _perception.Sample(_time.CombatTime);

            _context.TickCooldowns(deltaTime);
            _poise.Tick(deltaTime);
            _machine.Tick(deltaTime);

            // Learning is advanced after the state machine, so the habits it derives describe a
            // frame that has already been played rather than one in progress.
            _adaptation.Tick(
                deltaTime, _time.CombatTime, _context.DistanceToPlayer, _context.PhaseIndex + 1);

            UpdatePhase();
            UpdateResolve(deltaTime);

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
            if (_resolve == null)
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

            _animator.SetMotionState(state, _context.Attacks.Phase, speed01);
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

            if (_context.IsParrying)
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

            // A successful parry refuses the hit and hands the punish window back to the boss. It is
            // only reachable because the boss committed to the stance beforehand and guessed right.
            if (_context.IsParrying)
            {
                _context.AttackCooldownRemaining = 0f;
                return DamageResult.NoDamage(DamageOutcome.Blocked);
            }

            // Mid-transformation the boss is briefly untouchable. The window is short and unmistakably
            // telegraphed, so it reads as the escalation rather than as a swallowed hit.
            if (_phaseTransitionInvulnRemaining > 0f && !damage.IgnoresInvulnerability)
            {
                return DamageResult.NoDamage(DamageOutcome.Invulnerable);
            }

            float applied = _health.Reduce(damage.Amount, damage);
            _hitFlash?.Play();
            _animator?.Recoil(damage.HitDirection);

            // While overbalanced the boss's stance is wide open, so a hit landed in the stumble rushes
            // the posture break that opens the execution — the concrete reward for baiting the read.
            if (_poise.ApplyPoiseDamage(damage.PoiseDamage * _context.IncomingPoiseMultiplier))
            {
                _context.RequestStagger(PoiseBreakStaggerSeconds);
            }

            return DamageResult.Applied(applied, !_health.IsAlive);
        }

        /// <summary>Restores the boss to its starting condition for a retry.</summary>
        /// <param name="spawnPosition">Where to place the boss.</param>
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
            _context.OverbalanceSeconds = 0f;
            _context.OverbalancePoiseMultiplier = 1f;
            _context.LastAttackWasCommitted = false;
            _resolve?.Reset();
            _context.StaggerRequested = false;
            _context.IsParrying = false;
            _context.PendingAttack = null;
            _context.Attacks.Cancel();
            _context.Motor.Teleport(spawnPosition);
            _adaptation.Reset();

            ApplyPhase(0);
            _machine.ForceState(_idleState);
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
            // Selection happens here and only here. The approach transition, evaluated immediately
            // afterwards, reads the result rather than choosing again, so one decision draws from
            // the random stream once and a seeded fight stays reproducible.
            if (!TryCommitToAttack(context))
            {
                return false;
            }

            AttackDefinition attack = context.PendingAttack;
            float reach = attack.Range + attack.LungeSpeed * attack.StartupSeconds;

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
        private static bool WantsToApproach(BossContext context) => context.PendingAttack != null;

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

            if (context.Reaction.RollMissedOpportunity(context.Random))
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
        /// Decides whether to attempt a parry against a heavy attack the boss has seen begin.
        /// </summary>
        /// <remarks>
        /// Gated on the perceived, and therefore stale, action state. The boss is reacting to a
        /// wind-up it watched start, not to a decision the player has just made. Its willingness to
        /// try is the learned parry chance, which is zero until the player gives it a reason.
        /// </remarks>
        private bool WantsToParry(BossContext context)
        {
            if (context.AttackCooldownRemaining > 0f)
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

            return context.Random.NextBool(parryChance);
        }

        private bool HasArrived(BossContext context) => _approachState.HasArrived(context);

        private bool AttackFinished(BossContext context) => _attackState.IsComplete(context);

        private bool RecoveryFinished(BossContext context) => _recoverState.IsComplete(context);

        private bool ParryFinished(BossContext context) => _parryState.IsComplete(context);

        private bool StaggerFinished(BossContext context) => _staggerState.IsComplete(context);

        /// <summary>Escalates the boss when its health crosses a phase threshold.</summary>
        private void UpdatePhase()
        {
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

            if (isEscalation)
            {
                // The transformation beat: rear up, flare the aura, and shrug off hits briefly while
                // it happens. The roar and the arena-clearing shockwave are raised by the phase
                // channel's listeners; here we own only what belongs to the boss itself.
                _animator?.Flourish();
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
            FloatEventChannel posture)
        {
            _healthChannel = health;
            _phaseChannel = phase;
            _defeatChannel = defeat;
            _adaptationChannel = adaptation;
            _postureChannel = posture;
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
                _context.RequestStagger(PoiseBreakStaggerSeconds);
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
