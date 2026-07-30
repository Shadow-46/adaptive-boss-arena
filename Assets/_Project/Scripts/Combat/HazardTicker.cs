using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// The lifecycle of a ground hazard: a warning, a period that bites, and a fade.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Split out from <see cref="HazardZone"/> as pure logic so the two things that make a hazard fair
    /// — that it warns before it hurts, and that it only ticks damage on a fixed cadence rather than
    /// every frame — can be pinned by an edit-mode test without a scene. Getting either wrong turns a
    /// "manage your space" hazard into an unreadable frame-rate-dependent trap.
    /// </para>
    /// <para>
    /// The first damage tick lands the instant the active period opens, so a zone that forms under a
    /// standing player bites immediately rather than granting a free interval.
    /// </para>
    /// </remarks>
    public sealed class HazardTicker
    {
        /// <summary>Which part of its life a hazard is in.</summary>
        public enum Stage
        {
            /// <summary>Telegraphed but not yet dangerous.</summary>
            Warning,

            /// <summary>Live and dealing damage on its cadence.</summary>
            Active,

            /// <summary>Dissipating; no longer dangerous.</summary>
            Fading,

            /// <summary>Finished and ready to be recycled.</summary>
            Expired
        }

        private readonly float _warningSeconds;
        private readonly float _activeSeconds;
        private readonly float _fadeSeconds;
        private readonly float _damageIntervalSeconds;

        private float _elapsed;
        private float _sinceDamage;

        /// <summary>Creates a ticker for one hazard instance.</summary>
        /// <param name="warningSeconds">Telegraph before the zone becomes dangerous.</param>
        /// <param name="activeSeconds">How long the zone deals damage.</param>
        /// <param name="fadeSeconds">Harmless dissipation after the active period.</param>
        /// <param name="damageIntervalSeconds">Seconds between damage ticks while active.</param>
        public HazardTicker(
            float warningSeconds, float activeSeconds, float fadeSeconds, float damageIntervalSeconds)
        {
            _warningSeconds = Mathf.Max(0f, warningSeconds);
            _activeSeconds = Mathf.Max(0f, activeSeconds);
            _fadeSeconds = Mathf.Max(0f, fadeSeconds);
            _damageIntervalSeconds = Mathf.Max(0.01f, damageIntervalSeconds);

            // Primed so the first advance inside the active window fires a tick at once.
            _sinceDamage = _damageIntervalSeconds;
        }

        /// <summary>Seconds elapsed since the hazard began.</summary>
        public float Elapsed => _elapsed;

        /// <summary>Total life of the hazard.</summary>
        public float TotalSeconds => _warningSeconds + _activeSeconds + _fadeSeconds;

        /// <summary>The stage the hazard is currently in.</summary>
        public Stage CurrentStage =>
            _elapsed >= TotalSeconds ? Stage.Expired
            : _elapsed < _warningSeconds ? Stage.Warning
            : _elapsed < _warningSeconds + _activeSeconds ? Stage.Active
            : Stage.Fading;

        /// <summary>True once the hazard has finished and may be recycled.</summary>
        public bool IsExpired => CurrentStage == Stage.Expired;

        /// <summary>
        /// A zero-to-one presence value for the visual: rising through the warning, full while active,
        /// falling through the fade.
        /// </summary>
        public float VisualPresence
        {
            get
            {
                switch (CurrentStage)
                {
                    case Stage.Warning:
                        return _warningSeconds <= 0f ? 1f : Mathf.Clamp01(_elapsed / _warningSeconds);
                    case Stage.Active:
                        return 1f;
                    case Stage.Fading:
                        float into = _elapsed - _warningSeconds - _activeSeconds;
                        return _fadeSeconds <= 0f ? 0f : Mathf.Clamp01(1f - into / _fadeSeconds);
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>
        /// Advances the hazard and reports whether a damage tick should fire this step.
        /// </summary>
        /// <param name="deltaTime">Elapsed scaled time, so a hazard freezes during hit-stop.</param>
        /// <returns>True when the caller should apply one tick of damage.</returns>
        public bool Advance(float deltaTime)
        {
            _elapsed += Mathf.Max(0f, deltaTime);

            // Only the active window bites, and the accumulator only runs there, so the warning and
            // fade never bank up ticks that fire the instant the zone goes live or dies.
            if (CurrentStage != Stage.Active)
            {
                return false;
            }

            _sinceDamage += deltaTime;

            if (_sinceDamage >= _damageIntervalSeconds)
            {
                _sinceDamage -= _damageIntervalSeconds;
                return true;
            }

            return false;
        }
    }
}
