using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Game;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests that sparks and blood come off a blow where it landed, the way the blade was travelling.
    /// </summary>
    /// <remarks>
    /// A blade's hit reports the point it touched, already at chest height, and every burst was lifted a further
    /// metre, so sparks flew above the knight's head. And blood sprayed along the attacker's facing, at a right
    /// angle to a sideways cut.
    /// </remarks>
    [TestFixture]
    public sealed class ImpactPlacementTests
    {
        [Test]
        public void ABladeContactPlaysWhereItTouched()
        {
            var contact = new Vector3(0.4f, 1.3f, -5.6f);

            Assert.AreEqual(contact, CombatEffectsDirector.BurstPosition(contact));
        }

        [Test]
        public void AnEventAtABodysFeetIsRaisedToTheBody()
        {
            var feet = new Vector3(2f, 0f, 3f);

            Assert.Greater(CombatEffectsDirector.BurstPosition(feet).y, 0.8f);
            Assert.AreEqual(feet.x, CombatEffectsDirector.BurstPosition(feet).x);
        }

        [Test]
        public void ASwingSpraysTheWayTheBladeTravelled()
        {
            Vector3 facing = Vector3.forward;
            var sweepingLeft = new Vector3(-0.3f, -0.05f, 0.02f);

            Vector3 direction = StrikeSweep.Direction(sweepingLeft, facing, 0.005f);

            Assert.Less(Vector3.Angle(direction, sweepingLeft), 0.01f);
            Assert.AreEqual(1f, direction.magnitude, 0.0001f);
        }

        [Test]
        public void AStillPartFallsBackToTheFacing()
        {
            Vector3 direction = StrikeSweep.Direction(new Vector3(0.001f, 0f, 0f), Vector3.forward * 3f, 0.005f);

            Assert.AreEqual(Vector3.forward, direction);
        }
    }
}
