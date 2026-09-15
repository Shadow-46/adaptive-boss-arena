using AdaptiveBossArena.Combat.Feel;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests how movement is read relative to facing for the directional locomotion blend.
    /// </summary>
    /// <remarks>
    /// A single speed drove clips that only step forward, so sideways and backwards movement slid on legs
    /// walking straight ahead.
    /// </remarks>
    [TestFixture]
    public sealed class LocomotionDirectionTests
    {
        [Test]
        public void MovingTheWayYouFaceIsForward()
        {
            Vector2 blend = LocomotionDirection.BlendPosition(new Vector3(0f, 0f, 0.1f), Quaternion.identity, 1f, Vector2.zero);

            Assert.AreEqual(0f, blend.x, 0.001f);
            Assert.AreEqual(1f, blend.y, 0.001f);
        }

        [Test]
        public void MovingToYourRightWhileFacingEastIsAStrafeRight()
        {
            // Facing east (+x), moving south (-z): that is to the right of the facing.
            Quaternion east = Quaternion.LookRotation(Vector3.right);
            Vector2 blend = LocomotionDirection.BlendPosition(new Vector3(0f, 0f, -0.1f), east, 0.3f, Vector2.zero);

            Assert.AreEqual(0.3f, blend.x, 0.001f);
            Assert.AreEqual(0f, blend.y, 0.001f);
        }

        [Test]
        public void BackingAwayIsBackwards()
        {
            Vector2 blend = LocomotionDirection.BlendPosition(new Vector3(0f, 0f, -0.1f), Quaternion.identity, 0.5f, Vector2.zero);

            Assert.AreEqual(-0.5f, blend.y, 0.001f);
        }

        [Test]
        public void AFrozenFrameKeepsTheLastDirection()
        {
            Vector2 blend = LocomotionDirection.BlendPosition(Vector3.zero, Quaternion.identity, 0.4f, new Vector2(-1f, 0f));

            Assert.AreEqual(-0.4f, blend.x, 0.001f);
            Assert.AreEqual(0f, blend.y, 0.001f);
        }

        [Test]
        public void VerticalMovementIsIgnored()
        {
            Vector2 blend = LocomotionDirection.BlendPosition(new Vector3(0.1f, 5f, 0f), Quaternion.identity, 1f, Vector2.zero);

            Assert.AreEqual(1f, blend.x, 0.001f);
        }
    }
}
