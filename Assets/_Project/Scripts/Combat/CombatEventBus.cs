using System;
using AdaptiveBossArena.Core.Combat;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// Default <see cref="ICombatEventBus"/> implementation.
    /// </summary>
    /// <remarks>
    /// A direct multicast delegate rather than a ScriptableObject channel. Combat events fire far
    /// more often than the interface signals channels are meant for, and unlike those signals they
    /// have exactly one consumer that matters: the boss's combat memory.
    /// </remarks>
    public sealed class CombatEventBus : ICombatEventBus
    {
        /// <inheritdoc />
        public event Action<CombatEvent> EventRecorded;

        /// <summary>Number of occurrences published since construction.</summary>
        public int PublishedCount { get; private set; }

        /// <inheritdoc />
        public void Publish(in CombatEvent combatEvent)
        {
            PublishedCount++;
            EventRecorded?.Invoke(combatEvent);
        }

        /// <inheritdoc />
        public void Clear()
        {
            EventRecorded = null;
            PublishedCount = 0;
        }
    }
}
