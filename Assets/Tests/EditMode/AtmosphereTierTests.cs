using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Game;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests which atmosphere each graphics quality draws: the ground mist and the richer bloom.
    /// </summary>
    /// <remarks>
    /// Low is the budget for the weakest machines, so it sheds the mist's overdraw; High alone can afford the filtered,
    /// brighter bloom. A swap the other way round would cost the slowest player the most.
    /// </remarks>
    [TestFixture]
    public sealed class AtmosphereTierTests
    {
        [TestCase(GraphicsQuality.Low, false)]
        [TestCase(GraphicsQuality.Medium, true)]
        [TestCase(GraphicsQuality.High, true)]
        public void TheMistLiesOnEveryFloorButLows(GraphicsQuality quality, bool shown)
        {
            Assert.AreEqual(shown, ArenaAtmosphere.ShowsMist(quality));
        }

        [Test]
        public void OnlyHighBloomsBrighter()
        {
            Assert.AreEqual(0f, ScreenEffects.BloomLift(GraphicsQuality.Low));
            Assert.AreEqual(0f, ScreenEffects.BloomLift(GraphicsQuality.Medium));
            Assert.Greater(ScreenEffects.BloomLift(GraphicsQuality.High), 0f);
        }
    }
}
