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

        /// <summary>
        /// Chance each extra combo hit is taken before adaptation, so longer chains grow rarer.
        /// </summary>
        /// <remarks>
        /// The untrained baseline. A boss that has learned to string blows together raises this
        /// through <see cref="BossTuningParameter.ComboExtensionChance"/>; exposed so tests can state
        /// the baseline rather than repeat the number.
        /// </remarks>
        public const float BaseComboContinueChance = 0.6f;

        /// <summary>How often an opening-phase attack grows a second blow, before learning.</summary>
        public const float OpeningChainChance = 0.25f;

        /// <summary>The most often the opening phase strings two blows, however hard the boss has learned to press.</summary>
        public const float OpeningChainCeiling = 0.45f;

        /// <summary>
        /// The longest wind-up a follow-up may have in the opening phase: only the quick blows follow there, never the
        /// held overhead, so an opening string is a flurry the knight can read rather than a trap.
        /// </summary>
        public const float OpeningFollowUpLongestWindup = 0.5f;

        /// <summary>Share of a full cooldown a feint costs, since nothing was committed.</summary>
        private const float FeintCooldownFraction = 0.45f;

        private bool _isFeinting;
        private int _comboRemaining;

        /// <summary>The swing just thrown, whose authored follow-ups the chain continues with.</summary>
        private AttackDefinition _lastSwing;

        /// <summary>Share of a wind-up at which a held blow hangs, raised and plainly visible.</summary>
        public const float HoldAtWindupShare = 0.65f;

        /// <summary>How long a held blow hangs, as a share of its wind-up, within a floor and a ceiling.</summary>
        /// <remarks>
        /// Long enough that a roll begun when the arms went up is over before the blade comes; short enough
        /// that a player who simply waits for the blade is never caught. Only wind-ups long enough to read are
        /// ever held.
        /// </remarks>
        private const float HoldWindupShare = 0.45f;
        private const float MinimumHoldSeconds = 0.25f;
        private const float MaximumHoldSeconds = 0.5f;
        private const float ShortestHeldWindup = 0.35f;

        /// <summary>How far the boss leads a learned roll, in metres, at full prediction.</summary>
        private const float HabitualRollLeadMetres = 1.4f;

        private float _holdRemaining;

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
            // Never on the phase shockwave, which is a set piece rather than a blow, nor on an opening being
            // taken, whose whole point is to land before the player recovers.
            _isFeinting = attack != context.Config.PhaseShockwave && !context.IsTakingOpening &&
                          context.Random.NextBool(Mathf.Clamp01(context.Tuning.Get(BossTuningParameter.FeintChance)));

            // A feint is a single beat by definition; only a committed opener grows into a chain. A
            // boss that has learned to press its combos strings more of them together.
            _comboRemaining = _isFeinting
                ? 0
                : ComboLength(
                    context.PhaseIndex,
                    context.Random,
                    BaseComboContinueChance +
                    context.Tuning.Get(BossTuningParameter.ComboExtensionChance));

            // A lunging or unblockable opener is a real commitment the boss can be made to regret if it
            // misses; a light poke is not. A feint is never a commitment — nothing was thrown.
            context.LastAttackWasCommitted = !_isFeinting && (attack.LungeSpeed > 0f || attack.Unblockable);

            context.Motor.Halt();
            _lastSwing = attack;
            context.IsTakingOpening = false;

            // Decided at the start, like the feint, so the wind-up reads the same until the blade hangs.
            _holdRemaining = !_isFeinting && attack.StartupSeconds >= ShortestHeldWindup &&
                             context.Random.NextBool(Mathf.Clamp01(context.Tuning.Get(BossTuningParameter.HoldWindupChance)))
                ? Mathf.Clamp(attack.StartupSeconds * HoldWindupShare, MinimumHoldSeconds, MaximumHoldSeconds)
                : 0f;

            context.Attacks.Begin(attack, context.DistanceToPlayer);
            context.RecentMoves.Record(attack);
        }

        /// <inheritdoc />
        protected override void OnTick(BossContext context, float deltaTime)
        {
            AttackDefinition attack = context.Attacks.CurrentAttack;

            if (TryCancelFeint(context, attack))
            {
                return;
            }

            if (TryHoldWindup(context, attack, deltaTime))
            {
                return;
            }

            if (attack != null && context.Attacks.Phase == AttackPhase.Startup)
            {
                // Tracks early, then commits. The lunge keeps its speed for the whole wind-up, so its
                // reach is unchanged, but once tracking locks it travels along the committed facing
                // instead of re-homing every frame - which is what lets a late sidestep escape it.
                if (attack.CanTrack(context.Attacks.Phase, context.Attacks.ElapsedSeconds))
                {
                    context.Motor.FaceDirection(ResolveAimDirection(context), deltaTime);
                }

                if (attack.LungeSpeed > 0f)
                {
                    context.Motor.SetPlanarVelocity(context.Motor.Facing * attack.LungeSpeed);
                }
            }
            else if (attack != null && attack.LungeSpeed > 0f && context.Motor.Speed > context.Motor.CurrentTopSpeed)
            {
                // A finished wind-up brakes hard rather than sliding metres through its own strike at
                // the boss's gentle walking deceleration.
                context.Motor.Brake(context.Config.LungeBrakeDeceleration, deltaTime);
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

            context.AttackCooldownRemaining = AttackCooldownFor(
                context.CurrentPhase.AttackCooldownSeconds,
                context.Tuning.Get(BossTuningParameter.AttackDelay),
                _isFeinting,
                context.Tuning.Get(BossTuningParameter.Aggression));

            _isFeinting = false;
            _holdRemaining = 0f;
            _comboRemaining = 0;
            context.PendingAttack = null;
        }

        /// <summary>
        /// Works out how long the boss waits before it may swing again.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A feint costs less than a committed swing, which is what makes it a threat rather than a
        /// gift. It still costs something, so a boss that feinted constantly would never actually
        /// pressure the player.
        /// </para>
        /// <para>
        /// The learned delay is what gives <c>CounterPostDodgeAggression</c> its teeth. Against a
        /// player who strikes on reflex the instant a dodge ends, the boss holds back and lets that
        /// reflex swing fall on empty air — which is precisely what its tell, "It waits for your
        /// opening now.", claims it is doing. The delay is added rather than scaled so it reads as
        /// patience layered on the phase's own rhythm instead of a different rhythm.
        /// </para>
        /// <para>
        /// Pure and static so the behaviour behind that tell is verified by a test rather than by
        /// watching the boss and forming an impression.
        /// </para>
        /// </remarks>
        /// <param name="baseCooldownSeconds">The current phase's cooldown between attacks.</param>
        /// <param name="learnedDelaySeconds">Extra patience the boss has learned, in seconds.</param>
        /// <param name="isFeint">Whether the swing being recovered from was a feint.</param>
        /// <param name="aggression">
        /// Learned aggression. Above its neutral half it shortens the phase's rhythm, by up to
        /// <see cref="PressuredCooldownShare"/> at its peak: a boss pressing a passive player swings more
        /// often, not merely walks in faster.
        /// </param>
        /// <returns>Seconds before the boss may attack again.</returns>
        public static float AttackCooldownFor(
            float baseCooldownSeconds, float learnedDelaySeconds, bool isFeint, float aggression = NeutralAggression)
        {
            float cooldown = isFeint ? baseCooldownSeconds * FeintCooldownFraction : baseCooldownSeconds;
            float pressure = Mathf.InverseLerp(NeutralAggression, 1f, aggression);
            cooldown *= Mathf.Lerp(1f, PressuredCooldownShare, pressure);

            return cooldown + Mathf.Max(0f, learnedDelaySeconds);
        }

        /// <summary>Aggression at which the boss keeps the phase's own rhythm.</summary>
        public const float NeutralAggression = 0.5f;

        /// <summary>The share of its cooldown a boss at full aggression keeps.</summary>
        public const float PressuredCooldownShare = 0.7f;

        /// <summary>
        /// Holds a raised blade a beat before it falls, re-aiming at the player while it hangs.
        /// </summary>
        /// <remarks>
        /// The attack's own clock stops, so the scrubbed pose hangs in the air in plain sight; the boss keeps
        /// turning to wherever it now sees the player. A roll spent when the arms went up is over by the time
        /// the blow comes, while a player who waits for the blade dodges it as easily as ever.
        /// </remarks>
        /// <returns>True while holding, when the rest of the tick is skipped.</returns>
        private bool TryHoldWindup(BossContext context, AttackDefinition attack, float deltaTime)
        {
            if (_holdRemaining <= 0f || attack == null || context.Attacks.Phase != AttackPhase.Startup ||
                context.Attacks.ElapsedSeconds < attack.StartupSeconds * HoldAtWindupShare)
            {
                return false;
            }

            _holdRemaining -= deltaTime;
            context.Motor.FaceDirection(ResolveAimDirection(context), deltaTime);
            context.Motor.Decelerate(deltaTime);
            context.Motor.Tick(deltaTime);
            return true;
        }

        /// <summary>
        /// Decides how many extra hits a committed attack strings together.
        /// </summary>
        /// <remarks>
        /// The opening phase throws mostly single, readable strikes, now and then a pair. Each later phase adds one to the ceiling
        /// on extra hits, and each additional hit is a fresh coin-flip, so a two-hit string is common
        /// by phase two and a three-hit flurry is a rare, dangerous escalation rather than the norm.
        /// It draws from the same seeded stream as every other boss decision, so a replay is exact —
        /// which is exactly what the edit-mode test pins down. Static and free of the context so that
        /// property can be verified without standing up a whole boss.
        /// </remarks>
        /// <param name="phaseIndex">Zero-based phase; also the ceiling on extra hits.</param>
        /// <param name="random">Seeded source, so a chain is reproducible.</param>
        /// <param name="continueChance">
        /// Chance each further hit is taken. Passed in rather than fixed so a boss that has learned to
        /// press its combos actually presses them — the behaviour
        /// <see cref="BossTuningParameter.ComboExtensionChance"/> exists to change.
        /// </param>
        /// <returns>Extra hits to chain: zero or one in the opening phase, otherwise zero to <paramref name="phaseIndex"/>.</returns>
        public static int ComboLength(int phaseIndex, IRandomProvider random, float continueChance)
        {
            // The opening phase strings two blows now and then - about one attack in four - so its rhythm is not a
            // metronome of single strikes, but never more than two: it is still where the knight learns to read him.
            // Learning to press raises the chance by half what it raises the later phases', up to a ceiling.
            if (phaseIndex <= 0)
            {
                float learned = Mathf.Max(0f, continueChance - BaseComboContinueChance) * 0.5f;
                return random.NextBool(Mathf.Clamp(OpeningChainChance + learned, 0f, OpeningChainCeiling)) ? 1 : 0;
            }

            int maxExtra = Mathf.Max(0, phaseIndex);
            float chance = Mathf.Clamp01(continueChance);
            int extra = 0;

            while (extra < maxExtra && random.NextBool(chance))
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

            AttackDefinition[] moveset = context.PhaseIndex <= 0
                ? System.Array.FindAll(
                    context.CurrentPhase.Attacks,
                    attack => attack != null && attack.StartupSeconds <= OpeningFollowUpLongestWindup)
                : context.CurrentPhase.Attacks;

            AttackDefinition next = PickFollowUp(_lastSwing, moveset, context.Random, context.RecentMoves);
            bool inReach = next != null && context.HasPerceivedPlayer &&
                           context.DistanceToPlayer <= next.EffectiveReach;

            if (!inReach)
            {
                _comboRemaining = 0;
                return;
            }

            _comboRemaining--;

            // A follow-up is a commitment by definition — the boss has strung itself into a chain — so
            // a chain that swings through empty air can overbalance it just as a lunge can.
            context.LastAttackWasCommitted = true;
            _lastSwing = next;
            context.Attacks.Begin(next, context.DistanceToPlayer);
            context.RecentMoves.Record(next);
        }

        /// <summary>
        /// Chooses the next blow of a chain: one of the last swing's authored follow-ups that this phase allows,
        /// or any move of the phase when it has none.
        /// </summary>
        /// <remarks>
        /// Filtered by the phase's moveset first, so an authored drill can never reach for a move the phase has
        /// not unlocked - the opening's sweep does not flow into a thrust the knight has not yet seen.
        /// </remarks>
        /// <param name="lastSwing">The swing just thrown, or null.</param>
        /// <param name="moveset">The current phase's moves.</param>
        /// <param name="random">The seeded random source.</param>
        /// <param name="recent">
        /// The blows thrown lately, or null. A chain is a string of different blows: a move just thrown is
        /// weighed down, and the same move never comes a third time running while anything else is possible.
        /// </param>
        /// <returns>The follow-up, or null for an empty moveset.</returns>
        public static AttackDefinition PickFollowUp(
            AttackDefinition lastSwing, AttackDefinition[] moveset, IRandomProvider random, RecentMoves recent = null)
        {
            if (moveset == null || moveset.Length == 0)
            {
                return null;
            }

            if (lastSwing != null)
            {
                AttackDefinition[] drilled = System.Array.FindAll(
                    lastSwing.FollowUps, followUp => followUp != null && System.Array.IndexOf(moveset, followUp) >= 0);

                if (drilled.Length > 0)
                {
                    return PickByRecency(drilled, random, recent);
                }
            }

            return PickByRecency(moveset, random, recent);
        }

        /// <summary>
        /// Draws one of the candidates, each weighted by how lately it was thrown.
        /// </summary>
        /// <remarks>
        /// With no history, or when every candidate was ruled out, every candidate is equally likely, as
        /// before: a single drilled follow-up is still always the one thrown.
        /// </remarks>
        private static AttackDefinition PickByRecency(
            AttackDefinition[] candidates, IRandomProvider random, RecentMoves recent)
        {
            float total = 0f;

            if (recent != null)
            {
                foreach (AttackDefinition candidate in candidates)
                {
                    total += candidate != null ? recent.WeightFor(candidate) : 0f;
                }
            }

            if (total <= 0f)
            {
                return candidates[random.NextInt(0, candidates.Length)];
            }

            float roll = random.NextFloat(0f, total);

            foreach (AttackDefinition candidate in candidates)
            {
                float weight = candidate != null ? recent.WeightFor(candidate) : 0f;

                if (weight <= 0f)
                {
                    continue;
                }

                roll -= weight;

                if (roll <= 0f)
                {
                    return candidate;
                }
            }

            // Floating-point accumulation can leave the roll marginally past the total.
            return System.Array.FindLast(candidates, candidate => candidate != null && recent.WeightFor(candidate) > 0f);
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

            // A boss that has learned the player's favourite roll leans its aim that way too, so the roll the
            // player always takes is the one it is waiting for. Relative to the boss, as the habit was read.
            if (context.HasHabitualDodge && predictionStrength > 0f)
            {
                Vector3 away = observation.Position - context.Transform.position;
                away.y = 0f;

                if (away.sqrMagnitude > Mathf.Epsilon)
                {
                    away.Normalize();
                    Vector3 right = Vector3.Cross(Vector3.up, away);
                    Vector3 roll = right * context.HabitualDodge.x + away * context.HabitualDodge.y;
                    aimPoint += roll * (HabitualRollLeadMetres * predictionStrength);
                }
            }

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
        /// <summary>True once the parry attempt has resolved one way or the other.</summary>
        /// <remarks>
        /// The stance runs for its window plus its tail. It used to be a flat half-second during
        /// which every hit was refused, which made it something to wait out rather than something
        /// to beat: there was no moment at which committing to the stance had been the wrong call.
        /// </remarks>
        /// <param name="context">The boss context.</param>
        /// <returns>Whether the boss may act again.</returns>
        public bool IsComplete(BossContext context) =>
            TimeInState >= context.Config.ParryWindowSeconds + context.Config.ParryTailSeconds;

        /// <inheritdoc />
        protected override void OnEnter(BossContext context)
        {
            context.IsParrying = true;
            context.ParryWindowOpen = true;

            // Announced the instant the window opens, and only then. The player has to be able to
            // see what they are being asked to beat, or a parry they cannot read is indistinguishable
            // from the boss refusing hits at random.
            context.PublishCombatEvent(CombatEventKind.GuardRaised);
            context.Motor.Halt();
        }

        /// <inheritdoc />
        protected override void OnTick(BossContext context, float deltaTime)
        {
            context.ParryWindowOpen = TimeInState <= context.Config.ParryWindowSeconds;

            context.Motor.FaceDirection(context.DirectionToPlayer, deltaTime);
            context.Motor.Tick(deltaTime);
        }

        /// <inheritdoc />
        protected override void OnExit(BossContext context)
        {
            context.IsParrying = false;
            context.ParryWindowOpen = false;

            // On its own timer, not the attack cooldown. A successful parry clears the attack
            // cooldown to hand the boss the punish it earned - and while the two shared one timer,
            // that also re-armed the next parry, so a boss that kept guessing right could not be
            // attacked at all. The old flat 0.4s clamp was papering over exactly this.
            context.ParryCooldownRemaining =
                Mathf.Max(context.ParryCooldownRemaining, context.Config.ParryCooldownSeconds);
        }
    }

    /// <summary>Reeling from a poise break.</summary>
    public sealed class BossStaggerState : StateBase<BossContext>
    {
        private float _remainingSeconds;

        private StaggerReason _reason;

        /// <summary>True once the interruption has run its course.</summary>
        /// <param name="context">The boss context.</param>
        /// <returns>Whether the boss may act again.</returns>
        public bool IsComplete(BossContext context) => _remainingSeconds <= 0f;

        /// <inheritdoc />
        protected override void OnEnter(BossContext context)
        {
            _remainingSeconds = context.RequestedStaggerSeconds;
            _reason = context.RequestedStaggerReason;
            context.StaggerRequested = false;
            context.RequestedStaggerReason = StaggerReason.Hit;

            context.Motor.Halt();

            // Announced only when the guard actually broke. PoiseBroken throws the loudest burst and
            // cue in the game, and firing it for a parry recoil would tell the player they had won
            // something they had not - the parry has its own, quieter feedback at the moment of
            // contact.
            if (_reason == StaggerReason.PoiseBreak)
            {
                context.PublishCombatEvent(CombatEventKind.PoiseBroken);
            }
        }

        /// <inheritdoc />
        protected override void OnTick(BossContext context, float deltaTime)
        {
            if (context.StaggerRequested)
            {
                _remainingSeconds = Mathf.Max(_remainingSeconds, context.RequestedStaggerSeconds);
                context.StaggerRequested = false;

                // A poise break arriving on the same frame as a parry recoil must not be downgraded
                // to one. Clearing the reason afterwards matters as much: left set, the next
                // ordinary stagger would inherit it and announce the wrong thing.
                if (context.RequestedStaggerReason > _reason)
                {
                    _reason = context.RequestedStaggerReason;

                    if (_reason == StaggerReason.PoiseBreak)
                    {
                        context.PublishCombatEvent(CombatEventKind.PoiseBroken);
                    }
                }

                context.RequestedStaggerReason = StaggerReason.Hit;
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
