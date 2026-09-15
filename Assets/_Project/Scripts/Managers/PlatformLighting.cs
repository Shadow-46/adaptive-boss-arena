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

            if (!web || _webShaftMaterial == null)
            {
                return;
            }

            foreach (Renderer shaft in _shafts)
            {
                if (shaft != null)
                {
                    shaft.sharedMaterial = _webShaftMaterial;
                }
            }
        }
    }
}
