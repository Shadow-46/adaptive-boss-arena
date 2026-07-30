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

        private Light _light;
        private Color _targetColor = Color.black;
        private float _targetIntensity;
        private float _steadyIntensity;
        private float _pulse;
        private float _charge;

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

            SetPhase(0);
        }

        /// <summary>Sets the steady glow to match a phase, from calm at zero to furious at the top.</summary>
        /// <param name="phaseIndex">Zero-based phase index.</param>
        public void SetPhase(int phaseIndex)
        {
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

        /// <summary>Clears the glow back to its opening state, for a retry.</summary>
        public void ResetAura()
        {
            _pulse = 0f;
            _charge = 0f;
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

            _light.intensity = _steadyIntensity + _pulse + _charge * ChargeIntensity;
            _light.color = Color.Lerp(_light.color, _targetColor, DampFactor(EaseHalfLife, deltaTime));

            // Switched off entirely once dark, so the opening phase spends nothing on a light that
            // contributes nothing.
            _light.enabled = _light.intensity > 0.01f;
        }

        /// <summary>Frame-rate independent interpolation factor for a half-life.</summary>
        private static float DampFactor(float halfLifeSeconds, float deltaTime) =>
            1f - Mathf.Exp(-deltaTime / Mathf.Max(0.0001f, halfLifeSeconds));
    }
}
