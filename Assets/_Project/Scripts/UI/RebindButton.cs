using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveBossArena.UI
{
    /// <summary>
    /// One rebindable control row: a button whose label shows the current key and, when clicked,
    /// listens for a new one.
    /// </summary>
    /// <remarks>
    /// A component per row rather than a lambda, because a button's click event carries no arguments,
    /// so the row it belongs to has to be discoverable from the callback. This holds exactly the three
    /// facts the rebind needs — which action, which binding, and where to write the result — and hands
    /// itself to the menu, which owns the interactive rebind operation and the input asset.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RebindButton : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Name of the action this row rebinds.")]
        private string _actionName;

        [SerializeField]
        [Tooltip("Index of the binding within the action to rebind (the keyboard one).")]
        private int _bindingIndex;

        [SerializeField]
        [Tooltip("Label showing the current key, updated after a rebind.")]
        private Text _label;

        [SerializeField]
        [Tooltip("The menu that owns the input asset and the rebind operation.")]
        private SettingsMenu _menu;

        /// <summary>Action this row rebinds.</summary>
        public string ActionName => _actionName;

        /// <summary>Binding index within the action.</summary>
        public int BindingIndex => _bindingIndex;

        /// <summary>Starts a rebind for this row. Bound to the row's button.</summary>
        public void OnClick() => _menu?.BeginRebind(this);

        /// <summary>Writes the displayed key text.</summary>
        /// <param name="text">Human-readable binding, or a prompt while listening.</param>
        public void SetLabel(string text)
        {
            if (_label != null)
            {
                _label.text = text;
            }
        }

        /// <summary>Assigns the row's facts. Used by the interface generator.</summary>
        /// <param name="actionName">Action to rebind.</param>
        /// <param name="bindingIndex">Binding index within the action.</param>
        /// <param name="label">Label showing the current key.</param>
        /// <param name="menu">The settings menu.</param>
        public void Configure(string actionName, int bindingIndex, Text label, SettingsMenu menu)
        {
            _actionName = actionName;
            _bindingIndex = bindingIndex;
            _label = label;
            _menu = menu;
        }
    }
}
