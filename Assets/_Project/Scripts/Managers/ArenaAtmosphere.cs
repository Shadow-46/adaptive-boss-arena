using AdaptiveBossArena.Core.Events;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Shifts the arena's light, ambience and fog as the boss escalates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The arena is one flat disc for the whole fight, so nothing about the space itself tells the
    /// player the encounter is turning against them. This lets the room answer the boss: cool and
    /// even at the start, warmer and closer as it presses, and a hot, hazy red once it is desperate.
    /// It reads as pressure mounting rather than as a number on a bar.
    /// </para>
    /// <para>
    /// Driven off the same boss-phase channel as the roar and the aura, so the whole world turns on
    /// one signal. It only ever reads that channel — it is pure presentation and tells the boss
    /// nothing. Values ease on unscaled time so a phase change during a hit-stop still transitions
    /// smoothly rather than snapping.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ArenaAtmosphere : MonoBehaviour
    {
        /// <summary>Everything about the room's mood for one phase.</summary>
        /// <remarks>
        /// Deliberately light and ambient only. An earlier version also drove depth fog, which on a
        /// small arena of untextured primitives — and under a URP forward path — fell to black beyond
        /// the near fighters rather than reading as haze. The colour shift alone carries the mood, and
        /// carries it without that failure mode.
        /// </remarks>
        private readonly struct Profile
        {
            public Profile(
                Color light, float lightIntensity,
                Color sky, Color equator, Color ground)
            {
                Light = light;
                LightIntensity = lightIntensity;
                Sky = sky;
                Equator = equator;
                Ground = ground;
            }

            public Color Light { get; }
            public float LightIntensity { get; }
            public Color Sky { get; }
            public Color Equator { get; }
            public Color Ground { get; }
        }

        /// <summary>Half-life of the mood transition, in seconds. Slow, so it reads as a mood, not a switch.</summary>
        private const float TransitionHalfLife = 0.8f;

        private static readonly Profile[] Profiles =
        {
            // Phase 0 — measured: cool, even.
            new Profile(
                new Color(1f, 0.96f, 0.9f), 1.15f,
                new Color(0.22f, 0.24f, 0.30f), new Color(0.14f, 0.14f, 0.17f), new Color(0.06f, 0.06f, 0.08f)),

            // Phase 1 — pressing: warmer, the room closing in a little.
            new Profile(
                new Color(1f, 0.82f, 0.62f), 1.25f,
                new Color(0.34f, 0.24f, 0.22f), new Color(0.22f, 0.14f, 0.13f), new Color(0.11f, 0.07f, 0.07f)),

            // Phase 2 — relentless: hot and red.
            new Profile(
                new Color(1f, 0.55f, 0.4f), 1.4f,
                new Color(0.46f, 0.18f, 0.17f), new Color(0.30f, 0.11f, 0.11f), new Color(0.16f, 0.06f, 0.06f)),

            // Phase 3 — Last Stand: a violent, blown-out crimson shading toward violet, brighter than
            // the rest so the desperation phase reads as the fight's climax at a glance.
            new Profile(
                new Color(1f, 0.4f, 0.5f), 1.6f,
                new Color(0.52f, 0.14f, 0.26f), new Color(0.34f, 0.09f, 0.18f), new Color(0.18f, 0.05f, 0.10f))
        };

        [SerializeField]
        [Tooltip("The arena's directional light, recoloured per phase.")]
        private Light _directionalLight;

        [SerializeField]
        [Tooltip("Carries the boss phase index that drives the mood.")]
        private IntEventChannel _phaseChannel;

        private int _targetPhase;
        private Color _lightColor;
        private float _lightIntensity;
        private Color _sky;
        private Color _equator;
        private Color _ground;

        private void OnEnable()
        {
            if (_phaseChannel != null)
            {
                _phaseChannel.Raised += OnPhaseChanged;
            }
        }

        private void OnDisable()
        {
            if (_phaseChannel != null)
            {
                _phaseChannel.Raised -= OnPhaseChanged;
            }
        }

        private void Start()
        {
            // Ambient must be Trilight for the three-band colours to apply, which the scene builder
            // already sets; asserted here in case a future scene forgets. Fog is deliberately left
            // off — see the note on Profile.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            // Snap to the opening mood so the fight does not fade in from whatever the editor left set.
            _targetPhase = 0;
            SnapTo(Profiles[0]);
            Apply();
        }

        private void Update()
        {
            Profile target = Profiles[Mathf.Clamp(_targetPhase, 0, Profiles.Length - 1)];
            float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.0001f, TransitionHalfLife));

            _lightColor = Color.Lerp(_lightColor, target.Light, t);
            _lightIntensity = Mathf.Lerp(_lightIntensity, target.LightIntensity, t);
            _sky = Color.Lerp(_sky, target.Sky, t);
            _equator = Color.Lerp(_equator, target.Equator, t);
            _ground = Color.Lerp(_ground, target.Ground, t);

            Apply();
        }

        /// <summary>Assigns the references. Used by the scene generator.</summary>
        /// <param name="directionalLight">The arena's directional light.</param>
        /// <param name="phaseChannel">Boss phase index channel.</param>
        public void Bind(Light directionalLight, IntEventChannel phaseChannel)
        {
            _directionalLight = directionalLight;
            _phaseChannel = phaseChannel;
        }

        private void OnPhaseChanged(int phaseIndex) => _targetPhase = phaseIndex;

        private void SnapTo(Profile profile)
        {
            _lightColor = profile.Light;
            _lightIntensity = profile.LightIntensity;
            _sky = profile.Sky;
            _equator = profile.Equator;
            _ground = profile.Ground;
        }

        private void Apply()
        {
            if (_directionalLight != null)
            {
                _directionalLight.color = _lightColor;
                _directionalLight.intensity = _lightIntensity;
            }

            RenderSettings.ambientSkyColor = _sky;
            RenderSettings.ambientEquatorColor = _equator;
            RenderSettings.ambientGroundColor = _ground;
        }
    }
}
