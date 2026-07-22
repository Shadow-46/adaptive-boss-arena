using System;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Combat.Vitals;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Learning;
using AdaptiveBossArena.Utilities;
using UnityEngine;

namespace AdaptiveBossArena.AI
{
    /// <summary>
    /// Shared state and services handed to every boss state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Note what this context holds in place of a reference to the player: an
    /// <see cref="IPerceptionSource"/>. Every question a state might ask about the player — where
    /// they are, how far away, what they appear to be doing — is answered from a snapshot that is
    /// deliberately out of date. The boss's assembly cannot reference the player's, so there is no
    /// other answer available to it.
    /// </para>
    /// <para>
    /// That is also why the convenience properties below all degrade gracefully. Early in a fight,
    /// before enough history exists to satisfy the perception delay, the boss genuinely knows
    /// nothing and must behave sensibly anyway.
    /// </para>
    /// </remarks>
    public sealed class BossContext
    {
        /// <summary>Distance reported when the boss has not yet perceived the player.</summary>
        private const float UnknownDistance = float.MaxValue;

        /// <summary>Creates a context.</summary>
        /// <param name="config">Boss tuning values and phase definitions.</param>
        /// <param name="transform">The boss's transform.</param>
        /// <param name="motor">Movement driver.</param>
        /// <param name="health">Health pool.</param>
        /// <param name="poise">Poise pool.</param>
        /// <param name="attacks">Attack executor.</param>
        /// <param name="perception">Delayed view of the player.</param>
        /// <param name="tuning">Behaviour numbers the learning system adjusts.</param>
        /// <param name="time">Time service supplying the combat clock.</param>
        /// <param name="random">Seedable randomness, so probabilistic behaviour stays reproducible.</param>
        /// <param name="events">Bus that witnessed occurrences are published to.</param>
        /// <exception cref="ArgumentNullException">Thrown when any dependency is missing.</exception>
        public BossContext(
            BossConfig config,
            Transform transform,
            BossMotor motor,
            HealthPool health,
            PoisePool poise,
            AttackExecutor attacks,
            IPerceptionSource perception,
            BossTuning tuning,
            ITimeService time,
            IRandomProvider random,
            ICombatEventBus events)
        {
            Config = config != null ? config : throw new ArgumentNullException(nameof(config));
            Transform = transform != null ? transform : throw new ArgumentNullException(nameof(transform));
            Motor = motor ?? throw new ArgumentNullException(nameof(motor));
            Health = health ?? throw new ArgumentNullException(nameof(health));
            Poise = poise ?? throw new ArgumentNullException(nameof(poise));
            Attacks = attacks ?? throw new ArgumentNullException(nameof(attacks));
            Perception = perception ?? throw new ArgumentNullException(nameof(perception));
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Time = time ?? throw new ArgumentNullException(nameof(time));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Events = events ?? throw new ArgumentNullException(nameof(events));
        }

        /// <summary>Boss tuning values and phase definitions.</summary>
        public BossConfig Config { get; }

        /// <summary>The boss's transform.</summary>
        public Transform Transform { get; }

        /// <summary>Movement driver.</summary>
        public BossMotor Motor { get; }

        /// <summary>Health pool.</summary>
        public HealthPool Health { get; }

        /// <summary>Poise pool.</summary>
        public PoisePool Poise { get; }

        /// <summary>Attack executor.</summary>
        public AttackExecutor Attacks { get; }

        /// <summary>Deliberately delayed view of the player.</summary>
        public IPerceptionSource Perception { get; }

        /// <summary>Behaviour numbers the learning system adjusts.</summary>
        public BossTuning Tuning { get; }

        /// <summary>Time service supplying the combat clock.</summary>
        public ITimeService Time { get; }

        /// <summary>Seedable randomness source.</summary>
        public IRandomProvider Random { get; }

        /// <summary>Bus that witnessed occurrences are published to.</summary>
        public ICombatEventBus Events { get; }

        /// <summary>Zero-based index of the phase currently in effect.</summary>
        public int PhaseIndex { get; set; }

