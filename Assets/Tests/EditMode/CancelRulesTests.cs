using System.Reflection;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Player.States;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests when the knight may break out of an action.
    /// </summary>
    /// <remarks>
    /// The player felt they had "no proper control over the character": a guard could not be left for a roll,
    /// a roll only for an attack, an attack never for the guard, and a heal not at all.
    /// </remarks>
    [TestFixture]
    public sealed class CancelRulesTests
    {
        private static AttackDefinition Attack(float startup, float active, float recovery)
        {
            var attack = ScriptableObject.CreateInstance<AttackDefinition>();
            Set(attack, "_startupSeconds", startup);
            Set(attack, "_activeSeconds", active);
            Set(attack, "_recoverySeconds", recovery);
            return attack;
        }

        private static void Set(AttackDefinition attack, string field, float value) =>
            typeof(AttackDefinition).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(attack, value);

        [Test]
        public void AnAttacksWindupAndBlowAreCommitted()
        {
            AttackDefinition swing = Attack(0.3f, 0.1f, 0.5f);

            Assert.IsFalse(CancelRules.AttackCanDodge(AttackPhase.Startup));
            Assert.IsFalse(CancelRules.AttackCanDodge(AttackPhase.Active));
            Assert.IsFalse(CancelRules.AttackCanGuard(AttackPhase.Active, 0.35f, swing));
        }

        [Test]
        public void TheRecoveryCanBeRolledOutOfAtOnceAndGuardedOutOfHalfwayThrough()
        {
            AttackDefinition swing = Attack(0.3f, 0.1f, 0.5f);

            Assert.IsTrue(CancelRules.AttackCanDodge(AttackPhase.Recovery));
            Assert.IsFalse(CancelRules.AttackCanGuard(AttackPhase.Recovery, 0.5f, swing), "Guarded out too early.");
            Assert.IsTrue(CancelRules.AttackCanGuard(AttackPhase.Recovery, 0.66f, swing), "Could not guard out late in recovery.");
        }

        [Test]
        public void ARollsSlowTailCanBeLeft()
        {
            Assert.IsFalse(CancelRules.RollCanBeLeft(0.1f, 0.32f, 0.7f));
            Assert.IsTrue(CancelRules.RollCanBeLeft(0.25f, 0.32f, 0.7f));
        }

        [Test]
        public void AHealCanOnlyBeAbandonedEarly()
        {
            Assert.IsTrue(CancelRules.HealCanDodge(0.2f, 0.9f));
            Assert.IsFalse(CancelRules.HealCanDodge(0.5f, 0.9f));
        }
    }
}
