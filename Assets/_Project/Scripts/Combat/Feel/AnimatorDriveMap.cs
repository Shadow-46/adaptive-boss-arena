using AdaptiveBossArena.Core.Perception;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>An animation event a skeletal rig should play once, edge-triggered from state.</summary>
    /// <remarks>
    /// Kept as a small enum rather than raw trigger strings so the decision of <em>when</em> a
    /// trigger fires is separable from the detail of <em>which</em> Animator parameter carries it.
    /// The first is a rule worth testing; the second is just a string table on the rig.
    /// </remarks>
    public enum AnimatorDriveTrigger
    {
        /// <summary>Nothing to fire this frame.</summary>
        None = 0,

        /// <summary>A light-chain swing began.</summary>
        LightAttack,

        /// <summary>A heavy swing began.</summary>
        HeavyAttack,

        /// <summary>A special / ability cast began.</summary>
        Ability,

        /// <summary>A dash began.</summary>
        Dash,

        /// <summary>A guard was raised.</summary>
        Guard,

        /// <summary>The character died.</summary>
        Death
    }

    /// <summary>
    /// Decides which one-shot animation trigger a skeletal rig should fire as the character's
    /// visible state changes, so the same state stream that drives the procedural animator can drive
    /// an imported Animator once real art exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pure and static on purpose. The rule that a new combo swing must re-fire the attack trigger
    /// even though the character never left the "light attacking" state — because the swing restarts
    /// at <see cref="AttackPhase.Startup"/> — is exactly the kind of thing that is easy to get wrong
    /// and cheap to pin with a unit test. Keeping it out of the <see cref="CharacterAnimationBridge"/>
    /// MonoBehaviour means it can be tested with no scene and no Animator.
    /// </para>
    /// <para>
    /// The hit reaction is deliberately absent here: a recoil happens on any damage, blocked or not,
    /// and is driven by the explicit recoil call rather than by a state change, so it is fired
    /// directly by the bridge instead of inferred from the state stream.
    /// </para>
    /// </remarks>
    public static class AnimatorDriveMap
    {
        /// <summary>
        /// The trigger to fire for a transition from one visible state and attack phase to another.
        /// </summary>
        /// <param name="previousState">The state on the previous frame.</param>
        /// <param name="currentState">The state on this frame.</param>
        /// <param name="previousPhase">The attack phase on the previous frame.</param>
        /// <param name="currentPhase">The attack phase on this frame.</param>
        /// <returns>The trigger to fire, or <see cref="AnimatorDriveTrigger.None"/>.</returns>
        public static AnimatorDriveTrigger TriggerFor(
            ObservableActionState previousState,
            ObservableActionState currentState,
            AttackPhase previousPhase,
            AttackPhase currentPhase)
        {
            // A fresh combo swing restarts the timeline at startup while the state stays "attacking".
            // Without catching that, only the first hit of a chain would ever animate.
            bool swingRestarted =
                currentPhase == AttackPhase.Startup && previousPhase != AttackPhase.Startup;

            bool stateEntered = currentState != previousState;

            if (!stateEntered && !swingRestarted)
            {
                return AnimatorDriveTrigger.None;
            }

            switch (currentState)
            {
                case ObservableActionState.LightAttacking:
                    return AnimatorDriveTrigger.LightAttack;

                case ObservableActionState.HeavyAttacking:
                    return AnimatorDriveTrigger.HeavyAttack;

                case ObservableActionState.UsingAbility:
                    return AnimatorDriveTrigger.Ability;

                // Movement states never restart a swing, so these only fire on a genuine state entry.
                case ObservableActionState.Dashing:
                    return stateEntered ? AnimatorDriveTrigger.Dash : AnimatorDriveTrigger.None;

                case ObservableActionState.Guarding:
                    return stateEntered ? AnimatorDriveTrigger.Guard : AnimatorDriveTrigger.None;

                case ObservableActionState.Dead:
                    return stateEntered ? AnimatorDriveTrigger.Death : AnimatorDriveTrigger.None;

                default:
                    return AnimatorDriveTrigger.None;
            }
        }
    }
}
