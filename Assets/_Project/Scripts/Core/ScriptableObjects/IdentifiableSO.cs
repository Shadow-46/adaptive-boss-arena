using UnityEngine;

namespace AdaptiveBossArena.Core.ScriptableObjects
{
    /// <summary>
    /// Base class for configuration assets that need a stable identity and designer notes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The identifier is stable across renames and file moves, which matters because saved data and
    /// analytics reference strategies and attacks by identifier. Unity's own asset GUIDs would work
    /// but are unreadable in a save file or a debug overlay.
    /// </para>
    /// <para>
    /// Designer notes are stripped from builds; they exist so the reasoning behind a tuning value
    /// lives next to the value rather than in someone's memory.
    /// </para>
    /// </remarks>
    public abstract class IdentifiableSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        [Tooltip("Stable identifier used by save data and debug tooling. Must be unique and must not " +
                 "change once the asset ships, or saved data will fail to resolve it.")]
        private string _id = string.Empty;

        [SerializeField]
        [TextArea(2, 6)]
        [Tooltip("Editor-only rationale for this asset's values. Not included in builds.")]
        private string _designerNotes = string.Empty;

        /// <summary>Stable identifier for this asset.</summary>
        public string Id => _id;

        /// <summary>True when the asset has been given an identifier.</summary>
        public bool HasValidId => !string.IsNullOrWhiteSpace(_id);

#if UNITY_EDITOR
        /// <summary>Editor-only designer rationale.</summary>
        public string DesignerNotes => _designerNotes;

        /// <summary>Assigns the identifier. Available to editor tooling only.</summary>
        /// <param name="id">The identifier to assign.</param>
        public void EditorSetId(string id) => _id = id;

        /// <summary>Defaults the identifier to the asset's file name when left blank.</summary>
        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                _id = name;
            }
        }
#endif
    }
}
