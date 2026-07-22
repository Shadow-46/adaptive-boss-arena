using AdaptiveBossArena.Core.Services;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests for the single owner of hit-stop, slow-motion and pause.
    /// </summary>
    /// <remarks>
    /// The composition rules matter more than any individual effect. The classic failure is a pause
    /// entered while a hit-stop is in flight, which on resume restores the wrong time scale and
    /// leaves the game running in permanent slow-motion. These tests pin the precedence order that
    /// prevents it.
    /// </remarks>
    [TestFixture]
    public sealed class TimeServiceTests
    {
        private const float Frame = 1f / 60f;
        private const float Tolerance = 0.0001f;

        private float _originalTimeScale;
        private float _originalFixedDeltaTime;

        [SetUp]
        public void CaptureEngineTimeState()
        {
            // The service writes global engine state, so it is restored afterwards to keep these
            // tests from affecting anything that runs later in the session.
            _originalTimeScale = Time.timeScale;
            _originalFixedDeltaTime = Time.fixedDeltaTime;
        }

        [TearDown]
        public void RestoreEngineTimeState()
        {
            Time.timeScale = _originalTimeScale;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
        }

        [Test]
        public void WithNoEffects_TimeRunsAtNormalSpeed()
        {
            var service = new TimeService();

            service.Tick(Frame);

            Assert.AreEqual(1f, service.TimeScale, Tolerance);
            Assert.AreEqual(Frame, service.DeltaTime, Tolerance);
            Assert.AreEqual(Frame, service.CombatTime, Tolerance);
        }

        [Test]
        public void HitStop_FreezesScaledTimeButNotRealTime()
        {
            var service = new TimeService();
            service.RequestHitStop(0.1f);

            service.Tick(Frame);

            Assert.Less(service.TimeScale, 0.01f);
            Assert.AreEqual(Frame, service.UnscaledDeltaTime, Tolerance);
            Assert.Less(service.DeltaTime, 0.001f);
        }

        [Test]
        public void HitStop_ExpiresAfterItsRealTimeDuration()
        {
            var service = new TimeService();
            service.RequestHitStop(0.05f);

            for (int i = 0; i < 4; i++)
            {
                service.Tick(Frame);
            }

            Assert.AreEqual(1f, service.TimeScale, Tolerance);
        }

        [Test]
        public void HitStop_TakesTheLongerOfTwoOverlappingRequests()
        {
            var service = new TimeService();

            service.RequestHitStop(0.12f);
            service.Tick(Frame);
            service.RequestHitStop(0.03f);

            // A light hit landing during a heavy hit's freeze must not cut the heavier impact short.
            for (int i = 0; i < 4; i++)
            {
                service.Tick(Frame);
            }

            Assert.Less(service.TimeScale, 0.01f, "The shorter request must not have shortened the freeze.");
        }

        [Test]
        public void SlowMotion_ScalesTimeAndThenRestoresIt()
        {
            var service = new TimeService();
            service.RequestSlowMotion(0.35f, 0.1f);

            service.Tick(Frame);
            Assert.AreEqual(0.35f, service.TimeScale, Tolerance);

            for (int i = 0; i < 8; i++)
            {
                service.Tick(Frame);
            }

            Assert.AreEqual(1f, service.TimeScale, Tolerance);
        }

        [Test]
        public void HitStop_TakesPrecedenceOverSlowMotion()
        {
            var service = new TimeService();
            service.RequestSlowMotion(0.3f, 1f);
            service.RequestHitStop(0.1f);

            service.Tick(Frame);

            Assert.Less(service.TimeScale, 0.01f);
        }

        [Test]
        public void Pause_TakesPrecedenceOverEverythingElse()
        {
            var service = new TimeService();
            service.RequestSlowMotion(0.3f, 1f);
            service.RequestHitStop(0.5f);
            service.Tick(Frame);

            service.SetPaused(true);

            Assert.AreEqual(0f, service.TimeScale, Tolerance);
            Assert.IsTrue(service.IsPaused);
        }

        [Test]
        public void Unpause_RestoresTheEffectThatWasInFlight()
        {
            var service = new TimeService();
            service.RequestSlowMotion(0.4f, 10f);
            service.Tick(Frame);

            service.SetPaused(true);
            service.Tick(Frame);
            service.SetPaused(false);
            service.Tick(Frame);

            // The bug this guards against is unpausing to full speed and discarding the slow-motion,
            // or worse, unpausing into a stale scale that never clears.
            Assert.AreEqual(0.4f, service.TimeScale, Tolerance);
        }

        [Test]
        public void WhilePaused_EffectTimersDoNotExpire()
        {
            var service = new TimeService();
            service.RequestSlowMotion(0.5f, 0.2f);
            service.Tick(Frame);

            service.SetPaused(true);
            for (int i = 0; i < 60; i++)
            {
                service.Tick(Frame);
            }

            service.SetPaused(false);
            service.Tick(Frame);

            Assert.AreEqual(0.5f, service.TimeScale, Tolerance);
        }

        [Test]
        public void CombatTime_AdvancesWithScaledTimeOnly()
        {
            var service = new TimeService();

            service.Tick(Frame);
            float afterNormalFrame = service.CombatTime;

            service.RequestHitStop(0.2f);
            for (int i = 0; i < 5; i++)
            {
                service.Tick(Frame);
            }

            // Observation timestamps ride this clock, so a hit-stop must freeze the boss's
            // perception too rather than handing it fresher data than the fight has produced.
            Assert.AreEqual(afterNormalFrame, service.CombatTime, 0.001f);
        }

        [Test]
        public void ClearTimeEffects_ReturnsToNormalSpeedImmediately()
        {
            var service = new TimeService();
            service.RequestHitStop(1f);
            service.RequestSlowMotion(0.2f, 1f);
            service.Tick(Frame);

            service.ClearTimeEffects();
            service.Tick(Frame);

            Assert.AreEqual(1f, service.TimeScale, Tolerance);
        }

        [Test]
        public void ResetCombatClock_ReturnsTheClockToZero()
        {
            var service = new TimeService();
            service.Tick(Frame);
            service.Tick(Frame);

            service.ResetCombatClock();

            Assert.AreEqual(0f, service.CombatTime, Tolerance);
        }
    }
}
