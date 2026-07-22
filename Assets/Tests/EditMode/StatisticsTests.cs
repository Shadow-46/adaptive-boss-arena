using System;
using AdaptiveBossArena.Utilities.Statistics;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests for the smoothing primitive that governs how quickly the boss adapts.
    /// </summary>
    /// <remarks>
    /// The half-life contract is a design promise, not just an implementation detail. If a
    /// half-life of ten seconds did not actually halve a value's influence in ten seconds, every
    /// adaptation timing tuned against it would be wrong in a way no play test could diagnose.
    /// </remarks>
    [TestFixture]
    public sealed class EwmaTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void Constructor_WithNonPositiveHalfLife_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Ewma(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Ewma(-1f));
        }

        [Test]
        public void BeforeAnySample_ReportsNoSamples()
        {
            var average = new Ewma(1f);

            Assert.IsFalse(average.HasSamples);
            Assert.AreEqual(0, average.SampleCount);
            Assert.AreEqual(0f, average.Value, Tolerance);
        }

        [Test]
        public void FirstSample_IsAdoptedOutright()
        {
            var average = new Ewma(10f);

            average.AddSample(0.8f, 0.5f);

            // Blending the first sample against the implicit zero would bias every behaviour feature
            // downward for the opening seconds of a fight.
            Assert.AreEqual(0.8f, average.Value, Tolerance);
        }

        [Test]
        public void AfterOneHalfLife_InfluenceOfOldValueIsHalved()
        {
            const float halfLife = 4f;
            var average = new Ewma(halfLife);

            average.AddSample(0f, 0f);
            average.AddSample(1f, halfLife);

            // Half the distance from the old value to the new one should have been closed.
            Assert.AreEqual(0.5f, average.Value, Tolerance);
        }

        [Test]
        public void RepeatedSamples_ConvergeTowardTheSampledValue()
        {
            var average = new Ewma(1f);
            average.AddSample(0f, 0f);

            for (int i = 0; i < 50; i++)
            {
                average.AddSample(1f, 0.25f);
            }

            Assert.AreEqual(1f, average.Value, 0.01f);
            Assert.AreEqual(51, average.SampleCount);
        }

        [Test]
        public void DecayToward_ErodesTheValueWhenBehaviourStops()
        {
            const float halfLife = 2f;
            var average = new Ewma(halfLife);
            average.AddSample(1f, 0f);

            average.DecayToward(0f, halfLife);

            // A habit the player abandons has to fade, or the boss keeps countering a tactic that
            // is no longer being used.
            Assert.AreEqual(0.5f, average.Value, Tolerance);
        }

        [Test]
        public void DecayToward_BeforeAnySample_DoesNothing()
        {
            var average = new Ewma(1f);

            average.DecayToward(1f, 10f);

            Assert.AreEqual(0f, average.Value, Tolerance);
            Assert.IsFalse(average.HasSamples);
        }

        [Test]
        public void Reset_ReturnsToUnobservedState()
        {
            var average = new Ewma(1f);
            average.AddSample(0.7f, 0f);

            average.Reset();

            Assert.IsFalse(average.HasSamples);
            Assert.AreEqual(0f, average.Value, Tolerance);
        }
    }

    /// <summary>
    /// Tests for the direction histogram that decides whether the player's movement is exploitable.
    /// </summary>
    /// <remarks>
    /// Predictability gates the counter-strategy that leads attacks toward a favoured dodge
    /// direction. Were it to report a high value for evenly distributed movement, the boss would
    /// appear to predict dodges it had no basis to predict, which is precisely the impression of
    /// cheating the design forbids.
    /// </remarks>
    [TestFixture]
    public sealed class DirectionHistogramTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void BeforeAnyInput_ReportsNoSignal()
        {
            var histogram = new DirectionHistogram();

            Assert.IsFalse(histogram.HasSignal);
            Assert.AreEqual(-1, histogram.DominantBin);
            Assert.AreEqual(0f, histogram.Predictability, Tolerance);
        }

        [Test]
        public void Add_ZeroLengthDirection_IsDiscarded()
        {
            var histogram = new DirectionHistogram();

            histogram.Add(Vector2.zero);

            Assert.IsFalse(histogram.HasSignal);
        }

        [Test]
        public void RepeatedSameDirection_ProducesMaximumPredictability()
        {
            var histogram = new DirectionHistogram();

            for (int i = 0; i < 20; i++)
            {
                histogram.Add(Vector2.left);
            }

            Assert.AreEqual(1f, histogram.Predictability, 0.01f);
            Assert.AreEqual(DirectionHistogram.BinForDirection(Vector2.left), histogram.DominantBin);
            Assert.AreEqual(1f, histogram.NormalizedWeight(histogram.DominantBin), Tolerance);
        }

        [Test]
        public void UniformlySpreadDirections_ProduceMinimumPredictability()
        {
            var histogram = new DirectionHistogram();

            for (int bin = 0; bin < DirectionHistogram.BinCount; bin++)
            {
                histogram.Add(DirectionHistogram.BinCenterDirection(bin));
            }

            Assert.AreEqual(0f, histogram.Predictability, 0.01f);
        }

        [Test]
        public void BinForDirection_MapsCardinalsToDistinctBins()
        {
            int east = DirectionHistogram.BinForDirection(Vector2.right);
            int north = DirectionHistogram.BinForDirection(Vector2.up);
            int west = DirectionHistogram.BinForDirection(Vector2.left);
            int south = DirectionHistogram.BinForDirection(Vector2.down);

            Assert.AreEqual(0, east);
            Assert.AreEqual(2, north);
            Assert.AreEqual(4, west);
            Assert.AreEqual(6, south);
        }

        [Test]
        public void BinForDirection_AtTheAngleSeam_StaysInRange()
        {
            // Directions just either side of pi are the case naive rounding pushes out of range.
            for (float degrees = -180f; degrees <= 180f; degrees += 7.5f)
            {
                float radians = degrees * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

                int bin = DirectionHistogram.BinForDirection(direction);

                Assert.GreaterOrEqual(bin, 0);
                Assert.Less(bin, DirectionHistogram.BinCount);
            }
        }

        [Test]
        public void Decay_ReducesAccumulatedWeightByHalfOverOneHalfLife()
        {
            const float halfLife = 5f;
            var histogram = new DirectionHistogram();
            histogram.Add(Vector2.right, 4f);

            histogram.Decay(halfLife, halfLife);

            Assert.AreEqual(2f, histogram.TotalWeight, Tolerance);
        }

        [Test]
        public void NormalizedWeight_OutsideBinRange_Throws()
        {
            var histogram = new DirectionHistogram();

            Assert.Throws<ArgumentOutOfRangeException>(() => histogram.NormalizedWeight(-1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => histogram.NormalizedWeight(DirectionHistogram.BinCount));
        }
    }
}
