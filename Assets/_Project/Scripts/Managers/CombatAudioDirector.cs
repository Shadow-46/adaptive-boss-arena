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
        }

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
                    // telegraph as well as a flourish.
                    _audio.PlayCue(AudioService.Cues.Whoosh, combatEvent.Position);
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
        public void Bind(
            VoidEventChannel deflect,
            VoidEventChannel perfectDodge,
            IntEventChannel phase,
            FloatEventChannel focus)
        {
            _deflectChannel = deflect;
            _perfectDodgeChannel = perfectDodge;
            _phaseChannel = phase;
            _focusChannel = focus;
        }
    }
}
