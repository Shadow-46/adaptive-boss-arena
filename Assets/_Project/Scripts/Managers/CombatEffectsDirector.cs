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
        /// How far above a reported event position a burst is placed, when the event was reported at a body's feet.
        /// </summary>
        /// <remarks>
        /// Some events are still positioned at a character's transform origin, which sits on the floor, and sparks
        /// emitted there are half swallowed by the ground. A blow struck with a blade now reports where it touched,
        /// already at the height it landed, so lifting every event put sparks and blood above the knight's head.
        /// Only a position at floor level is lifted.
        /// </remarks>
        private const float ImpactHeight = 1.1f;

        /// <summary>Below this height above the floor a reported position is a body's feet, not a contact.</summary>
        private const float FeetHeight = 0.15f;

        /// <summary>Field-of-view narrowing on a heavy blow landing, the view drawn in by its weight.</summary>
        private const float HeavyHitFovNarrow = 2.5f;

        /// <summary>Height the phase-transition shockwave erupts from, roughly the boss's centre.</summary>
        private const float ShockwaveHeight = 1.2f;

        /// <summary>Field-of-view kick, in degrees, that widens the view on a dash for a burst of speed.</summary>
        private const float DashFovWiden = 6f;

        /// <summary>Field-of-view narrowing on a clean deflect, snapping focus onto the parry.</summary>
        private const float DeflectFovNarrow = 3.5f;

        /// <summary>Field-of-view narrowing on a perfect dodge, complementing the slow-motion.</summary>
        private const float PerfectDodgeFovNarrow = 5f;

        /// <summary>Screen-shake trauma on an overbalance, a small jolt so the stumble lands physically.</summary>
        private const float OverbalanceTrauma = 0.2f;

        /// <summary>
        /// Shake for a body driven into the wall. Between a heavy hit's and a slam's: the wall is the
        /// harder thing to be hit by, but it is not an attack and should not read as one.
        /// </summary>
        private const float WallImpactTrauma = 0.32f;

        /// <summary>
        /// World speed for the beat after a guard breaks.
        /// </summary>
        /// <remarks>
        /// The biggest moment in an exchange landed at full speed, over as soon as it began. A third of a
        /// second at a third of the speed lets the stagger be seen, and is well inside the opening it grants.
        /// </remarks>
        private const float PoiseBreakSlowMotionScale = 0.35f;

        private const float PoiseBreakSlowMotionSeconds = 0.35f;

        /// <summary>Shake and camera punch on a broken guard: heavier than any single blow.</summary>
        private const float PoiseBreakTrauma = 0.38f;

        /// <summary>Rough size of the splatter a light blow leaves, in metres.</summary>
        private const float LightBloodSize = 0.8f;

        /// <summary>Rough size of the splatter a heavy blow leaves, in metres.</summary>
        private const float HeavyBloodSize = 1.4f;

        /// <summary>Seed for splatter turns and sizes: cosmetic, and kept off the gameplay generator.</summary>
        private const uint BloodSeed = 0xB100Du;

        [SerializeField]
        [Tooltip("Raised on a perfect dodge.")]
        private VoidEventChannel _perfectDodgeChannel;

        [SerializeField]
        [Tooltip("Carries the boss phase index, used to erupt a shockwave on each escalation.")]
        private IntEventChannel _bossPhaseChannel;

        [SerializeField]
        [Tooltip("Raised when the boss overbalances, used to kick up dust at its feet.")]
        private VoidEventChannel _overbalanceChannel;

        [SerializeField]
        [Tooltip("Material impact sparks are drawn with. Assigned by the scene generator, so its shader ships.")]
        private Material _impactMaterial;

        [SerializeField]
        [Tooltip("Alpha-blended material blood and dust are drawn with. Assigned by the scene generator.")]
        private Material _matterMaterial;

        [SerializeField]
        [Tooltip("Splatter left on the floor under a landed blow. Assigned by the scene generator.")]
        private Material _bloodMaterial;

        private BloodDecalPool _blood;

        private ImpactBurstPool _bursts;
        private ICombatEventBus _events;
        private IScreenShake _shake;
        private ScreenEffects _screenEffects;
        private ITimeService _time;
        private WeaponTrail _playerTrail;
        private WeaponTrail _bossTrail;
        private Transform _bossTransform;

        private void Awake()
        {
            _bursts = gameObject.AddComponent<ImpactBurstPool>();
            _bursts.Construct(_impactMaterial, _matterMaterial);

            // The controller's share of the same beats, added here because it listens to the same bus.
            gameObject.AddComponent<RumbleDirector>();

            // And the screen's share: the bars step aside while the camera plays a directed shot.
            gameObject.AddComponent<CinematicHud>();

            if (_bloodMaterial != null)
            {
                _blood = new GameObject("BloodDecals").AddComponent<BloodDecalPool>();
                _blood.transform.SetParent(transform, worldPositionStays: false);
                _blood.Construct(_bloodMaterial, new XorShiftRandomProvider(BloodSeed));
            }
        }

        private void Start()
        {
            ServiceRegistry.Current.TryGet(out _shake);
            ServiceRegistry.Current.TryGet(out _time);

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

            if (_overbalanceChannel != null)
            {
                _overbalanceChannel.Raised += OnOverbalance;
            }

            ResolveTrails();
            _screenEffects = FindAnyObjectByType<ScreenEffects>();
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

            if (_overbalanceChannel != null)
            {
                _overbalanceChannel.Raised -= OnOverbalance;
            }
        }

        /// <summary>Kicks up dust at the boss's feet as it stumbles, marking the opening a read bought.</summary>
        /// <remarks>
        /// A dull, dispersed burst low to the ground — the block flavour reads as dust rather than
        /// sparks — plus a small jolt, so the overextension is felt as well as seen. Placed on the
        /// boss because the miss is the boss's, not the player's.
        /// </remarks>
        private void OnOverbalance()
        {
            if (_bossTransform != null)
            {
                _bursts.Play(_bossTransform.position + Vector3.up * 0.25f, Vector3.up, ImpactFlavour.Stone);
            }

            _shake?.AddTrauma(OverbalanceTrauma);
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
                    ImpactFlavour flavour = FlavourFor(combatEvent.DamageType);
                    Burst(combatEvent.Position, combatEvent.Direction, flavour);
                    _blood?.Place(combatEvent.Position, combatEvent.Direction,
                        flavour == ImpactFlavour.Heavy ? HeavyBloodSize : LightBloodSize);

                    // The frame is thrown the way the blade travelled: hardest when the knight is the one struck,
                    // a firm nudge on a heavy that lands, a flick on a light one.
                    _shake?.Kick(combatEvent.Direction, KickFor(combatEvent.Actor, flavour));

                    // A heavy blow draws the view in as well as shaking it - weight felt, not only seen - and on
                    // High the colour fringes for an instant, the deflect's pulse at a lower pitch.
                    if (flavour == ImpactFlavour.Heavy)
                    {
                        _shake?.PunchFov(-HeavyHitFovNarrow);

                        if (GraphicsTier.Current == GraphicsQuality.High)
                        {
                            _screenEffects?.PulseHeavyImpact();
                        }
                    }

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

                case CombatEventKind.Parried when combatEvent.Actor != CombatantTeam.Player:
                    // Only when someone other than the player parried. The player's own deflect
                    // publishes Deflected as well and is served by the case above; bursting on both
                    // would double the sparks on the single most important beat in the fight, which
                    // is the same mistake the comment there already guards against.
                    Burst(combatEvent.Position, -combatEvent.Direction, ImpactFlavour.Deflect);
                    break;

                case CombatEventKind.WallImpact:
                    // Sparks thrown back off the wall, toward the fight, where the body met the stone.
                    Burst(combatEvent.Position, -combatEvent.Direction, ImpactFlavour.Stone);
                    _shake?.AddTrauma(WallImpactTrauma);
                    break;

                case CombatEventKind.PoiseBroken:
                    Burst(combatEvent.Position, Vector3.up, ImpactFlavour.PostureBreak);
                    _time?.RequestSlowMotion(PoiseBreakSlowMotionScale, PoiseBreakSlowMotionSeconds);
                    _shake?.AddTrauma(PoiseBreakTrauma);
                    _shake?.Punch(PoiseBreakTrauma);
                    break;

                case CombatEventKind.Riposte:
                    TrailFor(combatEvent.Actor)?.Begin();
                    Burst(combatEvent.Position, Vector3.up, ImpactFlavour.PostureBreak);
                    break;
            }
        }

        /// <summary>Plays a burst, raised off the floor to where the blow landed.</summary>
        private void Burst(Vector3 position, Vector3 direction, ImpactFlavour flavour) =>
            _bursts.Play(BurstPosition(position), direction, flavour);

        /// <summary>Where a burst plays for an event reported at a position: there, or chest height above feet.</summary>
        /// <param name="reported">The event's position.</param>
        /// <returns>The burst's position.</returns>
        public static Vector3 BurstPosition(Vector3 reported) =>
            reported.y < FeetHeight ? reported + Vector3.up * ImpactHeight : reported;

        /// <summary>How far a landed blow knocks the camera, in metres.</summary>
        /// <param name="attacker">Who struck.</param>
        /// <param name="flavour">How heavy the blow was.</param>
        /// <returns>The kick's strength.</returns>
        public static float KickFor(CombatantTeam attacker, ImpactFlavour flavour) =>
            attacker != CombatantTeam.Player ? BlowTakenKick
            : flavour == ImpactFlavour.Heavy ? HeavyBlowLandedKick
            : LightBlowLandedKick;

        /// <summary>Camera knock when the knight is struck: the biggest, because it is the blow that matters most.</summary>
        private const float BlowTakenKick = 0.16f;

        /// <summary>Camera knock when the knight lands a heavy blow.</summary>
        private const float HeavyBlowLandedKick = 0.09f;

        /// <summary>Camera knock when the knight lands a light blow: just enough to feel the contact.</summary>
        private const float LightBlowLandedKick = 0.035f;

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
            bool escalation = phaseIndex > _lastPhase;
            _lastPhase = phaseIndex;

            if (phaseIndex <= 0 || _bossTransform == null || !escalation)
            {
                return;
            }

            _bursts.Play(
                _bossTransform.position + Vector3.up * ShockwaveHeight, Vector3.up, ImpactFlavour.PostureBreak);

            PlayPhaseBeat(phaseIndex);
        }

        /// <summary>The last phase seen, so a reset to the opening is not taken for an escalation.</summary>
        private int _lastPhase;

        /// <summary>How long the camera holds on the boss as he turns: inside his transformation, never past it.</summary>
        public const float PhaseShotSeconds = 1.1f;

        /// <summary>The world's speed through the turn, and for how long.</summary>
        private const float PhaseSlowMotionScale = 0.5f;
        private const float PhaseSlowMotionSeconds = 0.5f;

        /// <summary>Shake as the room answers the boss.</summary>
        private const float PhaseTrauma = 0.45f;

        /// <summary>How long the phase's name stays on screen.</summary>
        private const float PhaseTitleSeconds = 2.4f;

        /// <summary>How bright the braziers flare, as a multiple of their own light, and how fast they settle.</summary>
        private const float BrazierFlare = 2.6f;
        private const float BrazierFlareSeconds = 1.8f;

        /// <summary>Stones shaken loose from the vault above, on a web build and on a desktop one.</summary>
        private const int WebDebrisPieces = 8;
        private const int DesktopDebrisPieces = 16;

        private ArenaCameraRig _cameraRig;
        private UI.RoundIntro _intro;
        private FireFlicker[] _fires;
        private DebrisPool _debris;
        private AI.BossController _boss;
        private readonly XorShiftRandomProvider _debrisRandom = new XorShiftRandomProvider(0xDEB1u);

        /// <summary>
        /// Plays a turn of the fight as the set piece it is in the big action games.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The camera cuts to the boss as he roars into his next form, the world slows for a breath, the phase's
        /// name comes up over him, every brazier in the room flares, and stones shake loose from the vault. The
        /// light's turn toward red was already the arena's; this is the moment it happens in.
        /// </para>
        /// <para>
        /// Fair by construction: the boss is invulnerable and cannot strike through his transformation, and the
        /// camera is back behind the knight before it ends, so the phase's shockwave is always seen winding up.
        /// </para>
        /// </remarks>
        private void PlayPhaseBeat(int phaseIndex)
        {
            ResolvePhaseActors();

            _cameraRig?.PlayPhaseShot(PhaseShotSeconds);
            _time?.RequestSlowMotion(PhaseSlowMotionScale, PhaseSlowMotionSeconds);
            _shake?.AddTrauma(PhaseTrauma);
            _intro?.Flash(PhaseTitle(phaseIndex, _boss != null ? _boss.PhaseNameOf(phaseIndex) : string.Empty),
                PhaseTitleSeconds);

            if (_fires != null)
            {
                foreach (FireFlicker fire in _fires)
                {
                    if (fire != null)
                    {
                        fire.Flare(BrazierFlare, BrazierFlareSeconds);
                    }
                }
            }

            RainDebris();
        }

        /// <summary>A phase's title: its numeral and its name, as "II · PRESSING".</summary>
        /// <param name="phaseIndex">Zero-based phase.</param>
        /// <param name="name">The phase's name.</param>
        /// <returns>The title.</returns>
        public static string PhaseTitle(int phaseIndex, string name)
        {
            string[] numerals = { "I", "II", "III", "IV", "V", "VI" };
            string numeral = phaseIndex >= 0 && phaseIndex < numerals.Length
                ? numerals[phaseIndex]
                : (phaseIndex + 1).ToString();

            return string.IsNullOrEmpty(name) ? numeral : $"{numeral} \u00B7 {name.ToUpperInvariant()}";
        }

        /// <summary>Finds the camera, the banner, the fires, the rubble and the boss once, at the first turn.</summary>
        private void ResolvePhaseActors()
        {
            if (_cameraRig == null)
            {
                _cameraRig = FindAnyObjectByType<ArenaCameraRig>();
            }

            if (_intro == null)
            {
                _intro = FindAnyObjectByType<UI.RoundIntro>(FindObjectsInactive.Include);
            }

            _fires ??= FindObjectsByType<FireFlicker>(FindObjectsSortMode.None);

            if (_debris == null)
            {
                _debris = FindAnyObjectByType<DebrisPool>();
            }

            if (_boss == null)
            {
                _boss = FindAnyObjectByType<AI.BossController>();
            }
        }

        /// <summary>Shakes stones loose from the vault, to fall around the two of them.</summary>
        private void RainDebris()
        {
            if (_debris == null || _bossTransform == null)
            {
                return;
            }

            int pieces = Application.platform == RuntimePlatform.WebGLPlayer ? WebDebrisPieces : DesktopDebrisPieces;
            Vector3 centre = _bossTransform.position;

            for (int i = 0; i < pieces; i++)
            {
                float angle = _debrisRandom.NextFloat(0f, 360f);
                float distance = _debrisRandom.NextFloat(2.5f, 8f);
                Vector3 position = centre + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * distance +
                                   Vector3.up * _debrisRandom.NextFloat(7f, 10f);
                float size = _debrisRandom.NextFloat(0.15f, 0.45f);

                _debris.Launch(
                    position,
                    Quaternion.Euler(_debrisRandom.NextFloat(0f, 360f), _debrisRandom.NextFloat(0f, 360f), 0f),
                    new Vector3(size, size * _debrisRandom.NextFloat(0.6f, 1f), size * _debrisRandom.NextFloat(0.6f, 1.2f)),
                    new Vector3(_debrisRandom.NextFloat(-1f, 1f), -_debrisRandom.NextFloat(1f, 3f), _debrisRandom.NextFloat(-1f, 1f)),
                    new Vector3(_debrisRandom.NextFloat(-6f, 6f), _debrisRandom.NextFloat(-6f, 6f), _debrisRandom.NextFloat(-6f, 6f)));
            }
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
        /// <param name="overbalance">Boss overbalance channel.</param>
        /// <param name="impactMaterial">Material the impact sparks are drawn with.</param>
        public void Bind(
            VoidEventChannel perfectDodge,
            IntEventChannel bossPhase,
            VoidEventChannel overbalance,
            Material impactMaterial,
            Material matterMaterial = null,
            Material bloodMaterial = null)
        {
            _perfectDodgeChannel = perfectDodge;
            _bossPhaseChannel = bossPhase;
            _overbalanceChannel = overbalance;
            _impactMaterial = impactMaterial;
            _matterMaterial = matterMaterial;
            _bloodMaterial = bloodMaterial;
        }

        /// <summary>The material floor splatters are drawn with.</summary>
        public Material BloodMaterial => _bloodMaterial;

        /// <summary>The material impact sparks are drawn with.</summary>
        public Material ImpactMaterial => _impactMaterial;

        /// <summary>The material blood and dust are drawn with.</summary>
        public Material MatterMaterial => _matterMaterial;
    }
}
