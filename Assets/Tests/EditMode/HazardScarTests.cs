using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the ground scar a lingering hazard is drawn with.
    /// </summary>
    /// <remarks>
    /// It was a flat red disc with a hard edge, which read as a marker on the floor. The scar must fade out
    /// before its rim, so no edge of the quad it is drawn on can ever show, and must glow along its cracks.
    /// </remarks>
    [TestFixture]
    public sealed class HazardScarTests
    {
        private Texture2D _scar;

        [SetUp]
        public void LoadTheScar()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/HazardDisc.mat");
            Assume.That(material != null, "Setup has not generated the hazard material.");

            _scar = material.GetTexture("_BaseMap") as Texture2D;
            Assert.IsNotNull(_scar, "The hazard is drawn untextured, as a flat disc.");
        }

        [Test]
        public void TheScarFadesOutBeforeTheQuadsEdge()
        {
            foreach (var uv in new[] { new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0.02f, 0.02f) })
            {
                Assert.Less(_scar.GetPixelBilinear(uv.x, uv.y).a, 0.02f, "The scar's edge is visible at " + uv + ".");
            }
        }

        [Test]
        public void TheScarIsDarkWithGlowingCracks()
        {
            float brightest = 0f, darkestOpaque = 1f;

            for (int y = 0; y < _scar.height; y += 1)
            {
                for (int x = 0; x < _scar.width; x += 1)
                {
                    Color c = _scar.GetPixel(x, y);

                    if (c.a > 0.5f)
                    {
                        brightest = Mathf.Max(brightest, c.r);
                        darkestOpaque = Mathf.Min(darkestOpaque, c.r);
                    }
                }
            }

            Assert.Greater(brightest, 0.8f, "No crack glows.");
            Assert.Less(darkestOpaque, 0.15f, "No part of the scar is scorched dark.");
        }
    }
}
