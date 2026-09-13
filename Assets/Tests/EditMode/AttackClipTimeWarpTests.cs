using AdaptiveBossArena.Combat.Feel;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests that an attack's clip strikes while its hitbox is live, whatever the two lengths are.
    /// </summary>
    [TestFixture]
    public sealed class AttackClipTimeWarpTests
    {
        // A light slash: 0.09 s wind-up, 0.07 s live, 0.2 s recovery, on a 1.1 s clip landing at 0.45 s.
        private static AttackClipTimeWarp Light() => new AttackClipTimeWarp(0.09f, 0.07f, 0.2f, 1.1f, 0.45f, 0.1f);

        // A heavy on the same clip: a much longer wind-up and recovery.
        private static AttackClipTimeWarp Heavy() => new AttackClipTimeWarp(0.34f, 0.10f, 0.52f, 1.1f, 0.45f, 0.1f);

        [Test]
        public void TheBladeLandsInsideTheActiveWindow()
        {
            AttackClipTimeWarp warp = Light();
            float midActive = warp.ClipTimeAt(0.09f + 0.035f);

            Assert.GreaterOrEqual(midActive, warp.ContactStart);
            Assert.LessOrEqual(midActive, warp.ContactEnd);
            Assert.AreEqual(0.45f, midActive, 0.01f, "The middle of the live window is not the contact frame.");
        }

        [Test]
        public void TheWindUpNeverShowsTheContact()
        {
            AttackClipTimeWarp warp = Light();

            Assert.Less(warp.ClipTimeAt(0.089f), warp.ContactStart + 0.001f);
        }

        [Test]
        public void ClipTimeNeverRunsBackwards()
        {
            // A pose that jumped backwards mid-swing would read as a stutter.
            AttackClipTimeWarp warp = Heavy();
            float previous = -1f;

            for (float t = 0f; t <= 1f; t += 0.005f)
            {
                float clip = warp.ClipTimeAt(t);
                Assert.GreaterOrEqual(clip, previous, "Clip time ran backwards at " + t);
                previous = clip;
            }
        }

        [Test]
        public void OneClipServesAQuickSwingAndASlowOneAlike()
        {
            Assert.AreEqual(0.45f, Light().ClipTimeAt(0.09f + 0.035f), 0.01f);
            Assert.AreEqual(0.45f, Heavy().ClipTimeAt(0.34f + 0.05f), 0.01f);
        }

        [Test]
        public void TheSwingEndsOnTheClipsLastFrame()
        {
            AttackClipTimeWarp warp = Light();

            Assert.AreEqual(1.1f, warp.ClipTimeAt(10f), 0.001f);
            Assert.AreEqual(1f, warp.NormalizedTimeAt(10f), 0.001f);
        }
    }
}
