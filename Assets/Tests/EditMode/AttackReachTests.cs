using System.Reflection;
using AdaptiveBossArena.Combat;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Pins the one definition of how far an attack can reach.
    /// </summary>
    /// <remarks>
    /// Four pieces of the boss's logic decide what it can hit - which attack to pick, when to stop
    /// approaching, whether a combo can continue, and the controller's own check - and they used to
    /// each carry their own copy of this sum. The lunge is about to change how it travels, and a copy
    /// left behind would have had the boss choosing attacks it could not land.
    /// </remarks>
    [TestFixture]
    public sealed class AttackReachTests
    {
        private static AttackDefinition Attack(float range, float lungeSpeed, float startupSeconds)
        {
            var attack = ScriptableObject.CreateInstance<AttackDefinition>();
            Set(attack, "_range", range);
            Set(attack, "_lungeSpeed", lungeSpeed);
            Set(attack, "_startupSeconds", startupSeconds);

            return attack;
        }

        private static void Set(AttackDefinition attack, string field, float value) =>
            typeof(AttackDefinition)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(attack, value);

        [Test]
        public void AStandingAttackReachesExactlyItsRange()
        {
            AttackDefinition jab = Attack(range: 3f, lungeSpeed: 0f, startupSeconds: 0.28f);

            Assert.AreEqual(3f, jab.EffectiveReach, 0.001f);
            Object.DestroyImmediate(jab);
        }

        [Test]
        public void ALungeAddsTheGroundItCoversDuringItsWindUp()
        {
            // The Charge: 2.6 m of hit range and 16 m/s over a 0.55 s wind-up.
            AttackDefinition charge = Attack(range: 2.6f, lungeSpeed: 16f, startupSeconds: 0.55f);

            Assert.AreEqual(2.6f + 16f * 0.55f, charge.EffectiveReach, 0.001f);
            Object.DestroyImmediate(charge);
        }
    }
}
