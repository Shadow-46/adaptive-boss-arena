using AdaptiveBossArena.Editor.Environment;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the pointed arch the cathedral's windows are cut with.
    /// </summary>
    [TestFixture]
    public sealed class PointedArchTests
    {
        [TestCase(7.4f, 5.55f)]
        [TestCase(4f, 2f)]
        public void TheArchSpringsFromItsSidesAndMeetsInAPointAtItsRise(float span, float rise)
        {
            Assert.AreEqual(0f, TiledMeshes.PointedArchHeight(-span * 0.5f, span, rise), 0.001f, "The left side does not spring from zero.");
            Assert.AreEqual(0f, TiledMeshes.PointedArchHeight(span * 0.5f, span, rise), 0.001f, "The right side does not spring from zero.");
            Assert.AreEqual(rise, TiledMeshes.PointedArchHeight(0f, span, rise), 0.001f, "The point is not at the rise.");
        }

        [Test]
        public void TheUndersideClimbsAllTheWayToThePoint()
        {
            const float span = 7.4f, rise = 5.55f;
            float previous = -1f;

            for (int i = 0; i <= 20; i++)
            {
                float height = TiledMeshes.PointedArchHeight(-span * 0.5f + span * 0.5f * i / 20f, span, rise);
                Assert.Greater(height, previous, "The arch dips on its way up.");
                previous = height;
            }
        }

        [Test]
        public void TheHeadIsAClosedWallAboveTheArch()
        {
            Mesh mesh = TiledMeshes.BuildPointedArchHead(1.5f, 8.5f, 7.4f, 5.55f, 2f);

            try
            {
                Assert.Greater(mesh.triangles.Length, 0, "The arch head has no faces.");
                Assert.AreEqual(8.5f, mesh.bounds.size.y, 0.01f, "The head is not as tall as asked.");
                Assert.AreEqual(7.4f, mesh.bounds.size.z, 0.01f, "The head does not span the window.");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
