using AdaptiveBossArena.Game;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests that the cathedral's haze can never turn distance black, the failure that removed fog before.
    /// </summary>
    [TestFixture]
    public sealed class HazeColorTests
    {
        [Test]
        public void NoLightAtAllStillGivesAVisibleHaze()
        {
            Color haze = HazeColor.Derive(Color.black, Color.black, 0f);

            Assert.GreaterOrEqual(HazeColor.Luminance(haze), HazeColor.MinimumLuminance - 0.0001f);
        }

        [Test]
        public void ADarkRedRoomKeepsItsRedWhenLifted()
        {
            Color haze = HazeColor.Derive(new Color(0.03f, 0.005f, 0.005f), Color.black, 0f);

            Assert.GreaterOrEqual(HazeColor.Luminance(haze), HazeColor.MinimumLuminance - 0.0001f);
            Assert.Greater(haze.r, haze.g * 3f, "Lifting the haze washed the phase colour out.");
        }

        [Test]
        public void ABrightRoomIsLeftAlone()
        {
            var equator = new Color(0.3f, 0.3f, 0.32f);
            Color haze = HazeColor.Derive(equator, Color.white, 1f);

            Assert.Greater(haze.r, equator.r, "The sun adds nothing to the haze.");
            Assert.AreEqual(1f, haze.a);
        }

        [Test]
        public void EveryPhaseOfTheShippedAtmosphereClearsTheFloor()
        {
            // The equator and sun colours of phase 0 and phase 3, the coolest and the reddest.
            Assert.GreaterOrEqual(
                HazeColor.Luminance(HazeColor.Derive(new Color(0.14f, 0.14f, 0.17f), new Color(1f, 0.96f, 0.9f), 1.15f)),
                HazeColor.MinimumLuminance);
            Assert.GreaterOrEqual(
                HazeColor.Luminance(HazeColor.Derive(new Color(0.34f, 0.09f, 0.18f), new Color(1f, 0.4f, 0.5f), 1.6f)),
                HazeColor.MinimumLuminance);
        }
    }
}
