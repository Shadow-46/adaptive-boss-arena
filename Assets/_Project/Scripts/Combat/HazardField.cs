using UnityEngine;
using UnityEngine.Rendering;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// A fixed pool of reusable ground hazards, handed out round-robin.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="Feel.ImpactBurstPool"/>: instantiating a zone per attack would allocate in
    /// the middle of combat, so every zone is built once at startup and recycled. A capacity of a
    /// handful covers even the Last Stand, where several scars can overlap at once.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HazardField : MonoBehaviour, IHazardField
    {
        private const string ShaderName = "Universal Render Pipeline/Unlit";
        private const string FallbackShaderName = "Sprites/Default";

        [SerializeField]
        [Range(2, 16)]
        [Tooltip("How many hazards may be active at once. Beyond this the oldest is recycled.")]
        private int _capacity = 8;

        private HazardZone[] _zones;
        private int _next;

        private void Awake()
        {
            Material material = CreateDiscMaterial();
            _zones = new HazardZone[_capacity];

            for (int i = 0; i < _capacity; i++)
            {
                var zoneObject = new GameObject($"HazardZone_{i}");
                zoneObject.transform.SetParent(transform, worldPositionStays: false);

                _zones[i] = zoneObject.AddComponent<HazardZone>();
                _zones[i].Construct(material);
            }
        }

        /// <inheritdoc />
        public void Spawn(Vector3 center, float radius, float damagePerTick, float durationSeconds)
        {
            if (_zones == null || _zones.Length == 0)
            {
                return;
            }

            _zones[_next].Activate(center, radius, damagePerTick, durationSeconds);
            _next = (_next + 1) % _zones.Length;
        }

        /// <summary>Builds the one transparent material every disc shares.</summary>
        private static Material CreateDiscMaterial()
        {
            Shader shader = Shader.Find(ShaderName) ?? Shader.Find(FallbackShaderName);

            var material = new Material(shader)
            {
                name = "HazardDisc (generated)",
                hideFlags = HideFlags.HideAndDontSave
            };

            // Alpha-blended rather than additive, so a scar reads as a stain darkening the floor
            // rather than as light. Property names set defensively: a shader lacking one ignores it.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.renderQueue = (int)RenderQueue.Transparent;

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            return material;
        }
    }
}
