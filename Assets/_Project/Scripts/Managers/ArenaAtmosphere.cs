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
        /// Light and ambient only; the fog is not a separate palette but derived from these through
        /// <see cref="HazeColor"/>. An earlier version drove fog colours of its own and fell to black
        /// beyond the near fighters. A haze made from the room's own light, with a luminance floor, cannot.
        /// </remarks>
        private readonly struct Profile
        {
            public Profile(
                Color light, float lightIntensity,
                Color sky, Color equator, Color ground, float grade)
            {
                Light = light;
                LightIntensity = lightIntensity;
                Sky = sky;
                Equator = equator;
                Ground = ground;
                Grade = grade;
            }

            public Color Light { get; }
            public float LightIntensity { get; }
            public Color Sky { get; }
            public Color Equator { get; }
            public Color Ground { get; }

            /// <summary>How far toward the Last Stand's grade the whole frame is turned, from nothing to all of it.</summary>
            public float Grade { get; }
        }

        /// <summary>Half-life of the mood transition, in seconds. Slow, so it reads as a mood, not a switch.</summary>
        private const float TransitionHalfLife = 0.8f;

        /// <summary>
        /// Distance the haze begins. Beyond the far edge of the fighting floor from anywhere on it, so the
        /// fighters and every telegraph stay perfectly clear and only the ruin fades.
        /// </summary>
        public const float FogStart = 14f;

        /// <summary>Distance at which the haze is complete, roughly the far outer wall.</summary>
        public const float FogEnd = 60f;

        /// <summary>The opening key: cold moonlight through the open crossing.</summary>
        /// <remarks>
        /// It was a warm white, which with a bright ambient lit the nave like an overcast afternoon - an even grey
        /// plaza. Moonlight blue against the braziers' orange is the look the genre's cathedrals are built on.
        /// </remarks>
        public static readonly Color OpeningLight = new Color(0.78f, 0.85f, 1f);

        /// <summary>How strongly the opening key shines.</summary>
        /// <remarks>
        /// Well down from 1.15. Measured in rendered views, a brighter moon flooded the whole floor into one pale
        /// sheet - the largest thing on screen and the flattest - and drowned the braziers' pools at its edge.
        /// The fighters still read against it: the grade's exposure lifts the frame, not this light.
        /// </remarks>
        public const float OpeningLightIntensity = 0.7f;

        /// <summary>The opening ambient from above. Darker and colder than before, so shadow reads as shadow.</summary>
        public static readonly Color OpeningSky = new Color(0.13f, 0.15f, 0.22f);

        /// <summary>The opening ambient from the sides.</summary>
        public static readonly Color OpeningEquator = new Color(0.08f, 0.09f, 0.12f);

        /// <summary>The opening ambient from below.</summary>
        public static readonly Color OpeningGround = new Color(0.035f, 0.035f, 0.05f);

        // Every later phase holds roughly the opening's brightness, so escalation reads as the light turning, not
        // as the room being switched brighter or darker. The warmer keys run at higher intensities because red
        // carries less luminance than moonlight: at the opening's intensity the Last Stand, under its grade,
        // rendered the fighters as silhouettes on black. It is still the brightest phase.
        private static readonly Profile[] Profiles =
        {
            // Phase 0 — measured: cold moonlight, deep shadow.
            new Profile(OpeningLight, OpeningLightIntensity, OpeningSky, OpeningEquator, OpeningGround, 0f),

            // Phase 1 — pressing: warmer, the room closing in a little.
            new Profile(
                new Color(1f, 0.82f, 0.62f), 0.8f,
                new Color(0.22f, 0.15f, 0.14f), new Color(0.14f, 0.09f, 0.085f), new Color(0.07f, 0.045f, 0.045f), 0.3f),

            // Phase 2 — relentless: hot and red.
            new Profile(
                new Color(1f, 0.55f, 0.4f), 1.05f,
                new Color(0.30f, 0.12f, 0.11f), new Color(0.19f, 0.07f, 0.07f), new Color(0.10f, 0.04f, 0.04f), 0.65f),

            // Phase 3 — Last Stand: a violent, blown-out crimson shading toward violet, brighter than
            // the rest so the desperation phase reads as the fight's climax at a glance.
            new Profile(
                new Color(1f, 0.4f, 0.5f), 1.35f,
                new Color(0.34f, 0.09f, 0.17f), new Color(0.22f, 0.06f, 0.12f), new Color(0.12f, 0.03f, 0.065f), 1f)
        };

        [SerializeField]
        [Tooltip("The arena's directional light, recoloured per phase.")]
        private Light _directionalLight;

        [SerializeField]
        [Tooltip("Carries the boss phase index that drives the mood.")]
        private IntEventChannel _phaseChannel;

        [SerializeField]
        [Tooltip("The escalation grade's volume, whose weight rises with the phase. Optional.")]
        private UnityEngine.Rendering.Volume _phaseGrade;

        /// <summary>The light probes as baked, under the opening ambience. Null when the scene has none.</summary>
        private UnityEngine.Rendering.SphericalHarmonicsL2[] _bakedProbes;

        private UnityEngine.Rendering.SphericalHarmonicsL2[] _tintedProbes;
        /// <summary>Starts impossible, so the first frame always writes the probes and applies the ambient floor.</summary>
        private Color _probeGain = Color.clear;

        /// <summary>The room's ambient light as the scene loaded, which the tinted probes never fall below.</summary>
        private UnityEngine.Rendering.SphericalHarmonicsL2 _ambientFloor;

        private int _targetPhase;
        private Color _lightColor;
        private float _lightIntensity;
        private Color _sky;
        private Color _equator;
        private Color _ground;
        private float _grade;

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
            // already sets; asserted here in case a future scene forgets, along with the fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            // Captured before anything is tinted: every later tint is measured from the bake, never compounded.
            LightProbes probes = LightmapSettings.lightProbes;

            if (probes != null && probes.count > 0)
            {
                _bakedProbes = probes.bakedProbes;
                _ambientFloor = RenderSettings.ambientProbe;
                _tintedProbes = new UnityEngine.Rendering.SphericalHarmonicsL2[_bakedProbes.Length];
            }

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
            _grade = Mathf.Lerp(_grade, target.Grade, t);

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

        /// <summary>Assigns the escalation grade's volume. Used by the scene generator.</summary>
        /// <param name="phaseGrade">The volume whose weight rises with the phase.</param>
        public void BindGrade(UnityEngine.Rendering.Volume phaseGrade) => _phaseGrade = phaseGrade;

        private void OnPhaseChanged(int phaseIndex) => _targetPhase = phaseIndex;

        private void SnapTo(Profile profile)
        {
            _lightColor = profile.Light;
            _lightIntensity = profile.LightIntensity;
            _sky = profile.Sky;
            _equator = profile.Equator;
            _ground = profile.Ground;
            _grade = profile.Grade;
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
            RenderSettings.fogColor = HazeColor.Derive(_equator, _lightColor, _lightIntensity);

            if (_phaseGrade != null)
            {
                _phaseGrade.weight = _grade;
            }

            TintBakedLight();
        }

        /// <summary>Carries the current ambience into the probes baked under the opening one.</summary>
        /// <remarks>
        /// Skipped while the gain has not moved, so the probes are only rewritten during a phase transition
        /// rather than every frame of a fight.
        /// </remarks>
        private void TintBakedLight()
        {
            if (_bakedProbes == null || LightmapSettings.lightProbes == null)
            {
                return;
            }

            Color gain = ProbeTint.Gain(_equator, Profiles[0].Equator);

            if (Mathf.Abs(gain.r - _probeGain.r) + Mathf.Abs(gain.g - _probeGain.g) + Mathf.Abs(gain.b - _probeGain.b) < 0.002f)
            {
                return;
            }

            _probeGain = gain;
            ProbeTint.Apply(_bakedProbes, _tintedProbes, gain, _ambientFloor);
            LightmapSettings.lightProbes.bakedProbes = _tintedProbes;
        }
    }
}
