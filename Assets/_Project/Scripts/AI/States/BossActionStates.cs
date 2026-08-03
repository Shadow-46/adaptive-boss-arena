using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Core.StateMachine;
using AdaptiveBossArena.Learning;
using UnityEngine;

namespace AdaptiveBossArena.AI.States
{
    /// <summary>
    /// Performing an attack, including the lead the boss puts on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Aim is committed during startup and then locked. The boss turns to face where it expects the
    /// player to be, and once the swing is live it cannot correct. That is the property that makes
    /// the whole encounter dodgeable: an attack that tracked its target through its active frames
    /// could not be evaded by moving, only by invincibility.
    /// </para>
    /// <para>
    /// The lead itself is where the dodge-prediction adaptation surfaces. At zero prediction
    /// strength the boss aims at where it last saw the player. As the player's dash directions prove
    /// lopsided, it begins aiming at where that habit says they are about to be. It is still aiming
    /// at a guess, and a player who changes direction beats it.
    /// </para>
    /// </remarks>
    public sealed class BossAttackState : StateBase<BossContext>
    {
        /// <summary>How far ahead the boss leads, in seconds of player movement, at full prediction.</summary>
        private const float MaxLeadSeconds = 0.35f;

        /// <summary>Fraction of the wind-up after which a feint is abandoned.</summary>
        /// <remarks>
        /// Late enough that the player has already committed to their answer, early enough that the
        /// boss recovers before they can punish the cancellation itself.
        /// </remarks>
        private const float FeintCancelFraction = 0.6f;

        /// <summary>Chance each extra combo hit is taken, so longer chains grow rarer.</summary>
        private const float ComboContinueChance = 0.6f;

        private bool _isFeinting;
        private int _comboRemaining;

        /// <summary>
        /// True once the attack — and any combo it grew into — has run its course.
        /// </summary>
        /// <remarks>
        /// Reports incomplete while a follow-up is still queued, so the state machine does not hand
        /// control to recovery between the blows of a chain.
        /// </remarks>
        /// <param name="context">The boss context.</param>
        /// <returns>Whether the boss may act again.</returns>
        public bool IsComplete(BossContext context) =>
            !context.Attacks.IsRunning && _comboRemaining <= 0;

        /// <inheritdoc />
        protected override void OnEnter(BossContext context)
        {
            AttackDefinition attack = context.PendingAttack;

            if (attack == null)
            {
                return;
            }

            // Decided before the swing begins, so the telegraph is genuinely indistinguishable from
            // a real one. Deciding partway through would let a careful player read the difference.
            _isFeinting = context.Random.NextBool(
                Mathf.Clamp01(context.Tuning.Get(BossTuningParameter.FeintChance)));

            // A feint is a single beat by definition; only a committed opener grows into a chain.
            _comboRemaining = _isFeinting ? 0 : ComboLength(context.PhaseIndex, context.Random);

            // A lunging or unblockable opener is a real commitment the boss can be made to regret if it
            // misses; a light poke is not. A feint is never a commitment — nothing was thrown.
            context.LastAttackWasCommitted = !_isFeinting && (attack.LungeSpeed > 0f || attack.Unblockable);

            context.Motor.Halt();
            context.Attacks.Begin(attack, context.DistanceToPlayer);
        }

        /// <inheritdoc />
        protected override void OnTick(BossContext context, float deltaTime)
        {
            AttackDefinition attack = context.Attacks.CurrentAttack;

            if (TryCancelFeint(context, attack))
            {
                return;
            }

            if (attack != null && context.Attacks.Phase == AttackPhase.Startup)
            {
                context.Motor.FaceDirection(ResolveAimDirection(context), deltaTime);

                if (attack.LungeSpeed > 0f)
                {
                    context.Motor.SetPlanarVelocity(context.Motor.Facing * attack.LungeSpeed);
                }
            }
            else
            {
                context.Motor.Decelerate(deltaTime);
            }

            context.Motor.Tick(deltaTime);
            context.Attacks.Tick(deltaTime);

            // Chained after the tick, so a swing that has just finished flows straight into its
            // follow-up without a frame of recovery showing between them.
            TryContinueCombo(context);
        }

        /// <inheritdoc />
        protected override void OnExit(BossContext context)
        {
            if (context.Attacks.IsRunning)
            {
                context.Attacks.Cancel();
            }

            // A feint costs less than a committed swing, which is what makes it a threat rather than
            // a gift. It still costs something, so a boss that feinted constantly would never
            // actually pressure the player.
            float cooldown = context.CurrentPhase.AttackCooldownSeconds;
            context.AttackCooldownRemaining = _isFeinting ? cooldown * 0.45f : cooldown;

            _isFeinting = false;
            _comboRemaining = 0;
            context.PendingAttack = null;
        }

