using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Turns witnessed combat occurrences into sound.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every mapping from "what happened" to "what you hear" lives here, rather than being scattered
    /// through the combat code. Combat systems publish what occurred and remain entirely unaware
    /// that audio exists, which is the same separation the heads-up display already uses.
    /// </para>
    /// <para>
    /// It listens to the combat event bus, which already carries exactly the right events at exactly
    /// the right moments — the bus was built for the learning system, and it turns out that the
    /// occurrences worth learning from are precisely the ones worth hearing.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CombatAudioDirector : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Raised on a clean deflect.")]
        private VoidEventChannel _deflectChannel;

        [SerializeField]
        [Tooltip("Raised on a perfect dodge.")]
        private VoidEventChannel _perfectDodgeChannel;

        [SerializeField]
        [Tooltip("Carries the boss phase index, used to trigger the roar.")]
        private IntEventChannel _phaseChannel;

        [SerializeField]
        [Tooltip("Carries normalised player focus, used to chime the moment it fills.")]
        private FloatEventChannel _focusChannel;

        [SerializeField]
        [Tooltip("Carries the drawn weapon's swing cue, so each weapon sounds like itself.")]
        private StringEventChannel _weaponSwingCueChannel;

        /// <summary>The cue the player's current weapon swings with.</summary>
        /// <remarks>
        /// Latched rather than looked up, because the combat event that announces a swing carries no
        /// weapon: the event struct is deliberately blittable so a fight's worth of them can sit in a
        /// ring buffer without allocating, and a reference would defeat that.
        /// </remarks>
        private string _playerSwingCue = AudioService.Cues.Whoosh;

        [SerializeField]
        [Tooltip("Raised when the boss overbalances, used to sound the unsteady stagger.")]
        private VoidEventChannel _overbalanceChannel;

        private IAudioService _audio;
        private ICombatEventBus _events;

        /// <summary>Latches so the focus-full chime plays once per fill, not every frame it is full.</summary>
        private bool _focusWasFull;

        private void Start()
        {
            ServiceRegistry.Current.TryGet(out _audio);

            if (ServiceRegistry.Current.TryGet(out _events))
            {
                _events.EventRecorded += OnCombatEvent;
            }

            if (_deflectChannel != null)
            {
                _deflectChannel.Raised += OnDeflect;
            }

            if (_perfectDodgeChannel != null)
            {
                _perfectDodgeChannel.Raised += OnPerfectDodge;
            }

            if (_phaseChannel != null)
            {
                _phaseChannel.Raised += OnPhaseChanged;
            }

            if (_focusChannel != null)
            {
                _focusChannel.Raised += OnFocusChanged;
            }

            if (_weaponSwingCueChannel != null)
            {
                _weaponSwingCueChannel.Raised += OnWeaponSwingCueChanged;
            }

            if (_overbalanceChannel != null)
            {
                _overbalanceChannel.Raised += OnOverbalance;
            }

            _audio?.PlayMusic("music.bed");
        }

        private void OnDestroy()
        {
            if (_events != null)
            {
                _events.EventRecorded -= OnCombatEvent;
            }

            if (_deflectChannel != null)
            {
                _deflectChannel.Raised -= OnDeflect;
            }

            if (_perfectDodgeChannel != null)
            {
                _perfectDodgeChannel.Raised -= OnPerfectDodge;
            }

            if (_phaseChannel != null)
            {
                _phaseChannel.Raised -= OnPhaseChanged;
            }

            if (_focusChannel != null)
            {
                _focusChannel.Raised -= OnFocusChanged;
            }

            if (_weaponSwingCueChannel != null)
            {
                _weaponSwingCueChannel.Raised -= OnWeaponSwingCueChanged;
            }

            if (_overbalanceChannel != null)
            {
                _overbalanceChannel.Raised -= OnOverbalance;
            }
        }

        /// <summary>Sounds the unsteady stagger the instant the boss overbalances, marking the opening.</summary>
        private void OnOverbalance() => _audio?.PlayCue2D(AudioService.Cues.Overbalance);

        /// <summary>Chimes once the instant focus fills, so the player hears the empowered special arm.</summary>
        private void OnFocusChanged(float normalized)
        {
            bool full = normalized >= 0.999f;

            if (full && !_focusWasFull)
            {
                _audio?.PlayCue2D(AudioService.Cues.FocusFull);
            }

            _focusWasFull = full;
        }

        /// <summary>Maps a witnessed occurrence onto a cue.</summary>
        private void OnCombatEvent(CombatEvent combatEvent)
        {
            if (_audio == null)
            {
                return;
            }

            switch (combatEvent.Kind)
            {
                case CombatEventKind.AttackStarted:
                    // Played on the wind-up rather than the strike, so it functions as an audible
                    // telegraph as well as a flourish. A perilous, unblockable wind-up gets an
                    // unmistakable warning sting instead — the audible half of "do not block this".
                    // The player's swing uses whichever weapon they are holding; the boss keeps the
                    // generic one, since it has no weapons to tell apart.
                    _audio.PlayCue(SwingCueFor(combatEvent), combatEvent.Position);
                    break;

                case CombatEventKind.AttackLanded:
                    _audio.PlayCue(ImpactCueFor(combatEvent.DamageType), combatEvent.Position);
                    break;

                case CombatEventKind.Blocked:
                    _audio.PlayCue(AudioService.Cues.Block, combatEvent.Position);
                    break;

                case CombatEventKind.GuardRaised:
                    _audio.PlayCue2D(AudioService.Cues.GuardRaise);
                    break;

                case CombatEventKind.PoiseBroken:
                    _audio.PlayCue(AudioService.Cues.PostureBreak, combatEvent.Position);
                    break;

                case CombatEventKind.DodgePerformed:
                    _audio.PlayCue2D(AudioService.Cues.Dash);
                    break;

                case CombatEventKind.HealStarted:
                    _audio.PlayCue2D(AudioService.Cues.Heal);
                    break;

                case CombatEventKind.WeaponDrawn:
                    _audio.PlayCue2D(AudioService.Cues.WeaponDraw);
                    break;

                case CombatEventKind.Riposte:
                    // The execution's own emphatic beat, on top of the impact it lands.
                    _audio.PlayCue2D(AudioService.Cues.Execution);
                    break;

                // The four occurrences below were published from the start and heard by nobody.
                case CombatEventKind.AttackWhiffed:
                    // Air and no contact. Without it a missed swing was indistinguishable from an
                    // input the game had ignored.
                    _audio.PlayCue(AudioService.Cues.Whiff, combatEvent.Position);
                    break;

                case CombatEventKind.AttackEvaded:
                    // Slipping a blow is a small victory and deserves to be audible as one.
                    _audio.PlayCue(AudioService.Cues.PerfectDodge, combatEvent.Position);
                    break;

                case CombatEventKind.HealCompleted:
                    _audio.PlayCue2D(AudioService.Cues.HealComplete);
                    break;

                case CombatEventKind.Defeated:
                    // The most important moment in the fight, and it used to happen in silence.
                    _audio.PlayCue(
                        combatEvent.Actor == CombatantTeam.Player
                            ? AudioService.Cues.PlayerDeath
                            : AudioService.Cues.BossDeath,
                        combatEvent.Position);
                    break;
            }
        }

        /// <summary>Chooses the sound a wind-up makes.</summary>
        /// <remarks>
        /// Peril outranks weapon identity: an unblockable attack has to read as danger before it
        /// reads as anything else.
        /// </remarks>
        private string SwingCueFor(in CombatEvent combatEvent)
        {
            if (combatEvent.Unblockable)
            {
                return AudioService.Cues.Peril;
            }

            return combatEvent.Actor == CombatantTeam.Player
                ? _playerSwingCue
                : AudioService.Cues.Whoosh;
        }

        /// <summary>Remembers which weapon the player is now holding.</summary>
        private void OnWeaponSwingCueChanged(string cueId)
        {
            if (!string.IsNullOrEmpty(cueId))
            {
                _playerSwingCue = cueId;
            }
        }

        /// <summary>Heavier attacks get a duller, longer impact so weight is audible.</summary>
        private static string ImpactCueFor(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Heavy:
                case DamageType.BossMelee:
                    return AudioService.Cues.HitHeavy;

                default:
                    return AudioService.Cues.HitLight;
            }
        }

        private void OnDeflect() => _audio?.PlayCue2D(AudioService.Cues.Deflect);

        private void OnPerfectDodge() => _audio?.PlayCue2D(AudioService.Cues.PerfectDodge);

        private void OnPhaseChanged(int phaseIndex)
        {
            // The music thickens with every phase, including the drop back to the bed on a reset.
            _audio?.SetMusicIntensity(phaseIndex);

            // The opening phase is not an escalation, so it gets no roar.
            if (phaseIndex > 0)
            {
                _audio?.PlayCue2D(AudioService.Cues.BossRoar);
            }
        }

        /// <summary>Assigns the channels. Used by the scene generator.</summary>
        /// <param name="deflect">Clean deflect channel.</param>
        /// <param name="perfectDodge">Perfect dodge channel.</param>
        /// <param name="phase">Boss phase channel.</param>
        /// <param name="focus">Normalised player focus channel.</param>
        /// <param name="overbalance">Boss overbalance channel.</param>
        public void Bind(
            VoidEventChannel deflect,
            VoidEventChannel perfectDodge,
            IntEventChannel phase,
            FloatEventChannel focus,
            VoidEventChannel overbalance,
            StringEventChannel weaponSwingCue = null)
        {
            _deflectChannel = deflect;
            _perfectDodgeChannel = perfectDodge;
            _phaseChannel = phase;
            _focusChannel = focus;
            _overbalanceChannel = overbalance;
            _weaponSwingCueChannel = weaponSwingCue;
        }
    }
}
