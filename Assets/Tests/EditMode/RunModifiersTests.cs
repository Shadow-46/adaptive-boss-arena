using AdaptiveBossArena.Core.Services;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests that each challenge modifier maps to the number the fight actually applies.
    /// </summary>
    /// <remarks>
    /// The whole point of the modifiers is that they are numbers the existing systems already respect,
    /// not new rules. These assertions pin the mapping so a renamed flag or a changed multiplier can
    /// never silently turn a challenge into a no-op.
    /// </remarks>
    [TestFixture]
    public sealed class RunModifiersTests
    {
        [Test]
        public void AnUnmodifiedRunLeavesEveryNumberNeutral()
        {
            var modifiers = new RunModifiers();

            Assert.AreEqual(1f, modifiers.AdaptationRateScale);
            Assert.AreEqual(1f, modifiers.IncomingDamageMultiplier);
            Assert.IsTrue(modifiers.HealingEnabled);
            Assert.IsFalse(modifiers.AnyActive);
            Assert.IsEmpty(modifiers.ActiveLabels());
        }

        [Test]
        public void FastLearnerSpeedsAdaptationAndNothingElse()
        {
            var modifiers = new RunModifiers { FastLearner = true };

            Assert.Greater(modifiers.AdaptationRateScale, 1f);
            Assert.AreEqual(1f, modifiers.IncomingDamageMultiplier);
            Assert.IsTrue(modifiers.HealingEnabled);
        }

        [Test]
        public void NoHealingDisablesHealing()
        {
            Assert.IsFalse(new RunModifiers { NoHealing = true }.HealingEnabled);
        }

        [Test]
        public void FragileRaisesIncomingDamage()
        {
            Assert.Greater(new RunModifiers { FragilePlayer = true }.IncomingDamageMultiplier, 1f);
        }

        [Test]
        public void ActiveLabelsListEveryChosenModifier()
        {
            var modifiers = new RunModifiers
            {
                FastLearner = true,
                NoHealing = true,
                FragilePlayer = true,
                TrainingMode = true
            };

            Assert.IsTrue(modifiers.AnyActive);
            Assert.AreEqual(4, modifiers.ActiveLabels().Count);
            CollectionAssert.Contains(modifiers.ActiveLabels(), "Training");
        }

        [Test]
        public void AnOrdinaryRunCountsTowardRecords()
        {
            Assert.IsTrue(new RunModifiers().CountsTowardRecords);
        }

        [Test]
        public void TrainingRunsNeverCountTowardRecords()
        {
            // Training makes the player unkillable, so a "win" there is not one. Recording it would
            // permanently set the fastest victory and the fewest adaptations allowed, with no way to
            // take it back.
            Assert.IsFalse(new RunModifiers { TrainingMode = true }.CountsTowardRecords);
        }

        [Test]
        public void HarderModifiersStillCountTowardRecords()
        {
            // These three only make the fight harder, so a win under them is worth at least as much
            // as an ordinary one.
            Assert.IsTrue(new RunModifiers { FastLearner = true }.CountsTowardRecords);
            Assert.IsTrue(new RunModifiers { NoHealing = true }.CountsTowardRecords);
            Assert.IsTrue(new RunModifiers { FragilePlayer = true }.CountsTowardRecords);
        }
    }
}
