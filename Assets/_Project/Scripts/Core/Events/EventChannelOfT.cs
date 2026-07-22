using System;
using UnityEngine;

namespace AdaptiveBossArena.Core.Events
{
    /// <summary>
    /// Base for asset-backed event channels that carry a payload.
    /// </summary>
    /// <remarks>
    /// Concrete channels are one-line subclasses that close the generic parameter, because Unity
    /// cannot serialise an open generic type as an asset. The subclass exists purely to give the
    /// closed type a name that <c>CreateAssetMenu</c> can attach to.
    /// </remarks>
    /// <typeparam name="TPayload">Data carried with the signal.</typeparam>
    public abstract class EventChannel<TPayload> : EventChannelBase
    {
        /// <summary>Raised when the channel fires, carrying the payload.</summary>
        public event Action<TPayload> Raised;

        /// <summary>Number of currently attached listeners.</summary>
        public int ListenerCount => Raised?.GetInvocationList().Length ?? 0;

        /// <summary>Fires the channel with a payload.</summary>
        /// <param name="payload">Data to deliver to listeners.</param>
        public void Raise(TPayload payload)
        {
            if (LogRaises)
            {
                Debug.Log($"[EventChannel] {name} raised with '{payload}' to {ListenerCount} listener(s).", this);
            }

            Raised?.Invoke(payload);
        }

        /// <summary>Drops all listeners when the asset unloads, preventing cross-session leaks.</summary>
        private void OnDisable() => Raised = null;
    }

    /// <summary>Event channel carrying a floating-point value, such as a normalised health fraction.</summary>
    [CreateAssetMenu(
        fileName = "FloatEventChannel",
        menuName = "Adaptive Boss Arena/Events/Float Event Channel",
        order = 1)]
    public sealed class FloatEventChannel : EventChannel<float>
    {
    }

    /// <summary>Event channel carrying an integer, such as a boss phase index.</summary>
    [CreateAssetMenu(
        fileName = "IntEventChannel",
        menuName = "Adaptive Boss Arena/Events/Int Event Channel",
        order = 2)]
    public sealed class IntEventChannel : EventChannel<int>
    {
    }

    /// <summary>Event channel carrying a flag, such as whether the game is paused.</summary>
    [CreateAssetMenu(
        fileName = "BoolEventChannel",
        menuName = "Adaptive Boss Arena/Events/Bool Event Channel",
        order = 3)]
    public sealed class BoolEventChannel : EventChannel<bool>
    {
    }

    /// <summary>Event channel carrying a string, such as an identifier for a triggered adaptation.</summary>
    [CreateAssetMenu(
        fileName = "StringEventChannel",
        menuName = "Adaptive Boss Arena/Events/String Event Channel",
        order = 4)]
    public sealed class StringEventChannel : EventChannel<string>
    {
    }
}
