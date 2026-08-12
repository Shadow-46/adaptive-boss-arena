using UnityEngine;

namespace AdaptiveBossArena.Core.Events
{
    /// <summary>Event channel carrying an integer, such as a boss phase index.</summary>
    /// <remarks>
    /// One class per file, named for the class. See <see cref="VoidEventChannel"/> for why: a
    /// concrete channel declared beside another type cannot be saved as a working asset.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "IntEventChannel",
        menuName = "Adaptive Boss Arena/Events/Int Event Channel",
        order = 2)]
    public sealed class IntEventChannel : EventChannel<int>
    {
    }
}
