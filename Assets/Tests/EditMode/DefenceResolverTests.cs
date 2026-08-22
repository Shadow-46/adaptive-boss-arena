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

        /// <summary>A guard held for a moment, of the kind the player raises.</summary>
        private static DefenceQuery Guard(float heldFor) => new DefenceQuery
        {
            IsDefending = true,
            CanDeflect = true,
            CanBlock = true,
            TimeInDefenceSeconds = heldFor,
            DeflectWindowSeconds = 0.2f
        };

        [Test]
        public void AHitMetOnTheBeatIsDeflected()
        {
            Assert.AreEqual(DefenceOutcome.Deflected, DefenceResolver.ResolveDefence(Guard(0f)));
            Assert.AreEqual(
                DefenceOutcome.Deflected, DefenceResolver.ResolveDefence(Guard(0.2f)),
                "The last instant of the window must still deflect. A window that is exclusive at " +
                "its own boundary is a frame shorter than the number it advertises.");
        }

        [Test]
        public void AHitMetLateIsBlockedRatherThanDeflected()
        {
            Assert.AreEqual(DefenceOutcome.Blocked, DefenceResolver.ResolveDefence(Guard(0.21f)));
        }

        [Test]
        public void NotDefendingResolvesNothing()
        {
            var dropped = new DefenceQuery
            {
                IsDefending = false,
                CanDeflect = true,
                CanBlock = true,
                TimeInDefenceSeconds = 0f,
                DeflectWindowSeconds = 0.2f
            };

            Assert.AreEqual(DefenceOutcome.None, DefenceResolver.ResolveDefence(dropped));
        }

        [Test]
        public void AnUnblockableHitBeatsEvenAPerfectlyTimedGuard()
        {
            var perilous = new DefenceQuery
            {
                IsDefending = true,
                CanDeflect = true,
                CanBlock = true,
                TimeInDefenceSeconds = 0f,
                DeflectWindowSeconds = 0.2f,
                Unblockable = true
            };

            // The point of marking an attack unblockable: it must turn defending from the safe
            // default into the wrong answer, or the player never has to read block-versus-dodge.
            Assert.AreEqual(DefenceOutcome.None, DefenceResolver.ResolveDefence(perilous));
        }

        [Test]
        public void AWeaponThatCannotParryStillBlocks()
        {
            // The greatsword. Inside the window it has no deflect to reach, so a hit it is guarding
            // against must fall through to an honest block rather than being refused for free.
            var greatsword = new DefenceQuery
            {
                IsDefending = true,
                CanDeflect = false,
                CanBlock = true,
                TimeInDefenceSeconds = 0f,
                DeflectWindowSeconds = 0.2f
            };

            Assert.AreEqual(DefenceOutcome.Blocked, DefenceResolver.ResolveDefence(greatsword));
        }

        [Test]
        public void ADefenceThatCannotBlockRefusesOnlyWhatItTimes()
        {
            // The boss's stance. Inside its window a hit is refused; a moment later it lands in
            // full, which is the punishable tail that makes baiting the stance out worth doing.
            var stance = new DefenceQuery
            {
                IsDefending = true,
                CanDeflect = true,
                CanBlock = false,
                TimeInDefenceSeconds = 0.1f,
                DeflectWindowSeconds = 0.18f
            };

            Assert.AreEqual(DefenceOutcome.Deflected, DefenceResolver.ResolveDefence(stance));

            var tail = new DefenceQuery
            {
                IsDefending = true,
                CanDeflect = true,
                CanBlock = false,
                TimeInDefenceSeconds = 0.3f,
                DeflectWindowSeconds = 0.18f
            };

            Assert.AreEqual(DefenceOutcome.None, DefenceResolver.ResolveDefence(tail));
        }
    }
}
