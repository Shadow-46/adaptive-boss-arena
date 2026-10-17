using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Services;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Shakes the controller in the hands on the blows that matter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The big action games are felt as much as seen: the pad thumps when the knight is struck, buzzes sharp on
    /// a deflect, and rolls long and heavy on a posture break or an execution. Without it a fight on a controller
    /// feels like watching one.
    /// </para>
    /// <para>
    /// Driven from the combat bus, which already reports every one of these beats, and presentation only: nothing
    /// here feeds back into either fighter. Pulses do not stack; the strongest one in flight wins, so a flurry
    /// reads as a flurry rather than a motor pinned at full. Counted on unscaled time so a hit-stop does not hold
    /// the motor on, and silenced while the game is paused or out of focus.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RumbleDirector : MonoBehaviour
    {
        /// <summary>Damage at which a blow taken shakes the pad at its hardest.</summary>
        private const float HeaviestBlowDamage = 25f;

        private ICombatEventBus _events;
        private RumblePulse _pulse;
        private float _remaining;
        private bool _motorsOn;

        private void Start()
        {
            if (ServiceRegistry.Current.TryGet(out _events))
            {
                _events.EventRecorded += OnCombatEvent;
            }
        }

        private void OnDestroy()
        {
            if (_events != null)
            {
                _events.EventRecorded -= OnCombatEvent;
            }

            StopMotors();
        }

        private void OnDisable() => StopMotors();

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                _remaining = 0f;
                StopMotors();
            }
        }

        private void OnCombatEvent(CombatEvent combatEvent)
        {
            if (!TryPulseFor(combatEvent, out RumblePulse pulse))
            {
                return;
            }

            // The strongest pulse in flight wins; a weaker one arriving mid-thump is felt inside it anyway.
            if (_remaining <= 0f || pulse.Strength >= _pulse.Strength * (_remaining / Mathf.Max(0.01f, _pulse.Seconds)))
            {
                _pulse = pulse;
                _remaining = pulse.Seconds;
            }
        }

        private void Update()
        {
            Gamepad pad = Gamepad.current;

            // Paused: the time scale is held at zero, and a pad buzzing over the pause menu is a bug.
            if (pad == null || _remaining <= 0f || Time.timeScale <= 0f || FeedbackSettings.VibrationIntensity <= 0f)
            {
                _remaining = Mathf.Max(0f, _remaining - Time.unscaledDeltaTime);
                StopMotors();
                return;
            }

            // Eases out over the pulse, so a thump has an attack and a tail rather than a square edge.
            float fade = Mathf.Clamp01(_remaining / Mathf.Max(0.01f, _pulse.Seconds));
            float scale = fade * FeedbackSettings.VibrationIntensity;

            pad.SetMotorSpeeds(_pulse.Low * scale, _pulse.High * scale);
            _motorsOn = true;
            _remaining -= Time.unscaledDeltaTime;
        }

        private void StopMotors()
        {
            if (!_motorsOn)
            {
                return;
            }

            Gamepad.current?.SetMotorSpeeds(0f, 0f);
            _motorsOn = false;
        }

        /// <summary>
        /// The pulse a witnessed beat of the fight earns, if any.
        /// </summary>
        /// <remarks>
        /// The low motor is weight, the high motor is edge. A blow taken leans on weight and grows with the
        /// damage; a blow landed is a short tap, sharper for a heavy; a deflect is nearly all edge, the ring of
        /// steel; a posture break, a riposte and a defeat roll long. Pure, so the mapping can be tested.
        /// </remarks>
        /// <param name="combatEvent">The beat.</param>
        /// <param name="pulse">The pulse to play.</param>
        /// <returns>True when the beat is felt at all.</returns>
        public static bool TryPulseFor(in CombatEvent combatEvent, out RumblePulse pulse)
        {
            switch (combatEvent.Kind)
            {
                case CombatEventKind.AttackLanded when combatEvent.Actor != CombatantTeam.Player:
                    float weight = Mathf.Clamp01(combatEvent.Magnitude / HeaviestBlowDamage);
                    pulse = new RumblePulse(
                        Mathf.Lerp(0.35f, 0.85f, weight), Mathf.Lerp(0.2f, 0.5f, weight), Mathf.Lerp(0.16f, 0.34f, weight));
                    return true;

                case CombatEventKind.AttackLanded:
                    pulse = combatEvent.DamageType == DamageType.Heavy
                        ? new RumblePulse(0.3f, 0.45f, 0.13f)
                        : new RumblePulse(0.1f, 0.3f, 0.07f);
                    return true;

                case CombatEventKind.Deflected:
                    pulse = new RumblePulse(0.2f, 0.7f, 0.12f);
                    return true;

                case CombatEventKind.Blocked:
                    pulse = new RumblePulse(0.3f, 0.2f, 0.12f);
                    return true;

                case CombatEventKind.PerfectDodge:
                    pulse = new RumblePulse(0f, 0.35f, 0.1f);
                    return true;

                case CombatEventKind.PoiseBroken:
                    pulse = new RumblePulse(0.65f, 0.6f, 0.38f);
                    return true;

                case CombatEventKind.Riposte:
                    pulse = new RumblePulse(0.85f, 0.55f, 0.45f);
                    return true;

                case CombatEventKind.Defeated:
                    pulse = new RumblePulse(0.9f, 0.4f, 0.7f);
                    return true;

                default:
                    pulse = default;
                    return false;
            }
        }
    }

    /// <summary>One thump of the controller: both motors' strength and how long it lasts.</summary>
    public readonly struct RumblePulse
    {
        /// <summary>Creates a pulse.</summary>
        /// <param name="low">Low-frequency motor, the weight, zero to one.</param>
        /// <param name="high">High-frequency motor, the edge, zero to one.</param>
        /// <param name="seconds">How long it lasts.</param>
        public RumblePulse(float low, float high, float seconds)
        {
            Low = Mathf.Clamp01(low);
            High = Mathf.Clamp01(high);
            Seconds = Mathf.Max(0f, seconds);
        }

        /// <summary>Low-frequency motor strength.</summary>
        public float Low { get; }

        /// <summary>High-frequency motor strength.</summary>
        public float High { get; }

        /// <summary>How long the pulse lasts, in seconds.</summary>
        public float Seconds { get; }

        /// <summary>How hard the pulse feels overall, for choosing between two.</summary>
        public float Strength => Mathf.Max(Low, High);
    }
}
