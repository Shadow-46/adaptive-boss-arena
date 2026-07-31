using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Turns witnessed combat occurrences into sparks, flashes and trails.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The visual counterpart to <see cref="CombatAudioDirector"/>, and deliberately built the same
    /// way: combat systems publish what happened and know nothing about how it is presented. Adding a
    /// new effect never means editing combat code.
    /// </para>
    /// <para>
    /// Note what this does <em>not</em> do — it never feeds anything back into the fight. It is a
    /// pure consumer of the event bus, which is the only role the bus is safe for outside the
    /// learning system's statistics.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CombatEffectsDirector : MonoBehaviour
    {
        /// <summary>
        /// How far above a reported event position a burst is placed.
        /// </summary>
        /// <remarks>
        /// Combat events are positioned at a character's transform origin, which sits on the floor.
        /// Sparks emitted there are half swallowed by the ground and read as dust rather than as a
        /// hit. Roughly chest height on both combatants puts them where the blow actually landed.
        /// </remarks>
        private const float ImpactHeight = 1.1f;

        /// <summary>Height the phase-transition shockwave erupts from, roughly the boss's centre.</summary>
        private const float ShockwaveHeight = 1.2f;

        /// <summary>Field-of-view kick, in degrees, that widens the view on a dash for a burst of speed.</summary>
        private const float DashFovWiden = 6f;

        /// <summary>Field-of-view narrowing on a clean deflect, snapping focus onto the parry.</summary>
        private const float DeflectFovNarrow = 3.5f;

        /// <summary>Field-of-view narrowing on a perfect dodge, complementing the slow-motion.</summary>
        private const float PerfectDodgeFovNarrow = 5f;

        [SerializeField]
        [Tooltip("Raised on a perfect dodge.")]
        private VoidEventChannel _perfectDodgeChannel;

        [SerializeField]
        [Tooltip("Carries the boss phase index, used to erupt a shockwave on each escalation.")]
        private IntEventChannel _bossPhaseChannel;

        private ImpactBurstPool _bursts;
        private ICombatEventBus _events;
        private IScreenShake _shake;
        private WeaponTrail _playerTrail;
        private WeaponTrail _bossTrail;
        private Transform _bossTransform;

        private void Awake()
        {
            _bursts = gameObject.AddComponent<ImpactBurstPool>();
        }

        private void Start()
        {
            ServiceRegistry.Current.TryGet(out _shake);

            if (ServiceRegistry.Current.TryGet(out _events))
            {
                _events.EventRecorded += OnCombatEvent;
            }

            if (_perfectDodgeChannel != null)
            {
                _perfectDodgeChannel.Raised += OnPerfectDodge;
            }

            if (_bossPhaseChannel != null)
            {
                _bossPhaseChannel.Raised += OnBossPhaseChanged;
            }

            ResolveTrails();
        }

        private void OnDestroy()
        {
            if (_events != null)
            {
                _events.EventRecorded -= OnCombatEvent;
            }

            if (_perfectDodgeChannel != null)
            {
                _perfectDodgeChannel.Raised -= OnPerfectDodge;
            }

            if (_bossPhaseChannel != null)
            {
                _bossPhaseChannel.Raised -= OnBossPhaseChanged;
            }
        }

        /// <summary>Maps a witnessed occurrence onto a burst or a trail.</summary>
        private void OnCombatEvent(CombatEvent combatEvent)
        {
            switch (combatEvent.Kind)
            {
                case CombatEventKind.AttackStarted:
                    TrailFor(combatEvent.Actor)?.Begin();
                    break;

                case CombatEventKind.AttackLanded:
                    Burst(combatEvent.Position, combatEvent.Direction, FlavourFor(combatEvent.DamageType));
                    break;

                case CombatEventKind.Deflected:
                    // Driven from the bus rather than from the deflect event channel, which carries
                    // no position and fires for the same moment. Listening to both would double the
                    // burst on the single most important beat in the fight.
                    Burst(combatEvent.Position, -combatEvent.Direction, ImpactFlavour.Deflect);
                    _shake?.PunchFov(-DeflectFovNarrow);
                    break;

                case CombatEventKind.DodgePerformed:
                    // The whole camera breathes out on a dash, which is most of what makes a roll read
                    // as a burst of speed rather than a teleport.
                    _shake?.PunchFov(DashFovWiden);
                    break;

                case CombatEventKind.Blocked:
                    Burst(combatEvent.Position, -combatEvent.Direction, ImpactFlavour.Block);
                    break;

                case CombatEventKind.PoiseBroken:
                    Burst(combatEvent.Position, Vector3.up, ImpactFlavour.PostureBreak);
                    break;

                case CombatEventKind.Riposte:
                    TrailFor(combatEvent.Actor)?.Begin();
                    Burst(combatEvent.Position, Vector3.up, ImpactFlavour.PostureBreak);
                    break;
            }
        }

        /// <summary>Plays a burst, raised off the floor to where the blow landed.</summary>
        private void Burst(Vector3 position, Vector3 direction, ImpactFlavour flavour) =>
            _bursts.Play(position + Vector3.up * ImpactHeight, direction, flavour);

        /// <summary>Heavier attacks throw a bigger, slower burst so weight is visible as well as audible.</summary>
        private static ImpactFlavour FlavourFor(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Heavy:
                case DamageType.BossMelee:
                    return ImpactFlavour.Heavy;

                default:
                    return ImpactFlavour.Light;
            }
        }

        /// <summary>Whose weapon is swinging.</summary>
        private WeaponTrail TrailFor(CombatantTeam team) =>
            team == CombatantTeam.Player ? _playerTrail : _bossTrail;

        /// <summary>
        /// Finds the trail on each combatant.
        /// </summary>
        /// <remarks>
        /// Resolved by scene search rather than injected, because the combatants are spawned from
        /// prefabs by the scene generator and there is no seam to hand references through. The search
        /// happens once, at startup.
        /// </remarks>
        private void ResolveTrails()
        {
            WeaponTrail[] trails = FindObjectsByType<WeaponTrail>(FindObjectsSortMode.None);

            foreach (WeaponTrail trail in trails)
            {
                var combatant = trail.GetComponentInParent<IDamageable>();

                if (combatant == null)
                {
                    continue;
                }

                if (combatant.Team == CombatantTeam.Player)
                {
                    _playerTrail = trail;
                }
                else
                {
                    _bossTrail = trail;
                    _bossTransform = (combatant as Component)?.transform;
                }
            }
        }

        /// <summary>Erupts a shockwave from the boss on each escalation.</summary>
        /// <remarks>
        /// The phase channel also fires for the opening phase and for a reset, so only an index above
        /// zero — a genuine escalation, since the boss's health only ever falls — gets the eruption.
        /// </remarks>
        private void OnBossPhaseChanged(int phaseIndex)
        {
            if (phaseIndex <= 0 || _bossTransform == null)
            {
                return;
            }

            _bursts.Play(
                _bossTransform.position + Vector3.up * ShockwaveHeight, Vector3.up, ImpactFlavour.PostureBreak);
        }

        private void OnPerfectDodge()
        {
            // A tight zoom-in to match the slow-motion — the frame narrows onto the opening the read
            // just bought.
            _shake?.PunchFov(-PerfectDodgeFovNarrow);

            // Placed on the player rather than at an impact point, because nothing was struck. The
            // burst marks where they were when they slipped it.
            if (_playerTrail != null)
            {
                // Not lifted, unlike the others: the trail already sits at chest height, so this is
                // the one position in the class that is already where it should be.
                _bursts.Play(_playerTrail.transform.position, Vector3.up, ImpactFlavour.Deflect);
            }
        }

        /// <summary>Assigns the channels. Used by the scene generator.</summary>
        /// <param name="perfectDodge">Perfect dodge channel.</param>
        /// <param name="bossPhase">Boss phase index channel.</param>
        public void Bind(VoidEventChannel perfectDodge, IntEventChannel bossPhase)
        {
            _perfectDodgeChannel = perfectDodge;
            _bossPhaseChannel = bossPhase;
        }
    }
}
