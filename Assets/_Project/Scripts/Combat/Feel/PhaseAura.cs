using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// A coloured glow that intensifies as a fight escalates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A phase change on a primitive is otherwise invisible — the capsule looks identical whether the
    /// boss is measured or desperate. This lights the boss from within, cool and dark at the start,
    /// hotter and angrier with each phase, so the escalation is something the player sees on the
    /// character rather than only infers from being hit harder.
    /// </para>
    /// <para>
    /// A real light rather than a material tint, on purpose: it stacks with nothing (so it never
    /// fights the hit-flash for the base colour), spills onto the floor around the boss, and picks up
    /// the bloom in the post-processing volume for free. Everything eases on unscaled time so the
    /// pulse at a transition still reads through the hit-stop it lands in.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PhaseAura : MonoBehaviour
    {
        /// <summary>Local height the glow sits at, roughly the boss's centre of mass.</summary>
        private const float LightHeight = 1.5f;

        /// <summary>Reach of the glow in world units.</summary>
        private const float LightRange = 7f;

        /// <summary>How fast the steady glow eases toward a new phase's level.</summary>
        private const float EaseHalfLife = 0.25f;

        /// <summary>How fast a transition pulse springs back.</summary>
        private const float PulseDecayPerSecond = 3.5f;

        /// <summary>Extra intensity a transition pulse adds on top of the steady glow.</summary>
        private const float PulseIntensity = 7f;

        /// <summary>Extra intensity a fully-charged gambit adds, so the boss visibly winds up.</summary>
        private const float ChargeIntensity = 6f;

        /// <summary>Extra intensity while the parry window is open.</summary>
        private const float ParryIntensity = 5f;

        /// <summary>
        /// The colour the aura snaps to while a parry window is open.
        /// </summary>
        /// <remarks>
        /// Cold and unlike anything else the boss does, all of which is hot. The distinction has to
        /// survive being seen for a sixth of a second in peripheral vision, which rules out anything
        /// that reads as a brighter version of the ordinary glow.
        /// </remarks>
        private static readonly Color ParryColor = new Color(0.55f, 0.85f, 1.6f);

        private Light _light;
        private Color _targetColor = Color.black;
        private float _targetIntensity;
        private float _steadyIntensity;
        private float _pulse;
        private float _charge;
        private bool _parryWindowOpen;

        [SerializeField]
        [Tooltip("Additive material the embers rising off the boss are drawn with. Assigned by the prefab generator.")]
        private Material _emberMaterial;

        private ParticleSystem _embers;

        /// <summary>Embers per second the boss sheds in each phase, from none while calm to a stream in its last.</summary>
        private static readonly float[] EmbersPerSecondByPhase = { 0f, 4f, 10f, 20f };

        /// <summary>Assigns the ember material. Used by the prefab generator.</summary>
        /// <param name="material">Additive spark material.</param>
        public void SetEmberMaterial(Material material) => _emberMaterial = material;

        private void Awake()
        {
            var lightObject = new GameObject("Aura");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, LightHeight, 0f);

            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = LightRange;
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
            _light.enabled = false;

            BuildEmbers(lightObject.transform);
            SetPhase(0);
        }

        /// <summary>
        /// Builds the embers that rise off the boss as its fury grows.
        /// </summary>
        /// <remarks>
        /// A light alone lit the floor around the boss but put nothing on the boss itself. Embers drifting up
        /// from its core make each escalation visible on the body, and a stream of them in the last phase is the
        /// "aura" the player asked for. Halved in a browser, where every particle is paid for on weak graphics.
        /// </remarks>
        private void BuildEmbers(Transform parent)
        {
            if (_emberMaterial == null)
            {
                return;
            }

            var embersObject = new GameObject("Embers");
            embersObject.transform.SetParent(parent, false);

            _embers = embersObject.AddComponent<ParticleSystem>();
            _embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _embers.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.055f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.15f), new Color(1f, 0.25f, 0.08f));
            main.gravityModifier = -0.12f;

            ParticleSystem.EmissionModule emission = _embers.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = _embers.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.45f;

            ParticleSystem.NoiseModule noise = _embers.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.8f;

            ParticleSystem.ColorOverLifetimeModule fade = _embers.colorOverLifetime;
            fade.enabled = true;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = embersObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _emberMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _embers.Play();
        }

        /// <summary>Sets the steady glow to match a phase, from calm at zero to furious at the top.</summary>
        /// <param name="phaseIndex">Zero-based phase index.</param>
        public void SetPhase(int phaseIndex)
        {
            if (_embers != null)
            {
                float rate = EmbersPerSecondByPhase[Mathf.Clamp(phaseIndex, 0, EmbersPerSecondByPhase.Length - 1)];
                ParticleSystem.EmissionModule emission = _embers.emission;
                emission.rateOverTime = EffectBudget.IsWebPlayer ? rate * 0.5f : rate;
            }

            switch (phaseIndex)
            {
                case 0:
                    _targetColor = Color.black;
                    _targetIntensity = 0f;
                    break;

                case 1:
                    _targetColor = new Color(1f, 0.5f, 0.2f);
                    _targetIntensity = 3f;
                    break;

                case 2:
                    _targetColor = new Color(1f, 0.2f, 0.15f);
                    _targetIntensity = 6f;
                    break;

                default:
                    // Last Stand and beyond: a hotter, blown-out crimson so the desperation phase
                    // burns visibly brighter than the merely relentless one before it.
                    _targetColor = new Color(1f, 0.28f, 0.4f);
                    _targetIntensity = 8.5f;
                    break;
            }
        }

        /// <summary>Flares the glow briefly, for a phase transition or a moment of adaptation.</summary>
        public void Pulse() => _pulse = PulseIntensity;

        /// <summary>
        /// Sets a steady charge glow, zero to one, so a filling gambit meter is visible on the boss.
        /// </summary>
        /// <remarks>
        /// The gambit's legibility. An attack the player cannot see coming is indistinguishable from
        /// the boss cheating, so the meter's approach to full is shown as the boss winding up brighter
        /// and brighter before it erupts.
        /// </remarks>
        /// <param name="normalized">How full the gambit meter is, zero to one.</param>
        public void SetCharge(float normalized) => _charge = Mathf.Clamp01(normalized);

        /// <summary>
        /// Shows or hides the parry window on the boss.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A term of its own rather than a use of <see cref="SetCharge"/>, which the gambit meter
        /// owns: borrowing it would make the wind-up glow lie about how full the meter is.
        /// </para>
        /// <para>
        /// The obligation runs both ways. The boss is told a guard is raised but never whether the
        /// player's deflect window is open; the player's channel is the screen, so the boss's window
        /// has to be visible there or a stance that refuses hits is indistinguishable from one that
        /// refuses them at random. What must be shown is the window alone - the punishable tail
        /// looks like the boss standing still, which is what it is.
        /// </para>
        /// </remarks>
        /// <param name="isOpen">Whether a hit arriving now would be refused.</param>
        public void SetParryWindow(bool isOpen) => _parryWindowOpen = isOpen;

        /// <summary>Clears the glow back to its opening state, for a retry.</summary>
        public void ResetAura()
        {
            _pulse = 0f;
            _charge = 0f;
            _parryWindowOpen = false;
            _steadyIntensity = 0f;
            SetPhase(0);

            if (_light != null)
            {
                _light.intensity = 0f;
                _light.color = _targetColor;
                _light.enabled = false;
            }
        }

        private void Update()
        {
            if (_light == null)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;

            // The steady glow eases toward the phase's level; the pulse rides on top and springs back
            // on its own. Keeping them separate means a transition flare never disturbs where the
            // steady glow is heading.
            _pulse = Mathf.Max(0f, _pulse - PulseDecayPerSecond * deltaTime);
            _steadyIntensity = Mathf.Lerp(
                _steadyIntensity, _targetIntensity, DampFactor(EaseHalfLife, deltaTime));

            _light.intensity = _steadyIntensity + _pulse + _charge * ChargeIntensity +
                               (_parryWindowOpen ? ParryIntensity : 0f);

            // Snapped, not eased. Every other colour change here is a slow escalation the player has
            // seconds to notice; this one has to be legible inside a window shorter than the ease's
            // own half-life, so easing it would show the player a colour that had barely started
            // moving by the time the window shut.
            _light.color = _parryWindowOpen
                ? ParryColor
                : Color.Lerp(_light.color, _targetColor, DampFactor(EaseHalfLife, deltaTime));

            // Switched off entirely once dark, so the opening phase spends nothing on a light that
            // contributes nothing.
            _light.enabled = _light.intensity > 0.01f;
        }

        /// <summary>Frame-rate independent interpolation factor for a half-life.</summary>
        private static float DampFactor(float halfLifeSeconds, float deltaTime) =>
            1f - Mathf.Exp(-deltaTime / Mathf.Max(0.0001f, halfLifeSeconds));
    }
}
