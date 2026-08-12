using AdaptiveBossArena.Utilities.Audio;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests for the synthesised audio, which is generated rather than authored.
    /// </summary>
    /// <remarks>
    /// Every sound in the game is a buffer of numbers built at startup, so its faults are arithmetic
    /// and can be asserted rather than listened for. These pin the three that made the game sound
    /// weak: loudness that depended on timbre, buffers that stopped without fading, and loops whose
    /// ends did not meet their beginnings.
    /// </remarks>
    [TestFixture]
    public sealed class ToneGeneratorTests
    {
        /// <summary>Tolerance on a peak, generous enough to survive resampling differences.</summary>
        private const float PeakTolerance = 0.02f;

        private static float[] Read(AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            return samples;
        }

        private static float PeakOf(float[] samples)
        {
            float loudest = 0f;

            foreach (float sample in samples)
            {
                loudest = Mathf.Max(loudest, Mathf.Abs(sample));
            }

            return loudest;
        }

        [Test]
        public void AClipIsBuiltAtTheRequestedLoudness()
        {
            AudioClip clip = ToneGenerator.CreateImpact("test.peak", peak: 0.6f);

            Assert.AreEqual(0.6f, PeakOf(Read(clip)), PeakTolerance);

            Object.DestroyImmediate(clip);
        }

        [Test]
        public void LoudnessDoesNotDependOnTimbre()
        {
            // The bug this exists for: the one-pole low-pass attenuates in proportion to its cutoff,
            // so the duller a sound was made the quieter it came out. The heavy hit, the execution
            // and the low-health heartbeat were all several times quieter than a light jab, which is
            // exactly backwards. Cutoff must choose timbre and nothing else.
            AudioClip bright = ToneGenerator.CreateImpact("test.bright", lowPassHz: 2200f, peak: 0.7f);
            AudioClip dull = ToneGenerator.CreateImpact("test.dull", lowPassHz: 85f, peak: 0.7f);

            Assert.AreEqual(PeakOf(Read(bright)), PeakOf(Read(dull)), PeakTolerance,
                "A duller impact should not be a quieter one.");

            Object.DestroyImmediate(bright);
            Object.DestroyImmediate(dull);
        }

        [Test]
        public void AOneShotFadesOutRatherThanStopping()
        {
            // An exponential decay never reaches zero, so a buffer that simply ends is a step, and a
            // step is a click. The stumble was the worst: it ended at a fifth of its peak.
            AudioClip clip = ToneGenerator.CreateStumble("test.stumble");
            float[] samples = Read(clip);

            Assert.Less(Mathf.Abs(samples[samples.Length - 1]), 0.001f,
                "The clip ends abruptly, which is heard as a click.");

            Object.DestroyImmediate(clip);
        }

        [Test]
        public void EveryOneShotEndsQuietly()
        {
            AudioClip[] clips =
            {
                ToneGenerator.CreateImpact("test.impact"),
                ToneGenerator.CreateMetallicRing("test.ring"),
                ToneGenerator.CreateShimmer("test.shimmer"),
                ToneGenerator.CreateWhoosh("test.whoosh"),
                ToneGenerator.CreatePerilWarning("test.peril"),
                ToneGenerator.CreateStumble("test.stumble2"),
                ToneGenerator.CreateRoar("test.roar")
            };

            foreach (AudioClip clip in clips)
            {
                float[] samples = Read(clip);

                Assert.Less(Mathf.Abs(samples[samples.Length - 1]), 0.001f,
                    $"{clip.name} ends abruptly.");

                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void ALoopMeetsItsOwnBeginning()
        {
            // A loop is played end-to-start forever, so a mismatch between the last sample and the
            // first is a tick once per cycle — far more noticeable than the drone carrying it.
            AudioClip clip = ToneGenerator.CreateDrone("test.drone");
            float[] samples = Read(clip);

            float seam = Mathf.Abs(samples[samples.Length - 1] - samples[0]);

            Assert.Less(seam, 0.05f, "The loop's end does not meet its start.");

            Object.DestroyImmediate(clip);
        }

        [Test]
        public void ALoopIsNotFadedToSilenceAtItsEnd()
        {
            // Tapering a loop would punch a hole in it once per cycle, so the release ramp applied to
            // one-shots must not be applied here.
            AudioClip clip = ToneGenerator.CreatePulse("test.pulse");
            float[] samples = Read(clip);

            Assert.Greater(PeakOf(samples), 0.1f);
            Assert.Greater(
                Mathf.Abs(samples[samples.Length - 1]) + Mathf.Abs(samples[samples.Length - 2]),
                0f,
                "A looping clip should not fade to digital silence at its end.");

            Object.DestroyImmediate(clip);
        }

        [Test]
        public void ClipsAreBuiltAtTheOutputSampleRate()
        {
            // Building at a fixed rate while the output runs at another means every clip is resampled
            // on every play, for nothing.
            AudioClip clip = ToneGenerator.CreateImpact("test.rate");

            Assert.AreEqual(ToneGenerator.SampleRate, clip.frequency);

            Object.DestroyImmediate(clip);
        }
    }
}
