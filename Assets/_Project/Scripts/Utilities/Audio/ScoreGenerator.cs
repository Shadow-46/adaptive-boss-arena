using System;
using UnityEngine;

namespace AdaptiveBossArena.Utilities.Audio
{
    /// <summary>
    /// Writes the fight's score from code: a dark choir, war drums and a brass ostinato, as stems that layer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The music used to be two sine drones and a beeping pulse, and the player heard it as a hum. A boss fight in
    /// the big action games is carried by a choir over drums and low brass, rising as the fight does. Recorded
    /// tracks replace these when they are downloaded; until then, and on a fresh clone, this is what plays.
    /// </para>
    /// <para>
    /// Every stem is the same four bars of D minor at the same tempo - i, i, VI, V - so the layers lock together
    /// however many are sounding: the choir alone in the opening, the drums under it as the fight presses, the
    /// brass driving over both at the end. Notes that ring past the end of the loop are wrapped onto its start,
    /// which is what a loop played forever would hear, so the seam is continuous, every loop is exactly four
    /// bars long, and the stems never drift apart.
    /// </para>
    /// <para>
    /// Voices are read from single-cycle wavetables rather than summed from sines sample by sample, so a stem
    /// renders in a few tens of milliseconds even in a browser. Rendered at a modest rate: none of these sounds
    /// has anything worth hearing above twelve kilohertz.
    /// </para>
    /// </remarks>
    public static class ScoreGenerator
    {
        /// <summary>Rate the score is rendered at; the mixer resamples it on playback.</summary>
        public const int ScoreSampleRate = 32000;

        /// <summary>Tempo: a march, heavy rather than hurried.</summary>
        public const float BeatsPerMinute = 84f;

        /// <summary>Bars in each loop.</summary>
        public const int Bars = 4;

        private const int BeatsPerBar = 4;
        private const int TableSize = 2048;

        /// <summary>Seconds in one beat.</summary>
        public static float BeatSeconds => 60f / BeatsPerMinute;

        /// <summary>Seconds in one loop of every stem.</summary>
        public static float LoopSeconds => BeatSeconds * BeatsPerBar * Bars;

        /// <summary>Samples in one loop of every stem.</summary>
        public static int LoopSamples => Mathf.RoundToInt(LoopSeconds * ScoreSampleRate);

        // D minor, i i VI V: the chord tones of each bar, as MIDI notes, low voice first.
        private static readonly int[][] Chords =
        {
            new[] { 50, 53, 57, 62 }, // Dm: D3 F3 A3 D4
            new[] { 50, 53, 57, 62 }, // Dm
            new[] { 46, 50, 53, 58 }, // Bb: Bb2 D3 F3 Bb3
            new[] { 45, 49, 52, 57 }  // A: A2 C#3 E3 A3
        };

        /// <summary>The bass root of each bar, under the chord.</summary>
        private static readonly int[] Roots = { 38, 38, 34, 33 };

        /// <summary>
        /// The ostinato, in eighth notes, as steps from the bar's root: the driving figure under the final phase.
        /// </summary>
        private static readonly int[] Ostinato = { 0, 0, 12, 0, 0, 7, 10, 7 };

        private static float[] _choirTable;
        private static float[] _brassTable;

        /// <summary>The choir: a slow-breathing pad of detuned voices over a low bass.</summary>
        /// <param name="name">Clip name.</param>
        /// <param name="peak">Loudness, zero to one.</param>
        /// <returns>The looping clip.</returns>
        public static AudioClip CreateChoir(string name, float peak = 0.42f) =>
            Finish(name, RenderChoir(), peak);

        /// <summary>The war drums: taiko hits on a marching figure, with a roll into each loop's turn.</summary>
        /// <param name="name">Clip name.</param>
        /// <param name="peak">Loudness, zero to one.</param>
        /// <returns>The looping clip.</returns>
        public static AudioClip CreateDrums(string name, float peak = 0.62f) =>
            Finish(name, RenderDrums(), peak);

        /// <summary>The brass: a low, biting eighth-note ostinato on each bar's root, with an anvil on the turns.</summary>
        /// <param name="name">Clip name.</param>
        /// <param name="peak">Loudness, zero to one.</param>
        /// <returns>The looping clip.</returns>
        public static AudioClip CreateBrass(string name, float peak = 0.4f) =>
            Finish(name, RenderBrass(), peak);

