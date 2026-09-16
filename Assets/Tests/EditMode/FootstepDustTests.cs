using AdaptiveBossArena.Combat.Feel;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests how much dust a footfall lifts.
    /// </summary>
    /// <remarks>
    /// Fighters crossed a stone floor without disturbing it. Dust on every step would be worse: a fighter
    /// edging into range would trail it behind them, so a creep lifts nothing at all.
    /// </remarks>
    [TestFixture]
    public sealed class FootstepDustTests
    {
        [Test]
        public void CreepingIntoRangeLiftsNoDust()
        {
            Assert.AreEqual(0, FootstepEmitter.PuffCount(0f));
            Assert.AreEqual(0, FootstepEmitter.PuffCount(0.2f));
        }

        [Test]
        public void ARunLiftsMoreThanAWalk()
        {
            int walk = FootstepEmitter.PuffCount(0.35f);
            int run = FootstepEmitter.PuffCount(1f);

            Assert.Greater(walk, 0, "A walk lifts nothing.");
            Assert.Greater(run, walk, "A run lifts no more than a walk.");
        }
    }
}
