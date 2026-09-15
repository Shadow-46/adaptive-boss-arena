using AdaptiveBossArena.Utilities.Statistics;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the percentile maths every performance budget in this project is checked against.
    /// </summary>
    /// <remarks>
    /// A budget is only as trustworthy as the number compared with it. A percentile that interpolated,
    /// or that averaged a spike away, would pass a build that stutters.
    /// </remarks>
    [TestFixture]
    public sealed class FrameTimeSamplerTests
    {
        [Test]
        public void AnEmptySamplerReportsZero()
        {
            var sampler = new FrameTimeSampler(10);

            Assert.AreEqual(0f, sampler.PercentileMilliseconds(0.95f));
            Assert.AreEqual(0f, sampler.MaxMilliseconds);
        }

        [Test]
        public void TheMedianOfOrderedFramesIsTheMiddleFrame()
        {
            var sampler = new FrameTimeSampler(10);

            for (int i = 1; i <= 9; i++)
            {
                sampler.Add(i / 1000f);
            }

            Assert.AreEqual(5f, sampler.PercentileMilliseconds(0.5f), 0.001f);
        }

        [Test]
        public void ASingleSpikeShowsUpInTheMaxButNotTheMedian()
        {
            // The whole reason for percentiles: one 40 ms hitch among ordinary frames must be visible
            // without dragging the typical frame along with it.
            var sampler = new FrameTimeSampler(100);

            for (int i = 0; i < 99; i++)
            {
                sampler.Add(0.004f);
            }

            sampler.Add(0.040f);

            Assert.AreEqual(4f, sampler.PercentileMilliseconds(0.5f), 0.001f);
            Assert.AreEqual(40f, sampler.MaxMilliseconds, 0.001f);
        }

        [Test]
        public void ThePercentileIsAlwaysAFrameThatActuallyHappened()
        {
            // Nearest-rank, not interpolation: the p95 of two very different frames is one of them,
            // never a value between that no frame took.
            var sampler = new FrameTimeSampler(10);
            sampler.Add(0.010f);
            sampler.Add(0.030f);

            float p95 = sampler.PercentileMilliseconds(0.95f);

            Assert.IsTrue(
                System.Math.Abs(p95 - 10f) < 0.001f || System.Math.Abs(p95 - 30f) < 0.001f,
                $"p95 was {p95} ms, which no recorded frame took.");
        }

        [Test]
        public void SamplesPastCapacityAreDroppedNotWrapped()
        {
            var sampler = new FrameTimeSampler(3);
            sampler.Add(0.001f);
            sampler.Add(0.002f);
            sampler.Add(0.003f);
            sampler.Add(0.900f);

            Assert.IsTrue(sampler.IsFull);
            Assert.AreEqual(3, sampler.Count);
            Assert.AreEqual(3f, sampler.MaxMilliseconds, 0.001f);
        }

        [Test]
        public void TheWebPageAsksForACaptureThroughItsQueryString()
        {
            // The only way to start a capture on the deployed WebGL build, which has no command line.
            Assert.IsTrue(PerfCaptureRequest.TryReadQuerySeconds(
                "https://example.test/arena/?perf=60", out float seconds));
            Assert.AreEqual(60f, seconds, 0.001f);

            Assert.IsTrue(PerfCaptureRequest.TryReadQuerySeconds(
                "https://example.test/?a=1&perf=12.5#top", out seconds));
            Assert.AreEqual(12.5f, seconds, 0.001f);
        }

        [Test]
        public void APageWithoutTheQueryKeyDoesNotCapture()
        {
            Assert.IsFalse(PerfCaptureRequest.TryReadQuerySeconds("https://example.test/arena/", out _));
            Assert.IsFalse(PerfCaptureRequest.TryReadQuerySeconds("https://example.test/?perfx=5", out _));
            Assert.IsFalse(PerfCaptureRequest.TryReadQuerySeconds("https://example.test/?perf=0", out _));
            Assert.IsFalse(PerfCaptureRequest.TryReadQuerySeconds(string.Empty, out _));
        }

        [Test]
        public void ADesktopRunAsksForACaptureOnTheCommandLine()
        {
            PerfCaptureRequest request = PerfCaptureRequest.Read(
                new[] { "game.exe", "-perfCapture", "60", "-perfLog", @"C:\logs\perf.txt", "-perfQuit" },
                string.Empty);

            Assert.IsTrue(request.IsRequested);
            Assert.AreEqual(60f, request.Seconds, 0.001f);
            Assert.AreEqual(@"C:\logs\perf.txt", request.LogPath);
            Assert.IsTrue(request.QuitWhenDone);
        }

        [Test]
        public void AnOrdinaryLaunchRequestsNothing()
        {
            // The shipped game must never skip its title screen or quit on its own.
            PerfCaptureRequest request = PerfCaptureRequest.Read(new[] { "game.exe" }, string.Empty);

            Assert.IsFalse(request.IsRequested);
            Assert.IsFalse(request.QuitWhenDone);
            Assert.IsNull(request.ShotFolder);
        }

        [Test]
        public void ACaptureCanRecordASequenceOfFrames()
        {
            // Motion is judged in motion: a single frame cannot show whether a swing has weight.
            PerfCaptureRequest request = PerfCaptureRequest.Read(
                new[] { "game.exe", "-perfCapture", "10", "-perfShots", @"C:\frames", "-perfShotEvery", "0.1" },
                string.Empty);

            Assert.AreEqual(@"C:\frames", request.ShotFolder);
            Assert.AreEqual(0.1f, request.ShotEverySeconds, 0.0001f);
        }

        [Test]
        public void AFrameSequenceDefaultsToTenFramesASecond()
        {
            PerfCaptureRequest request = PerfCaptureRequest.Read(
                new[] { "game.exe", "-perfCapture", "10", "-perfShots", @"C:\frames" }, string.Empty);

            Assert.AreEqual(0.1f, request.ShotEverySeconds, 0.0001f);
        }

        [Test]
        public void NonPositiveFramesAreIgnored()
        {
            // A zero delta appears on the first frame after a pause; counting it would drag every
            // percentile toward a frame that cost nothing.
            var sampler = new FrameTimeSampler(3);
            sampler.Add(0f);
            sampler.Add(-1f);

            Assert.AreEqual(0, sampler.Count);
        }
    }
}
