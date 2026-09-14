using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// A fixed set of physical stone pieces, reused so breaking things never allocates or grows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Capped by platform tier: a WebGL build keeps far fewer live bodies than a desktop one. When every
    /// piece is in use the oldest is taken back, so a long fight full of slams keeps its newest, most
    /// visible debris and never exceeds the budget.
    /// </para>
    /// <para>
    /// A piece settles into a static body once it sleeps or its settle time passes. A floor littered with
    /// awake rigidbodies costs the solver every frame for pieces nobody is looking at.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DebrisPool : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Material the pieces are drawn with. Assigned by the scene generator, so its shader ships.")]
        private Material _material;

        [SerializeField]
        [Min(1)]
        [Tooltip("Most pieces live at once in the WebGL build.")]
        private int _webCapacity = 40;

        [SerializeField]
        [Min(1)]
        [Tooltip("Most pieces live at once in a desktop build.")]
        private int _desktopCapacity = 150;

        [SerializeField]
        [Min(0.5f)]
        [Tooltip("Seconds before a WebGL piece is frozen where it lies.")]
        private float _webSettleSeconds = 6f;

        [SerializeField]
        [Min(0.5f)]
        [Tooltip("Seconds before a desktop piece is frozen where it lies.")]
        private float _desktopSettleSeconds = 20f;

        private Rigidbody[] _pieces;
        private float[] _settleAt;
        private int _next;
        private float _settleSeconds;

        /// <summary>How many pieces this platform may have live at once.</summary>
        public int Capacity => _pieces?.Length ?? 0;

        /// <summary>How many pieces are currently in the world.</summary>
        public int ActiveCount
        {
            get
            {
                int count = 0;

                if (_pieces != null)
                {
                    foreach (Rigidbody piece in _pieces)
                    {
                        count += piece.gameObject.activeSelf ? 1 : 0;
                    }
                }

                return count;
            }
        }

        /// <summary>Assigns the material and budgets. Used by the scene generator.</summary>
        /// <param name="material">Material for the pieces.</param>
        /// <param name="webCapacity">WebGL ceiling on live pieces.</param>
        /// <param name="desktopCapacity">Desktop ceiling on live pieces.</param>
        public void Bind(Material material, int webCapacity, int desktopCapacity)
        {
            _material = material;
            _webCapacity = webCapacity;
            _desktopCapacity = desktopCapacity;
        }

        private void Awake()
        {
            bool web = Application.platform == RuntimePlatform.WebGLPlayer;
            int capacity = DestructionRules.Capacity(web, _webCapacity, _desktopCapacity);

            _settleSeconds = web ? _webSettleSeconds : _desktopSettleSeconds;
            _pieces = new Rigidbody[capacity];
            _settleAt = new float[capacity];

            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            int layer = Core.Constants.Layers.Debris;

            for (int i = 0; i < capacity; i++)
            {
                var piece = new GameObject($"Debris_{i:D3}") { layer = layer };
                piece.transform.SetParent(transform, false);
                piece.AddComponent<MeshFilter>().sharedMesh = cube;
                piece.AddComponent<MeshRenderer>().sharedMaterial = _material;
                piece.AddComponent<BoxCollider>();

                Rigidbody body = piece.AddComponent<Rigidbody>();
                body.mass = 20f;
                body.interpolation = RigidbodyInterpolation.Interpolate;

                piece.SetActive(false);
                _pieces[i] = body;
            }
        }

        /// <summary>Throws one piece into the world, taking back the oldest if all are in use.</summary>
        /// <param name="position">Where the piece starts.</param>
        /// <param name="rotation">Its starting orientation.</param>
        /// <param name="size">Its dimensions, in metres.</param>
        /// <param name="velocity">Its initial velocity.</param>
        /// <param name="spin">Its initial angular velocity, in radians per second.</param>
        public void Launch(Vector3 position, Quaternion rotation, Vector3 size, Vector3 velocity, Vector3 spin)
        {
            if (_pieces == null || _pieces.Length == 0)
            {
                return;
            }

            Rigidbody body = _pieces[_next];
            _settleAt[_next] = Time.time + _settleSeconds;
            _next = (_next + 1) % _pieces.Length;

            body.gameObject.SetActive(true);
            body.isKinematic = false;
            body.transform.SetPositionAndRotation(position, rotation);
            body.transform.localScale = size;
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = velocity;
            body.angularVelocity = spin;
        }

        /// <summary>Removes every piece, for a retry.</summary>
        public void Clear()
        {
            if (_pieces == null)
            {
                return;
            }

            foreach (Rigidbody piece in _pieces)
            {
                if (!piece.isKinematic)
                {
                    piece.linearVelocity = Vector3.zero;
                    piece.angularVelocity = Vector3.zero;
                }

                piece.gameObject.SetActive(false);
            }

            _next = 0;
        }

        private void FixedUpdate()
        {
            if (_pieces == null)
            {
                return;
            }

            for (int i = 0; i < _pieces.Length; i++)
            {
                Rigidbody piece = _pieces[i];

                if (piece.isKinematic || !piece.gameObject.activeSelf)
                {
                    continue;
                }

                if (piece.IsSleeping() || Time.time >= _settleAt[i])
                {
                    piece.isKinematic = true;
                }
            }
        }
    }
}
