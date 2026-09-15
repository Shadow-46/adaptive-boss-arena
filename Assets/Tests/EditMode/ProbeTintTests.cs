using AdaptiveBossArena.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests that light baked in the opening phase's colours follows the arena into later phases.
    /// </summary>
    [TestFixture]
    public sealed class ProbeTintTests
    {
        private static readonly Color Opening = new Color(0.14f, 0.14f, 0.17f);

        [Test]
        public void TheOpeningPhaseLeavesTheBakeUntouched()
        {
            Color gain = ProbeTint.Gain(Opening, Opening);

            Assert.AreEqual(1f, gain.r, 0.0001f);
            Assert.AreEqual(1f, gain.g, 0.0001f);
            Assert.AreEqual(1f, gain.b, 0.0001f);
        }

        [Test]
        public void AReddeningRoomReddensTheBakedLight()
        {
            Color gain = ProbeTint.Gain(new Color(0.34f, 0.09f, 0.18f), Opening);

            Assert.Greater(gain.r, 1f, "The red phase did not lift red.");
            Assert.Less(gain.g, 1f, "The red phase did not dim green.");
        }

        [Test]
        public void ANearBlackBakedChannelCannotFlare()
        {
            Color gain = ProbeTint.Gain(Color.white, Color.black);

            Assert.LessOrEqual(gain.r, ProbeTint.MaximumGain);
        }

        [Test]
        public void TintingScalesEveryChannelAndLeavesTheBakeAlone()
        {
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(new Color(0.5f, 0.5f, 0.5f));

            var baked = new[] { probe };
            var tinted = new SphericalHarmonicsL2[1];
            float before = baked[0][0, 0];

            ProbeTint.Apply(baked, tinted, new Color(2f, 1f, 0.5f), new SphericalHarmonicsL2());

            Assert.AreEqual(before * 2f, tinted[0][0, 0], 0.0001f);
            Assert.AreEqual(baked[0][1, 0], tinted[0][1, 0], 0.0001f);
            Assert.AreEqual(baked[0][2, 0] * 0.5f, tinted[0][2, 0], 0.0001f);
            Assert.AreEqual(before, baked[0][0, 0], 0.0001f, "The baked probe was modified.");
        }

        [Test]
        public void NoProbeFallsBelowTheRoomsAmbientLight()
        {
            // A probe baked behind the wall, darker than the ambient light the scene had before any bake.
            var dim = new SphericalHarmonicsL2();
            dim.AddAmbientLight(new Color(0.02f, 0.02f, 0.02f));

            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(0.1f, 0.1f, 0.12f));

            var tinted = new SphericalHarmonicsL2[1];

            ProbeTint.Apply(new[] { dim }, tinted, Color.white, ambient);

            for (int channel = 0; channel < 3; channel++)
            {
                Assert.GreaterOrEqual(tinted[0][channel, 0], ambient[channel, 0] - 0.0001f,
                    "A probe came out darker than the room's ambient light.");
            }
        }
    }
}
