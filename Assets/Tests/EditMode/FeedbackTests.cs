using AdaptiveBossArena.Game;
using AdaptiveBossArena.UI;
using NUnit.Framework;
using UnityEngine;

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
        public void ADeathReachesTryAgainInUnderTwoSeconds()
        {
            // A death is the common case in a souls-hard fight; the verdict must not stand long between it and a retry.
            Assert.Less(EndScreen.CardOutEnd * EndScreen.DefeatCardPace, 2f);
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

    /// <summary>Tests for keeping the camera's lens out of Igris's body.</summary>
    [TestFixture]
    public sealed class CameraBodyClearanceTests
    {
        private const float Radius = 1.2f;
        private const float Height = 3f;

        [Test]
        public void ABoomRunningIntoHimStopsAtHisEdge()
        {
            // Pivot 3 m in front of him, the boom running straight back toward him.
            float clear = ArenaCameraRig.ClearOfBody(
                new Vector3(0f, 1.6f, -3f), Vector3.forward, 4f, Vector3.zero, Radius, Height);

            Assert.AreEqual(3f - Radius, clear, 1e-3f);
        }

        [Test]
        public void ABoomPassingBesideHimIsUntouched()
        {
            float clear = ArenaCameraRig.ClearOfBody(
                new Vector3(2f, 1.6f, -3f), Vector3.forward, 4f, Vector3.zero, Radius, Height);

            Assert.AreEqual(4f, clear, 1e-4f);
        }

        [Test]
        public void ABoomPassingOverHisHeadIsUntouched()
        {
            Vector3 up = new Vector3(0f, 1f, 1f).normalized;
            float clear = ArenaCameraRig.ClearOfBody(new Vector3(0f, 2.9f, -1.5f), up, 4f, Vector3.zero, Radius, Height);

            Assert.AreEqual(4f, clear, 1e-4f);
        }

        [Test]
        public void OverlappingFightersKeepTheFullBoom()
        {
            float clear = ArenaCameraRig.ClearOfBody(
                new Vector3(0.3f, 1.6f, 0f), Vector3.forward, 4f, Vector3.zero, Radius, Height);

            Assert.AreEqual(4f, clear, 1e-4f, "With the pivot inside him there is no edge to stop at.");
        }
    }
}
