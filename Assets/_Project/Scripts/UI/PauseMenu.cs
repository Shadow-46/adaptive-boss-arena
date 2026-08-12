using System;
using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.Core.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

        [SerializeField]
        [Tooltip("Settings menu opened from this pause menu. Optional, but without it the pause key " +
                 "would dismiss the pause panel and leave the settings panel over a running fight.")]
        private SettingsMenu _settingsMenu;

        [SerializeField]
        [Tooltip("The quit button, hidden on platforms where quitting does nothing.")]
        private GameObject _quitButton;

        [Header("Navigation")]
        [SerializeField]
        [Tooltip("Name of the title scene to return to. Must be in the build settings.")]
        private string _mainMenuSceneName = "MainMenu";

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

            if (_quitButton != null && !CanQuit)
            {
                _quitButton.SetActive(false);
            }
        }

        private void Update()
        {
            if (_isSuppressed || _pauseAction == null)
            {
                return;
            }

            if (!_pauseAction.WasPressedThisFrame())
            {
                return;
            }

            // Settings is built onto the canvas root, so it is a sibling of the pause panel rather
            // than a child of it. Without this the pause key would hide the pause panel, resume the
            // fight, and leave the near-opaque settings panel sitting over a live boss.
            if (_settingsMenu != null && _settingsMenu.IsOpen)
            {
                _settingsMenu.Close();
                return;
            }

            SetPaused(!_isPaused);
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

        /// <summary>
        /// Abandons the fight and returns to the title. Bound to the panel's main-menu button.
        /// </summary>
        /// <remarks>
        /// Unpauses before loading, without which the freshly loaded title would inherit a zero
        /// time scale and sit there apparently hung. The challenge modifiers live on the title
        /// screen, so without this route they are unreachable for the rest of the session once the
        /// first fight begins — and in a browser, where quitting does nothing, this is the only way
        /// out of the arena at all.
        /// </remarks>
        public void ReturnToMenu()
        {
            SetPaused(false);
            SceneManager.LoadScene(_mainMenuSceneName);
        }

        /// <summary>Abandons the attempt and asks for a restart. Bound to the panel's restart button.</summary>
        public void Restart()
        {
            SetPaused(false);
            RestartRequested?.Invoke();
        }

        /// <summary>
        /// Quits the application, or leaves play mode in the editor.
        /// </summary>
        /// <remarks>
        /// A browser tab cannot be closed by the page inside it, so on WebGL
        /// <see cref="Application.Quit"/> does nothing at all. The button is therefore hidden at
        /// runtime on that platform rather than left on screen doing nothing — hidden at runtime and
        /// not at generation time, because the scene is generated by an editor script that has no
        /// idea which platform the build will target. Returning to the title is the meaningful exit
        /// in a browser.
        /// </remarks>
        public void Quit()
        {
            SetPaused(false);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif !UNITY_WEBGL
            Application.Quit();
#endif
        }

        /// <summary>
        /// Whether quitting is meaningful on this platform, so a dead button is never shown.
        /// </summary>
        public static bool CanQuit
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            get => false;
#else
            get => true;
#endif
        }

        /// <summary>Assigns the references. Used by the interface generator.</summary>
        /// <param name="actions">Input actions asset.</param>
        /// <param name="panel">Root object shown while paused.</param>
        /// <param name="pauseStateChannel">Channel carrying the paused state.</param>
        /// <param name="settingsMenu">Settings menu this pause menu opens, so it can be closed again.</param>
        /// <param name="quitButton">Quit button, hidden where quitting is meaningless.</param>
        /// <param name="mainMenuSceneName">Title scene to return to.</param>
        public void Bind(
            InputActionAsset actions,
            GameObject panel,
            BoolEventChannel pauseStateChannel,
            SettingsMenu settingsMenu = null,
            GameObject quitButton = null,
            string mainMenuSceneName = "MainMenu")
        {
            _actions = actions;
            _panel = panel;
            _pauseStateChannel = pauseStateChannel;
            _settingsMenu = settingsMenu;
            _quitButton = quitButton;
            _mainMenuSceneName = mainMenuSceneName;
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

            // Covers every other way the fight can resume — the Resume button, restarting, or the
            // suppression that fires when the fight ends. Settings must never outlive the pause it
            // was opened from.
            if (!_isPaused && _settingsMenu != null && _settingsMenu.IsOpen)
            {
                _settingsMenu.Close();
            }
        }
    }
}
