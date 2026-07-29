using System.Collections.Generic;
using AdaptiveBossArena.Learning;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the rules deciding which habits the post-fight dossier tells the player about.
    /// </summary>
    /// <remarks>
    /// The dossier is the payoff of the whole "it studied you" mechanic, so it must report only what
    /// the boss genuinely leaned on: strong, well-evidenced habits, strongest first. A confident
    /// reading of a habit's <em>absence</em> ("you almost never whiffed") must not masquerade as
    /// something the boss read in you.
    /// </remarks>
    [TestFixture]
    public sealed class ProfileReportTests
    {
        // Large sample counts read as confident, tiny ones as unsure, well clear of the floor.
        private const int Confident = 100;
        private const int Unsure = 1;
        private const int HalfConfidence = 10;

        private static BehaviorProfile ProfileWith(params (BehaviorFeature feature, float value, int samples)[] set)
        {
            var profile = new BehaviorProfile();
            foreach ((BehaviorFeature feature, float value, int samples) in set)
            {
                profile.Set(feature, value, samples, HalfConfidence);
            }

            return profile;
        }

        [Test]
        public void AnUnreadPlayerProducesNoLines()
        {
            Assert.IsEmpty(ProfileReport.HabitLines(new BehaviorProfile()));
        }

        [Test]
        public void OnlyStrongWellEvidencedHabitsAreReported()
        {
            BehaviorProfile profile = ProfileWith(
                (BehaviorFeature.HeavyAttackRatio, 0.85f, Confident),   // strong + confident -> in
                (BehaviorFeature.WhiffRatio, 0.05f, Confident),         // confident but absent -> out
                (BehaviorFeature.PreferredDistance, 0.9f, Unsure));     // strong but a guess -> out

            IReadOnlyList<string> lines = ProfileReport.HabitLines(profile);

            Assert.AreEqual(1, lines.Count);
            StringAssert.Contains("heavy swings", lines[0]);
        }

        [Test]
        public void HabitsAreOrderedByConfidenceWeightedStrength()
        {
            // Both qualify; heavy-attacks has the higher confidence-weighted value and must lead.
            BehaviorProfile profile = ProfileWith(
                (BehaviorFeature.HeavyAttackRatio, 0.95f, Confident),
                (BehaviorFeature.PreferredDistance, 0.5f, Confident));

            IReadOnlyList<string> lines = ProfileReport.HabitLines(profile);

            Assert.AreEqual(2, lines.Count);
            StringAssert.Contains("heavy swings", lines[0]);
            StringAssert.Contains("range", lines[1]);
        }

        [Test]
        public void EachReportedHabitCarriesAConfidencePercentage()
        {
            BehaviorProfile profile = ProfileWith((BehaviorFeature.EvasionSuccess, 0.9f, Confident));

            IReadOnlyList<string> lines = ProfileReport.HabitLines(profile);

            Assert.AreEqual(1, lines.Count);
            StringAssert.Contains("% sure", lines[0]);
        }
    }
}