        /// <summary>A single hit to mark a turn of the fight: a deep drum under a choir swell that dies away.</summary>
        /// <param name="name">Clip name.</param>
        /// <param name="peak">Loudness, zero to one.</param>
        /// <returns>The one-shot clip.</returns>
        public static AudioClip CreateStinger(string name, float peak = 0.8f) =>
            Finish(name, RenderStinger(), peak);

        /// <summary>
        /// The arena's room: low wind through the stone, embers cracking in the braziers, a far-off rumble.
        /// </summary>
        /// <param name="name">Clip name.</param>
        /// <param name="peak">Loudness, zero to one.</param>
        /// <returns>The looping clip.</returns>
        public static AudioClip CreateAmbience(string name, float peak = 0.5f) =>
            Finish(name, RenderAmbience(), peak);

        /// <summary>A thunderclap: the crack of the strike, then a long roll dying away.</summary>
        /// <param name="name">Clip name.</param>
        /// <param name="peak">Loudness, zero to one.</param>
        /// <returns>The one-shot clip.</returns>
        public static AudioClip CreateThunder(string name, float peak = 0.85f) =>
            Finish(name, RenderThunder(), peak);

        /// <summary>Renders the thunderclap.</summary>
        /// <returns>The samples.</returns>
        public static float[] RenderThunder()
        {
            int length = Mathf.RoundToInt(4.5f * ScoreSampleRate);
            float[] buffer = new float[length];
            var random = new System.Random(0x7DD7);
            float roll = 0f;

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)ScoreSampleRate;
                float white = (float)random.NextDouble() * 2f - 1f;

                // The roll: noise smoothed heavily, swelling a moment after the crack and dying over seconds,
                // with its rumbles bunched the way thunder breaks over hills.
                roll = Mathf.Lerp(roll, white, 0.02f);
                float swell = Mathf.Clamp01(t / 0.25f) * Mathf.Exp(-t * 0.9f);
                float bunching = 0.6f + 0.4f * Mathf.Sin(t * 7.3f) * Mathf.Sin(t * 2.1f + 0.5f);

                // The crack: a bright burst in the first instant.
                float crack = white * Mathf.Exp(-t * 30f) * 0.9f;

                buffer[i] = roll * 6f * swell * bunching + crack;
            }

