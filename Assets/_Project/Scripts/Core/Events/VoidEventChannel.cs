using System;
using UnityEngine;

namespace AdaptiveBossArena.Core.Events
{
    /// <summary>Event channel carrying no payload.</summary>
    /// <remarks>
    /// In its own file, and named for its class, because Unity only creates a <c>MonoScript</c> for
    /// the type whose name matches the file. A concrete <see cref="ScriptableObject"/> declared
    /// beside another class cannot be saved as a working asset: the asset is written with
    /// <c>m_Script: {fileID: 0}</c>, loads as null, and every reference to it silently becomes null
    /// — with no error anywhere. That is exactly what happened to every channel in this project, and
    /// it took the whole signalling layer down with it while the console stayed clean.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "VoidEventChannel",
        menuName = "Adaptive Boss Arena/Events/Void Event Channel",
        order = 0)]
    public sealed class VoidEventChannel : EventChannelBase
    {
        /// <summary>Raised when the channel fires.</summary>
        public event Action Raised;

        /// <summary>Number of currently attached listeners.</summary>
        public int ListenerCount => Raised?.GetInvocationList().Length ?? 0;

        /// <summary>Fires the channel.</summary>
        public void Raise()
        {
            if (LogRaises)
            {
                Debug.Log($"[EventChannel] {name} raised to {ListenerCount} listener(s).", this);
            }

            Raised?.Invoke();
        }

        /// <summary>
        /// Drops all listeners when the asset unloads.
        /// </summary>
        /// <remarks>
        /// ScriptableObject assets outlive play mode in the editor. Without this, delegates captured
        /// from destroyed scene objects survive into the next play session and fire against dead
        /// references, producing errors that do not reproduce in a build.
        /// </remarks>
        private void OnDisable() => Raised = null;
    }
}
