using AdaptiveBossArena.Game;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Asserts the camera is kept inside the arena's wall ring, so the wall never stands between it and the fight.
    /// </summary>
    [TestFixture]
    public sealed class CameraConfinementTests
    {
        private const float Radius = 15.4f;

        [Test]
        public void ACameraInsideTheRingIsLeftExactlyWhereItWas()
        {
            var desired = new Vector3(3f, 4f, -5f);

            Vector3 confined = ArenaCameraRig.ConfineToArena(desired, Vector3.zero, Radius, out float lost);

            Assert.AreEqual(desired, confined);
            Assert.AreEqual(0f, lost);
        }

        [Test]
        public void ACameraBehindTheWallIsPulledInAlongItsBoom()
        {
            // The player against the wall at z = -15.2, framing toward the middle, camera wanting to be 3.6 m
            // further out - behind the wall.
            var focus = new Vector3(0f, 0f, -15.2f);
            var desired = new Vector3(0f, 2.5f, -18.8f);

            Vector3 confined = ArenaCameraRig.ConfineToArena(desired, focus, Radius, out float lost);

            Assert.AreEqual(Radius, new Vector2(confined.x, confined.z).magnitude, 0.001f, "Not on the confining ring.");
            Assert.AreEqual(0f, confined.x, 0.001f, "Pulled in off its boom rather than along it.");
            Assert.AreEqual(3.4f, lost, 0.001f, "The boom lost the wrong length.");
            Assert.AreEqual(desired.y, confined.y, 0.001f, "Height is the caller's to adjust, not this function's.");
        }

        [Test]
        public void AnAngledBoomStaysOnItsOwnLine()
        {
            var focus = new Vector3(10f, 0f, -10f);
            var desired = new Vector3(13f, 3f, -13f);

            Vector3 confined = ArenaCameraRig.ConfineToArena(desired, focus, Radius, out float lost);

            Vector2 boom = new Vector2(desired.x - focus.x, desired.z - focus.z).normalized;
            Vector2 kept = new Vector2(confined.x - focus.x, confined.z - focus.z).normalized;

            Assert.AreEqual(1f, Vector2.Dot(boom, kept), 0.0001f);
            Assert.LessOrEqual(new Vector2(confined.x, confined.z).magnitude, Radius + 0.001f);
            Assert.Greater(lost, 0f);
        }
    }
}
