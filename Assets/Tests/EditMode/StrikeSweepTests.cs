using AdaptiveBossArena.Combat;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Asserts a striking part's path between two frames is covered closely enough that no body is skipped.
    /// </summary>
    /// <remarks>
    /// Hits are now decided by what the blade actually touched. A great sword's tip crosses more than a metre in
    /// one frame of a swing, so testing only where it ends up would let it pass clean through the knight between
    /// frames - the opposite of the "blade passed beside me but still hurt" bug, and just as dishonest.
    /// </remarks>
    [TestFixture]
    public sealed class StrikeSweepTests
    {
        [Test]
        public void APartThatHasNotMovedIsTestedOnce()
        {
            int samples = StrikeSweep.SampleCount(
                Vector3.zero, Vector3.forward, Vector3.zero, Vector3.forward, radius: 0.1f, maximum: 6);

            Assert.AreEqual(1, samples);
        }

        [Test]
        public void NoStepIsLongerThanThePartIsThick()
        {
            // The tip swings 0.5 m; a 0.1 m blade needs five steps so the gap between them never exceeds its width.
            int samples = StrikeSweep.SampleCount(
                Vector3.zero, Vector3.forward, Vector3.zero, new Vector3(0.5f, 0f, 1f), radius: 0.1f, maximum: 10);

            Assert.AreEqual(5, samples);
        }

        [Test]
        public void ATeleportCannotCostUnboundedQueries()
        {
            int samples = StrikeSweep.SampleCount(
                Vector3.zero, Vector3.forward, Vector3.one * 100f, Vector3.one * 101f, radius: 0.1f, maximum: 6);

            Assert.AreEqual(6, samples);
        }

        [Test]
        public void TheSweepRunsFromLastFramesPoseToThisOne()
        {
            Vector3 previousStart = Vector3.zero;
            Vector3 previousEnd = Vector3.forward;
            Vector3 start = Vector3.right;
            Vector3 end = Vector3.right + Vector3.forward;

            StrikeSweep.Between(previousStart, previousEnd, start, end, 0f, out Vector3 a0, out Vector3 b0);
            StrikeSweep.Between(previousStart, previousEnd, start, end, 0.5f, out Vector3 aHalf, out Vector3 bHalf);
            StrikeSweep.Between(previousStart, previousEnd, start, end, 1f, out Vector3 a1, out Vector3 b1);

            Assert.AreEqual(previousStart, a0);
            Assert.AreEqual(previousEnd, b0);
            Assert.AreEqual(new Vector3(0.5f, 0f, 0f), aHalf);
            Assert.AreEqual(new Vector3(0.5f, 0f, 1f), bHalf);
            Assert.AreEqual(start, a1);
            Assert.AreEqual(end, b1);
        }
    }
}
