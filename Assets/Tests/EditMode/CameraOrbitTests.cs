using AdaptiveBossArena.Game;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Asserts the third-person camera's orientation keeps the horizon level whichever way it looks.
    /// </summary>
    /// <remarks>
    /// The pitch used to be applied about the world's X axis after the look rotation. Looking along Z that
    /// is a pitch; looking along X the same rotation is a roll, so the horizon tipped over as the
    /// fighters circled - rarely seen while they stayed on one axis, constantly once knockback, launches
    /// and charges moved them around the whole arena.
    /// </remarks>
    [TestFixture]
    public sealed class CameraOrbitTests
    {
        private const float Pitch = 12f;

        [Test]
        public void TheHorizonStaysLevelAtEveryHeading([NUnit.Framework.Range(0, 345, 15)] int headingDegrees)
        {
            Vector3 forward = Quaternion.Euler(0f, headingDegrees, 0f) * Vector3.forward;
            Quaternion rotation = ArenaCameraRig.OrbitRotation(forward, Pitch);

            Assert.AreEqual(0f, (rotation * Vector3.right).y, 0.001f, "The camera is rolled.");
        }

        [Test]
        public void ItLooksDownByThePitchAlongTheHeading([NUnit.Framework.Range(0, 315, 45)] int headingDegrees)
        {
            Vector3 forward = Quaternion.Euler(0f, headingDegrees, 0f) * Vector3.forward;
            Vector3 look = ArenaCameraRig.OrbitRotation(forward, Pitch) * Vector3.forward;

            var flat = new Vector3(look.x, 0f, look.z).normalized;

            Assert.AreEqual(1f, Vector3.Dot(flat, forward), 0.001f, "The camera is not looking along its heading.");
            Assert.AreEqual(-Mathf.Sin(Pitch * Mathf.Deg2Rad), look.y, 0.001f, "The camera's pitch changes with heading.");
        }

        [Test]
        public void TheCameraLooksPastTheShoulderRatherThanThroughTheKnight([NUnit.Framework.Range(0, 315, 45)] int headingDegrees)
        {
            // Over the shoulder: the pivot at the knight's shoulders sits to the left of the view's centre line, at
            // exactly the shoulder offset, and the full boom ahead of the lens. Looking straight at the knight
            // from close behind would put the knight's back over the brute.
            var pivot = new Vector3(3f, 1.55f, -2f);
            Quaternion view = ArenaCameraRig.OrbitRotation(
                Quaternion.Euler(0f, headingDegrees, 0f) * Vector3.forward, Pitch);

            Vector3 lens = ArenaCameraRig.ShoulderPosition(pivot, view, boom: 2.8f, shoulder: 0.55f);
            Vector3 pivotSeen = Quaternion.Inverse(view) * (pivot - lens);

            Assert.AreEqual(-0.55f, pivotSeen.x, 0.001f, "The knight is not held to the left of the frame.");
            Assert.AreEqual(0f, pivotSeen.y, 0.001f, "The pivot is not level with the view's centre line.");
            Assert.AreEqual(2.8f, pivotSeen.z, 0.001f, "The lens is not the boom's length behind the pivot.");
        }
    }
}
