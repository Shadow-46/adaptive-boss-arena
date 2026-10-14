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

        [Test]
        public void AtFullSpeedTheLegsKeepPaceWithTheGround()
        {
            // A body running 6 m/s on a run clip that covers 5 m/s plays it a fifth faster.
            Assert.AreEqual(1.2f, LocomotionDirection.PlaybackRate(6f, 5f, 1f), 0.001f);
        }

        [Test]
        public void StandingStillPlaysTheIdleAtItsOwnPace()
        {
            Assert.AreEqual(1f, LocomotionDirection.PlaybackRate(9f, 5f, 0f), 0.001f);
        }

        [Test]
        public void TheLegsAreNeverPlayedAsSlowMotionOrACartoon()
        {
            Assert.AreEqual(1.4f, LocomotionDirection.PlaybackRate(20f, 5f, 1f), 0.001f);
            Assert.AreEqual(0.7f, LocomotionDirection.PlaybackRate(1f, 5f, 1f), 0.001f);
        }

        [Test]
        public void AnUnmeasuredClipPlaysAsAuthored()
        {
            Assert.AreEqual(1f, LocomotionDirection.PlaybackRate(7f, 0f, 1f), 0.001f);
        }
    }
}
