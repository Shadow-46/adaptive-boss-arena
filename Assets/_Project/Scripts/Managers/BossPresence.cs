using AdaptiveBossArena.AI;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Igris's presence as the fight turns against him: eyes that burn, a light that cuts him out of the dark, and
    /// the floor scorched where he stands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The player once said the boss looked ordinary, and asked for presence through effects rather than a new
    /// body. A boss in the big action games grows more terrible as the fight goes on; this is how Igris does: from
    /// the second phase his eyes kindle, a rim of light behind him draws his outline against the arena, and in the
    /// last two the stone chars and glows in a ring around his feet.
    /// </para>
    /// <para>
    /// Presentation only - nothing here changes his reach or what he can strike, which is why he does not grow:
    /// his blade's strike volumes ride on his bones, and a larger body would reach further. Built at runtime from
    /// the two materials the scene already ships, with textures drawn in code, so it adds no assets and no shaders.
    /// The rim light is left off on the lowest graphics tier.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BossPresence : MonoBehaviour
    {
        /// <summary>How wide the scorch spreads around his feet in each phase, in metres of radius.</summary>
        private static readonly float[] ScorchRadiusByPhase = { 0f, 0f, 2.2f, 3f };

        /// <summary>How brightly the scorch's edge glows in each phase.</summary>
        /// <remarks>Halved after the first capture, where under bloom the ring's arcs outshone Igris himself.</remarks>
        private static readonly float[] EmberByPhase = { 0f, 0f, 0.7f, 1.1f };

        /// <summary>How brightly his eyes burn in each phase.</summary>
        private static readonly float[] EyesByPhase = { 0f, 1.2f, 2f, 3f };

        /// <summary>How strong the rim light behind him is in each phase.</summary>
        private static readonly float[] RimByPhase = { 0f, 0.8f, 1.6f, 2.4f };

        /// <summary>How fast every effect eases to its phase's value.</summary>
        private const float EaseHalfLife = 0.5f;

        /// <summary>The eyes' size, and how far apart and in front of the head they sit.</summary>
        private const float EyeSize = 0.07f;
        private const float EyeSpacing = 0.045f;
        private const float EyeForward = 0.12f;
        private const float EyeLift = 0.04f;

        /// <summary>Where the rim light sits behind him, away from the camera, and how far it reaches.</summary>
        private const float RimBehind = 1.6f;
        private const float RimHeight = 2.2f;
        private const float RimRange = 5f;

        private static readonly Color CharColor = new Color(0.06f, 0.03f, 0.02f, 0.8f);
        private static readonly Color EmberColor = new Color(1f, 0.32f, 0.08f, 1f);
        private static readonly Color EyeColor = new Color(1f, 0.15f, 0.05f, 1f);
        private static readonly Color RimColor = new Color(1f, 0.35f, 0.25f);

        private Material _glowMaterial;
        private Material _stainMaterial;
        private BossController _boss;
        private Transform _head;
        private Camera _camera;

        private MeshRenderer _char;
        private MeshRenderer _ember;
        private MeshRenderer[] _eyes;
        private Light _rim;

        private float _scorch;
        private float _emberLevel;
        private float _eyesLevel;
        private float _rimLevel;

        /// <summary>The scorch's current radius. For tests and debug tooling.</summary>
        public float ScorchRadius => _scorch;

        /// <summary>How brightly his eyes burn now. For tests and debug tooling.</summary>
        public float EyeGlow => _eyesLevel;

        /// <summary>Gives the effects the scene's own materials to draw with.</summary>
        /// <param name="glow">An additive material: the impact sparks'.</param>
        /// <param name="stain">An alpha-blended floor material: the blood decals'.</param>
        public void Construct(Material glow, Material stain)
        {
            _glowMaterial = glow;
            _stainMaterial = stain;
        }

        private void Update()
        {
            if (!Resolve())
            {
                return;
            }

            int phase = Mathf.Clamp(_boss.PhaseIndex, 0, ScorchRadiusByPhase.Length - 1);
            bool alive = _boss.IsAlive;
            float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / EaseHalfLife);

            _scorch = Mathf.Lerp(_scorch, ScorchRadiusByPhase[phase], t);
            _emberLevel = Mathf.Lerp(_emberLevel, alive ? EmberByPhase[phase] : 0f, t);
            _eyesLevel = Mathf.Lerp(_eyesLevel, alive ? EyesByPhase[phase] : 0f, t);
            _rimLevel = Mathf.Lerp(_rimLevel, alive ? RimByPhase[phase] : 0f, t);

            PlaceScorch();
            PlaceEyes();
            PlaceRim();
        }

        /// <summary>Finds the boss, his head and the camera, and builds the effects, once.</summary>
        private bool Resolve()
        {
            if (_boss == null)
            {
                _boss = FindAnyObjectByType<BossController>();

                if (_boss == null)
                {
                    return false;
                }

                Animator animator = _boss.GetComponentInChildren<Animator>();
                _head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                Build();
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            return true;
        }

        private void Build()
        {
            if (_stainMaterial != null)
            {
                _char = Quad("Scorch", _stainMaterial, DrawDisc(64));
            }

            if (_glowMaterial != null)
            {
                _ember = Quad("ScorchEmbers", _glowMaterial, DrawRing(64));
                Texture2D dot = DrawDot(32);
                _eyes = new[] { Quad("EyeLeft", _glowMaterial, dot), Quad("EyeRight", _glowMaterial, dot) };
            }

            if (GraphicsTier.Current != GraphicsQuality.Low)
            {
                var rimObject = new GameObject("BossRimLight");
                rimObject.transform.SetParent(transform, false);
                _rim = rimObject.AddComponent<Light>();
                _rim.type = LightType.Point;
                _rim.color = RimColor;
                _rim.range = RimRange;
                _rim.shadows = LightShadows.None;
                _rim.intensity = 0f;
                _rim.enabled = false;
            }
        }

        private MeshRenderer Quad(string name, Material material, Texture2D texture)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);

            // Each effect draws with its own copy of the material, its texture set as the main texture: these
            // shaders do not take a texture through a per-renderer override, and the scorch first drew as a bright
            // square for it. The main texture and colour resolve to whatever each shader names them.
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(material) { name = name, mainTexture = texture };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        private static void Tint(MeshRenderer renderer, Color colour) => renderer.sharedMaterial.color = colour;

        /// <summary>Lays the charred disc and its glowing edge flat on the floor under him.</summary>
        private void PlaceScorch()
        {
            bool show = _scorch > 0.05f;
            Vector3 feet = _boss.transform.position + Vector3.up * 0.03f;

            foreach (MeshRenderer layer in new[] { _char, _ember })
            {
                if (layer == null)
                {
                    continue;
                }

                layer.enabled = show;

                if (!show)
                {
                    continue;
                }

                layer.transform.SetPositionAndRotation(feet, Quaternion.Euler(90f, 0f, 0f));
                layer.transform.localScale = Vector3.one * (_scorch * 2f);
            }

            if (show && _char != null)
            {
                Tint(_char, CharColor);
            }

            if (show && _ember != null)
            {
                Tint(_ember, new Color(EmberColor.r * _emberLevel, EmberColor.g * _emberLevel, EmberColor.b * _emberLevel, 1f));
            }
        }

        /// <summary>Hangs two burning points in front of his face, turned to the camera.</summary>
        private void PlaceEyes()
        {
            if (_eyes == null)
            {
                return;
            }

            bool show = _head != null && _eyesLevel > 0.05f && _camera != null;

            for (int i = 0; i < _eyes.Length; i++)
            {
                MeshRenderer eye = _eyes[i];
                eye.enabled = show;

                if (!show)
                {
                    continue;
                }

                Vector3 forward = _boss.transform.forward;
                Vector3 right = _boss.transform.right;
                Vector3 position = _head.position + forward * EyeForward + Vector3.up * EyeLift +
                                   right * (i == 0 ? -EyeSpacing : EyeSpacing);

                eye.transform.SetPositionAndRotation(position, _camera.transform.rotation);
                eye.transform.localScale = Vector3.one * EyeSize;
                Tint(eye, new Color(EyeColor.r * _eyesLevel, EyeColor.g * _eyesLevel, EyeColor.b * _eyesLevel, 1f));
            }
        }

        /// <summary>Keeps the rim light behind him from where the camera stands, so his outline is lit.</summary>
        private void PlaceRim()
        {
            if (_rim == null)
            {
                return;
            }

            _rim.enabled = _rimLevel > 0.05f;

            if (!_rim.enabled || _camera == null)
            {
                return;
            }

            Vector3 away = _boss.transform.position - _camera.transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > Mathf.Epsilon ? away.normalized : -_boss.transform.forward;

            _rim.transform.position = _boss.transform.position + away * RimBehind + Vector3.up * RimHeight;
            _rim.intensity = _rimLevel;
        }

        /// <summary>A soft disc, dark in the middle and fading out at the edge, irregular like burned stone.</summary>
        private static Texture2D DrawDisc(int size)
        {
            return Draw(size, (radius, angle) =>
            {
                float edge = 1f - Mathf.SmoothStep(0.7f, 1f, radius);
                float grain = 0.75f + 0.25f * Mathf.Sin(angle * 7f + radius * 13f) * Mathf.Sin(angle * 3f);
                return edge * grain;
            });
        }

        /// <summary>A thin ring near the edge: the stone still glowing where the heat spreads.</summary>
        private static Texture2D DrawRing(int size)
        {
            return Draw(size, (radius, angle) =>
            {
                float band = Mathf.Exp(-Mathf.Pow((radius - 0.82f) / 0.08f, 2f));
                float flicker = 0.6f + 0.4f * Mathf.Sin(angle * 11f) * Mathf.Sin(angle * 5f + 1.3f);
                return band * flicker;
            });
        }

        /// <summary>A soft round point of light.</summary>
        private static Texture2D DrawDot(int size) => Draw(size, (radius, _) => Mathf.Pow(Mathf.Clamp01(1f - radius), 2f));

        /// <summary>Draws a texture whose brightness and alpha are a function of distance and angle from its centre.</summary>
        private static Texture2D Draw(int size, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = radius > 1f ? 0f : Mathf.Clamp01(alpha(radius, Mathf.Atan2(dy, dx)));
                    // The shape in the colour as well as the alpha: an additive shader adds the colour and ignores
                    // alpha, so a white square with a shaped alpha drew as a bright square.
                    byte value = (byte)(a * 255f);
                    pixels[y * size + x] = new Color32(value, value, value, value);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
