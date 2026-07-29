using AdaptiveBossArena.Core.Perception;
using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Drives a skeletal <see cref="Animator"/> from the character's visible state, so a rigged model
    /// dropped under the visual root animates from the same signals the procedural animator reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the character-art seam. Today the characters are primitives with no <see cref="Animator"/>,
    /// so every method here is a no-op and costs nothing. Import a humanoid rig with a controller that
    /// exposes the parameters documented below, make it a child of the visual root, regenerate, and the
    /// same state stream that leans and lunges the capsule now plays real clips — with no change to the
    /// controller code, because both consumers are fed from one place (<see cref="CharacterAnimator"/>).
    /// </para>
    /// <para>
    /// Expected Animator parameters (all optional — a missing one is simply skipped by Unity):
    /// <list type="bullet">
    /// <item><description><c>Speed</c> (float): planar speed as a fraction of top speed, for the locomotion blend.</description></item>
    /// <item><description><c>Grounded</c> (bool): always true in this arena; present so a standard locomotion controller works unmodified.</description></item>
    /// <item><description><c>Attack</c>, <c>Heavy</c>, <c>Ability</c>, <c>Dash</c>, <c>Guard</c>, <c>Hit</c>, <c>Death</c> (triggers).</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// It is presentation only and, like the procedural animator beside it, never reads input and
    /// never tells the AI anything — it only receives what it is pushed.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationBridge : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int GroundedParam = Animator.StringToHash("Grounded");
        private static readonly int LightAttackParam = Animator.StringToHash("Attack");
        private static readonly int HeavyAttackParam = Animator.StringToHash("Heavy");
        private static readonly int AbilityParam = Animator.StringToHash("Ability");
        private static readonly int DashParam = Animator.StringToHash("Dash");
        private static readonly int GuardParam = Animator.StringToHash("Guard");
        private static readonly int HitParam = Animator.StringToHash("Hit");
        private static readonly int DeathParam = Animator.StringToHash("Death");

        private Animator _animator;

        private ObservableActionState _previousState = ObservableActionState.Idle;
        private AttackPhase _previousPhase = AttackPhase.Inactive;

        /// <summary>True when a skeletal rig is present and this bridge is actually driving it.</summary>
        /// <remarks>
        /// Read by <see cref="CharacterAnimator"/> so the procedural layer stands down from posing the
        /// whole body when a rig is doing that job, leaving only its additive impact juice.
        /// </remarks>
        public bool HasSkeleton => _animator != null;

        private void Awake()
        {
            // Resolved once. Art is added by regenerating the prefab, not at runtime, so a rig present
            // at Awake is present for the whole encounter; a search each frame would buy nothing.
            _animator = GetComponentInChildren<Animator>(includeInactive: true);
        }

        /// <summary>Pushes this frame's visible state to the rig, firing any one-shot triggers.</summary>
        /// <param name="state">The action a watcher would see.</param>
        /// <param name="attackPhase">Phase of any attack in flight.</param>
        /// <param name="planarSpeed01">Horizontal speed as a fraction of top speed.</param>
        public void SetMotionState(ObservableActionState state, AttackPhase attackPhase, float planarSpeed01)
        {
            if (_animator == null)
            {
                return;
            }

            _animator.SetFloat(SpeedParam, Mathf.Clamp01(planarSpeed01));
            _animator.SetBool(GroundedParam, true);

            Fire(AnimatorDriveMap.TriggerFor(_previousState, state, _previousPhase, attackPhase));

            _previousState = state;
            _previousPhase = attackPhase;
        }

        /// <summary>Fires the hit reaction. Called on any damage, blocked or not.</summary>
        public void Recoil()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(HitParam);
            }
        }

        /// <summary>Clears carried state for a retry, so the next attempt starts from idle.</summary>
        public void ResetState()
        {
            _previousState = ObservableActionState.Idle;
            _previousPhase = AttackPhase.Inactive;

            if (_animator != null)
            {
                _animator.SetFloat(SpeedParam, 0f);
            }
        }

        /// <summary>Translates a drive trigger into the matching Animator trigger.</summary>
        private void Fire(AnimatorDriveTrigger trigger)
        {
            switch (trigger)
            {
                case AnimatorDriveTrigger.LightAttack:
                    _animator.SetTrigger(LightAttackParam);
                    break;
                case AnimatorDriveTrigger.HeavyAttack:
                    _animator.SetTrigger(HeavyAttackParam);
                    break;
                case AnimatorDriveTrigger.Ability:
                    _animator.SetTrigger(AbilityParam);
                    break;
                case AnimatorDriveTrigger.Dash:
                    _animator.SetTrigger(DashParam);
                    break;
                case AnimatorDriveTrigger.Guard:
                    _animator.SetTrigger(GuardParam);
                    break;
                case AnimatorDriveTrigger.Death:
                    _animator.SetTrigger(DeathParam);
                    break;
            }
        }
    }
}
