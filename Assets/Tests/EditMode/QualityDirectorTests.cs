using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Game;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Asserts how the game picks and adjusts its graphics quality.
    /// </summary>
    /// <remarks>
    /// The browser demo was tuned for integrated graphics because that was the first machine it met; the player's
    /// laptop has a dedicated card and the demo looked flat on it. The quality now follows the machine, so the rules
    /// that decide it are pinned here.
    /// </remarks>
    [TestFixture]
    public sealed class QualityDirectorTests
    {
        [Test]
        public void TheBrowserStartsOnMediumBecauseItCannotSeeTheCard() =>
            Assert.AreEqual(GraphicsQuality.Medium, QualityDirector.StartingQuality(true, 8000, "NVIDIA GeForce RTX 5050"));

        [Test]
        public void ADedicatedCardStartsOnHigh() =>
            Assert.AreEqual(GraphicsQuality.High, QualityDirector.StartingQuality(false, 8188, "NVIDIA GeForce RTX 5050 Laptop GPU"));

        [TestCase("Intel(R) UHD Graphics 620", 4096)]
        [TestCase("Intel(R) Iris(R) Xe Graphics", 8000)]
        [TestCase("AMD Radeon(TM) Graphics", 2048)]
        [TestCase("Unknown", 1024)]
        public void IntegratedOrSmallGraphicsStartOnLow(string device, int memory) =>
            Assert.AreEqual(GraphicsQuality.Low, QualityDirector.StartingQuality(false, memory, device));

        [Test]
        public void ASmoothFrameStepsUpAndAStutterStepsDown()
        {
            Assert.AreEqual(GraphicsQuality.High, QualityDirector.AdjustedQuality(GraphicsQuality.Medium, 8f));
            Assert.AreEqual(GraphicsQuality.Low, QualityDirector.AdjustedQuality(GraphicsQuality.Medium, 30f));
            Assert.AreEqual(GraphicsQuality.Medium, QualityDirector.AdjustedQuality(GraphicsQuality.Medium, 15f));
            Assert.AreEqual(GraphicsQuality.High, QualityDirector.AdjustedQuality(GraphicsQuality.High, 8f));
            Assert.AreEqual(GraphicsQuality.Low, QualityDirector.AdjustedQuality(GraphicsQuality.Low, 30f));
        }

        [Test]
        public void AQualityIsFoundByNameAmongTheLevelsABuildKeeps()
        {
            // A Windows build keeps only the desktop levels; a browser build only its own.
            string[] desktop = { "Desktop Low", "Desktop Medium", "Desktop High" };
            string[] browser = { "Browser Low", "Browser Medium", "Browser High" };
            string[] all = { "Browser Low", "Browser Medium", "Browser High", "Desktop Low", "Desktop Medium", "Desktop High" };

            Assert.AreEqual(2, QualityDirector.LevelIndex(desktop, GraphicsQuality.High));
            Assert.AreEqual(0, QualityDirector.LevelIndex(browser, GraphicsQuality.Low));
            Assert.AreEqual(1, QualityDirector.LevelIndex(all, GraphicsQuality.Medium));
        }
    }
}
