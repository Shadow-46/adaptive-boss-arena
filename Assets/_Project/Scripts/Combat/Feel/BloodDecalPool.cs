using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Blood left on the floor where blows landed, fading after a while.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A blow sprayed blood and the floor stayed clean, so a long fight left no trace of itself. Splatters
    /// that stay a while and then fade let the floor keep a record of where the exchange went.
    /// </para>
    /// <para>
    /// A fixed ring of flat quads, reused oldest first, so a long fight never allocates and never piles up
    /// more than <see cref="Capacity"/> of them. Each is stretched along the blow that made it. Fades on game
    /// time, so a pause or hit-stop holds it.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BloodDecalPool : MonoBehaviour
    {
        /// <summary>The most splatters on the floor at once.</summary>
        public const int Capacity = 24;

        /// <summary>How long a splatter stays at full strength before it starts to fade, in seconds.</summary>
        public const float HoldSeconds = 16f;

        /// <summary>How long a splatter takes to fade out, in seconds.</summary>
        public const float FadeSeconds = 6f;

        /// <summary>Height above the floor: over the old stains, under the scars and warnings.</summary>
        private const float Lift = 0.011f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MeshRenderer[] _renderers = new MeshRenderer[0];
        private float[] _placedAt = new float[0];
        private MaterialPropertyBlock _block;
        private IRandomProvider _random;
        private float _clock;
        private int _next;

        /// <summary>How many splatters are currently on the floor.</summary>
        public int VisibleCount
        {
            get
            {
                int count = 0;

                foreach (MeshRenderer renderer in _renderers)
                {
                    count += renderer != null && renderer.gameObject.activeSelf ? 1 : 0;
                }

                return count;
            }
        }

        /// <summary>A splatter's opacity at an age.</summary>
        /// <param name="ageSeconds">Seconds since it was placed.</param>
        /// <returns>One while fresh, easing to zero over the fade.</returns>
        public static float AlphaAt(float ageSeconds)
        {
            if (ageSeconds <= HoldSeconds)
            {
                return 1f;
            }

            float t = Mathf.Clamp01((ageSeconds - HoldSeconds) / FadeSeconds);
            return 1f - t * t;
        }

        /// <summary>Builds the pool.</summary>
        /// <param name="material">Alpha-blended splatter material.</param>
        /// <param name="random">Source for each splatter's turn and size, so no splatter reaches for the global generator.</param>
        public void Construct(Material material, IRandomProvider random)
        {
            _random = random;
            _block = new MaterialPropertyBlock();
            _renderers = new MeshRenderer[Capacity];
            _placedAt = new float[Capacity];

            for (int i = 0; i < Capacity; i++)
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = $"Blood_{i:D2}";

                // Visual only: nothing should collide with a stain.
                Destroy(quad.GetComponent<Collider>());

                quad.transform.SetParent(transform, worldPositionStays: false);

                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                _renderers[i] = renderer;
                quad.SetActive(false);
            }
        }

        /// <summary>Leaves a splatter on the floor under a blow.</summary>
        /// <param name="position">Where the blow landed; only its floor position is used.</param>
        /// <param name="direction">The blow's direction, which the splatter is flung along.</param>
        /// <param name="size">Rough size in metres.</param>
        public void Place(Vector3 position, Vector3 direction, float size)
        {
            if (_renderers.Length == 0 || _random == null)
            {
                return;
            }

            MeshRenderer renderer = _renderers[_next];
            _placedAt[_next] = _clock;
            _next = (_next + 1) % _renderers.Length;

            var along = new Vector3(direction.x, 0f, direction.z);
            along = along.sqrMagnitude > 1e-6f ? along.normalized : Quaternion.Euler(0f, _random.NextFloat(0f, 360f), 0f) * Vector3.forward;

            // A quad faces along -z; laid flat, its texture's up runs along the blow, so the droplets fly with it.
            Quaternion flat = Quaternion.LookRotation(Vector3.down, along);
            float scale = size * _random.NextFloat(0.75f, 1.25f);

            Transform quad = renderer.transform;
            quad.SetPositionAndRotation(new Vector3(position.x, Lift, position.z) + along * (scale * 0.25f), flat);
            quad.localScale = new Vector3(scale * _random.NextFloat(0.7f, 1f), scale * 1.4f, 1f);

            renderer.gameObject.SetActive(true);
            SetAlpha(renderer, 1f);
        }

        private void Update()
        {
            _clock += Time.deltaTime;

            for (int i = 0; i < _renderers.Length; i++)
            {
                MeshRenderer renderer = _renderers[i];

                if (renderer == null || !renderer.gameObject.activeSelf)
                {
                    continue;
                }

                float alpha = AlphaAt(_clock - _placedAt[i]);

                if (alpha <= 0f)
                {
                    renderer.gameObject.SetActive(false);
                    continue;
                }

                SetAlpha(renderer, alpha);
            }
        }

        private void SetAlpha(MeshRenderer renderer, float alpha)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, new Color(1f, 1f, 1f, alpha));
            renderer.SetPropertyBlock(_block);
        }
    }
}
