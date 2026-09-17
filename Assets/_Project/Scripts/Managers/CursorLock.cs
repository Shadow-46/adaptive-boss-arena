using AdaptiveBossArena.UI;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Captures the mouse during the fight and frees it for menus.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The camera now orbits with the mouse, which only works with the pointer captured: a free pointer runs off
    /// the edge of the window and stops turning, or in a browser clicks the page behind the game. A menu needs
    /// the pointer back, so capture follows what is on screen rather than being set once.
    /// </para>
    /// <para>
    /// A browser only grants pointer capture after a click, so on the web the first click into the fight is
    /// what captures it; asking again every frame until then is how Unity's WebGL player expects to be asked.
    /// Escape releases it in a browser whatever the game says, and Escape also opens the pause menu, so the two
    /// agree.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CursorLock : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The pause menu; the pointer is freed while it is open.")]
        private PauseMenu _pauseMenu;

        [SerializeField]
        [Tooltip("The outcome screen; the pointer is freed while it shows.")]
        private EndScreen _endScreen;

        [SerializeField]
        [Tooltip("The settings panel; the pointer is freed while it is open.")]
        private SettingsMenu _settingsMenu;

        /// <summary>Whether the pointer is currently captured by the game.</summary>
        public static bool IsCaptured => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>Whether the fight should hold the pointer, given what is on screen.</summary>
        /// <param name="paused">The pause menu is open.</param>
        /// <param name="outcomeShown">The victory or defeat screen is showing.</param>
        /// <param name="settingsOpen">The settings panel is open.</param>
        /// <returns>True when nothing on screen needs the pointer.</returns>
        public static bool ShouldCapture(bool paused, bool outcomeShown, bool settingsOpen) =>
            !paused && !outcomeShown && !settingsOpen;

        /// <summary>Assigns the menus that need the pointer. Used by the scene generator.</summary>
        /// <param name="pauseMenu">The pause menu.</param>
        /// <param name="endScreen">The outcome screen.</param>
        /// <param name="settingsMenu">The settings panel.</param>
        public void Bind(PauseMenu pauseMenu, EndScreen endScreen, SettingsMenu settingsMenu)
        {
            _pauseMenu = pauseMenu;
            _endScreen = endScreen;
            _settingsMenu = settingsMenu;
        }

        private void Update()
        {
            bool capture = ShouldCapture(
                _pauseMenu != null && _pauseMenu.IsPaused,
                _endScreen != null && _endScreen.IsShown,
                _settingsMenu != null && _settingsMenu.IsOpen);

            if (capture && !IsCaptured)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (!capture && Cursor.lockState != CursorLockMode.None)
            {
                Release();
            }
        }

        // Leaving the arena - to the title, or on quit - must never strand the pointer captured.
        private void OnDisable() => Release();

        private static void Release()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
