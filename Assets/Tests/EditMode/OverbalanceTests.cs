using AdaptiveBossArena.Learning;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests for the rule that decides when a committed boss swing that misses leaves the boss open.
    /// </summary>
    /// <remarks>
    /// The evaluator is pure, so the whole "adapting back" payoff is verified here rather than by
    /// feel. The load-bearing assertion is dormancy: at zero commitment the boss must never overbalance,
    /// because a fight it learns nothing in has to play exactly as it did before the mechanic existed.
    /// </remarks>
    [TestFixture]
    public sealed class OverbalanceTests
    {
        /// <summary>A representative evaluator; individual tests vary commitment and the roll.</summary>
        private static OverbalanceEvaluator Evaluator() =>
            new OverbalanceEvaluator(
                maxChance: 0.6f, intensityThreshold: 0.15f,
                recoverySecondsAtFull: 1.2f, poiseMultiplierAtFull: 2.5f);

        [Test]
        public void AtZeroCommitment_NeverOverbalances()
        {
            OverbalanceEvaluator evaluator = Evaluator();

            // roll 0 is the most favourable possible draw; even so, an unadapted boss must not stumble.
            Assert.IsFalse(evaluator.ShouldOverbalance(wasCommittedAttack: true, commitmentIntensity: 0f, roll: 0f));
            Assert.AreEqual(0f, evaluator.Chance(0f), 0.0001f);
        }

        [Test]
        public void BelowTheThreshold_NeverOverbalances()
        {
            OverbalanceEvaluator evaluator = Evaluator();

            // Minor adaptation sits in the dead zone, so a light commitment does not open the boss up.
            Assert.AreEqual(0f, evaluator.Chance(0.1f), 0.0001f);
            Assert.IsFalse(evaluator.ShouldOverbalance(true, 0.1f, 0f));
        }

        [Test]
        public void AnUncommittedSwing_NeverOverbalances()
        {
            OverbalanceEvaluator evaluator = Evaluator();

            // A light poke leaves no opening worth punishing, however hard the boss has committed.
            Assert.IsFalse(evaluator.ShouldOverbalance(wasCommittedAttack: false, commitmentIntensity: 1f, roll: 0f));
        }

        [Test]
        public void AFullyCommittedWhiff_CanOverbalance()
        {
            OverbalanceEvaluator evaluator = Evaluator();

            // At full commitment the chance is the maximum, so a roll below it stumbles the boss.
            Assert.AreEqual(0.6f, evaluator.Chance(1f), 0.0001f);
            Assert.IsTrue(evaluator.ShouldOverbalance(true, 1f, roll: 0.5f));
            Assert.IsFalse(evaluator.ShouldOverbalance(true, 1f, roll: 0.7f));
        }

        [Test]
        public void ChanceRisesWithCommitment()
        {
            OverbalanceEvaluator evaluator = Evaluator();

            Assert.Greater(evaluator.Chance(1f), evaluator.Chance(0.5f));
            Assert.Greater(evaluator.Chance(0.5f), evaluator.Chance(0.2f));
        }

        [Test]
        public void TheStumbleAndVulnerabilityGrowWithCommitment()
        {
            OverbalanceEvaluator evaluator = Evaluator();

            Assert.Greater(evaluator.ExtraRecoverySeconds(1f), evaluator.ExtraRecoverySeconds(0.3f));
            Assert.Greater(evaluator.PoiseVulnerabilityMultiplier(1f), evaluator.PoiseVulnerabilityMultiplier(0.3f));

            // The multiplier only ever makes the boss more vulnerable, never less.
            Assert.GreaterOrEqual(evaluator.PoiseVulnerabilityMultiplier(0f), 1f);
            Assert.AreEqual(2.5f, evaluator.PoiseVulnerabilityMultiplier(1f), 0.0001f);
        }

        [Test]
        public void ATriggeredStumble_IsLongEnoughToPunish()
        {
            OverbalanceEvaluator evaluator = Evaluator();

            // Even at the threshold edge the window is a usable fraction of the full stumble, not a
            // flicker the player could never react to.
            Assert.Greater(evaluator.ExtraRecoverySeconds(0.16f), 0f);
            Assert.GreaterOrEqual(evaluator.ExtraRecoverySeconds(1f), evaluator.ExtraRecoverySeconds(0.16f));
        }
    }
}
