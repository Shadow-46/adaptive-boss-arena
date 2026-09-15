using AdaptiveBossArena.Core.Perception;
using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Drives a rigged character's Animator from the same state stream the procedural animator reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The character's state machine decides what it is doing; this only decides what that looks like.
    /// It chooses one Animator state for the observable action - locomotion, roll, guard, stagger,
    /// death, or the attack in progress - and crossfades to it when that changes. The generated
    /// controller has no transitions of its own, so there is one source of truth for what a character
    /// is doing and it is not the Animator.
    /// </para>
    /// <para>
    /// Attacks are not left to play at the clip's own speed. The clip is scrubbed by the attack's
    /// elapsed time through <see cref="AttackClipTimeWarp"/>, so the blade lands while the hitbox is
    /// live, and freezes on the impact during hit-stop. Presentation only: nothing here feeds combat or
    /// the boss.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationBridge : MonoBehaviour
    {
        private const float CrossFadeSeconds = 0.08f;

        /// <summary>Clip time around the contact frame the live window plays, as a fraction of the clip.</summary>
        private const float ContactSpanFraction = 0.08f;

        private static readonly int SpeedParam = Animator.StringToHash(CharacterAnimatorParameters.Speed);
        private static readonly int AttackTimeParam = Animator.StringToHash(CharacterAnimatorParameters.AttackTime);
        private static readonly int ReactionTimeParam = Animator.StringToHash(CharacterAnimatorParameters.ReactionTime);

        private Animator _animator;
        private CharacterAnimationConfig _config;

        private string _currentState;
        private AttackDefinition _currentAttack;

        /// <summary>Index of the upper-body hit layer, or -1 when the controller has none.</summary>
        private int _hitLayer = -1;

        /// <summary>Seconds since the last flinch began, or a negative value when none is playing.</summary>
        private float _hitElapsed = -1f;

        private float _hitLength;

        /// <summary>Seconds a flinch takes to reach full weight: quick, so the blow registers on its frame.</summary>
        private const float HitAttackSeconds = 0.05f;

        /// <summary>
        /// The share of the flinch spent easing back to the stance underneath, so the torso settles rather than
        /// snapping back.
        /// </summary>
        private const float HitReleaseFraction = 0.45f;

        /// <summary>True while a rig with an Animator is present to drive.</summary>
        /// <remarks>
        /// False while the Animator is switched off, which is what a ragdoll does on death: the corpse
        /// belongs to physics, and a crossfade requested of a disabled Animator only logs warnings.
        /// </remarks>
        public bool HasSkeleton =>
            _animator != null && _animator.enabled && _animator.runtimeAnimatorController != null;

        private void Awake()
        {
            // Resolved once. Art is added by regenerating the prefab, not at runtime.
            _animator = GetComponentInChildren<Animator>(includeInactive: true);

            var procedural = GetComponent<CharacterAnimator>();
            _config = procedural != null ? procedural.Config : null;

            _hitLayer = _animator != null && _animator.runtimeAnimatorController != null
                ? _animator.GetLayerIndex(CharacterAnimatorParameters.HitLayer)
                : -1;
        }

        private void Update()
        {
            if (_hitElapsed < 0f || !HasSkeleton || _hitLayer < 0)
            {
                return;
            }

            if (_hitLength <= 0f)
            {
                AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(_hitLayer);
                _hitLength = info.IsName(CharacterAnimatorParameters.HitState) && info.length > 0.05f ? info.length : 0.6f;
            }

            // Game time: a hit-stop freezes the flinch at the blow, and the release plays out after it.
            _hitElapsed += Time.deltaTime;

            float release = _hitLength * HitReleaseFraction;
            float weight = _hitElapsed < HitAttackSeconds
                ? _hitElapsed / HitAttackSeconds
                : Mathf.Clamp01((_hitLength - _hitElapsed) / Mathf.Max(0.01f, release));

            _animator.SetLayerWeight(_hitLayer, weight);

            if (_hitElapsed >= _hitLength)
            {
                _hitElapsed = -1f;
                _animator.SetLayerWeight(_hitLayer, 0f);
            }
        }

        /// <summary>Pushes this frame's observable state, and the attack in progress if any.</summary>
        /// <param name="state">What an onlooker would see the character doing.</param>
        /// <param name="attackPhase">Phase of the attack in flight.</param>
        /// <param name="planarSpeed01">Horizontal speed as a fraction of top speed.</param>
        /// <param name="attack">The attack in progress, or null.</param>
        /// <param name="attackElapsedSeconds">Time since that attack began.</param>
        /// <param name="reactionProgress01">Progress through rising from the floor; zero while lying down.</param>
        public void SetMotionState(
            ObservableActionState state,
            AttackPhase attackPhase,
            float planarSpeed01,
            AttackDefinition attack,
            float attackElapsedSeconds,
            float reactionProgress01 = 0f)
        {
            if (!HasSkeleton)
            {
                return;
            }

            _animator.SetFloat(SpeedParam, Mathf.Clamp01(planarSpeed01));
            _animator.SetFloat(ReactionTimeParam, Mathf.Clamp01(reactionProgress01));

            bool attacking = attack != null && attackPhase != AttackPhase.Inactive && IsAttackState(state);
            string target = attacking ? AttackStateFor(attack) : StateFor(state);

            // A new link in a combo can play the same state as the last one, so a change of attack
            // restarts the state even when its name has not changed.
            bool newAttack = attacking && !ReferenceEquals(attack, _currentAttack);

            if (target != _currentState || newAttack)
            {
                _animator.CrossFadeInFixedTime(target, CrossFadeSeconds);
                _currentState = target;
            }

            _currentAttack = attacking ? attack : null;

            if (attacking)
            {
                _animator.SetFloat(AttackTimeParam, WarpFor(attack).NormalizedTimeAt(attackElapsedSeconds));
            }
        }

        /// <summary>Plays the fighter's flinch on the upper body, over whatever it is doing.</summary>
        /// <remarks>
        /// Not over a fall, a death or a stagger: those are full-body reactions with their own clips, and a flinch
        /// layered on top would bend a body lying on the floor.
        /// </remarks>
        public void Recoil()
        {
            if (!HasSkeleton || _hitLayer < 0 ||
                _currentState == CharacterAnimatorParameters.DeathState ||
                _currentState == CharacterAnimatorParameters.AirborneState ||
                _currentState == CharacterAnimatorParameters.KnockedDownState ||
                _currentState == CharacterAnimatorParameters.StaggerState)
            {
                return;
            }

            _animator.Play(CharacterAnimatorParameters.HitState, _hitLayer, 0f);

            // The clip's length is known once the Animator has entered the state, on its next update.
            _hitLength = 0f;
            _hitElapsed = 0f;
        }

        /// <summary>The upper-body flinch's current weight, from zero at rest to one at full strength.</summary>
        public float HitWeight => _hitLayer >= 0 && _animator != null ? _animator.GetLayerWeight(_hitLayer) : 0f;

        /// <summary>Returns the rig to its idle state for a retry.</summary>
        public void ResetState()
        {
            _currentState = null;
            _currentAttack = null;
            _hitElapsed = -1f;

            if (HasSkeleton && _hitLayer >= 0)
            {
                _animator.SetLayerWeight(_hitLayer, 0f);
            }

            if (HasSkeleton)
            {
                _animator.SetFloat(SpeedParam, 0f);
                _animator.Play(CharacterAnimatorParameters.LocomotionState, 0, 0f);
            }
        }

        private AttackClipTimeWarp WarpFor(AttackDefinition attack)
        {
            float contact = _config != null ? _config.ContactFractionFor(attack) : ClipContact.DefaultContact;

            // Normalised clip space: a clip of length one, so no clip lengths need storing anywhere.
            return new AttackClipTimeWarp(
                attack.StartupSeconds, attack.ActiveSeconds, attack.RecoverySeconds,
                1f, contact, ContactSpanFraction);
        }

        private string AttackStateFor(AttackDefinition attack) =>
            _config != null ? _config.AttackStateFor(attack) : CharacterAnimatorParameters.DefaultLightState;

        private static bool IsAttackState(ObservableActionState state) =>
            state == ObservableActionState.LightAttacking ||
            state == ObservableActionState.HeavyAttacking ||
            state == ObservableActionState.UsingAbility;

        private static string StateFor(ObservableActionState state)
        {
            switch (state)
            {
                case ObservableActionState.Dashing:
                    return CharacterAnimatorParameters.RollState;

                case ObservableActionState.Guarding:
                    return CharacterAnimatorParameters.GuardState;

                case ObservableActionState.Staggered:
                    return CharacterAnimatorParameters.StaggerState;

                case ObservableActionState.Dead:
                    return CharacterAnimatorParameters.DeathState;

                case ObservableActionState.Airborne:
                    return CharacterAnimatorParameters.AirborneState;

                case ObservableActionState.KnockedDown:
                    return CharacterAnimatorParameters.KnockedDownState;

                default:
                    return CharacterAnimatorParameters.LocomotionState;
            }
        }
    }
}
