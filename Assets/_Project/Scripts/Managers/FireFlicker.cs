using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Makes a fire breathe: its light wavers and its flame stretches and settles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A fire held perfectly steady reads as a lamp. The waver is two octaves of Perlin noise rather than a
    /// random draw, so it is smooth, costs nothing to reproduce, and never touches the injected random
    /// provider the combat depends on for its determinism. Each fire has its own seed, so the braziers round
    /// the nave never pulse together.
    /// </para>
    /// <para>
    /// Runs on unscaled time, like the dust: the air and the fire keep moving through a hit-stop, which is
    /// what makes the frozen fighters read as frozen rather than the whole picture as paused.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FireFlicker : MonoBehaviour
    {
        /// <summary>How fast the slow octave wanders, in noise units a second.</summary>
        private const float Speed = 2.2f;

        /// <summary>How much faster the fine octave runs than the slow one.</summary>
        private const float FineOctave = 2.7f;

        /// <summary>The fine octave's share of the waver. Small, so it flutters on top of a slow breath.</summary>
        private const float FineShare = 0.3f;

        /// <summary>Largest swing of the light either side of its set intensity, as a fraction.</summary>
        private const float LightDepth = 0.22f;

        /// <summary>Largest stretch of the flame either side of its set height, as a fraction.</summary>
        private const float FlameDepth = 0.14f;

        [SerializeField]
        [Tooltip("The fire's light, whose intensity wavers. Optional.")]
        private Light _light;

        [SerializeField]
        [Tooltip("Where this fire's noise starts, so no two fires pulse together.")]
        private float _seed;

        private float _baseIntensity;
        private Vector3 _baseScale;

        /// <summary>Assigns the light and seed. Used by the scene generator.</summary>
        /// <param name="fireLight">The fire's light, or null for a flame alone.</param>
        /// <param name="seed">Where this fire's noise starts.</param>
        public void Bind(Light fireLight, float seed)
        {
            _light = fireLight;
            _seed = seed;
        }

        private void Awake()
        {
            _baseIntensity = _light != null ? _light.intensity : 0f;
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            float t = Time.unscaledTime * Speed + _seed;
            float slow = Mathf.PerlinNoise(t, _seed) * 2f - 1f;
            float fine = Mathf.PerlinNoise(t * FineOctave, _seed + 17f) * 2f - 1f;
            float waver = Mathf.Lerp(slow, fine, FineShare);

            if (_light != null)
            {
                _light.intensity = _baseIntensity * (1f + waver * LightDepth);
            }

            // Taller as it flares, a touch narrower, the way a flame draws up.
            float stretch = 1f + waver * FlameDepth;
            transform.localScale = new Vector3(
                _baseScale.x / Mathf.Sqrt(stretch), _baseScale.y * stretch, _baseScale.z / Mathf.Sqrt(stretch));
        }
    }
}