        /// <summary>The attack the next entry into the attack state should perform.</summary>
        public AttackDefinition PendingAttack { get; set; }

        /// <summary>Seconds remaining before the boss may attack again.</summary>
        public float AttackCooldownRemaining { get; set; }

        /// <summary>Set when a hit should interrupt the boss.</summary>
        public bool StaggerRequested { get; set; }

        /// <summary>Duration of the interruption being requested.</summary>
        public float RequestedStaggerSeconds { get; set; }

        /// <summary>True while the boss is committed to a parry attempt.</summary>
        public bool IsParrying { get; set; }

        /// <summary>Combat-clock time at which the current reaction delay expires.</summary>
        public float ReactionReadyAt { get; set; }

        /// <summary>The phase definition currently in effect.</summary>
        public BossPhaseDefinition CurrentPhase =>
            Config.Phases != null && Config.Phases.Length > 0
                ? Config.Phases[Mathf.Clamp(PhaseIndex, 0, Config.Phases.Length - 1)]
                : default;

        /// <summary>Human-plausibility limits for every decision the boss makes.</summary>
        public ReactionProfile Reaction => Config.ReactionProfile;

        /// <summary>True once the boss has seen enough to act on.</summary>
        public bool HasPerceivedPlayer => Perception.TryGetPerceived(out _);

        /// <summary>
        /// The most recent player snapshot the boss is permitted to act on.
        /// </summary>
        /// <remarks>
        /// Always stale by the configured perception latency. This is the property that makes
        /// feinting work: a boss that committed during the latency window cannot take it back.
        /// </remarks>
        public PlayerObservation Perceived
        {
            get
            {
                Perception.TryGetPerceived(out PlayerObservation observation);
                return observation;
            }
        }

        /// <summary>Horizontal distance to where the player was last seen.</summary>
        public float DistanceToPlayer
        {
            get
            {
                PlayerObservation observation = Perceived;
                return observation.IsValid
                    ? MathUtil.PlanarDistance(Transform.position, observation.Position)
                    : UnknownDistance;
            }
        }

        /// <summary>Unit direction toward where the player was last seen.</summary>
        public Vector3 DirectionToPlayer
        {
            get
            {
                PlayerObservation observation = Perceived;

                if (!observation.IsValid)
                {
                    return Transform.forward;
                }

                Vector3 delta = observation.Position - Transform.position;
                delta.y = 0f;

                return delta.sqrMagnitude > Mathf.Epsilon ? delta.normalized : Transform.forward;
            }
        }

        /// <summary>Distance the boss currently wants to hold, after adaptation.</summary>
        public float PreferredRange => Tuning.Get(BossTuningParameter.PreferredRange);

        /// <summary>True when the boss's reaction delay has elapsed and it may act.</summary>
        public bool IsReactionReady => Time.CombatTime >= ReactionReadyAt;

        /// <summary>Starts a reaction delay, so the boss never responds faster than a human could.</summary>
        public void BeginReactionDelay() =>
            ReactionReadyAt = Time.CombatTime + Reaction.RollReactionDelay(Random);

        /// <summary>Records that a hit should interrupt the boss.</summary>
        /// <param name="durationSeconds">How long the interruption should last.</param>
        public void RequestStagger(float durationSeconds)
        {
            StaggerRequested = true;
            RequestedStaggerSeconds = Mathf.Max(0f, durationSeconds);
        }

        /// <summary>Advances timers that run regardless of the active state.</summary>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        public void TickCooldowns(float deltaTime)
        {
            if (AttackCooldownRemaining > 0f)
            {
                AttackCooldownRemaining = Mathf.Max(0f, AttackCooldownRemaining - deltaTime);
            }
        }

        /// <summary>Publishes a simple occurrence positioned at the boss.</summary>
        /// <param name="kind">What happened.</param>
        public void PublishCombatEvent(CombatEventKind kind) =>
            Events.Publish(new CombatEvent
            {
                Kind = kind,
                Actor = CombatantTeam.Boss,
                Timestamp = Time.CombatTime,
                Position = Transform.position,
                Direction = Transform.forward,
                SeparationDistance = DistanceToPlayer
            });
    }
}
