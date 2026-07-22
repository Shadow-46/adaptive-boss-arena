using System;
using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.Core.Services;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AdaptiveBossArena.UI
{
    /// <summary>
    /// Suspends the fight and shows the pause panel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pausing goes through <see cref="ITimeService"/> rather than writing
    /// <c>Time.timeScale</c> directly. That single indirection is what stops the classic bug where
    /// unpausing during a hit-stop restores the wrong speed and leaves the game running in
    /// permanent slow motion.
    /// </para>
    /// <para>
    /// The panel itself ticks on unscaled time, so it still animates and responds while the world
    /// behind it is frozen.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PauseMenu : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField]
        [Tooltip("Generated input actions asset, used to find the Pause action.")]
        private InputActionAsset _actions;

        [SerializeField]
        [Tooltip("Action map containing the Pause action.")]
        private string _actionMapName = "Gameplay";

        [SerializeField]
        [Tooltip("Name of the pause action.")]
        private string _pauseActionName = "Pause";

        [Header("Presentation")]
        [SerializeField]
        [Tooltip("Root object shown while paused.")]
        private GameObject _panel;

        [Header("Broadcast")]
        [SerializeField]
        [Tooltip("Carries whether the game is now paused, for anything that needs to react.")]
        private BoolEventChannel _pauseStateChannel;

        private ITimeService _time;
        private InputAction _pauseAction;
        private bool _isPaused;
        private bool _isSuppressed;

        /// <summary>True while the game is paused by this menu.</summary>
        public bool IsPaused => _isPaused;

        /// <summary>Raised when the player asks to abandon the attempt.</summary>
        public event Action RestartRequested;

        private void Start()
        {
            ServiceRegistry.Current.TryGet(out _time);
            ResolvePauseAction();
            ApplyPanelVisibility();
        }

        private void Update()
        {
            if (_isSuppressed || _pauseAction == null)
            {
                return;
            }

            if (_pauseAction.WasPressedThisFrame())
            {
                SetPaused(!_isPaused);
            }
        }

        /// <summary>Pauses or resumes the fight.</summary>
        /// <param name="paused">True to pause.</param>
        public void SetPaused(bool paused)
        {
            if (_isPaused == paused)
            {
                return;
            }

            _isPaused = paused;
            _time?.SetPaused(paused);

            ApplyPanelVisibility();
            _pauseStateChannel?.Raise(paused);
        }

        /// <summary>
        /// Prevents pausing, for use once the fight has ended.
        /// </summary>
        /// <remarks>
        /// Without this a player could pause over the victory screen, and unpausing would resume a
        /// fight whose participants are already dead.
        /// </remarks>
        /// <param name="suppressed">True to ignore the pause input.</param>
        public void SetSuppressed(bool suppressed)
        {
            _isSuppressed = suppressed;

            if (suppressed && _isPaused)
            {
                SetPaused(false);
            }
        }

        /// <summary>Resumes the fight. Bound to the panel's resume button.</summary>
        public void Resume() => SetPaused(false);

        /// <summary>Abandons the attempt and asks for a restart. Bound to the panel's restart button.</summary>
        public void Restart()
        {
            SetPaused(false);
            RestartRequested?.Invoke();
        }

        /// <summary>Quits the application, or leaves play mode in the editor.</summary>
        public void Quit()
        {
            SetPaused(false);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Assigns the references. Used by the interface generator.</summary>
        /// <param name="actions">Input actions asset.</param>
        /// <param name="panel">Root object shown while paused.</param>
        /// <param name="pauseStateChannel">Channel carrying the paused state.</param>
        public void Bind(InputActionAsset actions, GameObject panel, BoolEventChannel pauseStateChannel)
        {
            _actions = actions;
            _panel = panel;
            _pauseStateChannel = pauseStateChannel;
        }

        private void ResolvePauseAction()
        {
            if (_actions == null)
            {
                Debug.LogWarning(
                    "[Adaptive Boss Arena] PauseMenu has no input actions asset; pausing is unavailable.",
                    this);
                return;
            }

            InputActionMap map = _actions.FindActionMap(_actionMapName, throwIfNotFound: false);
            _pauseAction = map?.FindAction(_pauseActionName, throwIfNotFound: false);

            // Enabled explicitly. The gameplay map is enabled by the player's input reader, but the
            // menu must keep working even if the player object is disabled or destroyed.
            _pauseAction?.Enable();
        }

        private void ApplyPanelVisibility()
        {
            if (_panel != null)
            {
                _panel.SetActive(_isPaused);
            }
        }
    }
}
