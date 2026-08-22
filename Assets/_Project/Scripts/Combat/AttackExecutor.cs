using System;
using System.Collections.Generic;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// Runs one attack from wind-up to recovery and resolves everything it touches.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shared by the player and the boss. Both throw attacks defined the same way, so both should
    /// resolve them through the same code; duplicating it would let their damage, hit-stop and
    /// whiff reporting drift apart, and the learning system depends on those signals meaning the
    /// same thing for each side.
    /// </para>
    /// <para>
    /// A target can only be struck once per attack. Without that, an active window spanning several
    /// frames would deal its damage on every one of them, making an attack's real damage depend on
    /// frame rate.
    /// </para>
    /// </remarks>
    public sealed class AttackExecutor
    {
        /// <summary>
        /// Safety-net lifetime for a spawned cast effect, in seconds. Well past the longest attack, so
        /// a self-cleaning effect finishes first and only a leaked one is caught here.
        /// </summary>
        private const float CastVfxLifetimeSeconds = 4f;

        private readonly AttackHitDetector _detector = new AttackHitDetector();
        private readonly HashSet<int> _alreadyStruck = new HashSet<int>();
        private readonly AttackTimeline _timeline = new AttackTimeline();

        private readonly Transform _origin;
        private readonly CombatantTeam _team;
        private readonly int _instanceId;
        private readonly int _targetLayerMask;
        private readonly ITimeService _time;
        private readonly ICombatEventBus _events;
        private readonly IScreenShake _screenShake;
        private readonly Feel.AttackVisualizer _visualizer;
        private readonly IHazardField _hazardField;

        private bool _landedThisAttack;

        /// <summary>Creates an executor bound to one combatant.</summary>
        /// <param name="origin">Transform attack volumes are positioned relative to.</param>
        /// <param name="team">Team the attacks belong to.</param>
        /// <param name="targetLayerMask">Layers this combatant's attacks may strike.</param>
        /// <param name="time">Time service, used to request hit-stop.</param>
        /// <param name="events">Bus that witnessed occurrences are published to.</param>
        /// <param name="screenShake">Optional shake sink for impact feedback.</param>
        /// <param name="visualizer">
        /// Optional ground overlay showing the hit volume. Without it the attack is invisible, so it
        /// is only optional for tests that have no scene.
        /// </param>
        /// <param name="hazardField">
        /// Optional sink for ground hazards. Supplied to the boss and left null for the player, which
        /// is precisely what makes only boss attacks scar the ground.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is missing.</exception>
        public AttackExecutor(
            Transform origin,
            CombatantTeam team,
            int targetLayerMask,
            ITimeService time,
            ICombatEventBus events,
            IScreenShake screenShake = null,
            Feel.AttackVisualizer visualizer = null,
            IHazardField hazardField = null)
        {
            _origin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            _team = team;
            _instanceId = origin.GetInstanceID();
            _targetLayerMask = targetLayerMask;
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _screenShake = screenShake;
            _visualizer = visualizer;
            _hazardField = hazardField;
        }

        /// <summary>The attack currently running, or null.</summary>
        public AttackDefinition CurrentAttack => _timeline.CurrentAttack;

        /// <summary>True while an attack is in progress.</summary>
        public bool IsRunning => _timeline.IsRunning;

        /// <summary>The phase the current attack has reached.</summary>
        public AttackPhase Phase => _timeline.Phase;

        /// <summary>Seconds elapsed in the current attack.</summary>
        public float ElapsedSeconds => _timeline.ElapsedSeconds;

        /// <summary>True while the current attack will accept a follow-up input.</summary>
        public bool IsInComboWindow => _timeline.IsInComboWindow;

        /// <summary>True when the current attack has struck at least one target.</summary>
        public bool HasLanded => _landedThisAttack;

        /// <summary>Raised each time this attack deals damage.</summary>
        public event Action<DamageResult> HitLanded;

        /// <summary>Raised when the attack finishes and the attacker becomes actionable.</summary>
        public event Action Completed;

        /// <summary>Raised whenever the running attack changes phase.</summary>
        public event Action<AttackPhase> PhaseChanged;

        /// <summary>
        /// Raised when this attack was met on the beat and refused.
        /// </summary>
        /// <remarks>
        /// <b>A handler may set flags only.</b> This fires from inside the timeline's phase-stepping
        /// loop, so calling <see cref="Cancel"/> from a handler re-enters that loop while it is
        /// still running. The recoil a parry deserves must therefore be requested and acted on next
        /// tick — which the stagger request already does, since entering the stagger state exits the
        /// attack state and its exit cancels the swing.
        /// </remarks>
        public event Action Parried;

        /// <summary>Begins an attack, replacing any already running.</summary>
        /// <param name="attack">The attack to perform.</param>
        /// <param name="separationDistance">Distance to the opponent, recorded with the event.</param>
        public void Begin(AttackDefinition attack, float separationDistance = 0f)
        {
            if (attack == null)
            {
                return;
            }

            _alreadyStruck.Clear();
            _landedThisAttack = false;

            EnsureSubscribed();
            _timeline.Begin(attack);
            SpawnCastVfx(attack);

            _events.Publish(new CombatEvent
            {
                Kind = CombatEventKind.AttackStarted,
                Actor = _team,
                Timestamp = _time.CombatTime,
                DamageType = attack.DamageType,
                Unblockable = attack.Unblockable,
                Position = _origin.position,
                Direction = _origin.forward,
                SeparationDistance = separationDistance
            });
        }

        /// <summary>Advances the attack and resolves hits while its window is open.</summary>
        /// <param name="deltaTime">Elapsed scaled time, so hit-stop freezes attack progress.</param>
        public void Tick(float deltaTime)
        {
            if (!_timeline.IsRunning)
            {
                return;
            }

            _timeline.Tick(deltaTime);

            // Resolution runs after the tick so a frame that opens the window also tests it. Testing
            // first would cost every attack one frame of reach.
            if (_timeline.Phase == AttackPhase.Active)
            {
                ResolveHits(_timeline.CurrentAttack);
            }
        }

        /// <summary>Aborts the attack, typically because the attacker was staggered.</summary>
        public void Cancel() => _timeline.Cancel();

        /// <summary>Spawns the attack's optional cast effect at the attacker, parented so it follows.</summary>
        /// <remarks>
        /// The ability-art seam. Null for every attack today, so this does nothing. A prefab dropped
        /// onto an attack appears here, parented to the origin so it rides the lunge, and is destroyed
        /// after a generous safety lifetime in case the effect does not clean itself up.
        /// </remarks>
        private void SpawnCastVfx(AttackDefinition attack)
        {
            if (attack.CastVfxPrefab == null)
            {
                return;
            }

            GameObject effect = UnityEngine.Object.Instantiate(
                attack.CastVfxPrefab, _origin.position, _origin.rotation, _origin);
            UnityEngine.Object.Destroy(effect, CastVfxLifetimeSeconds);
        }

        /// <summary>Tests every hurtbox in the volume and applies damage to those not yet struck.</summary>
        private void ResolveHits(AttackDefinition attack)
        {
            int count = _detector.Query(attack, _origin, _targetLayerMask);

            for (int i = 0; i < count; i++)
            {
                Hurtbox hurtbox = _detector.Results[i];
                int targetId = hurtbox.Owner.GetHashCode();

                // Recording the target before knowing the outcome is intentional. An attack that was
                // dodged must not get a second chance at the same target on the next frame.
                if (!_alreadyStruck.Add(targetId))
                {
                    continue;
                }

                ApplyTo(attack, hurtbox);
            }
        }

        /// <summary>Offers the attack to one hurtbox and reacts to what the target decided.</summary>
        private void ApplyTo(AttackDefinition attack, Hurtbox hurtbox)
        {
            Vector3 contactPoint = hurtbox.transform.position;
            Vector3 direction = contactPoint - _origin.position;
            direction.y = 0f;
            direction = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : _origin.forward;

            DamageInfo damage = attack
                .BuildDamageInfo(_team, _instanceId)
                .AtPoint(contactPoint, direction);

            DamageResult result = hurtbox.Receive(damage);

            switch (result.Outcome)
            {
                case DamageOutcome.Applied:
                    OnDamageDealt(attack, contactPoint, result);
                    break;

                case DamageOutcome.PerfectDodged:
                case DamageOutcome.Invulnerable:
                    PublishEvaded(attack, contactPoint);
                    break;

                case DamageOutcome.Deflected:
                    OnParried(attack, contactPoint, direction);
                    break;
            }
        }

        /// <summary>Reports that this swing was parried, so the attacker can pay for it.</summary>
        /// <remarks>
        /// The only place an attacker learns it was parried. The event carries the <em>defender</em>
        /// as its actor, per the convention documented on <see cref="CombatEventKind.Parried"/>, and
        /// the incoming direction so sparks are thrown back the way the blow came rather than in an
        /// undirected puff.
        /// </remarks>
        private void OnParried(AttackDefinition attack, Vector3 contactPoint, Vector3 direction)
        {
            _events.Publish(new CombatEvent
            {
                Kind = CombatEventKind.Parried,
                Actor = _team == CombatantTeam.Player ? CombatantTeam.Boss : CombatantTeam.Player,
                Timestamp = _time.CombatTime,
                DamageType = attack.DamageType,
                Unblockable = attack.Unblockable,
                Position = contactPoint,
                Direction = direction
            });

            Parried?.Invoke();
        }

        /// <summary>Applies impact feedback and reports a landed hit.</summary>
        private void OnDamageDealt(AttackDefinition attack, Vector3 contactPoint, DamageResult result)
        {
            _landedThisAttack = true;

            _time.RequestHitStop(Mathf.Min(attack.HitStopSeconds, GameplayConstants.MaxHitStopSeconds));
            _screenShake?.AddTrauma(attack.CameraTrauma);

            // A punch on top of the shake, scaled by the same trauma, so a heavy blow lunges the
            // camera in while a light one barely registers it.
            _screenShake?.Punch(attack.CameraTrauma);

            _events.Publish(new CombatEvent
            {
                Kind = CombatEventKind.AttackLanded,
                Actor = _team,
                Timestamp = _time.CombatTime,
                DamageType = attack.DamageType,
                Position = contactPoint,
                Direction = _origin.forward,
                Magnitude = result.AmountApplied
            });

            HitLanded?.Invoke(result);
        }

        /// <summary>Reports an attack that reached its target but was avoided.</summary>
        private void PublishEvaded(AttackDefinition attack, Vector3 contactPoint) =>
            _events.Publish(new CombatEvent
            {
                Kind = CombatEventKind.AttackEvaded,
                Actor = _team,
                Timestamp = _time.CombatTime,
                DamageType = attack.DamageType,
                Position = contactPoint,
                Direction = _origin.forward
            });

        /// <summary>Relays timeline completion, reporting a whiff when nothing was struck.</summary>
        private void OnTimelineCompleted()
        {
            if (!_landedThisAttack)
            {
                // The signal behind the boss noticing its pattern has been read, and behind the
                // player's whiff ratio. An attack that touched nothing is as informative as one that
                // connected.
                _events.Publish(new CombatEvent
                {
                    Kind = CombatEventKind.AttackWhiffed,
                    Actor = _team,
                    Timestamp = _time.CombatTime,
                    Position = _origin.position,
                    Direction = _origin.forward
                });
            }

            Completed?.Invoke();
        }

        private bool _isSubscribed;

        /// <summary>Attaches to the timeline once, on first use.</summary>
        private void EnsureSubscribed()
        {
            if (_isSubscribed)
            {
                return;
            }

            _timeline.Completed += OnTimelineCompleted;
            _timeline.PhaseChanged += OnPhaseChanged;
            _isSubscribed = true;
        }

        /// <summary>
        /// Drives the on-ground visual and relays the phase to subscribers.
        /// </summary>
        /// <remarks>
        /// Driven from the phase transition rather than polled, so the telegraph appears on exactly
        /// the frame the wind-up begins and the flash on exactly the frame the hitbox opens. Any
        /// drift between the two would teach the player a timing that is not real.
        /// </remarks>
        private void OnPhaseChanged(AttackPhase phase)
        {
            if (_visualizer != null)
            {
                AttackDefinition attack = _timeline.CurrentAttack;

                switch (phase)
                {
                    case AttackPhase.Startup:
                        if (attack != null && attack.ShowTelegraph)
                        {
                            _visualizer.ShowTelegraph(attack);
                        }

                        break;

                    case AttackPhase.Active:
                        _visualizer.ShowStrike(attack);
                        break;

                    default:
                        _visualizer.Hide();
                        break;
                }
            }

            if (phase == AttackPhase.Active)
            {
                TrySpawnHazard(_timeline.CurrentAttack);

                // Resolved here as well as per frame, so entering the window always tests it at
                // least once. The timeline steps one phase at a time specifically so a long frame
                // cannot skip past an active window without the hitbox opening -- but Tick then
                // reads the phase only after that stepping has finished, so a frame long enough to
                // cross startup, active and recovery together ended on recovery and never queried
                // at all. The swing looked like it should have connected and nothing happened.
                //
                // Running twice on an ordinary frame is harmless: the already-struck set dedupes
                // per swing, so the second pass is a no-op for anything already hit and can still
                // catch something that moved in.
                //
                // One constraint this creates: damage is now applied from inside the timeline's own
                // phase-stepping loop, so nothing reached from a damage path may cancel this
                // executor's timeline synchronously. Every Cancel call site today sits in a state
                // tick or a state exit, so none does -- but a deflect that aborts the attacker's
                // swing would, and must therefore defer rather than cancel inline.
                ResolveHits(_timeline.CurrentAttack);
            }

            PhaseChanged?.Invoke(phase);
        }

        /// <summary>Scars the ground beneath the attacker when a hazard-leaving attack goes live.</summary>
        /// <remarks>
        /// Spawned at the attacker's own position, at floor height: the boss's slams and shockwaves
        /// erupt from under it, so the scar sits where it struck. Null field (the player) or a
        /// non-hazard attack does nothing.
        /// </remarks>
        private void TrySpawnHazard(AttackDefinition attack)
        {
            if (_hazardField == null || attack == null || !attack.LeavesHazard)
            {
                return;
            }

            Vector3 center = _origin.position;
            center.y = GameplayConstants.GroundPlaneHeight;

            _hazardField.Spawn(
                center, attack.HazardRadius, attack.HazardDamagePerTick, attack.HazardDurationSeconds);
        }
    }
}
