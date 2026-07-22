using AdaptiveBossArena.Utilities;
using AdaptiveBossArena.Utilities.Timing;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>Tests for the shared numeric helpers.</summary>
    [TestFixture]
    public sealed class MathUtilTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Remap_MapsBetweenRanges()
        {
            Assert.AreEqual(0f, MathUtil.Remap(0f, 0f, 10f, 0f, 100f), Tolerance);
            Assert.AreEqual(50f, MathUtil.Remap(5f, 0f, 10f, 0f, 100f), Tolerance);
            Assert.AreEqual(100f, MathUtil.Remap(10f, 0f, 10f, 0f, 100f), Tolerance);
        }

        [Test]
        public void Remap_WithDegenerateInputRange_ReturnsTheOutputMinimum()
        {
            Assert.AreEqual(7f, MathUtil.Remap(5f, 3f, 3f, 7f, 9f), Tolerance);
        }

        [Test]
        public void Remap01_ClampsOutsideTheRange()
        {
            Assert.AreEqual(0f, MathUtil.Remap01(-5f, 0f, 10f), Tolerance);
            Assert.AreEqual(1f, MathUtil.Remap01(15f, 0f, 10f), Tolerance);
        }

        [Test]
        public void Damp_ClosesHalfTheDistanceInOneHalfLife()
        {
            Assert.AreEqual(5f, MathUtil.Damp(0f, 10f, halfLifeSeconds: 1f, deltaTimeSeconds: 1f), Tolerance);
        }

        [Test]
        public void Damp_IsFrameRateIndependent()
        {
            const float halfLife = 0.5f;
            const float totalTime = 1f;

            float coarse = MathUtil.Damp(0f, 10f, halfLife, totalTime);

            float fine = 0f;
            const int steps = 120;
            for (int i = 0; i < steps; i++)
            {
                fine = MathUtil.Damp(fine, 10f, halfLife, totalTime / steps);
            }

            // The whole reason this exists instead of a Lerp against deltaTime: the same elapsed
            // time must produce the same result at any frame rate.
            Assert.AreEqual(coarse, fine, 0.001f);
        }

        [Test]
        public void Damp_WithNonPositiveHalfLife_SnapsToTarget()
        {
            Assert.AreEqual(10f, MathUtil.Damp(0f, 10f, 0f, 0.016f), Tolerance);
        }

        [Test]
        public void ConfidenceFromSampleCount_RisesFromZeroTowardOne()
        {
            Assert.AreEqual(0f, MathUtil.ConfidenceFromSampleCount(0, 10), Tolerance);
            Assert.AreEqual(0.5f, MathUtil.ConfidenceFromSampleCount(10, 10), 0.01f);
            Assert.Greater(MathUtil.ConfidenceFromSampleCount(100, 10), 0.99f);
        }

        [Test]
        public void ConfidenceFromSampleCount_IsMonotonic()
        {
            float previous = -1f;

            for (int samples = 0; samples < 60; samples++)
            {
                float confidence = MathUtil.ConfidenceFromSampleCount(samples, 12);
                Assert.GreaterOrEqual(confidence, previous);
                previous = confidence;
            }
        }

        [Test]
        public void PlanarDistance_IgnoresHeight()
        {
            var a = new Vector3(0f, 0f, 0f);
            var b = new Vector3(3f, 100f, 4f);

            Assert.AreEqual(5f, MathUtil.PlanarDistance(a, b), Tolerance);
        }

        [Test]
        public void FlattenAndLift_RoundTripOnThePlane()
        {
            var world = new Vector3(2f, 7f, -3f);

            Vector2 flat = MathUtil.FlattenToPlane(world);
            Vector3 lifted = MathUtil.ToWorld(flat, world.y);

            Assert.AreEqual(world.x, lifted.x, Tolerance);
            Assert.AreEqual(world.y, lifted.y, Tolerance);
            Assert.AreEqual(world.z, lifted.z, Tolerance);
        }

        [Test]
        public void SafeNormalize_OnZeroVector_ReturnsZero()
        {
            Assert.AreEqual(Vector2.zero, MathUtil.SafeNormalize(Vector2.zero));
            Assert.AreEqual(Vector3.zero, MathUtil.SafeNormalize(Vector3.zero));
        }
    }

    /// <summary>Tests for the externally ticked countdown timer.</summary>
    [TestFixture]
    public sealed class CountdownTimerTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void BeforeStarting_IsNotRunning()
        {
            var timer = new CountdownTimer();

            Assert.IsFalse(timer.IsRunning);
            Assert.IsFalse(timer.Tick(1f));
        }

        [Test]
        public void Tick_ReturnsTrueExactlyOnceOnCompletion()
        {
            var timer = new CountdownTimer(0.1f);

            Assert.IsFalse(timer.Tick(0.05f));
            Assert.IsTrue(timer.Tick(0.06f));
            Assert.IsFalse(timer.Tick(0.06f), "Completion must not be reported more than once.");
        }

        [Test]
        public void Completed_IsRaisedOnce()
        {
            var timer = new CountdownTimer();
            int raiseCount = 0;
            timer.Completed += () => raiseCount++;

            timer.Start(0.1f);
            timer.Tick(0.2f);
            timer.Tick(0.2f);

            Assert.AreEqual(1, raiseCount);
        }

        [Test]
        public void Progress01_RunsFromZeroToOne()
        {
            var timer = new CountdownTimer(1f);

            Assert.AreEqual(0f, timer.Progress01, Tolerance);

            timer.Tick(0.25f);
            Assert.AreEqual(0.25f, timer.Progress01, Tolerance);

            timer.Tick(0.75f);
            Assert.AreEqual(1f, timer.Progress01, Tolerance);
        }

        [Test]
        public void Cancel_StopsWithoutRaisingCompletion()
        {
            var timer = new CountdownTimer(1f);
            int raiseCount = 0;
            timer.Completed += () => raiseCount++;

            timer.Cancel();
            timer.Tick(2f);

            Assert.IsFalse(timer.IsRunning);
            Assert.AreEqual(0, raiseCount);
        }

        [Test]
        public void Start_RestartsAnAlreadyRunningTimer()
        {
            var timer = new CountdownTimer(1f);
            timer.Tick(0.9f);

            timer.Start(1f);

            Assert.AreEqual(1f, timer.Remaining, Tolerance);
        }
    }
}
