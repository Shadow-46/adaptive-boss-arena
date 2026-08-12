using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdaptiveBossArena.UI
{
    /// <summary>
    /// The title screen: the game's front door.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately its own scene rather than a panel over the arena. The fight scene spins up
    /// services, combatants and a learning loop the moment it loads; a menu has no business paying
    /// that cost, and keeping them separate means the fight always begins from a clean, freshly
    /// loaded state.
    /// </para>
    /// <para>
    /// Starting the game is a scene load rather than an additive reveal for the same reason a retry
    /// within a fight is <em>not</em> a scene load: the fight wants one clean construction up front,
    /// and then to reset itself in place so a seed survives repeated attempts.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Name of the arena scene to load. Must be in the build settings.")]
        private string _arenaSceneName = "Arena";

        [SerializeField]
        [Tooltip("The quit button, hidden on platforms where quitting does nothing.")]
        private GameObject _quitButton;

        /// <summary>Loads the fight. Bound to the start button.</summary>
        public void StartGame() => SceneManager.LoadScene(_arenaSceneName);

        /// <summary>
        /// Quits the application, or leaves play mode in the editor. Bound to the quit button.
        /// </summary>
        /// <remarks>
        /// Does nothing in a browser, where a page cannot close its own tab. The button is hidden
        /// there instead of sitting on the front door doing nothing — see <see cref="Start"/>.
        /// </remarks>
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif !UNITY_WEBGL
            Application.Quit();
#endif
        }

        private void Start()
        {
            if (_quitButton != null && !PauseMenu.CanQuit)
            {
                _quitButton.SetActive(false);
            }
        }

        /// <summary>Assigns the scene to load. Used by the scene generator.</summary>
        /// <param name="arenaSceneName">Name of the arena scene.</param>
        /// <param name="quitButton">Quit button, hidden where quitting is meaningless.</param>
        public void Bind(string arenaSceneName, GameObject quitButton = null)
        {
            _arenaSceneName = arenaSceneName;
            _quitButton = quitButton;
        }
    }
}
