using System;

namespace AdaptiveBossArena.Utilities.Statistics
{
    /// <summary>
    /// Collects frame times and reports the percentiles a performance budget is written in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Percentiles rather than an average, because an average hides exactly what a player feels. A
    /// fight that runs at 4 ms for most frames and 40 ms whenever a slam lands averages to something
    /// that looks fine, and plays like a stutter. The 95th percentile is the number the budget is set
    /// against; the median says what an ordinary frame costs.
    /// </para>
    /// <para>
    /// Samples are kept in a fixed array allocated once, so measuring the game does not itself add
    /// garbage-collection spikes to the frames being measured.
    /// </para>
    /// </remarks>
    public sealed class FrameTimeSampler
    {
        private readonly float[] _samples;
        private float[] _sortBuffer;
        private int _count;

        /// <summary>Creates a sampler that holds up to a fixed number of frames.</summary>
        /// <param name="capacity">
        /// Maximum samples kept. Frames past this are dropped rather than overwriting old ones, so a
        /// capture always describes the start of its window consistently.
        /// </param>
        public FrameTimeSampler(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
            }

            _samples = new float[capacity];
        }

        /// <summary>Number of samples collected.</summary>
        public int Count => _count;

        /// <summary>Whether the sampler has no room for more samples.</summary>
        public bool IsFull => _count >= _samples.Length;

        /// <summary>Records one frame's duration.</summary>
        /// <param name="seconds">How long the frame took, in seconds. Non-positive values are ignored.</param>
        public void Add(float seconds)
        {
            if (seconds <= 0f || IsFull)
            {
                return;
            }

            _samples[_count++] = seconds;
        }

        /// <summary>Discards every sample.</summary>
        public void Clear() => _count = 0;

        /// <summary>
        /// The frame time below which the given fraction of samples fall, in milliseconds.
        /// </summary>
        /// <remarks>
        /// Nearest-rank, which always returns a frame time that actually occurred. Interpolating
        /// between ranks would report a p95 that no frame ever took.
        /// </remarks>
        /// <param name="fraction">Between zero and one; 0.95 for the 95th percentile.</param>
        /// <returns>The percentile in milliseconds, or zero when nothing has been sampled.</returns>
        public float PercentileMilliseconds(float fraction)
        {
            if (_count == 0)
            {
                return 0f;
            }

            if (_sortBuffer == null || _sortBuffer.Length < _samples.Length)
            {
                _sortBuffer = new float[_samples.Length];
            }

            Array.Copy(_samples, _sortBuffer, _count);
            Array.Sort(_sortBuffer, 0, _count);

            float clamped = Math.Max(0f, Math.Min(1f, fraction));
            int rank = (int)Math.Ceiling(clamped * _count) - 1;
            rank = Math.Max(0, Math.Min(_count - 1, rank));

            return _sortBuffer[rank] * 1000f;
        }

        /// <summary>The longest frame recorded, in milliseconds.</summary>
        public float MaxMilliseconds => PercentileMilliseconds(1f);
    }
}
