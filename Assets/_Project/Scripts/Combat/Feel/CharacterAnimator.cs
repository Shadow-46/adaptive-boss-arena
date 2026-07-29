using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Utilities;
using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Brings a primitive character to life with procedural, code-driven animation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lives on the character's <em>visual</em> root — the child that also carries
    /// <see cref="HitFlash"/> and the weapon trail — and eases that transform's local position,
    /// rotation and scale toward a pose chosen from the character's current action and the phase of
    /// any attack in flight. The collider and hurtbox live on the character root, one level up, so
    /// nothing here moves a hitbox: a lunge is entirely cosmetic and the fight plays out on the
    /// unanimated root exactly as the tests describe it.
    /// </para>
    /// <para>
    /// It is a pure consumer. The owning controller pushes state in each frame through
    /// <see cref="SetMotionState"/> and reports impacts through <see cref="Recoil"/>; the animator
    /// never reaches back, never reads input, and — crucially for the boss — never tells the AI
    /// anything. Presentation only.
    /// </para>
    /// <para>
    /// Everything is eased on <see cref="Time.unscaledDeltaTime"/> so a lunge holds and reads through
    /// the hit-stop it triggers rather than freezing mid-air with the world.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimator : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Animation magnitudes. Assigned by the prefab generator; without it the character " +
                 "simply stands still.")]
        private CharacterAnimationConfig _config;

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private Vector3 _restScale;

        private ObservableActionState _state = ObservableActionState.Idle;
        private AttackPhase _attackPhase = AttackPhase.Inactive;
        private float _planarSpeed01;

        private float _bobTime;

        private Vector3 _recoilLocalDir;
        private float _recoilRemaining;

        private float _flourishRemaining;

        /// <summary>
        /// The optional skeletal bridge beside this animator. When it is driving a real rig, this
        /// procedural layer steps back to additive impact only so the two never fight over the pose.
        /// </summary>
        private CharacterAnimationBridge _bridge;

        private void Awake()
        {
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;
            _restScale = transform.localScale;

            // Cached rather than searched each frame: art is added by regenerating the prefab, so the
            // bridge (and any rig it finds) is fixed for the lifetime of the character.
            _bridge = GetComponent<CharacterAnimationBridge>();
        }

        /// <summary>True while a skeletal rig, not this procedural layer, owns the base pose.</summary>
        private bool SkeletonDrivesPose => _bridge != null && _bridge.HasSkeleton;

        /// <summary>Assigns the tuning asset. Used by the prefab generator.</summary>
        /// <param name="config">Animation magnitudes.</param>
        public void SetConfig(CharacterAnimationConfig config) => _config = config;

        /// <summary>
        /// Pushes the character's visible state for this frame.
        /// </summary>
        /// <remarks>
        /// Called by the owning controller once per tick. The animator holds the last value it was
        /// given, so a paused controller simply freezes the pose.
        /// </remarks>
        /// <param name="state">The action a watcher would see.</param>
        /// <param name="attackPhase">Phase of any attack in flight, driving the wind-up and lunge.</param>
        /// <param name="planarSpeed01">Horizontal speed as a fraction of top speed, for the run lean.</param>
        public void SetMotionState(ObservableActionState state, AttackPhase attackPhase, float planarSpeed01)
        {
            _state = state;
            _attackPhase = attackPhase;
            _planarSpeed01 = Mathf.Clamp01(planarSpeed01);

            // Forwarded so both presentation layers ride one push from the controller. No-op until a
            // rig is present.
            _bridge?.SetMotionState(state, attackPhase, planarSpeed01);
        }

        /// <summary>Throws the body along an incoming blow.</summary>
        /// <param name="worldHitDirection">Direction the blow travels, from attacker toward target.</param>
        public void Recoil(Vector3 worldHitDirection)
        {
            if (_config == null || transform.parent == null)
            {
                return;
            }

            // Converted into the root's local space so the recoil is applied in the same frame as the
            // body's facing, then flattened so a hit never launches the character off the floor.
            Vector3 local = transform.parent.InverseTransformDirection(worldHitDirection);
            local.y = 0f;

            _recoilLocalDir = MathUtil.SafeNormalize(local);
            _recoilRemaining = _config.RecoilDurationSeconds;

            // The additive shove reads on a rigged model too, so it is kept even when the skeleton
            // owns the pose; the rig's own Hit clip plays alongside it.
            _bridge?.Recoil();
        }

        /// <summary>Rears the body up for a phase transition or other flourish.</summary>
        public void Flourish()
        {
            if (_config != null)
            {
                _flourishRemaining = _config.FlourishDurationSeconds;
            }
        }

        /// <summary>Snaps the pose back to rest and clears any impulses, for a retry.</summary>
        public void ResetPose()
        {
            _recoilRemaining = 0f;
            _flourishRemaining = 0f;
            _state = ObservableActionState.Idle;
            _attackPhase = AttackPhase.Inactive;

            transform.localPosition = _restPosition;
            transform.localRotation = _restRotation;
            transform.localScale = _restScale;

            _bridge?.ResetState();
        }

        private void LateUpdate()
        {
            if (_config == null)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            _bobTime += deltaTime;

            AdvanceImpulses(deltaTime);

            ComposeTarget(out Vector3 targetPosition, out Quaternion targetRotation, out Vector3 targetScale);

            transform.localPosition = MathUtil.Damp(
                transform.localPosition, targetPosition, _config.PositionHalfLife, deltaTime);
            transform.localScale = MathUtil.Damp(
                transform.localScale, targetScale, _config.ScaleHalfLife, deltaTime);
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, targetRotation, DampFactor(_config.RotationHalfLife, deltaTime));
        }

        /// <summary>Decays the transient recoil and flourish envelopes.</summary>
        private void AdvanceImpulses(float deltaTime)
        {
            _recoilRemaining = Mathf.Max(0f, _recoilRemaining - deltaTime);
            _flourishRemaining = Mathf.Max(0f, _flourishRemaining - deltaTime);
        }

        /// <summary>Builds the full target pose from the base state plus any active impulses.</summary>
        private void ComposeTarget(out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            BasePose(out Vector3 offset, out float leanDegrees, out Vector3 scaleFactor);

            // Recoil and flourish are additive on top of whatever the character is doing, so being
            // hit mid-swing throws the swing rather than replacing it.
            if (_recoilRemaining > 0f)
            {
                float envelope = _recoilRemaining / _config.RecoilDurationSeconds;
                offset += _recoilLocalDir * (_config.RecoilDistance * envelope);
                scaleFactor = Vector3.Scale(scaleFactor, Squash(_config.RecoilSquash * envelope));
            }

            if (_flourishRemaining > 0f)
            {
                float envelope = _flourishRemaining / _config.FlourishDurationSeconds;
                leanDegrees -= _config.FlourishRearDegrees * envelope;
                scaleFactor = Vector3.Scale(scaleFactor, Stretch(_config.FlourishStretch * envelope));
            }

            position = _restPosition + offset;
            rotation = _restRotation * Quaternion.Euler(leanDegrees, 0f, IdleSway());
            scale = Vector3.Scale(_restScale, scaleFactor);
        }

        /// <summary>Chooses the resting-state pose for the current action.</summary>
        /// <remarks>
        /// Returns a local-space offset, a pitch lean in degrees (positive tips the body forward,
        /// toward the direction it faces), and a multiplicative scale factor for squash and stretch.
        /// </remarks>
        private void BasePose(out Vector3 offset, out float leanDegrees, out Vector3 scaleFactor)
        {
            offset = Vector3.zero;
            leanDegrees = 0f;
            scaleFactor = Vector3.one;

            // When a rig owns the body, the base pose stays neutral and only the additive recoil and
            // flourish layered on in ComposeTarget survive — extra impact weight, not a rival pose.
            if (SkeletonDrivesPose)
            {
                return;
            }

            switch (_state)
            {
                case ObservableActionState.LightAttacking:
                case ObservableActionState.HeavyAttacking:
                case ObservableActionState.UsingAbility:
                    AttackPose(out offset, out leanDegrees, out scaleFactor);
                    break;

                case ObservableActionState.Dashing:
                    leanDegrees = _config.DashLeanDegrees;
                    scaleFactor = Stretch(_config.DashStretch);
                    break;

                case ObservableActionState.Guarding:
                    offset = new Vector3(0f, -_config.GuardCrouch, 0f);
                    leanDegrees = _config.GuardLeanDegrees;
                    break;

                case ObservableActionState.Healing:
                    offset = new Vector3(0f, -_config.HealCrouch, 0f);
                    break;

                case ObservableActionState.Staggered:
                    // The recoil impulse does the visible work; the base only slumps slightly so a
                    // long stagger does not snap upright while still reeling.
                    leanDegrees = -_config.AnticipationLeanDegrees * 0.5f;
                    break;

                case ObservableActionState.Dead:
                    offset = new Vector3(0f, -_config.DeathSink, 0f);
                    leanDegrees = _config.DeathFallDegrees;
                    break;

                case ObservableActionState.Moving:
                    leanDegrees = _config.MoveLeanDegrees * _planarSpeed01;
                    offset = new Vector3(0f, Bob(1f + _planarSpeed01), 0f);
                    break;

                default:
                    offset = new Vector3(0f, Bob(1f), 0f);
                    break;
            }
        }

        /// <summary>The wind-up, lunge and follow-through of a swing, keyed off the attack phase.</summary>
        private void AttackPose(out Vector3 offset, out float leanDegrees, out Vector3 scaleFactor)
        {
            float weight = _state == ObservableActionState.LightAttacking ? 1f : _config.HeavyMultiplier;

            switch (_attackPhase)
            {
                case AttackPhase.Startup:
                    // Coil back and down, lean away — the anticipation that makes the strike read.
                    offset = new Vector3(0f, -_config.AnticipationCrouch * 0.4f, -_config.AnticipationCrouch) * weight;
                    leanDegrees = -_config.AnticipationLeanDegrees * weight;
                    scaleFactor = Squash(_config.AnticipationSquash * weight);
                    break;

                case AttackPhase.Active:
                    // Commit: lunge into the blow and stretch after it.
                    offset = new Vector3(0f, 0f, _config.StrikeLunge * weight);
                    leanDegrees = _config.StrikeLeanDegrees * weight;
                    scaleFactor = Stretch(_config.StrikeStretch * weight);
                    break;

                default:
                    // Recovery and the brief inactive frames settle back toward neutral; the easing
                    // supplies the follow-through rather than a distinct pose.
                    offset = Vector3.zero;
                    leanDegrees = 0f;
                    scaleFactor = Vector3.one;
                    break;
            }
        }

        /// <summary>Vertical breathing offset, scaled by how energetic the state is.</summary>
        private float Bob(float energy) =>
            Mathf.Sin(_bobTime * _config.IdleBobFrequency * energy * Mathf.PI * 2f)
            * _config.IdleBobAmplitude * energy;

        /// <summary>Slow side-to-side breathing roll, in degrees.</summary>
        private float IdleSway() =>
            _state == ObservableActionState.Idle
                ? Mathf.Sin(_bobTime * _config.IdleBobFrequency * Mathf.PI) * _config.IdleSwayDegrees
                : 0f;

        /// <summary>Wider and shorter — the shape of a coil or a flinch.</summary>
        private static Vector3 Squash(float amount) =>
            new Vector3(1f + amount, 1f - amount, 1f + amount);

        /// <summary>Taller and thinner — the shape of a committed reach.</summary>
        private static Vector3 Stretch(float amount) =>
            new Vector3(1f - amount * 0.5f, 1f + amount, 1f - amount * 0.5f);

        /// <summary>Frame-rate independent interpolation factor for a half-life.</summary>
        private static float DampFactor(float halfLifeSeconds, float deltaTime) =>
            1f - Mathf.Exp(-deltaTime / Mathf.Max(0.0001f, halfLifeSeconds));
    }
}
