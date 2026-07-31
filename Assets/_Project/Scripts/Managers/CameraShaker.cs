using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Utilities.Feel;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Displaces the camera on impact, and registers itself as the game's shake sink.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Applied as a local offset on a child of the camera rig, so shake and framing never fight each
    /// other. Shaking the rig itself would have the follow easing chase the shake and smear it into
    /// a drift.
    /// </para>
    /// <para>
    /// Runs on unscaled time deliberately, unlike the follow. A hit-stop freezes the world precisely
    /// so the impact registers; a shake that froze along with it would remove the very motion that
    /// sells the impact.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CameraShaker : MonoBehaviour, IScreenShake
    {
        /// <summary>Largest field-of-view kick, so a flurry of dashes cannot balloon the view.</summary>
        private const float MaxFovKick = 10f;

        [Header("Intensity")]
        [SerializeField]
        [Range(0f, 2f)]
        [Tooltip("Peak positional offset in world units at full trauma.")]
        private float _maxTranslation = 0.55f;

        [SerializeField]
        [Range(0f, 10f)]
        [Tooltip("Peak camera roll in degrees at full trauma.")]
        private float _maxRollDegrees = 2.5f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Player-facing intensity scale. Zero disables shake entirely, which some players " +
                 "need rather than merely prefer.")]
        private float _userIntensity = 1f;

        [Header("Response")]
        [SerializeField]
        [Range(0.5f, 5f)]
        [Tooltip("Trauma removed per second. Higher values make shakes shorter and snappier.")]
        private float _decayPerSecond = 1.8f;

        [SerializeField]
        [Range(5f, 40f)]
        [Tooltip("Noise sampling rate. Higher values feel sharper and more violent.")]
        private float _frequency = 24f;

        [Header("Punch")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Largest dolly-in offset a single punch can reach, in world units.")]
        private float _maxPunch = 0.35f;

        [SerializeField]
        [Range(1f, 12f)]
        [Tooltip("How fast a punch springs back. Higher is snappier.")]
        private float _punchDecayPerSecond = 5f;

        [SerializeField]
        [Range(2f, 20f)]
        [Tooltip("How fast a field-of-view kick springs back.")]
        private float _fovDecayPerSecond = 7f;

        private TraumaShake _shake;
        private Camera _camera;
        private Vector3 _restLocalPosition;
        private float _baseFieldOfView;
        private float _punch;
        private float _fovKick;

        private void Awake()
        {
            _shake = new TraumaShake(_decayPerSecond, _frequency);
            _restLocalPosition = transform.localPosition;

            // The perspective camera the FOV kick animates. Resolved forgivingly: the shaker usually
            // carries the camera, but a rig that puts it elsewhere still works.
            _camera = GetComponent<Camera>() ?? GetComponentInChildren<Camera>() ?? Camera.main;
            if (_camera != null)
            {
                _baseFieldOfView = _camera.fieldOfView;
            }

            ServiceRegistry.Current.RegisterOrReplace<IScreenShake>(this);
        }

        private void LateUpdate()
        {
            float deltaTime = Time.unscaledDeltaTime;

            _shake.Tick(deltaTime);
            _punch = Mathf.Max(0f, _punch - _punchDecayPerSecond * deltaTime);

            // The field-of-view kick springs back toward zero from either side — faster when large,
            // with a floor so it settles exactly at rest rather than asymptotically hovering off it.
            _fovKick = Mathf.MoveTowards(
                _fovKick, 0f, _fovDecayPerSecond * (Mathf.Abs(_fovKick) + 1f) * deltaTime);

            if (_camera != null)
            {
                _camera.fieldOfView = _baseFieldOfView + _fovKick * _userIntensity;
            }

            if (!_shake.IsShaking && _punch <= 0f)
            {
                // Snapping back rather than easing avoids leaving a permanent sub-pixel offset that
                // accumulates across a long fight.
                transform.localPosition = _restLocalPosition;
                transform.localRotation = Quaternion.identity;
                return;
            }

            Vector2 translation = Vector2.zero;
            float rollDegrees = 0f;

            if (_shake.IsShaking)
            {
                _shake.Sample(
                    _maxTranslation * _userIntensity,
                    _maxRollDegrees * _userIntensity,
                    out translation,
                    out rollDegrees);
            }

            // The punch pushes along local forward (+Z), toward whatever the camera is framing, and
            // springs back on its own. It rides on the same intensity scale as shake so one
            // accessibility slider quiets both.
            float punch = _punch * _userIntensity;

            transform.localPosition = _restLocalPosition + new Vector3(translation.x, translation.y, punch);
            transform.localRotation = Quaternion.Euler(0f, 0f, rollDegrees);
        }

        /// <inheritdoc />
        public void AddTrauma(float amount)
        {
            if (_userIntensity <= 0f)
            {
                return;
            }

            _shake.AddTrauma(amount);
        }

        /// <inheritdoc />
        public void Punch(float amount)
        {
            if (_userIntensity <= 0f)
            {
                return;
            }

            _punch = Mathf.Min(_maxPunch, _punch + Mathf.Max(0f, amount));
        }

        /// <inheritdoc />
        public void PunchFov(float degrees)
        {
            if (_userIntensity <= 0f)
            {
                return;
            }

            // Clamped so a rapid string of dashes cannot balloon the view; the largest single kick
            // is what lands, not their sum.
            _fovKick = Mathf.Clamp(_fovKick + degrees, -MaxFovKick, MaxFovKick);
        }

        /// <inheritdoc />
        public void ClearTrauma()
        {
            _shake.Reset();
            _punch = 0f;
            _fovKick = 0f;

            if (_camera != null)
            {
                _camera.fieldOfView = _baseFieldOfView;
            }
        }

        /// <summary>Sets the player-facing intensity scale, from the settings menu.</summary>
        /// <param name="intensity">Scale from zero, meaning off, to one.</param>
        public void SetUserIntensity(float intensity) => _userIntensity = Mathf.Clamp01(intensity);
    }
}
