using AdaptiveBossArena.Game;
using AdaptiveBossArena.UI;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>Tests for the fight's warning cues: the low-posture pulse and the perilous glint.</summary>
    [TestFixture]
    public sealed class FeedbackTests
    {
        [Test]
        public void ABarWarnsOnlyBetweenEmptyAndItsThreshold()
        {
            Assert.IsTrue(ResourceBar.IsLow(0.2f, 0.25f));
            Assert.IsFalse(ResourceBar.IsLow(0.3f, 0.25f), "Above the threshold is not low.");
            Assert.IsFalse(ResourceBar.IsLow(0f, 0.25f), "An empty stance is already broken; the break has its own beat.");
            Assert.IsFalse(ResourceBar.IsLow(0.1f, 0f), "A zero threshold turns the warning off.");
        }

        [Test]
        public void TheGlintRisesQuicklyAndFallsSlowly()
        {
            Assert.AreEqual(0f, PerilousFlare.Strength(0f), 1e-4f);
            Assert.AreEqual(1f, PerilousFlare.Strength(PerilousFlare.GrowSeconds), 1e-3f);
            Assert.Greater(PerilousFlare.FadeSeconds, PerilousFlare.GrowSeconds,
                "The burst is sudden so it catches the eye; the fade is long enough to be read.");
            Assert.AreEqual(0f, PerilousFlare.Strength(PerilousFlare.GrowSeconds + PerilousFlare.FadeSeconds), 1e-4f);
        }

        [Test]
        public void TheVerdictCardComesInHoldsAndGoesBeforeTheReport()
        {
            Assert.AreEqual(0f, EndScreen.CardAlpha(0f), "The last blow lands before the card.");
            Assert.AreEqual(1f, EndScreen.CardAlpha((EndScreen.CardInEnd + EndScreen.CardOutStart) * 0.5f), 1e-4f);
            Assert.AreEqual(0f, EndScreen.CardAlpha(EndScreen.CardOutEnd), 1e-4f);
            Assert.Greater(EndScreen.CardOutStart - EndScreen.CardInEnd, 1f, "Held long enough to be read.");
        }

        [Test]
        public void TheGlintPeaksWellInsideTheShortestPerilousWindUp()
        {
            // The grab, the quickest blow that cannot be blocked, winds up for 0.55 s. The glint must be at full
            // strength with most of that left for a human to see it and roll.
            const float shortestPerilousWindUp = 0.55f;
            Assert.Less(PerilousFlare.GrowSeconds, shortestPerilousWindUp * 0.3f);
        }
    }
}
