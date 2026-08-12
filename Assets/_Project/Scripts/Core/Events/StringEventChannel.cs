using UnityEngine;

namespace AdaptiveBossArena.Core.Events
{
    /// <summary>Event channel carrying a string, such as the tell for a newly adopted adaptation.</summary>
    /// <remarks>
    /// One class per file, named for the class. See <see cref="VoidEventChannel"/> for why: a
    /// concrete channel declared beside another type cannot be saved as a working asset.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "StringEventChannel",
        menuName = "Adaptive Boss Arena/Events/String Event Channel",
        order = 4)]
    public sealed class StringEventChannel : EventChannel<string>
    {
    }
}
