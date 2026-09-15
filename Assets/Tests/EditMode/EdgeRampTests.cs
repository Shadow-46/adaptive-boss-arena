using AdaptiveBossArena.Editor.Environment;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Pins the edge ramp the generated textures are masked with.
    /// </summary>
    /// <remarks>
    /// <c>Mathf.SmoothStep(a, b, t)</c> reads like GLSL's <c>smoothstep(edge0, edge1, x)</c> and is not: it
    /// returns a value between <c>a</c> and <c>b</c>. Used as a mask it never reaches zero or one, which is how
    /// the first roof cookie dimmed the sun to about a fifth across the whole arena.
    /// </remarks>
    [TestFixture]
    public sealed class EdgeRampTests
    {
        [Test]
        public void TheRampIsAMaskFromZeroToOneAcrossItsEdges()
        {
            Assert.AreEqual(0f, CathedralBuilder.Edge(0.6f, 0.7f, 0.5f), 0.0001f);
            Assert.AreEqual(1f, CathedralBuilder.Edge(0.6f, 0.7f, 0.8f), 0.0001f);
            Assert.AreEqual(0.5f, CathedralBuilder.Edge(0.6f, 0.7f, 0.65f), 0.0001f);
        }

        [Test]
        public void MathfSmoothStepIsNotTheSameThing()
        {
            // Kept as a record of the trap: given edges for arguments, it answers between the edges.
            float misused = Mathf.SmoothStep(0.6f, 0.7f, 0.5f);

            Assert.AreEqual(0.65f, misused, 0.0001f);
            Assert.AreNotEqual(CathedralBuilder.Edge(0.6f, 0.7f, 0.5f), misused);
        }
    }
}
