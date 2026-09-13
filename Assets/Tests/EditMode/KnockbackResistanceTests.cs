using AdaptiveBossArena.Combat.Vitals;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the rule that makes the boss movable in proportion to how worn down its stance is.
    /// </summary>
    [TestFixture]
    public sealed class KnockbackResistanceTests
    {
        private const float AtFull = 0.85f;
        private const float WhenBroken = 0f;

        [Test]
        public void AFreshStanceShrugsOffMostOfABlow()
        {
            float shove = KnockbackResistance.ShoveMultiplier(1f, false, AtFull, WhenBroken);

            Assert.AreEqual(0.15f, shove, 0.001f);
        }

        [Test]
        public void ABrokenStanceTakesTheWholeBlow()
        {
            // Checked with full poise reported, because the pool refills to full at the end of a break
            // and must not be read as a fresh stance while the break is still in effect.
            float shove = KnockbackResistance.ShoveMultiplier(1f, true, AtFull, WhenBroken);

            Assert.AreEqual(1f, shove, 0.001f);
        }

        [Test]
        public void WearingTheStanceDownMakesItGiveGroundSteadily()
        {
            float fresh = KnockbackResistance.ShoveMultiplier(1f, false, AtFull, WhenBroken);
            float worn = KnockbackResistance.ShoveMultiplier(0.5f, false, AtFull, WhenBroken);
            float nearlyGone = KnockbackResistance.ShoveMultiplier(0.1f, false, AtFull, WhenBroken);

            Assert.Less(fresh, worn);
            Assert.Less(worn, nearlyGone);
        }

        [Test]
        public void OutOfRangeValuesNeverProduceANegativeOrAmplifiedShove()
        {
            Assert.AreEqual(0f, KnockbackResistance.ShoveMultiplier(5f, false, 2f, 0f), 0.001f);
            Assert.AreEqual(1f, KnockbackResistance.ShoveMultiplier(-3f, false, AtFull, -1f), 0.001f);
        }
    }
}
