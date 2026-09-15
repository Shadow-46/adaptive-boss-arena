using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Frames the fight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rewritten after the first version felt, in the player's words, "either too fast or too slow
    /// according to my movement". The cause was that it followed a point blended thirty percent
    /// toward the boss, so the camera moved whenever the <em>boss</em> moved. Motion the player did
    /// not cause reads as the camera fighting them, however smooth the easing is.
    /// </para>
    /// <para>
    /// The rule now: in free look the camera answers the player and nobody else. The boss only
    /// influences framing when the player has explicitly asked for it by locking on.
    /// </para>
    /// <para>
    /// Three further things make it feel attached rather than dragged:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// A deadzone, so small adjustments move the character within the frame instead of hauling the
    /// whole view around.
    /// </description></item>
    /// <item><description>
    /// Lookahead in the direction of travel, so the player sees where they are going rather than
    /// where they have been.
    /// </description></item>
    /// <item><description>
    /// Separate position and rotation damping. Sharing one rate makes rotation feel sluggish
    /// whenever position is tuned to feel weighty.
    /// </description></item>
    /// </list>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ArenaCameraRig : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Arena configuration supplying the top-down framing values.")]
        private ArenaConfig _config;

        [Header("Targets")]
        [SerializeField]
        [Tooltip("The player. The camera answers this and, unless locked on, only this.")]
        private Transform _primaryTarget;

        [SerializeField]
        [Tooltip("The boss. Only influences framing while locked on.")]
        private Transform _secondaryTarget;

        [Header("Mode")]
        [SerializeField]
        [Tooltip("Vantage point the camera starts in.")]
        private CameraMode _mode = CameraMode.ThirdPerson;

        [SerializeField]
        [Tooltip("Key that cycles through the camera modes.")]
        private Key _cycleModeKey = Key.C;

        [SerializeField]
        [Tooltip("Key that toggles lock-on. Weapon swap deliberately lives elsewhere.")]
        private Key _lockOnKey = Key.Tab;

        [SerializeField]
        [Tooltip("Whether lock-on engages automatically at the start of a fight.")]
        private bool _lockOnByDefault = true;

        [Header("Third Person")]
        [SerializeField]
        [Range(2f, 12f)]
        [Tooltip("Distance behind the player.")]
        private float _distance = 6f;

        [SerializeField]
        [Range(1f, 6f)]
        [Tooltip("Height above the player.")]
        private float _height = 2.8f;

        [SerializeField]
        [Range(0f, 40f)]
        [Tooltip("Downward pitch.")]
        private float _pitchDegrees = 16f;

        [Header("Feel")]
        [SerializeField]
        [Range(0.02f, 0.6f)]
        [Tooltip("Half-life of positional follow. Lower is tighter and more responsive.")]
        private float _positionHalfLife = 0.09f;

        [SerializeField]
        [Range(0.02f, 0.6f)]
        [Tooltip("Half-life of rotational follow. Kept faster than position so aiming stays crisp.")]
        private float _rotationHalfLife = 0.06f;

        [SerializeField]
        [Range(0f, 2f)]
        [Tooltip("Radius the target may move within before the camera follows at all.")]
        private float _deadzoneRadius = 0.6f;

        [SerializeField]
        [Range(0f, 3f)]
        [Tooltip("How far ahead of the player's motion the camera leads.")]
        private float _lookaheadDistance = 1.4f;

        [Header("First Person")]
        [SerializeField]
        [Range(1f, 2.5f)]
        [Tooltip("Eye height above the player's origin.")]
        private float _eyeHeight = 1.6f;

        private ITimeService _time;
        private Vector3 _anchor;
        private Vector3 _smoothedLookahead;

        /// <summary>How much of the boom the arena wall took away this frame, in metres.</summary>
        private float _boomLoss;

        /// <summary>
        /// Distance kept between the camera and the wall ring. Enough that the near clip plane never
        /// touches the parapet, so the view is never half stone.
        /// </summary>
        private const float WallClearance = 0.6f;

        /// <summary>Metres the camera rises for each metre of boom the wall takes away, to look down over the player.</summary>
        private const float RisePerLostMetre = 0.45f;

        /// <summary>Extra degrees of pitch at a fully collapsed boom, so a raised camera still frames the player.</summary>
        private const float PitchAtCollapsedBoom = 32f;
        private bool _isLockedOn;

        /// <summary>The vantage point currently in use.</summary>
        public CameraMode Mode => _mode;

        /// <summary>True while the camera is holding the boss in frame.</summary>
        public bool IsLockedOn => _isLockedOn && _secondaryTarget != null;

        private void Start()
        {
            if (_config == null)
            {
                Debug.LogError(
                    "[Adaptive Boss Arena] ArenaCameraRig has no ArenaConfig assigned and has been disabled.",
                    this);
                enabled = false;
                return;
            }

            ServiceRegistry.Current.TryGet(out _time);

            if (_primaryTarget != null)
            {
                _anchor = _primaryTarget.position;
            }

            // Locked on from the start. This is a duel against a single opponent, so the framing
            // that keeps both fighters visible is the right default rather than something the
            // player has to discover.
            _isLockedOn = _lockOnByDefault;

            SnapToTarget();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (keyboard[_cycleModeKey].wasPressedThisFrame)
            {
                CycleMode();
            }

            if (keyboard[_lockOnKey].wasPressedThisFrame)
            {
                _isLockedOn = !_isLockedOn;
            }
        }

        private void LateUpdate()
        {
            if (_primaryTarget == null)
            {
                return;
            }

            float deltaTime = _time?.DeltaTime ?? Time.deltaTime;

            UpdateAnchor(deltaTime);

            if (_mode == CameraMode.FirstPerson)
            {
                // Snapped, because any lag between the head and the camera in first person reads as
                // the world sliding rather than as smoothing.
                transform.SetPositionAndRotation(DesiredPosition(), DesiredRotation());
                return;
            }

            transform.position = MathUtil.Damp(
                transform.position, DesiredPosition(), _positionHalfLife, deltaTime);

            transform.rotation = Quaternion.Slerp(
                transform.rotation, DesiredRotation(), DampFactor(_rotationHalfLife, deltaTime));
        }

        /// <summary>Places the camera at its framing position immediately.</summary>
        public void SnapToTarget()
        {
            if (_primaryTarget == null)
            {
                return;
            }

            _anchor = _primaryTarget.position;
            _smoothedLookahead = Vector3.zero;

            transform.SetPositionAndRotation(DesiredPosition(), DesiredRotation());
        }

        /// <summary>Switches vantage point and reframes immediately.</summary>
        /// <param name="mode">Mode to adopt.</param>
        public void SetMode(CameraMode mode)
        {
            _mode = mode;
            SnapToTarget();
        }

        /// <summary>Advances to the next vantage point in order.</summary>
        public void CycleMode() =>
            SetMode(_mode == CameraMode.FirstPerson ? CameraMode.TopDown : _mode + 1);

        /// <summary>Assigns the framing targets. Used by the scene generator.</summary>
        /// <param name="primary">The player.</param>
        /// <param name="secondary">The boss. May be null.</param>
        public void SetTargets(Transform primary, Transform secondary)
        {
            _primaryTarget = primary;
            _secondaryTarget = secondary;
        }

        /// <summary>Assigns the configuration. Used by the scene generator.</summary>
        /// <param name="config">Arena configuration.</param>
        public void SetConfig(ArenaConfig config) => _config = config;

        /// <summary>
        /// Advances the anchor the camera actually frames, applying deadzone and lookahead.
        /// </summary>
        /// <remarks>
        /// The anchor trails the player rather than being the player. Inside the deadzone it does
        /// not move at all, which is what stops small corrections from dragging the whole view and
        /// is most of the difference between a camera that feels attached and one that feels towed.
        /// </remarks>
        private void UpdateAnchor(float deltaTime)
        {
            Vector3 targetPosition = _primaryTarget.position;
            Vector3 offset = targetPosition - _anchor;
            offset.y = 0f;

            float distance = offset.magnitude;

            if (distance > _deadzoneRadius)
            {
                // Pulled only as far as the deadzone edge, so the target sits exactly on the
                // boundary rather than being re-centred.
                _anchor += offset.normalized * (distance - _deadzoneRadius);
            }

            _anchor.y = targetPosition.y;

            Vector3 lookahead = _primaryTarget.forward * _lookaheadDistance;
            _smoothedLookahead = MathUtil.Damp(
                _smoothedLookahead, lookahead, _positionHalfLife * 2f, deltaTime);
        }

        /// <summary>Where the camera wants to be for the current vantage point.</summary>
        private Vector3 DesiredPosition()
        {
            switch (_mode)
            {
                case CameraMode.FirstPerson:
                    return _primaryTarget.position + Vector3.up * _eyeHeight;

                case CameraMode.TopDown:
                    return FocusPoint()
                           + new Vector3(0f, _config.CameraHeight, -_config.CameraDistance);

                default:
                    Vector3 focus = FocusPoint();
                    Vector3 desired = focus - OrbitForward() * _distance + Vector3.up * _height;

                    // Knockback and wall shoves pin the player against the ring all the time now, and the
                    // boom then put the camera outside it, looking at the back of the wall. Kept inside,
                    // it rises for what it lost and looks over the player instead.
                    Vector3 confined = ConfineToArena(desired, focus, _config.Radius - WallClearance, out _boomLoss);
                    confined.y += _boomLoss * RisePerLostMetre;

                    return confined;
            }
        }

        /// <summary>Orientation for the current vantage point.</summary>
        private Quaternion DesiredRotation()
        {
            switch (_mode)
            {
                case CameraMode.FirstPerson:
                    return Quaternion.LookRotation(OrbitForward(), Vector3.up);

                case CameraMode.TopDown:
                    return Quaternion.Euler(_config.CameraPitchDegrees, 0f, 0f);

                default:
                    float collapsed = _distance > 0f ? Mathf.Clamp01(_boomLoss / _distance) : 0f;
                    return OrbitRotation(OrbitForward(), _pitchDegrees + PitchAtCollapsedBoom * collapsed);
            }
        }

        /// <summary>
        /// Pulls a camera position that has left the arena back inside, along the line from what it frames.
        /// </summary>
        /// <remarks>
        /// Along the boom rather than straight toward the centre, so the camera keeps looking the way it was
        /// and only comes closer. Height is left alone: how much to rise for the lost distance is framing, and
        /// belongs to the caller. Horizontal only, because the wall is a vertical ring.
        /// </remarks>
        /// <param name="desired">Where the camera wants to be.</param>
        /// <param name="focus">The point it frames, assumed inside the ring.</param>
        /// <param name="radius">Horizontal radius the camera must stay within.</param>
        /// <param name="lost">How much of the horizontal boom was taken away, in metres.</param>
        /// <returns>The confined position.</returns>
        public static Vector3 ConfineToArena(Vector3 desired, Vector3 focus, float radius, out float lost)
        {
            lost = 0f;

            var point = new Vector2(desired.x, desired.z);

            if (point.sqrMagnitude <= radius * radius)
            {
                return desired;
            }

            var from = new Vector2(focus.x, focus.z);
            Vector2 boom = point - from;

            // Where the boom leaves the circle: |from + t * boom| = radius, taking the exit root.
            float a = Vector2.Dot(boom, boom);
            float b = 2f * Vector2.Dot(from, boom);
            float c = Vector2.Dot(from, from) - radius * radius;
            float discriminant = b * b - 4f * a * c;

            if (a <= Mathf.Epsilon || discriminant < 0f || c > 0f)
            {
                // The framed point is itself outside the ring; the best available is the nearest point on it.
                Vector2 onRing = point.normalized * radius;
                lost = Vector2.Distance(point, onRing);
                return new Vector3(onRing.x, desired.y, onRing.y);
            }

            float t = Mathf.Clamp01((-b + Mathf.Sqrt(discriminant)) / (2f * a));
            Vector2 kept = from + boom * t;

            lost = boom.magnitude * (1f - t);

            return new Vector3(kept.x, desired.y, kept.y);
        }

        /// <summary>The third-person orientation: looking along a heading, tipped down by a pitch.</summary>
        /// <remarks>
        /// The pitch is applied in the camera's own frame, after the heading. It used to be applied first,
        /// about the world's X axis, which is a pitch only while looking along Z: looking along X the same
        /// rotation rolls the view, so the horizon tipped over whenever the boss was off to one side.
        /// </remarks>
        /// <param name="heading">Horizontal direction to look along.</param>
        /// <param name="pitchDegrees">How far to look down, in degrees.</param>
        /// <returns>The camera's rotation.</returns>
        public static Quaternion OrbitRotation(Vector3 heading, float pitchDegrees) =>
            Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(pitchDegrees, 0f, 0f);

        /// <summary>
        /// The direction the camera looks along.
        /// </summary>
        /// <remarks>
        /// Locked on, this points from the player to the boss, which is what makes a duel read as a
        /// duel. Free, it follows the player's own facing and nothing else.
        /// </remarks>
        private Vector3 OrbitForward()
        {
            if (IsLockedOn)
            {
                Vector3 toBoss = _secondaryTarget.position - _primaryTarget.position;
                toBoss.y = 0f;

                if (toBoss.sqrMagnitude > Mathf.Epsilon)
                {
                    return toBoss.normalized;
                }
            }

            Vector3 facing = _primaryTarget.forward;
            facing.y = 0f;

            return facing.sqrMagnitude > Mathf.Epsilon ? facing.normalized : Vector3.forward;
        }

        /// <summary>
        /// The point being framed.
        /// </summary>
        /// <remarks>
        /// Only shifts toward the boss while locked on. In free look this is the anchor alone, which
        /// is the fix for the camera moving in response to the boss rather than the player.
        /// </remarks>
        private Vector3 FocusPoint()
        {
            Vector3 focus = _anchor + _smoothedLookahead;

            if (IsLockedOn)
            {
                focus = Vector3.Lerp(focus, _secondaryTarget.position, _config.CameraBiasTowardBoss);
            }

            focus.y = _anchor.y;
            return focus;
        }

        /// <summary>Frame-rate independent interpolation factor for a half-life.</summary>
        private static float DampFactor(float halfLifeSeconds, float deltaTime) =>
            1f - Mathf.Exp(-deltaTime / Mathf.Max(0.0001f, halfLifeSeconds));
    }
}
