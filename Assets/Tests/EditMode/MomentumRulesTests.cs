using AdaptiveBossArena.Combat.Movement;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the rule that gives a moving body mass when it changes direction.
    /// </summary>
    [TestFixture]
    public sealed class MomentumRulesTests
    {
        private const float TopSpeed = 7f;
        private const float Floor = 0.35f;

        [Test]
        public void StartingFromRestCostsNothing()
        {
            // Weight must not become sluggishness: a standing character goes wherever it is told.
            float factor = MomentumRules.TargetSpeedFactor(Vector3.zero, Vector3.back, TopSpeed, Floor);

            Assert.AreEqual(1f, factor, 0.001f);
        }

        [Test]
        public void CarryingOnInTheSameDirectionCostsNothing()
        {
            float factor = MomentumRules.TargetSpeedFactor(
                Vector3.forward * TopSpeed, Vector3.forward, TopSpeed, Floor);

            Assert.AreEqual(1f, factor, 0.001f);
        }

        [Test]
        public void AFullReversalAtFullSpeedDropsToTheFloor()
        {
            float factor = MomentumRules.TargetSpeedFactor(
                Vector3.forward * TopSpeed, Vector3.back, TopSpeed, Floor);

            Assert.AreEqual(Floor, factor, 0.001f);
        }

        [Test]
        public void ASharperTurnCostsMoreThanAGentleOne()
        {
            Vector3 running = Vector3.forward * TopSpeed;

            float gentle = MomentumRules.TargetSpeedFactor(running, new Vector3(0.3f, 0f, 1f), TopSpeed, Floor);
            float sharp = MomentumRules.TargetSpeedFactor(running, Vector3.right, TopSpeed, Floor);

            Assert.Greater(gentle, sharp);
        }

        [Test]
        public void TheSameTurnCostsMoreAtASprintThanAtAJog()
        {
            float jog = MomentumRules.TargetSpeedFactor(Vector3.forward * 3f, Vector3.back, TopSpeed, Floor);
            float sprint = MomentumRules.TargetSpeedFactor(Vector3.forward * TopSpeed, Vector3.back, TopSpeed, Floor);

            Assert.Greater(jog, sprint);
        }
    }
}
