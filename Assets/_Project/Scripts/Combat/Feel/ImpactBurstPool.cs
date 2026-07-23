using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// A fixed set of reusable impact bursts, handed out round-robin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Instantiating an effect per hit would allocate and destroy several objects a second during a
    /// combo, which is the classic source of a stutter that arrives exactly when the game most needs
    /// to feel smooth. Every burst is built once at startup and reused.
    /// </para>
    /// <para>
    /// Allocation is round-robin rather than first-idle. With a pool comfortably larger than the
    /// number of hits that can overlap, the oldest entry is always the safest one to reuse, and it
    /// avoids a scan on a path that runs in the middle of combat.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ImpactBurstPool : MonoBehaviour
    {
        /// <summary>
        /// Shader used for the particles.
        /// </summary>
        /// <remarks>
        /// Additive unlit, so sparks read as light rather than as coloured paper and pick up bloom
        /// from the post-processing volume.
        /// </remarks>
        private const string ParticleShaderName = "Universal Render Pipeline/Particles/Unlit";

        /// <summary>Fallback for the rare case the render pipeline's particle shader is unavailable.</summary>
        private const string FallbackShaderName = "Sprites/Default";

        [SerializeField]
        [Range(4, 32)]
        [Tooltip("How many impacts may be visible at once. Beyond this the oldest is recycled.")]
        private int _capacity = 12;

        private ImpactBurst[] _bursts;
        private int _next;

        private void Awake()
        {
            Material material = CreateParticleMaterial();

            _bursts = new ImpactBurst[_capacity];

            for (int i = 0; i < _capacity; i++)
            {
                var burstObject = new GameObject($"ImpactBurst_{i}");
                burstObject.transform.SetParent(transform, worldPositionStays: false);

                _bursts[i] = burstObject.AddComponent<ImpactBurst>();
                _bursts[i].Construct(material);
            }
        }

        /// <summary>Plays an impact at a point in the world.</summary>
        /// <param name="position">Where the hit landed.</param>
        /// <param name="direction">Direction the sparks are thrown along.</param>
        /// <param name="flavour">What kind of impact this was.</param>
        public void Play(Vector3 position, Vector3 direction, ImpactFlavour flavour)
        {
            if (_bursts == null || _bursts.Length == 0)
            {
                return;
            }

            _bursts[_next].Play(position, direction, flavour);
            _next = (_next + 1) % _bursts.Length;
        }

        /// <summary>Builds the one material every burst shares.</summary>
        private static Material CreateParticleMaterial()
        {
            Shader shader = Shader.Find(ParticleShaderName) ?? Shader.Find(FallbackShaderName);

            var material = new Material(shader)
            {
                name = "ImpactSparks (generated)",
                hideFlags = HideFlags.HideAndDontSave
            };

            // Named through the property strings rather than a keyword helper because the two
            // shaders above disagree about which properties exist; setting one that is absent is a
            // no-op rather than an error, so this covers both.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_EMISSION");

            return material;
        }
    }
}
