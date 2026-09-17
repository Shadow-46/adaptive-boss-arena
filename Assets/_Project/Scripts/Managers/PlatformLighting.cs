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

        // Before any Start, so ScreenEffects clones the profile the platform actually renders with.
        private void Awake() => Apply(UsesWebLighting(Application.platform));

        /// <summary>Applies one platform's lighting. Public so both variants can be tested in one editor.</summary>
        /// <param name="web">True for the web build's lighting.</param>
        public void Apply(bool web)
        {
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
            if (_volume != null && _webProfile != null)
            {
                _volume.sharedProfile = _webProfile;
            }

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
    }
}
