using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.Core.Services;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Drives the post-processing volume in response to the fight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The volume profile is otherwise a fixed look. This makes it answer the moment: the frame
    /// reddens and desaturates as the player is dying, jolts on a hit, and flares with a lens punch on
    /// a clean deflect. It is the screen equivalent of the hit-stop and the shake — feedback the player
    /// feels before they read it.
    /// </para>
    /// <para>
    /// It edits <see cref="Volume.profile"/>, the per-instance copy, never the shared asset, so nothing
    /// it does here is written back to disk. The two transient jolts respect the reduced-flashing
    /// accessibility setting; the sustained low-health grade is a slow, gentle shift rather than a
    /// flash, so it stays. Everything runs on unscaled time to read through a hit-stop.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ScreenEffects : MonoBehaviour
    {
        /// <summary>Health fraction at or below which the danger grade begins.</summary>
        private const float LowHealthThreshold = 0.4f;

        /// <summary>Health fraction at which the danger grade is fully applied.</summary>
        private const float LowHealthFloor = 0.08f;

        private const float LowHealthVignetteAdd = 0.22f;
        private const float LowHealthSaturationDrop = 45f;
        private const float LowHealthEaseHalfLife = 0.35f;

        private const float DamagePulseVignette = 0.22f;
        private const float DeflectChromaticAdd = 0.55f;
        private const float DeflectBloomAdd = 0.7f;
        private const float PulseDecayPerSecond = 3.5f;

        private static readonly Color LowHealthVignetteColor = new Color(0.5f, 0.02f, 0.03f);

        [SerializeField]
        [Tooltip("The global post-processing volume whose profile is driven.")]
        private Volume _volume;

        [SerializeField]
        [Tooltip("Normalised player health, driving the low-health grade and the damage pulse.")]
        private FloatEventChannel _playerHealthChannel;

        [SerializeField]
        [Tooltip("Raised on a clean deflect, driving the lens punch.")]
        private VoidEventChannel _deflectChannel;

        private Vignette _vignette;
        private ColorAdjustments _color;
        private ChromaticAberration _aberration;
        private Bloom _bloom;

        private float _baseVignette;
        private Color _baseVignetteColor;
        private float _baseSaturation;
        private float _baseAberration;
        private float _baseBloom;

        private float _health = 1f;
        private float _lowHealthEased;
        private float _damagePulse;
        private float _deflectPunch;

        private void OnEnable()
        {
            if (_playerHealthChannel != null)
            {
                _playerHealthChannel.Raised += OnHealthChanged;
            }

            if (_deflectChannel != null)
            {
                _deflectChannel.Raised += OnDeflect;
            }
        }

        private void OnDisable()
        {
            if (_playerHealthChannel != null)
            {
                _playerHealthChannel.Raised -= OnHealthChanged;
            }

            if (_deflectChannel != null)
            {
                _deflectChannel.Raised -= OnDeflect;
            }
        }

        private void Start()
        {
            if (_volume == null || _volume.profile == null)
            {
                enabled = false;
                return;
            }

            // The per-instance profile, cloned on first access, so runtime edits never touch the asset.
            VolumeProfile profile = _volume.profile;

            profile.TryGet(out _vignette);
            profile.TryGet(out _color);
            profile.TryGet(out _aberration);
            profile.TryGet(out _bloom);

            _baseVignette = _vignette != null ? _vignette.intensity.value : 0f;
            _baseVignetteColor = _vignette != null ? _vignette.color.value : Color.black;
            _baseSaturation = _color != null ? _color.saturation.value : 0f;
            _baseAberration = _aberration != null ? _aberration.intensity.value : 0f;
            _baseBloom = _bloom != null ? _bloom.intensity.value : 0f;
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;

            _damagePulse = Mathf.Max(0f, _damagePulse - PulseDecayPerSecond * deltaTime);
            _deflectPunch = Mathf.Max(0f, _deflectPunch - PulseDecayPerSecond * deltaTime);

            float lowHealthTarget = 1f - Mathf.InverseLerp(LowHealthFloor, LowHealthThreshold, _health);
            float ease = 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.0001f, LowHealthEaseHalfLife));
            _lowHealthEased = Mathf.Lerp(_lowHealthEased, lowHealthTarget, ease);

            Apply();
        }

        /// <summary>Assigns the references. Used by the scene generator.</summary>
        /// <param name="volume">Global post-processing volume.</param>
        /// <param name="playerHealth">Normalised player health channel.</param>
        /// <param name="deflect">Clean deflect channel.</param>
        public void Bind(Volume volume, FloatEventChannel playerHealth, VoidEventChannel deflect)
        {
            _volume = volume;
            _playerHealthChannel = playerHealth;
            _deflectChannel = deflect;
        }

        private void OnHealthChanged(float normalized)
        {
            // A drop is a hit taken; the jolt is suppressed for players who have asked for less flashing.
            if (normalized < _health - Mathf.Epsilon && !FeedbackSettings.ReducedFlashing)
            {
                _damagePulse = 1f;
            }

            _health = normalized;
        }

        private void OnDeflect()
        {
            if (!FeedbackSettings.ReducedFlashing)
            {
                _deflectPunch = 1f;
            }
        }

        private void Apply()
        {
            if (_vignette != null)
            {
                _vignette.intensity.value =
                    _baseVignette + _lowHealthEased * LowHealthVignetteAdd + _damagePulse * DamagePulseVignette;

                float redward = Mathf.Clamp01(_lowHealthEased * 0.85f + _damagePulse);
                _vignette.color.value = Color.Lerp(_baseVignetteColor, LowHealthVignetteColor, redward);
            }

            if (_color != null)
            {
                _color.saturation.value = _baseSaturation - _lowHealthEased * LowHealthSaturationDrop;
            }

            if (_aberration != null)
            {
                _aberration.intensity.value = Mathf.Clamp01(_baseAberration + _deflectPunch * DeflectChromaticAdd);
            }

            if (_bloom != null)
            {
                _bloom.intensity.value = _baseBloom + _deflectPunch * DeflectBloomAdd;
            }
        }
    }
}
