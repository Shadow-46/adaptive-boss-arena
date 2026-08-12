using System;
using AdaptiveBossArena.Utilities.Collections;
using UnityEngine;

namespace AdaptiveBossArena.Core.Perception
{
    /// <summary>
    /// Records a rolling history of player observations and serves them back on a delay.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sampling is driven from outside, by the composition root, rather than pulled by the AI. That
    /// inversion is deliberate: the boss cannot ask for a fresher reading than it is given, because
    /// it never holds a reference to the player in the first place.
    /// </para>
    /// <para>
    /// History is kept in a fixed-capacity ring buffer, so a fight of any length costs the same
    /// memory and the same per-frame work.
    /// </para>
    /// </remarks>
    public sealed class DelayedPerceptionSource : IPerceptionSource
    {
        /// <summary>Roughly four seconds of history at a sixty hertz sampling rate.</summary>
        public const int DefaultHistoryCapacity = 256;

        /// <summary>
        /// Sampling rate the capacity is guaranteed to cover, in samples per second.
        /// </summary>
        /// <remarks>
        /// The buffer is counted in samples but queried in seconds, so a high enough frame rate can
        /// leave it spanning less wall-clock time than the perception latency. When that happens the
        /// boss can never see far enough back, <c>TryGetPerceived</c> fails on every call, and it
        /// stands still for the whole fight — silently, because going blind is the safe failure here
        /// and nothing treats it as an error. Sizing the capacity against this rate keeps the window
        /// wide enough for any plausible frame rate; the fixed cost is a few kilobytes.
        /// </remarks>
        private const float GuaranteedSamplesPerSecond = 2000f;

        /// <summary>Extra history beyond the latency itself, so the lookup is never at the boundary.</summary>
        private const float HistoryMarginSeconds = 0.5f;

        private readonly CircularBuffer<PlayerObservation> _history;
        private readonly IObservablePlayer _player;

        private float _latencySeconds;
        private float _currentTime;

        /// <summary>Creates a perception source that watches the supplied player.</summary>
        /// <param name="player">The observable player to sample.</param>
        /// <param name="latencySeconds">How far behind the present perceived data should lag.</param>
        /// <param name="historyCapacity">
        /// Number of samples retained. Leave at zero to size the buffer from the latency, which is
        /// what production should do; pass a value only to pin the capacity, as the bounding test
        /// does.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when no player is supplied.</exception>
        public DelayedPerceptionSource(
            IObservablePlayer player,
            float latencySeconds,
            int historyCapacity = 0)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _latencySeconds = Mathf.Max(0f, latencySeconds);
            _history = new CircularBuffer<PlayerObservation>(
                historyCapacity > 0 ? historyCapacity : CapacityFor(_latencySeconds));
        }

        /// <summary>
        /// Samples needed to cover a latency at the guaranteed sampling rate.
        /// </summary>
        /// <param name="latencySeconds">How far back the boss must be able to look.</param>
        /// <returns>A capacity that spans the latency plus a margin.</returns>
        public static int CapacityFor(float latencySeconds) =>
            Mathf.CeilToInt(
                (Mathf.Max(0f, latencySeconds) + HistoryMarginSeconds) * GuaranteedSamplesPerSecond);

        /// <inheritdoc />
        public float LatencySeconds => _latencySeconds;

        /// <summary>Number of samples currently retained.</summary>
        public int SampleCount => _history.Count;

        /// <summary>
        /// Captures one observation. Call once per frame from the composition root.
        /// </summary>
        /// <param name="timestamp">Current combat-clock time, which must increase monotonically.</param>
        public void Sample(float timestamp)
        {
            _currentTime = timestamp;
            _history.Add(_player.CaptureObservation(timestamp));
        }

        /// <inheritdoc />
        public bool TryGetPerceived(out PlayerObservation observation) =>
            TryGetPerceivedAt(0f, out observation);

        /// <inheritdoc />
        public bool TryGetPerceivedAt(float secondsFurtherBack, out PlayerObservation observation)
        {
            observation = default;

            if (_history.Count == 0)
            {
                return false;
            }

            float targetTime = _currentTime - _latencySeconds - Mathf.Max(0f, secondsFurtherBack);

            // History does not reach back far enough yet, which happens in the opening moments of a
            // fight. The boss simply has nothing to act on, exactly as it should.
            if (_history.Oldest.Timestamp > targetTime)
            {
                return false;
            }

            observation = _history[FindNewestIndexAtOrBefore(targetTime)];
            return observation.IsValid;
        }

        /// <summary>Adjusts perception latency, used when a later boss phase sharpens its senses.</summary>
        /// <param name="latencySeconds">New latency. Negative values are clamped to zero.</param>
        public void SetLatency(float latencySeconds) => _latencySeconds = Mathf.Max(0f, latencySeconds);

        /// <summary>Discards all recorded history, for example when a fight restarts.</summary>
        public void Reset()
        {
            _history.Clear();
            _currentTime = 0f;
        }

        /// <summary>
        /// Binary searches the chronologically ordered history for the newest sample at or before a time.
        /// </summary>
        /// <param name="targetTime">Upper bound on the sample's timestamp.</param>
        /// <returns>Index into the history buffer. Callers must have verified history reaches this far back.</returns>
        private int FindNewestIndexAtOrBefore(float targetTime)
        {
            int low = 0;
            int high = _history.Count - 1;
            int best = 0;

            while (low <= high)
            {
                int mid = low + (high - low) / 2;

                if (_history[mid].Timestamp <= targetTime)
                {
                    best = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return best;
        }
    }
}
