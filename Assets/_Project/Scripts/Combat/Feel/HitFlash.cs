using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Briefly tints a combatant's renderers when they are hit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cheapest legible confirmation that a hit registered. Damage numbers and health bars both
    /// require the player to look away from the fight; a flash on the model itself is read
    /// peripherally, which is the only kind of feedback that survives a fast exchange.
    /// </para>
    /// <para>
    /// Uses a <see cref="MaterialPropertyBlock"/> rather than assigning to
    /// <c>Renderer.material</c>. The latter silently instantiates a copy of the material on first
    /// access, leaking one per combatant and breaking batching for everything sharing it.
    /// </para>
    /// <para>
    /// Driven by unscaled time so the flash still plays during the hit-stop it accompanies.
    /// </para>
    /// <para>
    /// A multiply toward blood red, not a blend toward white. A textured body's base colour is already
    /// white, so blending to white changed nothing on the skin and turned only the flat-coloured parts - the
    /// shield, the blade - into pale cards. Multiplying darkens every part alike.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HitFlash : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FallbackColorId = Shader.PropertyToID("_Color");

        [SerializeField]
        [Tooltip("Colour every part is multiplied by at peak flash.")]
        private Color _flashTint = new Color(1f, 0.4f, 0.34f);

        [SerializeField]
        [Range(0.02f, 0.4f)]
        [Tooltip("Flash duration. Short enough not to obscure the character during a combo.")]
        private float _durationSeconds = 0.09f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How far toward the flash colour the model travels at peak.")]
        private float _intensity = 0.85f;

        private Renderer[] _renderers;
        private Color[] _baseColors;
        private MaterialPropertyBlock _propertyBlock;
        private float _remaining;

        private void Awake()
        {
            // Bodies and arms only: a trail or a particle system tinted through the same property would lose its
            // own colour and fade.
            var bodies = new System.Collections.Generic.List<Renderer>();

            foreach (Renderer candidate in GetComponentsInChildren<Renderer>())
            {
                if (candidate is MeshRenderer || candidate is SkinnedMeshRenderer)
                {
                    bodies.Add(candidate);
                }
            }

            _renderers = bodies.ToArray();
            _propertyBlock = new MaterialPropertyBlock();
            _baseColors = new Color[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                _baseColors[i] = ReadBaseColor(_renderers[i]);
            }
        }

        private void LateUpdate()
        {
            if (_remaining <= 0f)
            {
                return;
            }

            _remaining -= Time.unscaledDeltaTime;

            // Fades from full intensity to none, so the flash reads as an impact rather than a
            // toggle that pops off.
            float strength = Mathf.Clamp01(_remaining / _durationSeconds) * _intensity;
            ApplyTint(strength);
        }

        /// <summary>Starts a flash. Safe to call again while one is already running.</summary>
        /// <remarks>
        /// Honours the reduced-flashing accessibility setting here, at the single point a flash
        /// begins, so nothing downstream has to know about it.
        /// </remarks>
        public void Play()
        {
            if (FeedbackSettings.ReducedFlashing)
            {
                return;
            }

            _remaining = _durationSeconds;
        }

        /// <summary>Starts a flash only for hits that actually dealt damage.</summary>
        /// <param name="result">Outcome of the hit.</param>
        public void PlayIfDamaged(in DamageResult result)
        {
            if (result.Connected)
            {
                Play();
            }
        }

        /// <summary>Blends every renderer toward the flash colour.</summary>
        private void ApplyTint(float strength)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer target = _renderers[i];

                if (target == null)
                {
                    continue;
                }

                target.GetPropertyBlock(_propertyBlock);

                Color tinted = _baseColors[i] * Color.Lerp(Color.white, _flashTint, strength);
                tinted.a = _baseColors[i].a;
                _propertyBlock.SetColor(BaseColorId, tinted);
                _propertyBlock.SetColor(FallbackColorId, tinted);

                target.SetPropertyBlock(_propertyBlock);
            }
        }

        /// <summary>Reads a renderer's untinted colour, tolerating either pipeline's property name.</summary>
        private static Color ReadBaseColor(Renderer target)
        {
            Material material = target != null ? target.sharedMaterial : null;

            if (material == null)
            {
                return Color.white;
            }

            if (material.HasProperty(BaseColorId))
            {
                return material.GetColor(BaseColorId);
            }

            return material.HasProperty(FallbackColorId) ? material.GetColor(FallbackColorId) : Color.white;
        }
    }
}
