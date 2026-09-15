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

            ProbeTint.Apply(baked, tinted, new Color(2f, 1f, 0.5f));

            Assert.AreEqual(before * 2f, tinted[0][0, 0], 0.0001f);
            Assert.AreEqual(baked[0][1, 0], tinted[0][1, 0], 0.0001f);
            Assert.AreEqual(baked[0][2, 0] * 0.5f, tinted[0][2, 0], 0.0001f);
            Assert.AreEqual(before, baked[0][0, 0], 0.0001f, "The baked probe was modified.");
        }
    }
}
