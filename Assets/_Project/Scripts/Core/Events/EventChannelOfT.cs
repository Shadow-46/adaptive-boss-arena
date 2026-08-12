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
}
