using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Guards that a build actually ships the two scenes, title first.
    /// </summary>
    /// <remarks>
    /// A build compiles cleanly even with an empty scene list and then boots to a black screen, which
    /// is the kind of failure that only shows up after the artefact has been handed to someone. The
    /// scene generator pins this order; this test makes a regression fail in CI instead of in a player's
    /// hands.
    /// </remarks>
    [TestFixture]
    public sealed class BuildSettingsTests
    {
        private const string TitleScene = "Assets/_Project/Scenes/MainMenu.unity";
        private const string ArenaScene = "Assets/_Project/Scenes/Arena.unity";

        private static string[] EnabledScenes() =>
            EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();

        [Test]
        public void BothScenesAreRegisteredAndEnabled()
        {
            string[] enabled = EnabledScenes();

            CollectionAssert.Contains(enabled, TitleScene, "The title scene is not in the build.");
            CollectionAssert.Contains(enabled, ArenaScene, "The arena scene is not in the build.");
        }

        [Test]
        public void TheTitleSceneLoadsFirst()
        {
            Assert.AreEqual(TitleScene, EnabledScenes().FirstOrDefault(),
                "The title scene must be first, or the game boots straight into the fight.");
        }
    }
}