        /// <summary>
        /// Decides how many extra hits a committed attack strings together.
        /// </summary>
        /// <remarks>
        /// The opening phase throws single, readable strikes. Each later phase adds one to the ceiling
        /// on extra hits, and each additional hit is a fresh coin-flip, so a two-hit string is common
        /// by phase two and a three-hit flurry is a rare, dangerous escalation rather than the norm.
        /// It draws from the same seeded stream as every other boss decision, so a replay is exact —
        /// which is exactly what the edit-mode test pins down. Static and free of the context so that
        /// property can be verified without standing up a whole boss.
        /// </remarks>
        /// <param name="phaseIndex">Zero-based phase; also the ceiling on extra hits.</param>
        /// <param name="random">Seeded source, so a chain is reproducible.</param>
        /// <returns>Extra hits to chain, from zero to <paramref name="phaseIndex"/>.</returns>
        public static int ComboLength(int phaseIndex, IRandomProvider random)
        {
            int maxExtra = Mathf.Max(0, phaseIndex);
            int extra = 0;

            while (extra < maxExtra && random.NextBool(ComboContinueChance))
            {
                extra++;
            }

            return extra;
        }

        /// <summary>
        /// Flows a finished swing into a queued follow-up, if the player is still there to threaten.
        /// </summary>
        /// <remarks>
        /// The follow-up is drawn from the current phase's own moveset, so a combo can never use an
        /// attack the boss was not already allowed. It is abandoned the moment the target is out of
        /// perceived reach, because a boss swinging at empty air reads as broken, not relentless. All
        /// of this stays behind the perception delay: the boss chains blows on stale information,
        /// never on the player's live position.
        /// </remarks>
        private void TryContinueCombo(BossContext context)
        {
            if (context.Attacks.IsRunning || _comboRemaining <= 0)
            {
                return;
            }

            AttackDefinition next = PickFollowUp(context);
            bool inReach = next != null && context.HasPerceivedPlayer &&
                           context.DistanceToPlayer <= next.Range + next.LungeSpeed * next.StartupSeconds;

            if (!inReach)
            {
                _comboRemaining = 0;
                return;
            }

            _comboRemaining--;

            // A follow-up is a commitment by definition — the boss has strung itself into a chain — so
            // a chain that swings through empty air can overbalance it just as a lunge can.
            context.LastAttackWasCommitted = true;
            context.Attacks.Begin(next, context.DistanceToPlayer);
        }

        /// <summary>Chooses a follow-up at random from the current phase's moveset.</summary>
        private static AttackDefinition PickFollowUp(BossContext context)
        {
            AttackDefinition[] moveset = context.CurrentPhase.Attacks;

            if (moveset == null || moveset.Length == 0)
            {
                return null;
            }

            return moveset[context.Random.NextInt(0, moveset.Length)];
        }

        /// <summary>
        /// Abandons a feinted swing part-way through its wind-up.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is where the deflect system and the learning system finally meet. The boss only
        /// feints once it has learned that the player answers wind-ups on a rhythm — either
        /// deflecting or dodging predictably — and the counter to a feint is precisely to stop doing
        /// that. A player who never developed the habit will never see one.
        /// </para>
        /// <para>
        /// It is also the clearest demonstration that perception latency matters: the boss commits
        /// to feinting before it can possibly know what the player will do, so it is guessing on the
        /// same terms a human would.
        /// </para>
        /// </remarks>
        private bool TryCancelFeint(BossContext context, AttackDefinition attack)
        {
            if (!_isFeinting || attack == null || context.Attacks.Phase != AttackPhase.Startup)
            {
                return false;
            }

            if (context.Attacks.ElapsedSeconds < attack.StartupSeconds * FeintCancelFraction)
            {
                return false;
            }

            context.Attacks.Cancel();
            context.Motor.Decelerate(context.Time.DeltaTime);
            context.Motor.Tick(context.Time.DeltaTime);

            return true;
        }

        /// <summary>
        /// Aims at where the player was, adjusted toward where their habits suggest they will go.
        /// </summary>
        /// <remarks>
        /// The extrapolation uses the perceived velocity, which is itself already stale. The boss is
        /// therefore predicting from old information, exactly as a human opponent leading a shot
        /// would be.
        /// </remarks>
        private static Vector3 ResolveAimDirection(BossContext context)
        {
            PlayerObservation observation = context.Perceived;

            if (!observation.IsValid)
            {
                return context.Motor.Facing;
            }

            float predictionStrength =
                Mathf.Clamp01(context.Tuning.Get(BossTuningParameter.DodgePredictionStrength));

            Vector3 aimPoint = observation.Position +
                               observation.Velocity * (MaxLeadSeconds * predictionStrength);

            Vector3 direction = aimPoint - context.Transform.position;
            direction.y = 0f;

            return direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : context.Motor.Facing;
        }
    }

