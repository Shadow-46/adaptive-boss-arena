namespace AdaptiveBossArena.Core.Combat
{
    /// <summary>
    /// Anything that can receive a hit.
    /// </summary>
    /// <remarks>
    /// The entire hit pipeline speaks only this interface, so the player, the boss and any future
    /// destructible arena prop are interchangeable to an attacker. Implementations decide for
    /// themselves whether a hit lands, which is where invincibility frames and perfect dodges are
    /// resolved.
    /// </remarks>
    public interface IDamageable
    {
        /// <summary>Side this target fights for. Attacks from the same team are rejected.</summary>
        CombatantTeam Team { get; }

        /// <summary>False once the target has been defeated.</summary>
        bool IsAlive { get; }

        /// <summary>
        /// Offers a hit to this target and reports what became of it.
        /// </summary>
        /// <param name="damage">The incoming hit. Passed by reference to avoid copying.</param>
        /// <returns>
        /// The outcome. Callers must not assume damage was dealt; the target may have been
        /// invulnerable, may have perfect-dodged, or may already be dead.
        /// </returns>
        DamageResult TakeDamage(in DamageInfo damage);
    }

    /// <summary>Describes a change to a depletable pool such as health or stamina.</summary>
    public readonly struct ResourceChangedArgs
    {
        /// <summary>Value before the change.</summary>
        public float Previous { get; init; }

        /// <summary>Value after the change.</summary>
        public float Current { get; init; }

        /// <summary>Maximum the pool can hold.</summary>
        public float Maximum { get; init; }

        /// <summary>Current value as a fraction of the maximum, clamped to zero through one.</summary>
        public float Normalized => Maximum <= 0f ? 0f : UnityEngine.Mathf.Clamp01(Current / Maximum);

        /// <summary>Signed size of the change. Negative when the pool was drained.</summary>
        public float Delta => Current - Previous;

        /// <summary>Creates a change description.</summary>
        /// <param name="previous">Value before the change.</param>
        /// <param name="current">Value after the change.</param>
        /// <param name="maximum">Pool capacity.</param>
        /// <returns>A populated description.</returns>
        public static ResourceChangedArgs Create(float previous, float current, float maximum) =>
            new ResourceChangedArgs { Previous = previous, Current = current, Maximum = maximum };
    }

    /// <summary>Describes the defeat of a combatant.</summary>
    public readonly struct DeathArgs
    {
        /// <summary>Team the defeated combatant belonged to.</summary>
        public CombatantTeam Team { get; init; }

        /// <summary>Instance identifier of the killing blow's source.</summary>
        public int KillerInstanceId { get; init; }

        /// <summary>The hit that proved lethal.</summary>
        public DamageInfo FinalBlow { get; init; }
    }
}
