using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Draws an attack's hit volume on the ground while it winds up and swings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without this the combat is invisible. The hitboxes are query volumes with no renderer, so
    /// pressing attack produces damage numbers and nothing else; a player genuinely cannot tell
    /// whether they attacked, what reach they have, or whether the heavy is different from the
    /// light. That is not a polish problem, it is the difference between a playable game and an
    /// unreadable one.
    /// </para>
    /// <para>
    /// The two phases are drawn deliberately differently. Startup shows a dim outline of exactly
    /// where the blow will land, which is the boss's telegraph and the player's aiming aid. The
    /// active window flashes bright and brief, which is the confirmation that the swing happened.
    /// Because both are generated from the attack's own numbers, the visual cannot drift from the
    /// hitbox.
    /// </para>
    /// <para>
    /// Fades on unscaled time so the flash still reads during the hit-stop it triggers.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class AttackVisualizer : MonoBehaviour
    {
        /// <summary>Height above the floor, enough to avoid z-fighting with it.</summary>
        private const float GroundOffset = 0.03f;

        /// <summary>Pulse rate of a perilous (unblockable) telegraph, in cycles per second.</summary>
        private const float PerilousPulseHz = 4f;

        /// <summary>The unmistakable red of a perilous telegraph — "do not block this".</summary>
        private static readonly Color PerilousColor = new Color(1f, 0.14f, 0.11f);

        [SerializeField]
        [Tooltip("Transparent unlit material the overlay is drawn with. Assigned by the prefab generator.")]
        private Material _overlayMaterial;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Opacity of the wind-up telegraph. Low enough not to dominate, high enough to read.")]
        private float _telegraphAlpha = 0.28f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Opacity at the moment the blow lands.")]
        private float _strikeAlpha = 0.75f;

        [SerializeField]
        [Range(0.05f, 0.6f)]
        [Tooltip("How long the strike flash takes to fade out.")]
        private float _strikeFadeSeconds = 0.22f;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private Mesh _currentMesh;

        private Color _baseColor = Color.white;
        private float _alpha;
        private float _strikeFadeRemaining;
        private bool _isTelegraphing;
        private bool _perilous;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FallbackColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _propertyBlock = new MaterialPropertyBlock();

            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.enabled = false;

            // Assigned from the serialised field because the renderer does not exist until now, so
            // the prefab generator has nothing to write the material onto at build time.
            if (_overlayMaterial != null)
            {
                _meshRenderer.sharedMaterial = _overlayMaterial;
            }

            transform.localPosition = new Vector3(0f, GroundOffset, 0f);
        }

        private void LateUpdate()
        {
            // A perilous wind-up pulses red so the "dodge, don't block" read lands before the swing
            // does. Only while telegraphing, so it never fights the strike flash below.
            if (_isTelegraphing && _perilous && _strikeFadeRemaining <= 0f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PerilousPulseHz * Mathf.PI * 2f);
                _alpha = Mathf.Lerp(_telegraphAlpha, _telegraphAlpha * 2.2f, pulse);
                ApplyColor();
            }

            if (_strikeFadeRemaining > 0f)
            {
                _strikeFadeRemaining -= Time.unscaledDeltaTime;

                float fraction = Mathf.Clamp01(_strikeFadeRemaining / _strikeFadeSeconds);
                _alpha = Mathf.Lerp(_isTelegraphing ? _telegraphAlpha : 0f, _strikeAlpha, fraction);

                ApplyColor();

                if (_strikeFadeRemaining <= 0f && !_isTelegraphing)
                {
                    _meshRenderer.enabled = false;
                }
            }
        }

        /// <summary>Assigns the overlay material. Used by the prefab generator.</summary>
        /// <param name="material">A transparent unlit material. Shared across all visualisers.</param>
        public void SetMaterial(Material material) => _overlayMaterial = material;

        /// <summary>Shows the wind-up telegraph for an attack.</summary>
        /// <param name="attack">Attack supplying the shape and colour.</param>
        public void ShowTelegraph(AttackDefinition attack)
        {
            if (attack == null || !BuildMeshFor(attack))
            {
                return;
            }

            _perilous = attack.Unblockable;
            _baseColor = _perilous ? PerilousColor : attack.TelegraphColor;
            _isTelegraphing = true;
            _alpha = _telegraphAlpha;

            _meshRenderer.enabled = true;
            ApplyColor();
        }

        /// <summary>Flashes the volume at the moment the attack becomes live.</summary>
        /// <param name="attack">Attack supplying the shape and colour.</param>
        public void ShowStrike(AttackDefinition attack)
        {
            if (attack == null || !BuildMeshFor(attack))
            {
                return;
            }

            _baseColor = attack.Unblockable ? PerilousColor : attack.TelegraphColor;
            _isTelegraphing = false;
            _perilous = false;
            _alpha = _strikeAlpha;
            _strikeFadeRemaining = _strikeFadeSeconds;

            _meshRenderer.enabled = true;
            ApplyColor();
        }

        /// <summary>Hides the overlay, letting any strike flash finish fading.</summary>
        public void Hide()
        {
            _isTelegraphing = false;

            if (_strikeFadeRemaining <= 0f)
            {
                _meshRenderer.enabled = false;
            }
        }

        /// <summary>Rebuilds the mesh when the attack changes, reusing it otherwise.</summary>
        private bool BuildMeshFor(AttackDefinition attack)
        {
            Mesh mesh = AttackShapeMesh.Build(attack);

            if (mesh == null)
            {
                return false;
            }

            // The previous mesh was created at runtime and is not owned by the asset database, so
            // it must be destroyed explicitly or every swing leaks one.
            if (_currentMesh != null)
            {
                Destroy(_currentMesh);
            }

            _currentMesh = mesh;
            _meshFilter.sharedMesh = mesh;

            return true;
        }

        /// <summary>Pushes the current colour and opacity through a property block.</summary>
        private void ApplyColor()
        {
            var color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, _alpha);

            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            _propertyBlock.SetColor(FallbackColorId, color);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void OnDestroy()
        {
            if (_currentMesh != null)
            {
                Destroy(_currentMesh);
            }
        }
    }
}
