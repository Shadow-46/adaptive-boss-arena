using UnityEngine;

namespace AdaptiveBossArena.Core.Services
{
    /// <summary>
    /// Default <see cref="ITimeService"/> implementation, driven by an external per-frame tick.
    /// </summary>
    /// <remarks>
    /// Written as a plain class rather than a <c>MonoBehaviour</c> so its logic can be exercised in
    /// edit-mode tests by feeding synthetic deltas. A thin component in the Game assembly owns the
    /// instance and calls <see cref="Tick"/> from its update loop.
    /// </remarks>
    public sealed class TimeService : ITimeService
    {
        /// <summary>
        /// Time scale used during hit-stop. Kept marginally above zero rather than at zero so
        /// animation and physics systems continue to receive steps and do not visibly re-settle
        /// when the freeze releases.
        /// </summary>
        private const float HitStopTimeScale = 0.0001f;

        private const float NormalTimeScale = 1f;

        private readonly float _baseFixedDeltaTime;

        private float _hitStopRemaining;
        private float _slowMotionRemaining;
        private float _slowMotionScale = NormalTimeScale;
        private bool _isPaused;

        /// <summary>Creates a time service.</summary>
        /// <param name="baseFixedDeltaTime">
        /// The project's unscaled physics step. Captured once so scaling can be reapplied without
        /// drift accumulating across repeated changes.
        /// </param>
        public TimeService(float baseFixedDeltaTime = 1f / 60f) =>
            _baseFixedDeltaTime = baseFixedDeltaTime;

        /// <inheritdoc />
        public float DeltaTime { get; private set; }

        /// <inheritdoc />
        public float UnscaledDeltaTime { get; private set; }

        /// <inheritdoc />
        public float FixedDeltaTime => _baseFixedDeltaTime * TimeScale;

        /// <inheritdoc />
        public float CombatTime { get; private set; }

        /// <inheritdoc />
        public float TimeScale { get; private set; } = NormalTimeScale;

        /// <inheritdoc />
        public bool IsPaused => _isPaused;

        /// <summary>
        /// Advances all time effects and republishes the resulting scale to the engine.
        /// </summary>
        /// <param name="unscaledDeltaTime">Real elapsed time this frame.</param>
        public void Tick(float unscaledDeltaTime)
        {
            UnscaledDeltaTime = unscaledDeltaTime;

            ExpireTimeEffects(unscaledDeltaTime);
            TimeScale = ResolveTimeScale();

            DeltaTime = unscaledDeltaTime * TimeScale;
            CombatTime += DeltaTime;

            ApplyToEngine();
        }

        /// <inheritdoc />
        public void RequestHitStop(float seconds)
        {
            // Take the longer of the two rather than overwriting, so a heavy hit landing mid-freeze
            // extends the impact instead of being cut short by the lighter hit that preceded it.
            _hitStopRemaining = Mathf.Max(_hitStopRemaining, Mathf.Max(0f, seconds));
        }

        /// <inheritdoc />
        public void RequestSlowMotion(float scale, float durationSeconds)
        {
            _slowMotionScale = Mathf.Clamp(scale, 0.01f, NormalTimeScale);
            _slowMotionRemaining = Mathf.Max(_slowMotionRemaining, Mathf.Max(0f, durationSeconds));
        }

        /// <inheritdoc />
        public void ClearTimeEffects()
        {
            _hitStopRemaining = 0f;
            _slowMotionRemaining = 0f;
            _slowMotionScale = NormalTimeScale;

            // Republished immediately rather than waiting for the next tick, because this is also the
            // teardown path. A service destroyed mid-hit-stop would otherwise leave the engine's time
            // scale near zero, carrying a frozen game into the next scene or back into the editor.
            TimeScale = ResolveTimeScale();
            ApplyToEngine();
        }

        /// <inheritdoc />
        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            TimeScale = ResolveTimeScale();
            ApplyToEngine();
        }

        /// <inheritdoc />
        public void ResetCombatClock() => CombatTime = 0f;

        /// <summary>Counts down active time effects using real time, so they are not self-slowing.</summary>
        private void ExpireTimeEffects(float unscaledDeltaTime)
        {
            if (_isPaused || unscaledDeltaTime <= 0f)
            {
                return;
            }

            if (_hitStopRemaining > 0f)
            {
                _hitStopRemaining = Mathf.Max(0f, _hitStopRemaining - unscaledDeltaTime);
            }

            if (_slowMotionRemaining > 0f)
            {
                _slowMotionRemaining = Mathf.Max(0f, _slowMotionRemaining - unscaledDeltaTime);
                if (_slowMotionRemaining <= 0f)
                {
                    _slowMotionScale = NormalTimeScale;
                }
            }
        }

        /// <summary>Resolves the effective scale from all active effects, most authoritative first.</summary>
        private float ResolveTimeScale()
        {
            if (_isPaused)
            {
                return 0f;
            }

            if (_hitStopRemaining > 0f)
            {
                return HitStopTimeScale;
            }

            return _slowMotionRemaining > 0f ? _slowMotionScale : NormalTimeScale;
        }

        /// <summary>Publishes the resolved scale to the engine's global time state.</summary>
        private void ApplyToEngine()
        {
            Time.timeScale = TimeScale;

            // Scaling the physics step alongside the time scale keeps physics resolution consistent
            // during slow-motion instead of letting it become coarse relative to what is on screen.
            Time.fixedDeltaTime = FixedDeltaTime;
        }
    }
}
