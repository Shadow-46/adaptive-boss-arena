using AdaptiveBossArena.AI;
using AdaptiveBossArena.AI.States;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Learning;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests that what the boss learns changes what it visibly does.
    /// </summary>
    /// <remarks>
    /// Written after an audit found most counters moved a number nothing read strongly: a tell appeared and
    /// the boss fought the same. Each test here pins one learned lever to a behaviour a player would notice.
    /// </remarks>
    [TestFixture]
    public sealed class LearnedBehaviourTests
    {
        [Test]
        public void ALearnedAnswerOutweighsAnAttackMerelyInRange()
        {
            // An attack in range scores about one; a learned answer of two must make the blow that beats the
            // habit the likelier pick.
            var tuning = new BossTuning(3f, 0.5f);
            tuning.SetBaseline(BossTuningParameter.AnswerGuardWeight, 2f);
            tuning.ResetToBaseline();

            Assert.AreEqual(2f, BossAttackSelector.AnswerBias(tuning, HabitAnswer.Guard | HabitAnswer.Crowding), 0.001f);
            Assert.AreEqual(0f, BossAttackSelector.AnswerBias(tuning, HabitAnswer.Circling), 0.001f);
        }

        [Test]
        public void AnUnlearnedBossAnswersNothingInParticular()
        {
            var tuning = new BossTuning(3f, 0.5f);

            Assert.AreEqual(0f, BossAttackSelector.AnswerBias(tuning, HabitAnswer.Guard | HabitAnswer.Retreat));
        }

        [Test]
        public void HavingLearnedTheSideItCirclesWithThePlayer()
        {
            Assert.AreEqual(-1f, BossPacing.StrafeSideFor(cutOffStrength: 1f, habitualCirclingSide: -0.8f));
            Assert.AreEqual(1f, BossPacing.StrafeSideFor(cutOffStrength: 1f, habitualCirclingSide: 0.8f));
        }

        [Test]
        public void UntaughtOrUnsureItCirclesItsOwnWay()
        {
            Assert.AreEqual(1f, BossPacing.StrafeSideFor(cutOffStrength: 0f, habitualCirclingSide: -0.9f));
            Assert.AreEqual(1f, BossPacing.StrafeSideFor(cutOffStrength: 1f, habitualCirclingSide: -0.1f));
        }

        [Test]
        public void APressingBossSwingsMoreOften()
        {
            float neutral = BossAttackState.AttackCooldownFor(1f, 0f, isFeint: false, aggression: 0.5f);
            float pressing = BossAttackState.AttackCooldownFor(1f, 0f, isFeint: false, aggression: 1f);

            Assert.AreEqual(1f, neutral, 0.001f);
            Assert.AreEqual(BossAttackState.PressuredCooldownShare, pressing, 0.001f);
        }

        [Test]
        public void APressingBossLetsFewerOpeningsPassButNeverNone()
        {
            float neutral = BossController.MissedOpportunityChance(0.12f, 0.5f);
            float pressing = BossController.MissedOpportunityChance(0.12f, 1f);

            Assert.AreEqual(0.12f, neutral, 0.001f);
            Assert.Less(pressing, neutral * 0.5f);
            Assert.Greater(pressing, 0f, "A boss that never hesitates has lost its designed fallibility.");
        }

        [Test]
        public void OpeningsAreTakenRarelyUntilLearned()
        {
            var tuning = new BossTuning(3f, 0.5f);

            Assert.AreEqual(BossTuning.BaselinePunishChance, tuning.Get(BossTuningParameter.PunishHealChance));
            Assert.AreEqual(BossTuning.BaselinePunishChance, tuning.Get(BossTuningParameter.PunishWhiffChance));
            Assert.AreEqual(BossTuning.BaselineHoldWindupChance, tuning.Get(BossTuningParameter.HoldWindupChance));
            Assert.Less(BossTuning.BaselinePunishChance, 0.2f);
        }
    }
}
