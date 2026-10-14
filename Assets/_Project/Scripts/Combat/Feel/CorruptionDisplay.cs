using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Shows the corrupted knight burning through as the fight escalates: cracks opening across the plate, the seams
    /// burning hotter, the blade catching fire and embers streaming off the armour.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The model is one skinned mesh, so its armour cannot come away in pieces. The corruption shows instead as the
    /// fire inside breaking out: each phase change swaps in a glow map with a wider network of cracks, so fresh cracks
    /// split open across the plate in the flash of the transition, and the whole glow runs hotter, breathing like a
    /// banked forge. By the Last Stand the plate is more fire than iron.
    /// </para>
    /// <para>
    /// Written through a <see cref="MaterialPropertyBlock"/>, read and written back, as the hit flash does, so the two
    /// never erase each other and the shared material is never copied. Pure presentation, eased on unscaled time so a
    /// transition still reads through the hit-stop it lands in. The embers are halved where effects are reduced.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CorruptionDisplay : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

        /// <summary>How hot the seams burn in each phase, as a multiple of the opening glow.</summary>
        private static readonly float[] SeamHeatByPhase = { 1f, 1.5f, 2.2f, 3.2f };

        /// <summary>How hot the blade burns in each phase, as a multiple of the ember: cold steel at the start.</summary>
        private static readonly float[] BladeHeatByPhase = { 0f, 0.5f, 1.2f, 2f };

        /// <summary>How fast the glow breathes in each phase, in pulses a second: slow, then labouring.</summary>
        private static readonly float[] BreathRateByPhase = { 0.5f, 0.8f, 1.3f, 2f };

        /// <summary>Embers shed off the armour each second in each phase.</summary>
        private static readonly float[] EmbersPerSecondByPhase = { 0f, 6f, 16f, 32f };

        /// <summary>Plumes of heat rising off the armour each second in each phase: none until the fire is loose.</summary>
        private static readonly float[] HazePerSecondByPhase = { 0f, 0f, 7f, 14f };

        /// <summary>How far the breath swings the glow either side of its level.</summary>
        private const float BreathDepth = 0.18f;

        /// <summary>The flare a phase change adds on top, as a multiple of the glow, and how fast it dies.</summary>
        private const float TransitionFlare = 2.5f;
        private const float FlareDecayPerSecond = 1.6f;

        /// <summary>Half-life of the glow easing to a new phase's heat.</summary>
        private const float EaseHalfLife = 0.4f;

        [SerializeField]
        [Tooltip("The knight's body, whose seams burn.")]
        private Renderer _body;

        [SerializeField]
        [Tooltip("The blade's renderers, which catch fire as the fight escalates.")]
        private Renderer[] _blade = new Renderer[0];

        [SerializeField]
        [Tooltip("The glow map for each phase, each with a wider network of cracks than the last.")]
        private Texture2D[] _crackLevels = new Texture2D[0];

        [SerializeField]
        [Tooltip("The fire's colour at its opening heat, above one so it blooms.")]
        private Color _ember = new Color(2.4f, 0.55f, 0.1f);

        [SerializeField]
        [Tooltip("Additive material the embers are drawn with.")]
        private Material _emberMaterial;

        [SerializeField]
        [Tooltip("Distortion material the heat haze is drawn with, or none.")]
        private Material _hazeMaterial;

        private ParticleSystem _haze;
        private bool _hazeAllowed;

        private MaterialPropertyBlock _block;
        private ParticleSystem _embers;
        private int _phase;
        private float _heat = 1f;
        private float _bladeHeat;
        private float _flare;
        private float _breathClock;

        /// <summary>The phase the corruption is showing.</summary>
        public int Phase => _phase;

        /// <summary>Assigns what burns. Used by the prefab generator.</summary>
        /// <param name="body">The knight's body.</param>
        /// <param name="blade">The blade's renderers.</param>
        /// <param name="crackLevels">A glow map per phase.</param>
        /// <param name="ember">The fire's opening colour.</param>
        /// <param name="emberMaterial">Additive material for the embers.</param>
        /// <param name="hazeMaterial">Distortion material for the heat haze, or null for none.</param>
        public void Bind(
            Renderer body, Renderer[] blade, Texture2D[] crackLevels, Color ember, Material emberMaterial,
            Material hazeMaterial = null)
        {
            _body = body;
            _blade = blade ?? new Renderer[0];
            _crackLevels = crackLevels ?? new Texture2D[0];
            _ember = ember;
            _emberMaterial = emberMaterial;
            _hazeMaterial = hazeMaterial;
        }

        /// <summary>Shows a phase. A step up flares and opens the next cracks; a reset snaps back to the start.</summary>
        /// <param name="phaseIndex">The boss's phase, from zero.</param>
        public void SetPhase(int phaseIndex)
        {
            int phase = Mathf.Clamp(phaseIndex, 0, SeamHeatByPhase.Length - 1);

            if (phase > _phase)
            {
                _flare = TransitionFlare;
            }
            else if (phase < _phase)
            {
                // A retry: start cold, with nothing left burning from the last attempt.
                _heat = SeamHeatByPhase[phase];
                _bladeHeat = BladeHeatByPhase[phase];
                _flare = 0f;
            }

            _phase = phase;
            ApplyEmbers();
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            BuildEmbers();
            BuildHaze();
            SetPhase(0);
            Apply(1f);
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            float t = 1f - Mathf.Exp(-deltaTime / EaseHalfLife);

            _heat = Mathf.Lerp(_heat, SeamHeatByPhase[_phase], t);
            _bladeHeat = Mathf.Lerp(_bladeHeat, BladeHeatByPhase[_phase], t);
            _flare = Mathf.Max(0f, _flare - FlareDecayPerSecond * deltaTime);
            _breathClock += deltaTime * BreathRateByPhase[_phase];

            // The quality can change mid-fight; the haze follows it on the next frame.
            if (_haze != null && EffectBudget.AllowsHeatHaze != _hazeAllowed)
            {
                ApplyEmbers();
            }

            // Perlin rather than a sine, so the breath is uneven, like a fire rather than a metronome.
            float breath = 1f + BreathDepth * (Mathf.PerlinNoise(_breathClock, 0.37f) * 2f - 1f);

            Apply(breath);
        }

        private void Apply(float breath)
        {
            if (_body != null)
            {
                _body.GetPropertyBlock(_block);
                _block.SetColor(EmissionColorId, _ember * ((_heat + _flare) * breath));

                if (_phase < _crackLevels.Length && _crackLevels[_phase] != null)
                {
                    _block.SetTexture(EmissionMapId, _crackLevels[_phase]);
                }

                _body.SetPropertyBlock(_block);
            }

            foreach (Renderer blade in _blade)
            {
                if (blade == null)
                {
                    continue;
                }

                blade.GetPropertyBlock(_block);
                _block.SetColor(EmissionColorId, _ember * ((_bladeHeat + _flare * 0.5f) * breath));
                blade.SetPropertyBlock(_block);
            }
        }

        /// <summary>Embers shed from the armour's own surface, so they rise off the whole knight and not one point.</summary>
        private void BuildEmbers()
        {
            if (!(_body is SkinnedMeshRenderer skin) || _emberMaterial == null)
            {
                return;
            }

            var emberObject = new GameObject("CorruptionEmbers");
            emberObject.transform.SetParent(transform, false);
            _embers = emberObject.AddComponent<ParticleSystem>();
            _embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _embers.main;
            main.loop = true;
            main.playOnAwake = true;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(_ember, _ember * 0.6f);
            main.gravityModifier = -0.15f;
            main.maxParticles = 160;

            ParticleSystem.ShapeModule shape = _embers.shape;
            shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
            shape.skinnedMeshRenderer = skin;

            ParticleSystem.ColorOverLifetimeModule fade = _embers.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.3f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            ParticleSystem.NoiseModule noise = _embers.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.8f;

            var renderer = emberObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _emberMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _embers.Play();
        }

        private void ApplyEmbers()
        {
            if (_embers != null)
            {
                ParticleSystem.EmissionModule emission = _embers.emission;
                float rate = EmbersPerSecondByPhase[_phase];
                emission.rateOverTime = EffectBudget.IsReduced ? rate * 0.5f : rate;
            }

            if (_haze != null)
            {
                _hazeAllowed = EffectBudget.AllowsHeatHaze;
                ParticleSystem.EmissionModule haze = _haze.emission;
                haze.rateOverTime = _hazeAllowed ? HazePerSecondByPhase[_phase] : 0f;
            }
        }

        /// <summary>
        /// Heat rising off the armour once the fire is loose, bending the air behind the knight.
        /// </summary>
        /// <remarks>
        /// Soft discs rising from the body's surface, each bending what stands behind it a little and fading as it
        /// climbs. Drawn only where <see cref="EffectBudget.AllowsHeatHaze"/>: elsewhere the scene copy it bends
        /// does not exist.
        /// </remarks>
        private void BuildHaze()
        {
            if (!(_body is SkinnedMeshRenderer skin) || _hazeMaterial == null)
            {
                return;
            }

            var hazeObject = new GameObject("CorruptionHaze");
            hazeObject.transform.SetParent(transform, false);
            _haze = hazeObject.AddComponent<ParticleSystem>();
            _haze.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _haze.main;
            main.loop = true;
            main.playOnAwake = true;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startColor = Color.white;
            main.gravityModifier = -0.12f;
            main.maxParticles = 40;

            ParticleSystem.ShapeModule shape = _haze.shape;
            shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
            shape.skinnedMeshRenderer = skin;

            ParticleSystem.ColorOverLifetimeModule fade = _haze.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = hazeObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _hazeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _haze.Play();
        }
    }
}
