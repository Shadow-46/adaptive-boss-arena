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

        /// <summary>
        /// The smallest fraction of the base physics step slow-motion may shrink it to.
        /// </summary>
        /// <remarks>
        /// Shrinking the step with slow-motion keeps physics as smooth on screen as it is at full
        /// speed, which is why it is scaled at all. Without a floor, a deep slow-motion would ask the
        /// solver for steps small enough that joints and contacts stop resolving sensibly — the kind
        /// of fault a ragdoll shows by exploding on the killing blow's slow-motion.
        /// </remarks>
        private const float MinimumPhysicsStepFraction = 0.25f;

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
        /// <remarks>
        /// <para>
        /// Scaled only by slow-motion, and never by hit-stop or pause. It used to follow the time scale
        /// down in every case, which handed the engine a physics step of about 1.7e-6 s during every
        /// hit-stop and exactly zero while paused. Nothing in the game used Rigidbodies, so nothing
        /// showed it; the ragdolls and debris the overhaul adds would have received thousands of
        /// microscopic steps per freeze.
        /// </para>
        /// <para>
        /// A freeze does not need a smaller step to stay frozen. With the time scale near zero the
        /// engine accumulates almost no simulated time, so at the ordinary step size it simply takes
        /// no physics steps at all — which is exactly what a freeze should look like.
        /// </para>
        /// </remarks>
        public float FixedDeltaTime => _baseFixedDeltaTime * PhysicsStepScale();

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

        /// <summary>How much slow-motion shrinks the physics step; hit-stop and pause never do.</summary>
        private float PhysicsStepScale()
        {
            if (_isPaused || _hitStopRemaining > 0f || _slowMotionRemaining <= 0f)
            {
                return NormalTimeScale;
            }

            return Mathf.Max(MinimumPhysicsStepFraction, _slowMotionScale);
        }

        /// <summary>Publishes the resolved scale to the engine's global time state.</summary>
        private void ApplyToEngine()
        {
            Time.timeScale = TimeScale;

            // See FixedDeltaTime: slow-motion shrinks the step so physics stays smooth on screen;
            // hit-stop and pause leave it alone so the solver is never handed a degenerate step.
            Time.fixedDeltaTime = FixedDeltaTime;
        }
    }
}
