namespace AdaptiveBossArena.Core.Services
{
    /// <summary>
    /// Reuses instances instead of allocating them, for objects created during combat.
    /// </summary>
    /// <remarks>
    /// Impact effects, damage numbers and projectiles are spawned in bursts precisely when frame
    /// time matters most. Instantiating them mid-combo invites a garbage collection spike during the
    /// exact moment the player is judging whether the controls feel responsive.
    /// </remarks>
    /// <typeparam name="T">Pooled type.</typeparam>
    public interface IObjectPool<T> where T : class
    {
        /// <summary>Number of instances currently available for reuse.</summary>
        int AvailableCount { get; }

        /// <summary>Number of instances currently checked out.</summary>
        int ActiveCount { get; }

        /// <summary>Takes an instance from the pool, creating one if none are spare.</summary>
        /// <returns>A ready-to-use instance.</returns>
        T Rent();

        /// <summary>Returns an instance for reuse.</summary>
        /// <param name="instance">The instance to return. Must have come from this pool.</param>
        void Return(T instance);

        /// <summary>Creates instances ahead of time so the first use does not stall.</summary>
        /// <param name="count">Number of instances to create.</param>
        void Prewarm(int count);

        /// <summary>Destroys all pooled instances.</summary>
        void Clear();
    }
}
