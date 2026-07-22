using UnityEngine;

namespace AdaptiveBossArena.Utilities.Timing
{
    /// <summary>
    /// Explicitly ticked countdown timer with no dependency on engine time.
    /// </summary>
    /// <remarks>
    /// Callers supply their own delta, which is what lets attack windows, cooldowns and adaptation
    /// intervals honour hit-stop and slow-motion (they are ticked with scaled time) while UI and
    /// pause logic ignore it (ticked with unscaled time). It also makes every timing-dependent
    /// system deterministically testable outside play mode.
    /// </remarks>
    public sealed class CountdownTimer
    {
        private float _duration;
        private float _remaining;
        private bool _isRunning;

        /// <summary>Creates a stopped timer.</summary>
        public CountdownTimer()
        {
        }

        /// <summary>Creates a timer and immediately starts it.</summary>
        /// <param name="duration">Countdown length in seconds.</param>
        public CountdownTimer(float duration) => Start(duration);

        /// <summary>True while the timer is counting down and has not yet elapsed.</summary>
        public bool IsRunning => _isRunning;

        /// <summary>Seconds left before the timer elapses.</summary>
        public float Remaining => _remaining;

        /// <summary>Total length the timer was started with.</summary>
        public float Duration => _duration;

        /// <summary>Fraction of the countdown already consumed, from zero at start to one at completion.</summary>
        public float Progress01 => _duration <= 0f ? 1f : Mathf.Clamp01(1f - _remaining / _duration);

        /// <summary>Raised once at the moment the countdown reaches zero.</summary>
        public event System.Action Completed;

        /// <summary>Starts or restarts the countdown.</summary>
        /// <param name="duration">Countdown length in seconds. Non-positive durations complete on the next tick.</param>
        public void Start(float duration)
        {
            _duration = Mathf.Max(0f, duration);
            _remaining = _duration;
            _isRunning = true;

            if (_duration <= 0f)
            {
                Complete();
            }
        }

        /// <summary>Advances the countdown.</summary>
        /// <param name="deltaTimeSeconds">Elapsed time. Non-positive values are ignored.</param>
        /// <returns>True on the single tick where the countdown completes.</returns>
        public bool Tick(float deltaTimeSeconds)
        {
            if (!_isRunning || deltaTimeSeconds <= 0f)
            {
                return false;
            }

            _remaining -= deltaTimeSeconds;
            if (_remaining > 0f)
            {
                return false;
            }

            Complete();
            return true;
        }

        /// <summary>Stops the countdown without raising <see cref="Completed"/>.</summary>
        public void Cancel()
        {
            _isRunning = false;
            _remaining = 0f;
        }

        private void Complete()
        {
            _remaining = 0f;
            _isRunning = false;
            Completed?.Invoke();
        }
    }
}
