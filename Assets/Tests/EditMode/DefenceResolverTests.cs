using AdaptiveBossArena.Combat;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests for the rules that make the three weapons defend differently.
    /// </summary>
    /// <remarks>
    /// These are fairness contracts as much as feel ones: hyper-armour must protect the committed
    /// part of a swing but never the recovery, or the greatsword would be strictly safer rather than
    /// a genuine trade; and only the sustained guard should bleed posture slower, or the distinction
    /// between the weapons collapses.
    /// </remarks>
    [TestFixture]
    public sealed class DefenceResolverTests
    {
        [Test]
        public void HyperArmourResistsStaggerDuringTheCommittedSwing()
        {
            Assert.IsTrue(DefenceResolver.ResistsStagger(DefenceStyle.HyperArmour, AttackPhase.Startup));
            Assert.IsTrue(DefenceResolver.ResistsStagger(DefenceStyle.HyperArmour, AttackPhase.Active));
        }

        [Test]
        public void HyperArmourDoesNotProtectRecoveryOrIdle()
        {
            Assert.IsFalse(DefenceResolver.ResistsStagger(DefenceStyle.HyperArmour, AttackPhase.Recovery));
            Assert.IsFalse(DefenceResolver.ResistsStagger(DefenceStyle.HyperArmour, AttackPhase.Inactive));
        }

        [Test]
        public void OtherStylesNeverResistStagger()
        {
            Assert.IsFalse(DefenceResolver.ResistsStagger(DefenceStyle.Deflect, AttackPhase.Active));
            Assert.IsFalse(DefenceResolver.ResistsStagger(DefenceStyle.SustainedGuard, AttackPhase.Active));
        }

        [Test]
        public void OnlySustainedGuardBlocksCheaply()
        {
            Assert.Less(DefenceResolver.BlockPostureMultiplier(DefenceStyle.SustainedGuard), 1f);
            Assert.AreEqual(1f, DefenceResolver.BlockPostureMultiplier(DefenceStyle.Deflect));
            Assert.AreEqual(1f, DefenceResolver.BlockPostureMultiplier(DefenceStyle.HyperArmour));
        }

        [Test]
        public void AGuardStopsAnOrdinaryHitButNotAPerilousOne()
        {
            Assert.IsTrue(DefenceResolver.GuardStopsHit(isGuarding: true, unblockable: false));
            Assert.IsFalse(DefenceResolver.GuardStopsHit(isGuarding: true, unblockable: true),
                "An unblockable attack must slip past a raised guard.");
        }

        [Test]
        public void NotGuardingNeverResolvesThroughTheGuardBranch()
        {
            Assert.IsFalse(DefenceResolver.GuardStopsHit(isGuarding: false, unblockable: false));
            Assert.IsFalse(DefenceResolver.GuardStopsHit(isGuarding: false, unblockable: true));
        }
    }
}