            LowPass(buffer, 2600f, circular: false);
            return buffer;
        }

        /// <summary>Renders the choir stem.</summary>
        /// <returns>One loop of samples.</returns>
        public static float[] RenderChoir()
        {
            float[] buffer = new float[LoopSamples];
            float barSeconds = BeatSeconds * BeatsPerBar;

            for (int bar = 0; bar < Bars; bar++)
            {
                float start = bar * barSeconds;

                foreach (int note in Chords[bar])
                {
                    // Three voices to a note, a few cents apart, is what turns an organ into a choir.
                    AddPad(buffer, ChoirTable, MidiToHz(note), start, barSeconds + 0.6f, 0.55f, 0.18f, 0f);
                    AddPad(buffer, ChoirTable, MidiToHz(note), start, barSeconds + 0.6f, 0.6f, 0.14f, 9f);
                    AddPad(buffer, ChoirTable, MidiToHz(note), start, barSeconds + 0.6f, 0.65f, 0.14f, -8f);
                }

                // The low root, sustained: the floor the whole score stands on.
                AddPad(buffer, ChoirTable, MidiToHz(Roots[bar]), start, barSeconds + 0.4f, 0.3f, 0.32f, 0f);
            }

            LowPass(buffer, 1800f);
            return buffer;
        }

        /// <summary>Renders the drum stem.</summary>
        /// <returns>One loop of samples.</returns>
        public static float[] RenderDrums()
        {
            float[] buffer = new float[LoopSamples];
            float sixteenth = BeatSeconds / 4f;
            var random = new System.Random(0xD2D2);

            for (int bar = 0; bar < Bars; bar++)
            {
                float start = bar * BeatSeconds * BeatsPerBar;

                // Boom on one, the pickup into three, boom on three, a lighter hit on four.
                AddTaiko(buffer, start, 1f, 58f, random);
                AddTaiko(buffer, start + sixteenth * 6f, 0.55f, 66f, random);
                AddTaiko(buffer, start + sixteenth * 8f, 0.9f, 58f, random);
                AddTaiko(buffer, start + sixteenth * 12f, 0.6f, 72f, random);

                // Rim clicks on the off-beats keep the march moving between the booms.
                for (int beat = 0; beat < BeatsPerBar; beat++)
                {
                    AddClick(buffer, start + BeatSeconds * (beat + 0.5f), 0.22f, random);
                }

                // The last bar turns with a sixteenth-note roll that swells into the loop's first boom.
                if (bar == Bars - 1)
                {
                    for (int step = 12; step < 16; step++)
                    {
                        AddTaiko(buffer, start + sixteenth * step, 0.35f + 0.12f * (step - 12), 80f, random);
                    }
                }
            }

            return buffer;
        }

        /// <summary>Renders the brass stem.</summary>
        /// <returns>One loop of samples.</returns>
        public static float[] RenderBrass()
        {
            float[] buffer = new float[LoopSamples];
            float eighth = BeatSeconds / 2f;
            var random = new System.Random(0xB4A5);

            for (int bar = 0; bar < Bars; bar++)
            {
                float start = bar * BeatSeconds * BeatsPerBar;

                for (int step = 0; step < Ostinato.Length; step++)
                {
                    float hz = MidiToHz(Roots[bar] + 12 + Ostinato[step]);
                    float accent = step % 4 == 0 ? 1f : 0.7f;
                    AddStab(buffer, BrassTable, hz, start + eighth * step, eighth * 0.9f, 0.22f * accent);
                }

                // Struck iron on the first and third bars: the forge under the march.
                if (bar % 2 == 0)
                {
                    AddAnvil(buffer, start, 0.3f, random);
                }
            }

            LowPass(buffer, 2400f);
            return buffer;
        }

        /// <summary>Renders the stinger.</summary>
        /// <returns>The samples.</returns>
        public static float[] RenderStinger()
        {
            float[] buffer = new float[Mathf.RoundToInt(3.2f * ScoreSampleRate)];
            var random = new System.Random(0x5717);

            AddTaiko(buffer, 0f, 1f, 46f, random, wrap: false);
            AddTaiko(buffer, 0.02f, 0.8f, 62f, random, wrap: false);

            foreach (int note in new[] { 38, 50, 57, 62 })
            {
                AddPad(buffer, ChoirTable, MidiToHz(note), 0f, 2.8f, 0.08f, 0.2f, 0f, wrap: false, release: 2.2f);
            }

            LowPass(buffer, 2000f, circular: false);
            return buffer;
        }

        /// <summary>Renders the ambience loop.</summary>
        /// <returns>One loop of samples.</returns>
        public static float[] RenderAmbience()
        {
            const float seconds = 16f;
            int length = Mathf.RoundToInt(seconds * ScoreSampleRate);
            float[] buffer = new float[length];
            var random = new System.Random(0xA4B1);

            // Wind: brown noise, swelling and falling on gusts whose periods divide the loop, so it wraps cleanly.
            float brown = 0f;

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                brown = Mathf.Clamp(brown + ((float)random.NextDouble() * 2f - 1f) * 0.02f, -1f, 1f) * 0.998f;
                float gust = 0.55f + 0.25f * Mathf.Sin(2f * Mathf.PI * t * 2f) + 0.2f * Mathf.Sin(2f * Mathf.PI * t * 3f + 1.3f);
                buffer[i] = brown * gust * 0.9f;
            }

            // The brown walk does not end where it began; a crossfade over the seam hides the step.
            int fade = ScoreSampleRate / 4;

            for (int i = 0; i < fade; i++)
            {
                float blend = (i + 1) / (float)fade;
                buffer[length - fade + i] = Mathf.Lerp(buffer[length - fade + i], buffer[i], blend);
            }

            LowPass(buffer, 700f);

            // Embers: sparse, bright cracks, some in quick clusters as a log settles.
            for (int crack = 0; crack < 90; crack++)
            {
                float at = (float)random.NextDouble() * seconds;
                float loud = 0.08f + 0.22f * (float)random.NextDouble();
                AddCrack(buffer, at, loud, random);

                if (random.NextDouble() < 0.3)
                {
                    AddCrack(buffer, at + 0.03f + 0.05f * (float)random.NextDouble(), loud * 0.6f, random);
                }
            }

            // Two distant rumbles, as if something huge shifts in the dark beyond the arena.
            AddRumble(buffer, 3.5f, 0.35f);
            AddRumble(buffer, 11f, 0.28f);

            return buffer;
        }

        private static float[] ChoirTable => _choirTable ??= BuildTable(harmonic => harmonic switch
        {
            // Strong low partials and a lift around the third to fifth, the "ah" of a vowel.
            1 => 1f,
            2 => 0.55f,
            3 => 0.42f,
            4 => 0.35f,
            5 => 0.22f,
            6 => 0.1f,
            7 => 0.06f,
            8 => 0.04f,
            _ => 0f
        });

        private static float[] BrassTable => _brassTable ??= BuildTable(harmonic =>
            harmonic <= 14 ? 1f / harmonic : 0f);

        private static float[] BuildTable(Func<int, float> amplitude)
        {
            var table = new float[TableSize];

            for (int i = 0; i < TableSize; i++)
            {
                double phase = 2.0 * Math.PI * i / TableSize;
                double sum = 0.0;

                for (int harmonic = 1; harmonic <= 16; harmonic++)
                {
                    sum += amplitude(harmonic) * Math.Sin(phase * harmonic);
                }

                table[i] = (float)sum;
            }

            float max = 0f;

            foreach (float sample in table)
            {
                max = Mathf.Max(max, Mathf.Abs(sample));
            }

            for (int i = 0; i < TableSize; i++)
            {
                table[i] /= Mathf.Max(0.0001f, max);
            }

            return table;
        }

        private static float MidiToHz(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        /// <summary>Adds into the buffer, wrapping past the end onto the start as a loop played forever would.</summary>
        private static void Write(float[] buffer, int index, float value, bool wrap)
        {
            if (wrap)
            {
                buffer[((index % buffer.Length) + buffer.Length) % buffer.Length] += value;
            }
            else if (index >= 0 && index < buffer.Length)
            {
                buffer[index] += value;
            }
        }

        private static void AddPad(
            float[] buffer, float[] table, float hz, float startSeconds, float seconds, float attackSeconds,
            float gain, float detuneCents, bool wrap = true, float release = 0.6f)
        {
            float frequency = hz * Mathf.Pow(2f, detuneCents / 1200f);
            int start = Mathf.RoundToInt(startSeconds * ScoreSampleRate);
            int count = Mathf.RoundToInt(seconds * ScoreSampleRate);
            double phase = 0.0;
            double step = frequency * TableSize / ScoreSampleRate;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)ScoreSampleRate;
                float envelope = Mathf.Clamp01(t / attackSeconds) * Mathf.Clamp01((seconds - t) / release);

                // A slow vibrato, the breath that keeps a held note alive.
                double vibrato = 1.0 + 0.004 * Math.Sin(2.0 * Math.PI * 4.8 * t + detuneCents);
                phase += step * vibrato;

                while (phase >= TableSize)
                {
                    phase -= TableSize;
                }

                Write(buffer, start + i, table[(int)phase] * envelope * gain, wrap);
            }
        }

        private static void AddStab(float[] buffer, float[] table, float hz, float startSeconds, float seconds, float gain)
        {
            int start = Mathf.RoundToInt(startSeconds * ScoreSampleRate);
            int count = Mathf.RoundToInt(seconds * ScoreSampleRate);
            double phase = 0.0;
            double step = hz * TableSize / ScoreSampleRate;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)ScoreSampleRate;
                float envelope = Mathf.Clamp01(t / 0.012f) * Mathf.Exp(-t * 7f) * Mathf.Clamp01((seconds - t) / 0.02f);
                phase += step;

                while (phase >= TableSize)
                {
                    phase -= TableSize;
                }

                Write(buffer, start + i, table[(int)phase] * envelope * gain, wrap: true);
            }
        }

        private static void AddTaiko(
            float[] buffer, float startSeconds, float gain, float baseHz, System.Random random, bool wrap = true)
        {
            int start = Mathf.RoundToInt(startSeconds * ScoreSampleRate);
            int count = Mathf.RoundToInt(0.9f * ScoreSampleRate);
            double phase = 0.0;
            float noise = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)ScoreSampleRate;

                // The skin's pitch drops as it is struck; the body of the drum rings low beneath the slap.
                float hz = baseHz * (1f + 1.6f * Mathf.Exp(-t * 28f));
                phase += 2.0 * Math.PI * hz / ScoreSampleRate;
                float body = (float)Math.Sin(phase) * Mathf.Exp(-t * 5.5f);

                noise = Mathf.Lerp(noise, (float)random.NextDouble() * 2f - 1f, 0.25f);
                float slap = noise * Mathf.Exp(-t * 45f) * 0.6f;

                Write(buffer, start + i, (float)Math.Tanh((body + slap) * 1.6f) * gain, wrap);
            }
        }

        private static void AddClick(float[] buffer, float startSeconds, float gain, System.Random random)
        {
            int start = Mathf.RoundToInt(startSeconds * ScoreSampleRate);
            int count = Mathf.RoundToInt(0.06f * ScoreSampleRate);
            float previous = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)ScoreSampleRate;
                float white = (float)random.NextDouble() * 2f - 1f;

                // A first difference of noise: bright, woody, gone in a moment.
                Write(buffer, start + i, (white - previous) * Mathf.Exp(-t * 90f) * gain, wrap: true);
                previous = white;
            }
        }

        private static void AddAnvil(float[] buffer, float startSeconds, float gain, System.Random random)
        {
            int start = Mathf.RoundToInt(startSeconds * ScoreSampleRate);
            int count = Mathf.RoundToInt(1.4f * ScoreSampleRate);
            float[] partials = { 523f, 1307f, 2213f, 2961f };

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)ScoreSampleRate;
                float sample = 0f;

                for (int p = 0; p < partials.Length; p++)
                {
                    sample += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * Mathf.Exp(-t * (3f + p * 2.5f)) / (p + 1);
                }

                float strike = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 120f) * 0.5f;
                Write(buffer, start + i, (sample + strike) * gain, wrap: true);
            }
        }

        private static void AddCrack(float[] buffer, float startSeconds, float gain, System.Random random)
        {
            int start = Mathf.RoundToInt(startSeconds * ScoreSampleRate);
            int count = Mathf.RoundToInt(0.025f * ScoreSampleRate);
            float previous = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)ScoreSampleRate;
                float white = (float)random.NextDouble() * 2f - 1f;
                Write(buffer, start + i, (white - previous) * Mathf.Exp(-t * 260f) * gain, wrap: true);
                previous = white;
            }
        }

        private static void AddRumble(float[] buffer, float startSeconds, float gain)
        {
            int start = Mathf.RoundToInt(startSeconds * ScoreSampleRate);
            int count = Mathf.RoundToInt(3.5f * ScoreSampleRate);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)ScoreSampleRate;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 3.5f));
                float sample = Mathf.Sin(2f * Mathf.PI * 31f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 43.5f * t + 0.7f) * 0.4f;
                Write(buffer, start + i, sample * envelope * envelope * gain, wrap: true);
            }
        }

        /// <summary>
        /// A one-pole low-pass. For a loop it runs twice round, keeping only the second pass, so its state at the
        /// seam is the state the loop actually arrives with.
        /// </summary>
        private static void LowPass(float[] buffer, float cutoffHz, bool circular = true)
        {
            float alpha = 1f - Mathf.Exp(-2f * Mathf.PI * cutoffHz / ScoreSampleRate);
            float state = 0f;
            int passes = circular ? 2 : 1;

            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    state += alpha * (buffer[i] - state);

                    if (pass == passes - 1)
                    {
                        buffer[i] = state;
                    }
                }
            }
        }

        /// <summary>Scales a buffer so its loudest sample sits at the given peak.</summary>
        /// <param name="buffer">The samples.</param>
        /// <param name="peak">The peak, zero to one.</param>
        public static void Normalise(float[] buffer, float peak)
        {
            float max = 0f;

            foreach (float sample in buffer)
            {
                max = Mathf.Max(max, Mathf.Abs(sample));
            }

            if (max <= 0f)
            {
                return;
            }

            float scale = Mathf.Clamp01(peak) / max;

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] *= scale;
            }
        }

        private static AudioClip Finish(string name, float[] buffer, float peak)
        {
            Normalise(buffer, peak);
            var clip = AudioClip.Create(name, buffer.Length, 1, ScoreSampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }
    }
}