    /// <summary>
    /// The pause after an attack, during which the boss is open to punishment.
    /// </summary>
    /// <remarks>
    /// This window is the player's entire reward for evading well, so it is a real state rather than
    /// a flag. Its length comes from the phase definition, which is how later phases become more
    /// oppressive without any of the boss's attacks changing.
    /// </remarks>
    public sealed class BossRecoverState : StateBase<BossContext>
    {
        /// <summary>True once the recovery window has closed and any overbalance stumble has passed.</summary>
        /// <param name="context">The boss context.</param>
        /// <returns>Whether the boss may act again.</returns>
        /// <remarks>
        /// An overbalanced boss is held here through its stumble, so a swing baited into empty air is a
        /// real, visible opening rather than something the boss immediately acts out of.
        /// </remarks>
        public bool IsComplete(BossContext context) =>
            context.AttackCooldownRemaining <= 0f && !context.IsOverbalanced;

        /// <inheritdoc />
        protected override void OnTick(BossContext context, float deltaTime)
        {
            // Facing continues, so the boss does not present its back for free, but it cannot move
            // or act. Recovery has to be a genuine opening or evasion stops being worth attempting.
            context.Motor.FaceDirection(context.DirectionToPlayer, deltaTime * 0.5f);
            context.Motor.Decelerate(deltaTime);
            context.Motor.Tick(deltaTime);
        }
    }

    /// <summary>
    /// Answering an incoming heavy attack with a parry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counter-behaviour the design calls for against a player who leans on heavy attacks. It is
    /// reached only when the boss has both perceived a heavy wind-up and rolled against its learned
    /// parry chance, so it appears gradually rather than switching on.
    /// </para>
    /// <para>
    /// It is a commitment, not a shield. The boss stops, and if the read was wrong it has given the
    /// player a free opening. That symmetry is what keeps the adaptation fair: the boss can be
    /// baited into parrying nothing.
    /// </para>
    /// </remarks>
    public sealed class BossParryState : StateBase<BossContext>
    {
        /// <summary>How long the parry stance is held.</summary>
        private const float ParryWindowSeconds = 0.5f;

        /// <summary>True once the parry attempt has resolved one way or the other.</summary>
        /// <param name="context">The boss context.</param>
        /// <returns>Whether the boss may act again.</returns>
        public bool IsComplete(BossContext context) => TimeInState >= ParryWindowSeconds;

        /// <inheritdoc />
        protected override void OnEnter(BossContext context)
        {
            context.IsParrying = true;
            context.Motor.Halt();
        }

        /// <inheritdoc />
        protected override void OnTick(BossContext context, float deltaTime)
        {
            context.Motor.FaceDirection(context.DirectionToPlayer, deltaTime);
            context.Motor.Tick(deltaTime);
        }

        /// <inheritdoc />
        protected override void OnExit(BossContext context)
        {
            context.IsParrying = false;

            // A short cooldown after any parry, successful or not, stops the boss from chaining
            // parries and becoming impossible to attack at all.
            context.AttackCooldownRemaining = Mathf.Max(context.AttackCooldownRemaining, 0.4f);
        }
    }

    /// <summary>Reeling from a poise break.</summary>
    public sealed class BossStaggerState : StateBase<BossContext>
    {
        private float _remainingSeconds;

        /// <summary>True once the interruption has run its course.</summary>
        /// <param name="context">The boss context.</param>
        /// <returns>Whether the boss may act again.</returns>
        public bool IsComplete(BossContext context) => _remainingSeconds <= 0f;

        /// <inheritdoc />
        protected override void OnEnter(BossContext context)
        {
            _remainingSeconds = context.RequestedStaggerSeconds;
            context.StaggerRequested = false;

            context.Motor.Halt();
            context.PublishCombatEvent(CombatEventKind.PoiseBroken);
        }

        /// <inheritdoc />
        protected override void OnTick(BossContext context, float deltaTime)
        {
            if (context.StaggerRequested)
            {
                _remainingSeconds = Mathf.Max(_remainingSeconds, context.RequestedStaggerSeconds);
                context.StaggerRequested = false;
            }

            _remainingSeconds -= deltaTime;

            context.Motor.Decelerate(deltaTime);
            context.Motor.Tick(deltaTime);
        }

        /// <inheritdoc />
        protected override void OnExit(BossContext context)
        {
            // Leaving a stagger with no cooldown would let the boss retaliate the instant it
            // recovers, erasing the punish window the player earned.
            context.AttackCooldownRemaining = Mathf.Max(context.AttackCooldownRemaining, 0.5f);
        }
    }

    /// <summary>Defeated. A terminal state with no outgoing transitions.</summary>
    public sealed class BossDeadState : StateBase<BossContext>
    {
        /// <inheritdoc />
        protected override void OnEnter(BossContext context)
        {
            context.Motor.Halt();

            if (context.Attacks.IsRunning)
            {
                context.Attacks.Cancel();
            }

            context.PublishCombatEvent(CombatEventKind.Defeated);
        }

        /// <inheritdoc />
        protected override void OnTick(BossContext context, float deltaTime) =>
            context.Motor.Tick(deltaTime);
    }
}
