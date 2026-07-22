using UnityEngine;

namespace AdaptiveBossArena.Utilities.Feel
{
    /// <summary>
    /// Trauma-driven procedural camera shake.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Callers add trauma; the shake magnitude is trauma raised to an exponent. Squaring makes small
    /// impacts nearly imperceptible while heavy ones hit hard, which reads far better than shake
    /// scaled linearly with damage.
    /// </para>
    /// <para>
    /// Offsets come from Perlin noise sampled on separate rows per axis rather than from random
    /// numbers. Noise is continuous, so the camera sweeps rather than jitters, and it stays smooth
    /// no matter the frame rate.
    /// </para>
    /// </remarks>
    public sealed class TraumaShake
    {
        /// <summary>Exponent applied to trauma. Two makes weak shakes fall away sharply.</summary>
        private const float TraumaExponent = 2f;

        /// <summary>Arbitrary distinct noise rows so the three axes never move in lockstep.</summary>
        private const float NoiseRowPositionX = 0f;
        private const float NoiseRowPositionY = 137f;
        private const float NoiseRowRoll = 311f;

        private readonly float _decayPerSecond;
        private readonly float _frequency;

        private float _trauma;
        private float _noiseTime;

        /// <summary>Creates a shake source.</summary>
        /// <param name="decayPerSecond">Trauma removed each second. Higher values make shakes shorter.</param>
        /// <param name="frequency">Noise sampling rate. Higher values feel sharper and more violent.</param>
        public TraumaShake(float decayPerSecond = 1.6f, float frequency = 22f)
        {
            _decayPerSecond = Mathf.Max(0f, decayPerSecond);
            _frequency = Mathf.Max(0f, frequency);
        }

        /// <summary>Current trauma level, from zero to one.</summary>
        public float Trauma => _trauma;

        /// <summary>True while the camera is still being displaced.</summary>
        public bool IsShaking => _trauma > 0f;

        /// <summary>
        /// Adds trauma, saturating at one.
        /// </summary>
        /// <remarks>
        /// Additive accumulation is intentional: several hits landing together produce one larger
        /// shake instead of restarting a small one, and the clamp stops a flurry from tearing the
        /// camera off the arena.
        /// </remarks>
        /// <param name="amount">Trauma to add, typically between 0.1 for a light hit and 0.6 for a phase transition.</param>
        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + Mathf.Max(0f, amount));

        /// <summary>Advances trauma decay and the noise cursor.</summary>
        /// <param name="deltaTimeSeconds">
        /// Elapsed time. Pass unscaled time so shake continues to read correctly during hit-stop.
        /// </param>
        public void Tick(float deltaTimeSeconds)
        {
            if (deltaTimeSeconds <= 0f)
            {
                return;
            }

            _noiseTime += deltaTimeSeconds * _frequency;
            _trauma = Mathf.Max(0f, _trauma - _decayPerSecond * deltaTimeSeconds);
        }

        /// <summary>
        /// Samples the current displacement.
        /// </summary>
        /// <param name="maxTranslation">Peak positional offset in world units at full trauma.</param>
        /// <param name="maxRollDegrees">Peak camera roll in degrees at full trauma.</param>
        /// <param name="translation">Receives the positional offset to add to the camera.</param>
        /// <param name="rollDegrees">Receives the roll to add to the camera.</param>
        public void Sample(float maxTranslation, float maxRollDegrees, out Vector2 translation, out float rollDegrees)
        {
            if (_trauma <= 0f)
            {
                translation = Vector2.zero;
                rollDegrees = 0f;
                return;
            }

            float magnitude = Mathf.Pow(_trauma, TraumaExponent);

            translation = new Vector2(
                SignedNoise(NoiseRowPositionX) * maxTranslation * magnitude,
                SignedNoise(NoiseRowPositionY) * maxTranslation * magnitude);

            rollDegrees = SignedNoise(NoiseRowRoll) * maxRollDegrees * magnitude;
        }

        /// <summary>Immediately stops the shake.</summary>
        public void Reset()
        {
            _trauma = 0f;
        }

        /// <summary>Samples Perlin noise on the given row and remaps it to the range minus one to one.</summary>
        private float SignedNoise(float row) => Mathf.PerlinNoise(row, _noiseTime) * 2f - 1f;
    }
}
