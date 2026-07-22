using System;

namespace AdaptiveBossArena.Core.Combat
{
    /// <summary>
    /// A depletable resource pool with change notifications.
    /// </summary>
    /// <remarks>
    /// Health, stamina and poise are all the same shape, so they share one contract. UI bars bind to
    /// this interface and therefore work unchanged for the player, the boss, and anything added
    /// later.
    /// </remarks>
    public interface IResourcePool
    {
        /// <summary>Current amount held.</summary>
        float Current { get; }

        /// <summary>Capacity of the pool.</summary>
        float Maximum { get; }

        /// <summary>Current amount as a fraction of capacity, from zero to one.</summary>
        float Normalized { get; }

        /// <summary>Raised whenever the amount held changes.</summary>
        event Action<ResourceChangedArgs> Changed;
    }

    /// <summary>Health of a combatant.</summary>
    public interface IHealth : IResourcePool
    {
        /// <summary>True while current health is above zero.</summary>
        bool IsAlive { get; }

        /// <summary>Raised once when health reaches zero.</summary>
        event Action<DeathArgs> Died;

        /// <summary>Restores health, clamped to the maximum.</summary>
        /// <param name="amount">Health to restore. Non-positive values are ignored.</param>
        /// <returns>Health actually restored after clamping.</returns>
        float Heal(float amount);
    }

    /// <summary>
    /// Stamina gating the player's dash and heavy attacks.
    /// </summary>
    /// <remarks>
    /// Deliberately absent from <see cref="AdaptiveBossArena.Core.Perception.IObservablePlayer"/>.
    /// A human opponent cannot see an exact stamina number, so neither can the boss; it must infer
    /// exhaustion from observable behaviour such as a dash that did not come.
    /// </remarks>
    public interface IStaminaPool : IResourcePool
    {
        /// <summary>True when the pool currently holds at least the requested amount.</summary>
        /// <param name="amount">Amount to test for.</param>
        /// <returns>True when the spend would succeed.</returns>
        bool CanSpend(float amount);

        /// <summary>Spends stamina if enough is available.</summary>
        /// <param name="amount">Amount to spend.</param>
        /// <returns>True when the spend succeeded and the pool was reduced.</returns>
        bool TrySpend(float amount);

        /// <summary>Restores stamina, clamped to the maximum.</summary>
        /// <param name="amount">Amount to restore.</param>
        void Restore(float amount);
    }

    /// <summary>
    /// Poise, the hidden pool that decides whether a hit staggers its target.
    /// </summary>
    /// <remarks>
    /// Poise regenerates while the owner is not being hit, so scattered chip damage never staggers
    /// but a committed combo does. This is what makes trading blows with the boss a decision rather
    /// than a reflex.
    /// </remarks>
    public interface IPoise : IResourcePool
    {
        /// <summary>True while the owner is in a broken-poise state and cannot act.</summary>
        bool IsBroken { get; }

        /// <summary>Raised when accumulated poise damage breaks the owner's stance.</summary>
        event Action Broken;

        /// <summary>Applies poise damage.</summary>
        /// <param name="amount">Poise damage to apply.</param>
        /// <returns>True when this application broke the owner's poise.</returns>
        bool ApplyPoiseDamage(float amount);
    }
}
