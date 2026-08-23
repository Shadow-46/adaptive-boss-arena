using AdaptiveBossArena.Combat;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the rule that decides when two swings met each other rather than their targets.
    /// </summary>
    /// <remarks>
    /// A clash refuses two hits that would otherwise have landed, so every one of these is a
    /// fairness contract: too loose and a fighter loses a hit they had already earned; too tight and
    /// simultaneous attacks stay the damage race the rule exists to replace.
    /// </remarks>
    [TestFixture]
    public sealed class ClashResolverTests
    {
        [Test]
        public void TwoSwingsMeetingHeadOnClash()
        {
            Assert.IsTrue(ClashResolver.ShouldClash(
                firstIsSwinging: true, secondIsSwinging: true,
                separation: 2f, combinedReach: 5f, facingDot: 1f));
        }

        [Test]
        public void OneSwingAloneIsAHitNotAClash()
        {
            Assert.IsFalse(ClashResolver.ShouldClash(
                firstIsSwinging: true, secondIsSwinging: false,
                separation: 2f, combinedReach: 5f, facingDot: 1f));

            Assert.IsFalse(ClashResolver.ShouldClash(
                firstIsSwinging: false, secondIsSwinging: true,
                separation: 2f, combinedReach: 5f, facingDot: 1f));
        }

        [Test]
        public void SwingsWhoseRangesMerelyOverlapDoNotClash()
        {
            // Both reach far enough that their ranges cross somewhere, but the fighters are not in
            // the same place. Blades that never met must not refuse each other.
            Assert.IsFalse(ClashResolver.ShouldClash(
                firstIsSwinging: true, secondIsSwinging: true,
                separation: 4f, combinedReach: 6f, facingDot: 1f));
        }

        [Test]
        public void AFighterStruckFromTheSideIsNotClashing()
        {
            // Being caught out is not being matched. Turning this into a clash would take away a hit
            // the attacker had already won by getting round the side.
            Assert.IsFalse(ClashResolver.ShouldClash(
                firstIsSwinging: true, secondIsSwinging: true,
                separation: 1f, combinedReach: 5f, facingDot: 0.2f));
        }

        [Test]
        public void FacingIsTheWorseOfTheTwoFighters()
        {
            // One looking straight at the other, the other looking away. Taking the smaller of the
            // pair is what makes one fighter's inattention enough to settle it as a hit.
            float dot = ClashResolver.FacingDot(
                Vector3.zero, Vector3.forward,
                Vector3.forward * 2f, Vector3.forward);

            Assert.Less(dot, 0f, "A fighter facing away should drag the pair's alignment negative.");
        }

        [Test]
        public void TwoFightersLookingAtEachOtherAreFullyAligned()
        {
            float dot = ClashResolver.FacingDot(
                Vector3.zero, Vector3.forward,
                Vector3.forward * 2f, Vector3.back);

            Assert.AreEqual(1f, dot, 0.001f);
        }

        [Test]
        public void HeightDoesNotChangeWhoIsFacingWhom()
        {
            // Measured on the horizontal plane, matching how the hit arcs are measured. A fighter
            // standing higher is not facing any less squarely.
            float level = ClashResolver.FacingDot(
                Vector3.zero, Vector3.forward,
                Vector3.forward * 2f, Vector3.back);

            float raised = ClashResolver.FacingDot(
                Vector3.zero, Vector3.forward,
                new Vector3(0f, 1.5f, 2f), Vector3.back);

            Assert.AreEqual(level, raised, 0.001f);
        }
    }
}
