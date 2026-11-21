using System.IO;
using AdaptiveBossArena.Game;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Points every save written during the play-mode tests at a throwaway folder, never at the player's own.
    /// </summary>
    /// <remarks>
    /// The editor and the Windows player share a persistent data path. Before this, the tests that win a fight
    /// in seconds wrote real records into the player's save: hundreds of attempts, a zero-second best, trials
    /// completed and the Ascended rematch unlocked by the harness. A set-up fixture covers every test in the
    /// namespace, so a new test cannot forget it.
    /// </remarks>
    [SetUpFixture]
    public sealed class TestSaveIsolation
    {
        /// <summary>Where the tests' saves go, under the system's temporary folder.</summary>
        public static string Folder => Path.Combine(Path.GetTempPath(), "AdaptiveBossArenaTestSaves");

        [OneTimeSetUp]
        public void RedirectSaves()
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, recursive: true);
            }

            Directory.CreateDirectory(Folder);
            JsonSaveService.DefaultRootOverride = Folder;
        }

        [OneTimeTearDown]
        public void RestoreSaves() => JsonSaveService.DefaultRootOverride = null;
    }
}
