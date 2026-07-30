using AdaptiveBossArena.Learning;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the boss's Resolve meter: it fills from time and from being dodged, and empties when
    /// spent.
    /// </summary>
    /// <remarks>
    /// The fairness point is that Resolve only ever accrues from things the boss legitimately
    /// witnesses, and that an evasive player fills it faster — the gambit is earned by the fight's
    /// state, not handed to the boss.
    /// </remarks>
    [TestFixture]
    public sealed class BossResolveTests
    {
        private static BossResolve Resolve() =>
            new BossResolve(threshold: 100f, buildPerSecond: 5f, buildPerWhiff: 20f);

        [Test]
        public void AMeterStartsEmptyAndNotReady()
        {
            BossResolve resolve = Resolve();
            Assert.AreEqual(0f, resolve.Normalized);
            Assert.IsFalse(resolve.IsReady);
        }

        [Test]
        public void TimeFillsIt()
        {
            BossResolve resolve = Resolve();
            for (int i = 0; i < 200; i++) // 200 * 0.1s = 20s at 5/s = 100
            {
                resolve.Tick(0.1f);
            }

            Assert.IsTrue(resolve.IsReady);
        }

        [Test]
        public void BeingDodgedFillsItFaster()
        {
            BossResolve resolve = Resolve();
            for (int i = 0; i < 5; i++)
            {
                resolve.RegisterBossWhiff(); // 5 * 20 = 100
            }

            Assert.IsTrue(resolve.IsReady, "Five whiffed swings should be enough to wind up a gambit.");
        }

        [Test]
        public void ItNeverExceedsFull()
        {
            BossResolve resolve = Resolve();
            for (int i = 0; i < 20; i++)
            {
                resolve.RegisterBossWhiff();
            }

            Assert.AreEqual(1f, resolve.Normalized, 0.001f);
        }

        [Test]
        public void SpendingEmptiesIt()
        {
            BossResolve resolve = Resolve();
            for (int i = 0; i < 5; i++)
            {
                resolve.RegisterBossWhiff();
            }

            resolve.Spend();

            Assert.IsFalse(resolve.IsReady);
            Assert.AreEqual(0f, resolve.Normalized);
        }
    }
}
