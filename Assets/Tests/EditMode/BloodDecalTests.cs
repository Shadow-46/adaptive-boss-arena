using AdaptiveBossArena.Combat.Feel;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests how long blood stays on the floor.
    /// </summary>
    /// <remarks>
    /// A blow sprayed blood and the floor stayed clean, so a long fight left no trace of itself.
    /// </remarks>
    [TestFixture]
    public sealed class BloodDecalTests
    {
        [Test]
        public void FreshBloodIsAtFullStrength()
        {
            Assert.AreEqual(1f, BloodDecalPool.AlphaAt(0f));
            Assert.AreEqual(1f, BloodDecalPool.AlphaAt(BloodDecalPool.HoldSeconds));
        }

        [Test]
        public void OldBloodFadesAwayCompletely()
        {
            float midFade = BloodDecalPool.AlphaAt(BloodDecalPool.HoldSeconds + BloodDecalPool.FadeSeconds * 0.5f);

            Assert.That(midFade, Is.InRange(0.1f, 0.9f));
            Assert.AreEqual(0f, BloodDecalPool.AlphaAt(BloodDecalPool.HoldSeconds + BloodDecalPool.FadeSeconds), 0.0001f);
        }
    }
}
