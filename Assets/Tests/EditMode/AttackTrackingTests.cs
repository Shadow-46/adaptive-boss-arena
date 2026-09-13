using AdaptiveBossArena.Combat;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests when a swing stops following its target, which is what makes a late dodge work.
    /// </summary>
    [TestFixture]
    public sealed class AttackTrackingTests
    {
        private const float Startup = 0.5f;
        private const float Window = 0.6f;

        [Test]
        public void ASwingTracksEarlyInItsWindUp()
        {
            Assert.IsTrue(AttackTracking.CanTrack(AttackPhase.Startup, 0.1f, Startup, Window));
        }

        [Test]
        public void ASwingCommitsBeforeItsWindUpEnds()
        {
            // The last stretch of the wind-up is committed. A swing that could still turn here would
            // follow a late dodge straight into the strike.
            Assert.IsFalse(AttackTracking.CanTrack(AttackPhase.Startup, 0.35f, Startup, Window));
        }

        [Test]
        public void ALiveOrRecoveringSwingNeverTurns()
        {
            Assert.IsFalse(AttackTracking.CanTrack(AttackPhase.Active, 0f, Startup, Window));
            Assert.IsFalse(AttackTracking.CanTrack(AttackPhase.Recovery, 0f, Startup, Window));
        }

        [Test]
        public void AZeroWindowMeansNoTrackingAtAll()
        {
            Assert.IsFalse(AttackTracking.CanTrack(AttackPhase.Startup, 0f, Startup, 0f));
        }
    }
}
