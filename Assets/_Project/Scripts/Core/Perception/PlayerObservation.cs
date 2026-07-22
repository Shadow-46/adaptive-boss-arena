using UnityEngine;

namespace AdaptiveBossArena.Core.Perception
{
    /// <summary>
    /// Player actions that are visible to an onlooker.
    /// </summary>
    /// <remarks>
    /// Each value corresponds to something with a distinct on-screen presentation. Nothing here
    /// describes intent: there is no "about to attack" state, because a human opponent could not see
    /// that either. Wind-up is inferred from <see cref="PlayerObservation.TimeInActionState"/>.
    /// </remarks>
    public enum ObservableActionState
    {
        /// <summary>Standing still.</summary>
        Idle = 0,

        /// <summary>Running or walking.</summary>
        Moving = 1,

        /// <summary>Mid-dash. Visually distinct and the window in which the player is invulnerable.</summary>
        Dashing = 2,

        /// <summary>Performing the fast primary attack.</summary>
        LightAttacking = 3,

        /// <summary>Performing the slow committed attack.</summary>
        HeavyAttacking = 4,

        /// <summary>Using the special ability.</summary>
        UsingAbility = 5,

        /// <summary>Reeling from a hit.</summary>
        Staggered = 6,

        /// <summary>Channelling a heal. Deliberately conspicuous so the boss may legitimately punish it.</summary>
        Healing = 7,

        /// <summary>Defeated.</summary>
        Dead = 8,

        /// <summary>
        /// Holding a guard. Visually distinct, and legitimately observable.
        /// </summary>
        /// <remarks>
        /// The boss is told that a guard is raised but never whether the deflect window is open.
        /// Seeing the stance is what an opponent could see; knowing the exact frame the parry
        /// becomes active is not, and exposing it would let the boss time feints perfectly rather
        /// than guessing like everyone else.
        /// </remarks>
        Guarding = 9
    }

    /// <summary>
    /// Everything the boss is permitted to know about the player at one instant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This struct is the narrow window in the anti-cheat firewall. The boss's assemblies cannot
    /// reference the player's, so this snapshot is the only channel through which player state
    /// reaches the AI, and it carries strictly what a skilled human opponent could perceive by
    /// watching the screen.
    /// </para>
    /// <para>
    /// Note what is absent and why it is absent:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// No input vector, buffered input, or held-button state. Reading intent before it becomes
    /// motion is the definition of the cheating this design forbids.
    /// </description></item>
    /// <item><description>
    /// No stamina. An opponent cannot see a number; exhaustion must be inferred from a dash that
    /// did not happen.
    /// </description></item>
    /// <item><description>
    /// No cooldown timers. The boss learns the player's ability rhythm from observed frequency,
    /// exactly as a human would.
    /// </description></item>
    /// <item><description>
    /// No invincibility flag. The boss discovers a dash beat its attack by whiffing, and that whiff
    /// is what feeds the learning system.
    /// </description></item>
    /// </list>
    /// </remarks>
    public readonly struct PlayerObservation
    {
        /// <summary>Time in seconds, on the combat clock, at which this snapshot was taken.</summary>
        public float Timestamp { get; init; }

        /// <summary>World position of the player.</summary>
        public Vector3 Position { get; init; }

        /// <summary>World-space velocity. Visible as motion, so legitimately perceivable.</summary>
        public Vector3 Velocity { get; init; }

        /// <summary>Unit vector the player is facing.</summary>
        public Vector3 Facing { get; init; }

        /// <summary>The visually distinct action the player is performing.</summary>
        public ObservableActionState ActionState { get; init; }

        /// <summary>
        /// Seconds the player has been in the current action state.
        /// </summary>
        /// <remarks>
        /// Legitimate because it is simply how long the onlooker has been watching the animation.
        /// This is what allows the boss to punish a long heavy-attack wind-up without ever being
        /// told that an attack is coming.
        /// </remarks>
        public float TimeInActionState { get; init; }

        /// <summary>Player health as a fraction of maximum. Visible on the health bar.</summary>
        public float NormalizedHealth { get; init; }

        /// <summary>True when this snapshot holds real data rather than a default value.</summary>
        public bool IsValid { get; init; }

        /// <summary>Convenience test for whether the player is currently attacking in any form.</summary>
        public bool IsAttacking =>
            ActionState == ObservableActionState.LightAttacking ||
            ActionState == ObservableActionState.HeavyAttacking ||
            ActionState == ObservableActionState.UsingAbility;

        /// <summary>Convenience test for whether the player is currently unable to act.</summary>
        public bool IsIncapacitated =>
            ActionState == ObservableActionState.Staggered ||
            ActionState == ObservableActionState.Dead;
    }
}
