using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.Services;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the rules that decide which stone a blow breaks and where the pieces go.
    /// </summary>
    [TestFixture]
    public sealed class DestructionRulesTests
    {
        [Test]
        public void ReachIsMeasuredAcrossTheFloorNotUpToTheStone()
        {
            // A stone crown a metre and a half up, 2.9 m away across the floor: inside a 3 m slam.
            Assert.IsTrue(DestructionRules.Reaches(Vector3.zero, 3f, new Vector3(2.9f, 1.5f, 0f)));
            Assert.IsFalse(DestructionRules.Reaches(Vector3.zero, 3f, new Vector3(3.1f, 0f, 0f)));
        }

        [Test]
        public void PiecesFlyAwayFromTheBlowAndUpward()
        {
            var random = new XorShiftRandomProvider(7u);

            for (int i = 0; i < 200; i++)
            {
                Vector3 velocity = DestructionRules.PieceVelocity(random, Vector3.zero, new Vector3(0f, 1f, 5f), 6f);

                Assert.Greater(velocity.z, 0f, "A piece flew back toward the blow that broke it.");
                Assert.Greater(velocity.y, 0f, "A piece left downward, into the floor.");
            }
        }

        [Test]
        public void TheSameSeedBreaksStoneTheSameWay()
        {
            Vector3 a = DestructionRules.PieceVelocity(new XorShiftRandomProvider(42u), Vector3.zero, Vector3.right, 5f);
            Vector3 b = DestructionRules.PieceVelocity(new XorShiftRandomProvider(42u), Vector3.zero, Vector3.right, 5f);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void TheWebBuildGetsTheSmallerBudget()
        {
            Assert.AreEqual(40, DestructionRules.Capacity(true, 40, 150));
            Assert.AreEqual(150, DestructionRules.Capacity(false, 40, 150));
            Assert.AreEqual(1, DestructionRules.Capacity(true, 0, 150));
        }
    }
}
