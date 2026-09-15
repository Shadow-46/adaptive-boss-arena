using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// A smear swept by the blade's whole length through a swing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The single cheapest way to make an attack look like it travelled somewhere. Without it a
    /// swing is a pose change between two frames; with it the arc is visible after the fact, which is
    /// what lets a player read range and direction from an attack they only half saw.
    /// </para>
    /// <para>
    /// A surface between the blade's base and tip, not a ribbon behind one point. The ribbon followed a
    /// fixed point beside the body, so it traced a circle around the fighter rather than the arc of the
    /// blade, and when a swing ended it was left hanging in the air as a flat coloured card.
    /// </para>
    /// <para>
    /// The trail is emitted only during the swing and then left to fade on its own. Clearing it on
    /// stop would cut the smear off mid-air at the exact moment the eye is following it.
    /// </para>
    /// <para>
    /// Ages on game time, so a hit-stop holds the smear with the frozen blade instead of letting it
    /// evaporate while the frozen frame is still on screen.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WeaponTrail : MonoBehaviour
    {
        /// <summary>Most blade positions kept at once. At sixty frames this covers well over the lifetime.</summary>
        private const int Capacity = 24;

        /// <summary>Points inserted between two recorded positions, so a fast swing curves instead of kinking.</summary>
        private const int Subdivisions = 3;

        [SerializeField]
        [Range(0.1f, 1.5f)]
        [Tooltip("How long the smear is emitted for after a swing begins. Covers wind-up through " +
                 "recovery without bleeding into the next attack.")]
        private float _emitSeconds = 0.45f;

        [SerializeField]
        [Range(0.05f, 0.5f)]
        [Tooltip("How long a stretch of the smear lasts before it has faded out.")]
        private float _lifetimeSeconds = 0.16f;

        [SerializeField]
        [Tooltip("Distance from this transform, along its forward, where the smear's inner edge runs.")]
        private float _baseDistance = 0.3f;

        [SerializeField]
        [Tooltip("Distance from this transform, along its forward, where the smear's outer edge runs.")]
        private float _tipDistance = 0.95f;

        [SerializeField]
        [Tooltip("Colour at the head of the smear. Kept dim; the material adds it as light.")]
        private Color _colour = new Color(0.5f, 0.48f, 0.45f, 1f);

        [SerializeField]
        [Tooltip("Additive material, bright toward the blade's edge. Assigned by the prefab generator.")]
        private Material _material;

        private readonly Vector3[] _bases = new Vector3[Capacity];
        private readonly Vector3[] _tips = new Vector3[Capacity];
        private readonly float[] _times = new float[Capacity];

        private Vector3[] _vertices;
        private Color[] _colours;
        private Vector2[] _uvs;
        private int[] _triangles;

        private Mesh _mesh;
        private GameObject _surface;
        private int _count;
        private int _head;
        private float _clock;
        private float _remaining;

        /// <summary>True while the blade is laying down a smear.</summary>
        public bool IsEmitting => _remaining > 0f;

        /// <summary>How many blade positions the smear is currently drawn through.</summary>
        public int VisibleSamples => _count;

        /// <summary>Configures the smear. Used by the prefab generator.</summary>
        /// <param name="baseDistance">Inner edge's distance along this transform's forward.</param>
        /// <param name="tipDistance">Outer edge's distance along this transform's forward.</param>
        /// <param name="colour">Colour at the head of the smear.</param>
        /// <param name="material">The additive smear material.</param>
        public void Configure(float baseDistance, float tipDistance, Color colour, Material material)
        {
            _baseDistance = baseDistance;
            _tipDistance = tipDistance;
            _colour = colour;
            _material = material;
        }

        /// <summary>Starts emitting, or extends emission if a swing is already under way.</summary>
        public void Begin()
        {
            // Restarting rather than accumulating: a combo's second hit should get a full smear, not
            // an ever-lengthening one.
            _remaining = _emitSeconds;
        }

        private void Awake()
        {
            int points = (Capacity - 1) * Subdivisions + 1;
            _vertices = new Vector3[points * 2];
            _colours = new Color[points * 2];
            _uvs = new Vector2[points * 2];
            _triangles = new int[(points - 1) * 6];

            _mesh = new Mesh { name = "BladeSmear" };
            _mesh.MarkDynamic();

            // In world space and outside the fighter's hierarchy: the recorded positions are already world
            // positions, and a parent that moves or scales would carry the smear along with the body.
            _surface = new GameObject(name + "Smear");
            _surface.AddComponent<MeshFilter>().sharedMesh = _mesh;

            var renderer = _surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void OnDestroy()
        {
            if (_surface != null)
            {
                Destroy(_surface);
            }

            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        private void LateUpdate()
        {
            // Game time: frozen with the blade during hit-stop.
            float delta = Time.deltaTime;
            _clock += delta;

            if (_remaining > 0f)
            {
                _remaining -= delta;
                Record();
            }

            Expire();
            Rebuild();
        }

        private void Record()
        {
            Vector3 origin = transform.position;
            Vector3 along = transform.forward;

            _head = (_head + 1) % Capacity;
            _bases[_head] = origin + along * _baseDistance;
            _tips[_head] = origin + along * _tipDistance;
            _times[_head] = _clock;
            _count = Mathf.Min(_count + 1, Capacity);
        }

        private void Expire()
        {
            while (_count > 0 && _clock - _times[Index(_count - 1)] > _lifetimeSeconds)
            {
                _count--;
            }
        }

        /// <summary>The ring-buffer slot of the sample a number of steps older than the newest.</summary>
        private int Index(int stepsBack) => (_head - stepsBack + Capacity) % Capacity;

        private void Rebuild()
        {
            _mesh.Clear();

            if (_count < 2)
            {
                return;
            }

            int points = (_count - 1) * Subdivisions + 1;

            for (int p = 0; p < points; p++)
            {
                // Position along the recorded samples, newest first, as a sample index with a fraction.
                float along = p / (float)Subdivisions;
                int i = Mathf.Min((int)along, _count - 2);
                float t = along - i;

                Vector3 inner = Sample(_bases, i, t);
                Vector3 outer = Sample(_tips, i, t);
                float age = Mathf.Lerp(_clock - _times[Index(i)], _clock - _times[Index(i + 1)], t);
                float life = Mathf.Clamp01(1f - age / _lifetimeSeconds);

                Color colour = _colour;
                colour.a *= life * life;

                _vertices[p * 2] = inner;
                _vertices[p * 2 + 1] = outer;
                _colours[p * 2] = colour;
                _colours[p * 2 + 1] = colour;
                _uvs[p * 2] = new Vector2(1f - life, 0f);
                _uvs[p * 2 + 1] = new Vector2(1f - life, 1f);
            }

            int triangle = 0;

            for (int p = 0; p < points - 1; p++)
            {
                int a = p * 2, b = a + 1, c = a + 2, d = a + 3;
                _triangles[triangle++] = a; _triangles[triangle++] = b; _triangles[triangle++] = d;
                _triangles[triangle++] = a; _triangles[triangle++] = d; _triangles[triangle++] = c;
            }

            _mesh.SetVertices(_vertices, 0, points * 2);
            _mesh.SetColors(_colours, 0, points * 2);
            _mesh.SetUVs(0, _uvs, 0, points * 2);
            _mesh.SetTriangles(_triangles, 0, triangle, 0, calculateBounds: true);
        }

        /// <summary>A Catmull-Rom point between two recorded samples, using their neighbours for the curve.</summary>
        private Vector3 Sample(Vector3[] positions, int i, float t)
        {
            Vector3 p1 = positions[Index(i)];
            Vector3 p2 = positions[Index(i + 1)];
            Vector3 p0 = i > 0 ? positions[Index(i - 1)] : p1 + (p1 - p2);
            Vector3 p3 = i + 2 < _count ? positions[Index(i + 2)] : p2 + (p2 - p1);

            float t2 = t * t, t3 = t2 * t;

            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }
    }
}
