using UnityEngine;

namespace AdaptiveBossArena.Core.Events
{
    /// <summary>
    /// Base for asset-backed event channels that decouple broadcasters from listeners.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A channel is an asset, so a broadcaster and a listener each reference the channel and never
    /// each other. That is what lets the heads-up display show boss health without the UI assembly
    /// knowing the boss exists, and what lets the victory screen react to a death it never
    /// subscribed to directly.
    /// </para>
    /// <para>
    /// Use channels for cross-system and user-interface signals that fire occasionally. Do not use
    /// them for per-frame combat traffic such as hitbox overlap results; those go through direct
    /// C# events on interfaces, where the extra indirection is not worth its cost.
    /// </para>
    /// </remarks>
    public abstract class EventChannelBase : ScriptableObject
    {
        [SerializeField]
        [TextArea(2, 5)]
        [Tooltip("What this channel signals and who is expected to listen. Editor-only documentation.")]
        private string _description = string.Empty;

        [SerializeField]
        [Tooltip("Log every raise to the console. Useful when a signal appears not to arrive.")]
        private bool _logRaises;

        /// <summary>Whether raises should be written to the console.</summary>
        protected bool LogRaises => _logRaises;

#if UNITY_EDITOR
        /// <summary>Editor-only description of the channel's purpose.</summary>
        public string Description => _description;
#endif
    }
}
