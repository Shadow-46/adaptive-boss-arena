using AdaptiveBossArena.Utilities.Audio;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Pins the written score: stems that lock together, loop without a click, and actually sound.
    /// </summary>
    public sealed class ScoreTests
    {
        /// <summary>The largest step allowed across a loop's seam, against the stem's own peak.</summary>
        private const float LargestSeamStep = 0.12f;

        private static float Peak(float[] samples)
        {
            float max = 0f;

            foreach (float sample in samples)
            {
                max = Mathf.Max(max, Mathf.Abs(sample));
            }

            return max;
        }

        private static float Rms(float[] samples)
        {
            double sum = 0.0;

            foreach (float sample in samples)
            {
                sum += sample * sample;
            }

            return (float)System.Math.Sqrt(sum / samples.Length);
        }

        [Test]
        public void EveryStem_IsExactlyTheSameFourBars()
        {
            int expected = ScoreGenerator.LoopSamples;

            Assert.AreEqual(expected, ScoreGenerator.RenderChoir().Length);
            Assert.AreEqual(expected, ScoreGenerator.RenderDrums().Length);
            Assert.AreEqual(expected, ScoreGenerator.RenderBrass().Length);
            Assert.AreEqual(ScoreGenerator.Bars * 4 * ScoreGenerator.BeatSeconds, ScoreGenerator.LoopSeconds, 1e-4f);
        }

        [Test]
        public void TheSustainedLoops_JoinWithoutAClick()
        {
            foreach (float[] stem in new[] { ScoreGenerator.RenderChoir(), ScoreGenerator.RenderAmbience() })
            {
                float peak = Peak(stem);
                float seam = Mathf.Abs(stem[0] - stem[stem.Length - 1]);

                Assert.Greater(peak, 0f, "A stem rendered silent.");
                Assert.Less(seam / peak, LargestSeamStep, "The loop jumps at its seam, which plays as a click.");
            }
        }

        [Test]
        public void ThePercussiveLoops_StepNoHarderAtTheSeamThanAnywhereElse()
        {
            // The drums and the brass strike on the loop's first beat, so the seam carries an onset by design. A
            // click would be a step larger than any the stem takes on its own, which wrapping makes impossible.
            foreach (float[] stem in new[] { ScoreGenerator.RenderDrums(), ScoreGenerator.RenderBrass() })
            {
                float largest = 0f;

                for (int i = 1; i < stem.Length; i++)
                {
                    largest = Mathf.Max(largest, Mathf.Abs(stem[i] - stem[i - 1]));
                }

                Assert.Greater(Peak(stem), 0f, "A stem rendered silent.");
                Assert.LessOrEqual(Mathf.Abs(stem[0] - stem[stem.Length - 1]), largest);
            }
        }

        [Test]
        public void TheStems_AreDifferentInstruments()
        {
            // A choir sustains; drums strike and decay. Measured as how far the loudest moment stands above a
            // typical one, over twenty-millisecond windows.
            float choir = Swing(ScoreGenerator.RenderChoir());
            float drums = Swing(ScoreGenerator.RenderDrums());

            Assert.Greater(drums, choir * 2f, $"Drums swing {drums:0.0}, the choir {choir:0.0}.");
        }

        /// <summary>The loudest twenty-millisecond window's level over the median window's.</summary>
        private static float Swing(float[] samples)
        {
            int window = ScoreGenerator.ScoreSampleRate / 50;
            int count = samples.Length / window;
            var levels = new float[count];

            for (int w = 0; w < count; w++)
            {
                var slice = new float[window];
                System.Array.Copy(samples, w * window, slice, 0, window);
                levels[w] = Rms(slice);
            }

            System.Array.Sort(levels);
            return levels[count - 1] / Mathf.Max(1e-6f, levels[count / 2]);
        }

        [Test]
        public void TheStinger_Rings_ThenDiesAway()
        {
            float[] stinger = ScoreGenerator.RenderStinger();
            int tenth = stinger.Length / 10;

            float start = 0f;
            float end = 0f;

            for (int i = 0; i < tenth; i++)
            {
                start = Mathf.Max(start, Mathf.Abs(stinger[i]));
                end = Mathf.Max(end, Mathf.Abs(stinger[stinger.Length - 1 - i]));
            }

            Assert.Greater(start, end * 4f);
        }
    }
}
