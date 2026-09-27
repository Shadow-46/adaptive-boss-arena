using AdaptiveBossArena.Core.Services;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// The lighting choices that differ between the WebGL build and the desktop build.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two, both decided once when the scene starts. The desktop build gives the sun a cookie - the shadow
    /// of the broken roof's beams and fallen slabs lying across the floor - which the web build skips as
    /// one more sampled texture on every lit pixel of a budget it cannot spare.
    /// </para>
    /// <para>
    /// The web build draws its sun shafts fainter. The same additive planes read noticeably brighter in
    /// the browser than on desktop, where anti-aliasing and ambient occlusion soften everything around
    /// them; a shaft that is atmosphere on one reads as a bright stripe on the other. Swapping to a second
    /// material rather than editing the shared one keeps the asset untouched and the batching intact.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlatformLighting : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The arena's directional light, which takes the cookie on desktop.")]
        private Light _sun;

        [SerializeField]
        [Tooltip("The broken roof's shadow pattern, projected by the sun on desktop.")]
        private Texture2D _sunCookie;

        [SerializeField]
        [Min(1f)]
        [Tooltip("Metres of floor one repeat of the cookie covers.")]
        private float _cookieSize = 26f;

        [SerializeField]
        [Tooltip("The sun shafts, whose material is swapped on the web.")]
        private Renderer[] _shafts = new Renderer[0];

        [SerializeField]
        [Tooltip("The fainter shaft material the web build uses.")]
        private Material _webShaftMaterial;

        [SerializeField]
        [Tooltip("The global post-processing volume, which takes the cheaper profile on the web.")]
        private UnityEngine.Rendering.Volume _volume;

        [SerializeField]
        [Tooltip("The browser's post-processing profile: the same grade without grain or aberration.")]
        private UnityEngine.Rendering.VolumeProfile _webProfile;

        [SerializeField]
        [Tooltip("The camera, whose anti-aliasing is the cheap kind on the web.")]
        private UniversalAdditionalCameraData _camera;

        [SerializeField]
        [Tooltip("The drifting dust, thinned on the web.")]
        private ParticleSystem _dust;

        [SerializeField]
        [Tooltip("The candle lights, switched off on the web; their flames still glow.")]
        private Light[] _candles = new Light[0];

        /// <summary>How many sun shafts the web build keeps; the rest are pure overdraw it cannot afford.</summary>
        public const int WebShaftCount = 3;

        /// <summary>Most dust motes drifting at once on the web, down from the desktop's 260.</summary>
        public const int WebDustParticles = 80;

        /// <summary>Whether a platform takes the lighter web lighting.</summary>
        /// <param name="platform">The platform running.</param>
        /// <returns>True for the WebGL player.</returns>
        public static bool UsesWebLighting(RuntimePlatform platform) => platform == RuntimePlatform.WebGLPlayer;

        /// <summary>Assigns the shafts and their web material. Used by the scene generator.</summary>
        /// <param name="shafts">The shaft renderers.</param>
        /// <param name="webShaftMaterial">The fainter material for the web.</param>
        public void BindShafts(Renderer[] shafts, Material webShaftMaterial)
        {
            _shafts = shafts ?? new Renderer[0];
            _webShaftMaterial = webShaftMaterial;
        }

        /// <summary>Assigns the sun and its cookie. Used by the scene generator.</summary>
        /// <param name="sun">The directional light.</param>
        /// <param name="cookie">The roof shadow pattern.</param>
        public void BindSun(Light sun, Texture2D cookie)
        {
            _sun = sun;
            _sunCookie = cookie;
        }

        /// <summary>Assigns the post-processing and camera the web build cheapens. Used by the scene generator.</summary>
        /// <param name="volume">The global volume.</param>
        /// <param name="webProfile">The browser's profile.</param>
        /// <param name="camera">The main camera's URP data.</param>
        public void BindWebBudget(
            UnityEngine.Rendering.Volume volume, UnityEngine.Rendering.VolumeProfile webProfile, UniversalAdditionalCameraData camera)
        {
            _volume = volume;
            _webProfile = webProfile;
            _camera = camera;
        }

        /// <summary>Assigns the scene dressing the web build thins. Used by the scene generator.</summary>
        /// <param name="dust">The drifting dust.</param>
        /// <param name="candles">The candle lights.</param>
        public void BindSceneBudget(ParticleSystem dust, Light[] candles)
        {
            _dust = dust;
            _candles = candles ?? new Light[0];
        }

        /// <summary>Whether the volume renders with the browser's cheaper profile. Exposed for tests.</summary>
        public bool RendersWebProfile => _volume != null && _webProfile != null && _volume.sharedProfile == _webProfile;

        // Before any Start, so ScreenEffects clones the profile the quality actually renders with.
        private void Awake()
        {
            RememberFullScene();
            Apply(GraphicsTier.Current);
            GraphicsTier.Changed += OnQualityChanged;
        }

        private void OnDestroy() => GraphicsTier.Changed -= OnQualityChanged;

        private void OnQualityChanged(GraphicsQuality quality)
        {
            Apply(quality);

            // The screen effects write into the volume's own copy of the profile; the swap just replaced it.
            FindAnyObjectByType<ScreenEffects>()?.RebindProfile();
        }

        /// <summary>The scene as built, so a step down in quality can be stepped back up.</summary>
        private Material[] _fullShaftMaterials;
        private UnityEngine.Rendering.VolumeProfile _fullProfile;
        private AntialiasingMode _fullAntialiasing;
        private int _fullDustParticles;
        private ParticleSystem.MinMaxCurve _fullDustRate;
        private bool _remembered;

        private void RememberFullScene()
        {
            if (_remembered)
            {
                return;
            }

            _remembered = true;
            _fullShaftMaterials = new Material[_shafts.Length];

            for (int i = 0; i < _shafts.Length; i++)
            {
                _fullShaftMaterials[i] = _shafts[i] != null ? _shafts[i].sharedMaterial : null;
            }

            _fullProfile = _volume != null ? _volume.sharedProfile : null;
            _fullAntialiasing = _camera != null ? _camera.antialiasing : AntialiasingMode.None;

            if (_dust != null)
            {
                _fullDustParticles = _dust.main.maxParticles;
                _fullDustRate = _dust.emission.rateOverTime;
            }
        }

        /// <summary>Applies one platform's lighting. Kept for the tests that compare the two.</summary>
        /// <param name="web">True for the browser's cheapest lighting, false for the full scene.</param>
        public void Apply(bool web) => Apply(web ? GraphicsQuality.Low : GraphicsQuality.High);

        /// <summary>
        /// Applies a graphics quality's lighting: the Low budget, or the full scene restored.
        /// </summary>
        /// <param name="quality">The quality to light the arena for.</param>
        public void Apply(GraphicsQuality quality)
        {
            RememberFullScene();
            bool web = quality == GraphicsQuality.Low;

            if (_sun != null)
            {
                _sun.cookie = web ? null : _sunCookie;

                // URP sizes a directional cookie through its own light data, not the built-in property.
                if (_sun.TryGetComponent(out UniversalAdditionalLightData data))
                {
                    data.lightCookieSize = new Vector2(_cookieSize, _cookieSize);
                }
            }

            if (!web)
            {
                RestoreFullScene();
                return;
            }

            for (int i = 0; i < _shafts.Length; i++)
            {
                if (_shafts[i] == null)
                {
                    continue;
                }

                if (_webShaftMaterial != null)
                {
                    _shafts[i].sharedMaterial = _webShaftMaterial;
                }

                // Each shaft is a tall additive plane covering much of the screen; three still read as sunlight.
                _shafts[i].enabled = i < WebShaftCount;
            }

            ApplyWebBudget();
        }

        /// <summary>
        /// Cheapens what the browser renders on a laptop's integrated graphics.
        /// </summary>
        /// <remarks>
        /// Each cut was chosen to cost the least of the look: the grade survives whole in the cheaper profile;
        /// fast approximate anti-aliasing is one pass where the subpixel kind is three; candle flames keep glowing
        /// through bloom with only their per-pixel light removed; the dust thins rather than disappears.
        /// </remarks>
        private void ApplyWebBudget()
        {
            SwapProfile(_webProfile);

            if (_camera != null)
            {
                _camera.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            }

            if (_dust != null)
            {
                ParticleSystem.MainModule main = _dust.main;
                main.maxParticles = WebDustParticles;

                ParticleSystem.EmissionModule emission = _dust.emission;
                emission.rateOverTime = 6f;
            }

            foreach (Light candle in _candles)
            {
                if (candle != null)
                {
                    candle.enabled = false;
                }
            }
        }

        /// <summary>Puts back everything the Low budget took away.</summary>
        private void RestoreFullScene()
        {
            for (int i = 0; i < _shafts.Length; i++)
            {
                if (_shafts[i] == null)
                {
                    continue;
                }

                if (_fullShaftMaterials != null && i < _fullShaftMaterials.Length && _fullShaftMaterials[i] != null)
                {
                    _shafts[i].sharedMaterial = _fullShaftMaterials[i];
                }

                _shafts[i].enabled = true;
            }

            SwapProfile(_fullProfile);

            if (_camera != null)
            {
                _camera.antialiasing = _fullAntialiasing;
            }

            if (_dust != null && _fullDustParticles > 0)
            {
                ParticleSystem.MainModule main = _dust.main;
                main.maxParticles = _fullDustParticles;

                ParticleSystem.EmissionModule emission = _dust.emission;
                emission.rateOverTime = _fullDustRate;
            }

            foreach (Light candle in _candles)
            {
                if (candle != null)
                {
                    candle.enabled = true;
                }
            }
        }

        /// <summary>
        /// Renders the volume with another profile.
        /// </summary>
        /// <remarks>
        /// Once anything has read the volume's profile, the volume renders from its own private copy and the shared
        /// profile is ignored - so a swap made after the fight began used to change nothing on screen. Clearing the
        /// copy makes the next read clone the new profile.
        /// </remarks>
        private void SwapProfile(UnityEngine.Rendering.VolumeProfile profile)
        {
            if (_volume == null || profile == null || _volume.sharedProfile == profile)
            {
                return;
            }

            _volume.sharedProfile = profile;
            _volume.profile = null;
        }
    }
}
