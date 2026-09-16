using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Plays a footstep every stride of actual travel, giving movement an audible weight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Driven by <em>distance moved</em> rather than by speed or a timer, so steps land in cadence
    /// with the character's real motion and stop the instant it does — including during a hit-stop,
    /// when the transform simply is not moving, so no clock needs consulting.
    /// </para>
    /// <para>
    /// Presentation only: it reads its own transform and plays a sound, and knows nothing of combat.
    /// The player and boss use the same component with different cues and stride lengths, so the boss
    /// lands heavier, slower footfalls than the player without any special-casing.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FootstepEmitter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Audio cue played on each footstep.")]
        private string _cueId = "step.player";

        [SerializeField]
        [Tooltip("Distance travelled between footsteps. Longer strides read as a larger character.")]
        private float _strideLength = 1.6f;

        [SerializeField]
        [Tooltip("Alpha-blended material the dust is drawn with. Assigned by the prefab generator.")]
        private Material _dustMaterial;

        [SerializeField]
        [Tooltip("Rough width of the dust a footfall lifts, in metres.")]
        private float _dustSize = 0.35f;

        /// <summary>Height the dust starts at: just off the floor, where a boot meets the stone.</summary>
        private const float DustHeight = 0.05f;

        private IAudioService _audio;
        private ParticleSystem _dust;
        private Vector3 _lastPosition;
        private float _accumulated;

        /// <summary>
        /// How many motes a footfall lifts at a speed.
        /// </summary>
        /// <remarks>
        /// A walk barely disturbs the floor; a run drives grit off it. Below a walk nothing is lifted at all, so
        /// a fighter edging into range does not trail dust behind them.
        /// </remarks>
        /// <param name="speed01">Planar speed as a fraction of top speed.</param>
        /// <returns>How many motes the step lifts.</returns>
        public static int PuffCount(float speed01) =>
            speed01 < 0.25f
                ? 0
                : Mathf.RoundToInt(Mathf.Lerp(2f, 7f, Mathf.InverseLerp(0.25f, 1f, Mathf.Clamp01(speed01))));

        /// <summary>Assigns the cue, stride and dust. Used by the prefab generator.</summary>
        /// <param name="cueId">Footstep cue to play.</param>
        /// <param name="strideLength">Distance between steps.</param>
        /// <param name="dustMaterial">Material for the dust a footfall lifts, or null for none.</param>
        /// <param name="dustSize">Rough width of that dust, in metres.</param>
        public void Configure(string cueId, float strideLength, Material dustMaterial = null, float dustSize = 0.35f)
        {
            _cueId = cueId;
            _strideLength = Mathf.Max(0.2f, strideLength);
            _dustMaterial = dustMaterial;
            _dustSize = dustSize;
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;

            // Primed so the first footstep lands almost as soon as the character starts moving,
            // rather than after a full silent stride.
            _accumulated = _strideLength * 0.6f;
        }

        private void Update()
        {
            _audio ??= ServiceRegistry.Current != null &&
                       ServiceRegistry.Current.TryGet(out IAudioService audio)
                ? audio
                : null;

            Vector3 position = transform.position;
            Vector3 travel = position - _lastPosition;
            travel.y = 0f;
            _lastPosition = position;

            float moved = travel.magnitude;

            // A jump larger than a stride is a teleport (a retry respawn), not a step — skip it so the
            // reset does not fire a footstep at the spawn point.
            if (moved <= 0f || moved > _strideLength)
            {
                return;
            }

            _accumulated += moved;

            if (_accumulated >= _strideLength)
            {
                _accumulated -= _strideLength;
                _audio?.PlayCue(_cueId, position);

                // Speed measured against four strides a second, roughly a run: enough to tell a walk from a
                // sprint without this component being told anything about the character it is on.
                float seconds = Time.deltaTime;
                float speed01 = seconds > Mathf.Epsilon
                    ? Mathf.Clamp01(moved / seconds / (_strideLength * 4f))
                    : 0f;

                LiftDust(position, travel, speed01);
            }
        }

        /// <summary>Lifts dust where the foot came down, drifting back along the travel.</summary>
        private void LiftDust(Vector3 position, Vector3 travel, float speed01)
        {
            int count = PuffCount(speed01);

            if (count == 0 || _dustMaterial == null)
            {
                return;
            }

            if (_dust == null)
            {
                BuildDust();
            }

            Vector3 back = travel.sqrMagnitude > 1e-6f ? -travel.normalized : Vector3.zero;

            var parameters = new ParticleSystem.EmitParams
            {
                position = new Vector3(position.x, DustHeight, position.z),
                applyShapeToPosition = false
            };

            for (int i = 0; i < count; i++)
            {
                float spread = (i / (float)count - 0.5f) * 2f;

                parameters.velocity = back * (0.5f + speed01) +
                                      new Vector3(spread * 0.4f, 0.25f + speed01 * 0.3f, spread * 0.4f);

                _dust.Emit(parameters, 1);
            }
        }

        /// <summary>
        /// Builds the one particle system every footfall emits from.
        /// </summary>
        /// <remarks>
        /// Manual emission into a permanently playing system, as the impact bursts do: restarting a system per
        /// step costs more and drops the dust still hanging from the step before.
        /// </remarks>
        private void BuildDust()
        {
            var child = new GameObject("FootDust");
            child.transform.SetParent(transform, worldPositionStays: false);

            _dust = child.AddComponent<ParticleSystem>();
            _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _dust.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.9f;
            main.startSize = _dustSize;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.38f, 0.35f, 0.3f, 0.24f));

            // Slightly buoyant: kicked grit hangs before it settles.
            main.gravityModifier = -0.02f;

            ParticleSystem.EmissionModule emission = _dust.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = _dust.shape;
            shape.enabled = false;

            ParticleSystem.SizeOverLifetimeModule size = _dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

            ParticleSystem.ColorOverLifetimeModule fade = _dust.colorOverLifetime;
            fade.enabled = true;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.LimitVelocityOverLifetimeModule drag = _dust.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 3.5f;

            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _dustMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _dust.Play();
        }
    }
}
