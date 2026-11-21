using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// A red star of light that bursts at Igris's head when he winds up a blow that cannot be blocked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The perilous wind-up already turned its floor telegraph red and sounded a sting, but in a duel the eye is
    /// on the opponent, not the floor, and the sting is lost under the score. The glint is where the player is
    /// already looking, on the one moment the right answer changes from guard to roll.
    /// </para>
    /// <para>
    /// Fair by construction: it is raised from the boss's own wind-up event, a fact about his attack that is on
    /// screen anyway, and nothing about the player is read. Ticks on unscaled time so a hit-stop landing in the
    /// same moment cannot freeze the warning.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PerilousFlare : MonoBehaviour
    {
        /// <summary>Seconds the star takes to burst to full size.</summary>
        public const float GrowSeconds = 0.14f;

        /// <summary>Seconds it then takes to fade.</summary>
        public const float FadeSeconds = 0.42f;

        /// <summary>Width of the star at its brightest, in metres.</summary>
        private const float PeakSize = 1.7f;

        /// <summary>Height above his root the star bursts at when no head bone is found, for his 1.5x frame.</summary>
        private const float FallbackHeadHeight = 2.55f;

        /// <summary>How far in front of his face the star sits, so it is never swallowed by the helm.</summary>
        private const float FaceOffset = 0.25f;

        /// <summary>Peak brightness of the red light thrown on the stone and on the knight.</summary>
        private const float PeakLightIntensity = 5f;

        /// <summary>Reach of that light.</summary>
        private const float LightRange = 7f;

        /// <summary>Degrees per second the star turns as it fades, so it reads as light rather than a sticker.</summary>
        private const float SpinDegreesPerSecond = 90f;

        /// <summary>Texture resolution of the star.</summary>
        private const int StarTextureSize = 64;

        private static readonly Color StarColor = new Color(1f, 0.16f, 0.08f, 1f);

        private Transform _boss;
        private Transform _head;
        private MeshRenderer _star;
        private Light _light;
        private float _elapsed = float.PositiveInfinity;

        /// <summary>Whether the star is on screen now. For tests.</summary>
        public bool IsShowing => _elapsed < GrowSeconds + FadeSeconds;

        /// <summary>Builds the star and its light.</summary>
        /// <param name="glow">An additive glow material, copied so the star can carry its own texture.</param>
        /// <param name="boss">The boss's root.</param>
        public void Construct(Material glow, Transform boss)
        {
            _boss = boss;
            _head = FindHead(boss);

            if (glow != null)
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "PerilousStar";
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(transform, false);

                // Its own copy of the material with the star as the main texture, shape in the colour as well as the
                // alpha: these additive shaders ignore per-renderer textures and alpha both (Stage O).
                _star = quad.GetComponent<MeshRenderer>();
                _star.sharedMaterial = new Material(glow) { name = "PerilousStar", mainTexture = DrawStar(StarTextureSize) };
                _star.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _star.receiveShadows = false;
                _star.enabled = false;
            }

            var lightObject = new GameObject("PerilousLight");
            lightObject.transform.SetParent(transform, false);
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = StarColor;
            _light.range = LightRange;
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
            _light.enabled = false;
        }

        /// <summary>Sets whose head the star bursts at, once the boss is known.</summary>
        /// <param name="boss">The boss's root.</param>
        public void SetBoss(Transform boss)
        {
            _boss = boss;
            _head = FindHead(boss);
        }

        /// <summary>Bursts the star at his head.</summary>
        public void Play() => _elapsed = 0f;

        private void LateUpdate()
        {
            if (!IsShowing)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            float strength = Strength(_elapsed);
            bool visible = strength > 0f;
            Vector3 position = HeadPosition();

            if (_star != null)
            {
                _star.enabled = visible;

                if (visible)
                {
                    PlaceStar(position, strength);
                }
            }

            if (_light != null)
            {
                _light.enabled = visible;
                _light.transform.position = position;
                _light.intensity = PeakLightIntensity * strength;
            }
        }

        /// <summary>Faces the star to the camera, just in front of his head, sized and tinted for the moment.</summary>
        private void PlaceStar(Vector3 head, float strength)
        {
            Camera view = Camera.main;
            Transform star = _star.transform;
            Vector3 toCamera = view != null ? (view.transform.position - head).normalized : Vector3.back;

            star.position = head + toCamera * FaceOffset;
            star.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);
            star.Rotate(0f, 0f, _elapsed * SpinDegreesPerSecond, Space.Self);
            star.localScale = Vector3.one * (PeakSize * Size(_elapsed));
            _star.sharedMaterial.color = StarColor * strength;
        }

        /// <summary>Brightness at a moment of the flare: up quickly, then a longer fall.</summary>
        /// <param name="elapsed">Seconds since it began.</param>
        /// <returns>Zero to one.</returns>
        public static float Strength(float elapsed)
        {
            if (elapsed < 0f || elapsed >= GrowSeconds + FadeSeconds)
            {
                return 0f;
            }

            return elapsed < GrowSeconds
                ? elapsed / GrowSeconds
                : 1f - Mathf.SmoothStep(0f, 1f, (elapsed - GrowSeconds) / FadeSeconds);
        }

        /// <summary>Size at a moment of the flare: bursts out, then shrinks a little as it fades.</summary>
        private static float Size(float elapsed) =>
            elapsed < GrowSeconds
                ? Mathf.Lerp(0.25f, 1f, elapsed / GrowSeconds)
                : Mathf.Lerp(1f, 0.7f, (elapsed - GrowSeconds) / FadeSeconds);

        private Vector3 HeadPosition()
        {
            if (_head != null)
            {
                return _head.position;
            }

            return _boss != null ? _boss.position + Vector3.up * FallbackHeadHeight : transform.position;
        }

        /// <summary>The head bone of the boss's humanoid rig, or null for a body without one.</summary>
        private static Transform FindHead(Transform boss)
        {
            if (boss == null)
            {
                return null;
            }

            foreach (Animator animator in boss.GetComponentsInChildren<Animator>(true))
            {
                Transform head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;

                if (head != null)
                {
                    return head;
                }
            }

            return null;
        }

        /// <summary>A four-pointed star with a hot core.</summary>
        private static Texture2D DrawStar(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x - half) / half);
                    float dy = Mathf.Abs((y - half) / half);
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);

                    // Two thin crossed rays that taper to the edge, over a soft round core.
                    float rays = Mathf.Max(Ray(dx, dy), Ray(dy, dx));
                    float core = Mathf.Pow(Mathf.Clamp01(1f - radius * 2.2f), 2f);
                    byte value = (byte)(Mathf.Clamp01(Mathf.Max(rays, core)) * 255f);
                    pixels[y * size + x] = new Color32(value, value, value, value);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>A ray along one axis: bright on the axis, thinning and fading toward the edge.</summary>
        private static float Ray(float along, float across)
        {
            float width = Mathf.Lerp(0.09f, 0.005f, along);
            return across > width ? 0f : (1f - along) * (1f - across / width);
        }
    }
}
