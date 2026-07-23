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

        /// <summary>Loads the fight. Bound to the start button.</summary>
        public void StartGame() => SceneManager.LoadScene(_arenaSceneName);

        /// <summary>Quits the application, or leaves play mode in the editor. Bound to the quit button.</summary>
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Assigns the scene to load. Used by the scene generator.</summary>
        /// <param name="arenaSceneName">Name of the arena scene.</param>
        public void Bind(string arenaSceneName) => _arenaSceneName = arenaSceneName;
    }
}
