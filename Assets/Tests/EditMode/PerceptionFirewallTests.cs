using AdaptiveBossArena.Core.Perception;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests for the perception delay that keeps the boss reacting on human timescales.
    /// </summary>
    /// <remarks>
    /// This is the runtime half of the anti-cheat design; the assembly graph is the compile-time
    /// half. If the delay were ever to collapse to zero, the boss would begin responding to the
    /// current frame and feints would stop working, so the guarantee is asserted directly rather
    /// than left to play-testing to notice.
    /// </remarks>
    [TestFixture]
    public sealed class DelayedPerceptionSourceTests
    {
        private const float Latency = 0.15f;
        private const float SampleInterval = 0.02f;

        /// <summary>
        /// Stand-in player whose position encodes the time it was sampled, so any observation can be
        /// traced back to the instant it was captured.
        /// </summary>
        private sealed class TimestampEncodingPlayer : IObservablePlayer
        {
            public ObservableActionState State { get; set; } = ObservableActionState.Idle;

            public PlayerObservation CaptureObservation(float timestamp) => new PlayerObservation
            {
                Timestamp = timestamp,
                Position = new Vector3(timestamp, 0f, 0f),
                ActionState = State,
                NormalizedHealth = 1f,
                IsValid = true
            };
        }

        [Test]
        public void BeforeHistoryCoversTheLatency_NothingIsPerceived()
        {
            var player = new TimestampEncodingPlayer();
            var source = new DelayedPerceptionSource(player, Latency);

            source.Sample(0f);
            source.Sample(SampleInterval);

            // The boss legitimately has nothing to act on in the opening moments of a fight.
            Assert.IsFalse(source.TryGetPerceived(out _));
        }

        [Test]
        public void OncePopulated_PerceivedObservationLagsByTheConfiguredLatency()
        {
            var player = new TimestampEncodingPlayer();
            var source = new DelayedPerceptionSource(player, Latency);

            float time = 0f;
            for (int i = 0; i < 100; i++)
            {
                source.Sample(time);
                time += SampleInterval;
            }

            float now = time - SampleInterval;

            Assert.IsTrue(source.TryGetPerceived(out PlayerObservation observation));

            float actualLag = now - observation.Timestamp;

            // The returned sample is the newest at or before the cutoff, so the lag lands between
            // the configured latency and one sampling interval beyond it.
            Assert.GreaterOrEqual(actualLag, Latency - 0.0001f);
            Assert.LessOrEqual(actualLag, Latency + SampleInterval + 0.0001f);
        }

        [Test]
        public void PerceivedObservation_NeverReflectsTheCurrentFrame()
        {
            var player = new TimestampEncodingPlayer();
            var source = new DelayedPerceptionSource(player, Latency);

            float time = 0f;
            for (int i = 0; i < 100; i++)
            {
                source.Sample(time);
                time += SampleInterval;
            }

            // Switch the player's visible action on the most recent frame only.
            player.State = ObservableActionState.HeavyAttacking;
            source.Sample(time);

            Assert.IsTrue(source.TryGetPerceived(out PlayerObservation observation));
            Assert.AreEqual(
                ObservableActionState.Idle,
                observation.ActionState,
                "The boss must not see an action that only began this frame.");
        }

        [Test]
        public void TryGetPerceivedAt_LooksFurtherIntoThePast()
        {
            var player = new TimestampEncodingPlayer();
            var source = new DelayedPerceptionSource(player, Latency);

            float time = 0f;
            for (int i = 0; i < 200; i++)
            {
                source.Sample(time);
                time += SampleInterval;
            }

            Assert.IsTrue(source.TryGetPerceived(out PlayerObservation recent));
            Assert.IsTrue(source.TryGetPerceivedAt(0.5f, out PlayerObservation older));

            Assert.Less(older.Timestamp, recent.Timestamp);
            Assert.AreEqual(0.5f, recent.Timestamp - older.Timestamp, SampleInterval + 0.0001f);
        }

        [Test]
        public void TryGetPerceivedAt_BeyondRetainedHistory_ReturnsFalse()
        {
            var player = new TimestampEncodingPlayer();
            var source = new DelayedPerceptionSource(player, Latency);

            float time = 0f;
            for (int i = 0; i < 50; i++)
            {
                source.Sample(time);
                time += SampleInterval;
            }

            Assert.IsFalse(source.TryGetPerceivedAt(60f, out _));
        }

        [Test]
        public void SetLatency_ChangesHowFarBackThePerceivedSampleSits()
        {
            var player = new TimestampEncodingPlayer();
            var source = new DelayedPerceptionSource(player, Latency);

            float time = 0f;
            for (int i = 0; i < 200; i++)
            {
                source.Sample(time);
                time += SampleInterval;
            }

            Assert.IsTrue(source.TryGetPerceived(out PlayerObservation beforeChange));

            source.SetLatency(0.4f);
            Assert.IsTrue(source.TryGetPerceived(out PlayerObservation afterChange));

            Assert.Less(afterChange.Timestamp, beforeChange.Timestamp);
        }

        [Test]
        public void Reset_DiscardsHistory()
        {
            var player = new TimestampEncodingPlayer();
            var source = new DelayedPerceptionSource(player, Latency);

            float time = 0f;
            for (int i = 0; i < 100; i++)
            {
                source.Sample(time);
                time += SampleInterval;
            }

            source.Reset();

            Assert.AreEqual(0, source.SampleCount);
            Assert.IsFalse(source.TryGetPerceived(out _));
        }

        [Test]
        public void HistoryIsBounded_SoALongFightCostsConstantMemory()
        {
            var player = new TimestampEncodingPlayer();
            var source = new DelayedPerceptionSource(player, Latency, historyCapacity: 64);

            float time = 0f;
            for (int i = 0; i < 5000; i++)
            {
                source.Sample(time);
                time += SampleInterval;
            }

            Assert.AreEqual(64, source.SampleCount);
        }
    }
}
