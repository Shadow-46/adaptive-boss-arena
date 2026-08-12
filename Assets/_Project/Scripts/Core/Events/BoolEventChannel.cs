using UnityEngine;

namespace AdaptiveBossArena.Core.Events
{
    /// <summary>Event channel carrying a flag, such as whether the game is paused.</summary>
    /// <remarks>
    /// One class per file, named for the class. See <see cref="VoidEventChannel"/> for why: a
    /// concrete channel declared beside another type cannot be saved as a working asset.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "BoolEventChannel",
        menuName = "Adaptive Boss Arena/Events/Bool Event Channel",
        order = 3)]
    public sealed class BoolEventChannel : EventChannel<bool>
    {
    }
}
