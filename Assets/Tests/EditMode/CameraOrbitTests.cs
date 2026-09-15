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
    }
}
