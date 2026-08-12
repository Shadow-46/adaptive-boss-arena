using UnityEngine;

namespace AdaptiveBossArena.Core.Events
{
    /// <summary>Event channel carrying a floating-point value, such as a normalised health fraction.</summary>
    /// <remarks>
    /// Lives in its own file, named for the class, because Unity only creates a <c>MonoScript</c> for
    /// the type whose name matches the file. See <see cref="VoidEventChannel"/> for what goes wrong
    /// otherwise — briefly, the asset saves with no script reference and loads as null.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "FloatEventChannel",
        menuName = "Adaptive Boss Arena/Events/Float Event Channel",
        order = 1)]
    public sealed class FloatEventChannel : EventChannel<float>
    {
    }
}
