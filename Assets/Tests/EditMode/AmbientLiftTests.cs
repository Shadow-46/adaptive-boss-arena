using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Game;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests that the Low quality, which loses the fires, keeps enough ambient to see the fighters by.
    /// </summary>
    /// <remarks>
    /// The ambient was darkened for the moonlit nave against four braziers. Low switches their lights off, and in
    /// the browser, where Low is common, the knight in the wall's moon shadow rendered black on black.
    /// </remarks>
    [TestFixture]
    public sealed class AmbientLiftTests
    {
        /// <summary>The opening ambient from above as it stood before the fires were added.</summary>
        private static readonly Color AmbientBeforeTheFires = new Color(0.22f, 0.24f, 0.30f);

        [Test]
        public void LowLiftsTheAmbientBackToWhereItStoodBeforeTheFires()
        {
            Color lifted = ArenaAtmosphere.OpeningSky * ArenaAtmosphere.AmbientLift(GraphicsQuality.Low);

            Assert.GreaterOrEqual(HazeColor.Luminance(lifted), HazeColor.Luminance(AmbientBeforeTheFires) - 0.01f);
        }

        [TestCase(GraphicsQuality.Medium)]
        [TestCase(GraphicsQuality.High)]
        public void QualitiesWithTheFiresKeepTheDarkerAmbient(GraphicsQuality quality)
        {
            Assert.AreEqual(1f, ArenaAtmosphere.AmbientLift(quality));
        }
    }
}
