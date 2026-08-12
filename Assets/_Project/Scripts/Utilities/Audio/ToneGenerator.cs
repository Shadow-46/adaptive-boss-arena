using System;
using UnityEngine;

namespace AdaptiveBossArena.Utilities.Audio
{
    /// <summary>
    /// Synthesises the game's sound effects at runtime.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The project ships with no audio assets and no sound designer, and a silent action game cannot
    /// feel good no matter how it looks — impact without sound reads as the hit not having landed.
    /// Rather than leave it silent, every cue here is generated from noise and simple oscillators.
    /// </para>
    /// <para>
    /// The approach is deliberately crude but it targets the properties that actually matter for
    /// combat feedback: a sharp transient so the hit lands on the exact frame, a short decay so
    /// rapid exchanges do not smear together, and clearly distinct timbres so the player can tell a
    /// deflect from a block without looking. Metallic ring for a deflect, dull thud for a block,
    /// filtered noise sweep for a swing.
    /// </para>
    /// <para>
    /// All clips are procedural and seeded, so they are identical every run and cost nothing to
    /// ship.
    /// </para>
    /// </remarks>
    public static class ToneGenerator
    {
        /// <summary>Rate assumed when the audio system has not reported one.</summary>
        public const int FallbackSampleRate = 44100;

        /// <summary>Length of the fade to silence at the end of a one-shot, in seconds.</summary>
        private const float ReleaseSeconds = 0.004f;

        /// <summary>
        /// Sample rate used for every generated clip.
        /// </summary>
        /// <remarks>
        /// Taken from the audio system rather than fixed, so clips are built at the rate the output
        /// actually runs at and are not resampled on every play. The project's output is 48 kHz while
        /// these clips were hard-coded to 44.1 kHz, so every one of them was being converted at
        /// runtime for nothing. Falls back when the value is unavailable, which happens in batch mode
        /// with no audio device.
        /// </remarks>
        public static int SampleRate
        {
            get
            {
                int configured = AudioSettings.outputSampleRate;

                return configured > 0 ? configured : FallbackSampleRate;
            }
        }

        /// <summary>
        /// Builds a percussive impact: a noise burst through a fast decay.
        /// </summary>
        /// <remarks>
        /// Noise rather than a tone, because impacts are broadband. The decay is what sells weight —
        /// a longer one reads as heavier without changing the volume.
        /// </remarks>
        /// <param name="name">Clip name.</param>
        /// <param name="durationSeconds">Total length.</param>
        /// <param name="lowPassHz">Cutoff. Lower sounds duller and heavier.</param>
        /// <param name="seed">Seed, so the clip is identical every run.</param>
        /// <param name="peak">
        /// How loud this impact should be. Independent of the cutoff, so a duller sound is not
        /// automatically a quieter one.
        /// </param>
        /// <returns>The generated clip.</returns>
        public static AudioClip CreateImpact(
            string name,
            float durationSeconds = 0.18f,
            float lowPassHz = 1400f,
            int seed = 1,
            float peak = 0.75f)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * durationSeconds));
            var samples = new float[sampleCount];

            var random = new System.Random(seed);
            float previous = 0f;
            float smoothing = Mathf.Clamp01(lowPassHz / (SampleRate * 0.5f));

            for (int i = 0; i < sampleCount; i++)
            {
                float noise = (float)(random.NextDouble() * 2d - 1d);

                // One-pole low pass. Cheap, and enough to turn white noise into something with a
                // sense of mass rather than a hiss.
                previous += (noise - previous) * smoothing;

                float envelope = FastDecay(i / (float)sampleCount, sharpness: 9f);
                samples[i] = previous * envelope;
            }

            return BuildClip(name, samples, peak);
        }

        /// <summary>
        /// Builds a metallic ring for a successful deflect.
        /// </summary>
        /// <remarks>
        /// Several inharmonic partials rather than a musical chord. Harmonically related tones sound
        /// like an instrument; deliberately detuned ones sound like struck metal, which is what a
        /// parry needs to feel like.
        /// </remarks>
        /// <param name="name">Clip name.</param>
        /// <param name="rootHz">Lowest partial.</param>
        /// <param name="durationSeconds">Total length.</param>
        /// <returns>The generated clip.</returns>
        public static AudioClip CreateMetallicRing(
            string name,
            float rootHz = 880f,
            float durationSeconds = 0.5f,
            float peak = 0.7f)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * durationSeconds));
            var samples = new float[sampleCount];

            float[] partials = { 1f, 2.76f, 5.4f, 8.93f };
            float[] gains = { 1f, 0.6f, 0.36f, 0.22f };

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float value = 0f;

                for (int p = 0; p < partials.Length; p++)
                {
                    value += Mathf.Sin(2f * Mathf.PI * rootHz * partials[p] * time) * gains[p];
                }

                // Higher partials should die first, as they do on real metal.
                float envelope = FastDecay(i / (float)sampleCount, sharpness: 4.5f);
                samples[i] = value * envelope * 0.25f;
            }

            return BuildClip(name, samples, peak);
        }

        /// <summary>
        /// Builds a soft rising shimmer, for restorative moments like a heal.
        /// </summary>
        /// <remarks>
        /// Harmonic partials rather than the inharmonic ones of the metallic ring, so it reads as
        /// warm and musical rather than struck. A gentle upward pitch glide and a soft attack make it
        /// feel like something being restored rather than something being hit.
        /// </remarks>
        /// <param name="name">Clip name.</param>
        /// <param name="baseHz">Fundamental of the chime.</param>
        /// <param name="durationSeconds">Total length.</param>
        /// <returns>The generated clip.</returns>
        public static AudioClip CreateShimmer(
            string name,
            float baseHz = 660f,
            float durationSeconds = 0.6f,
            float peak = 0.55f)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * durationSeconds));
            var samples = new float[sampleCount];

            float[] partials = { 1f, 1.5f, 2f, 3f };
            float[] gains = { 1f, 0.5f, 0.32f, 0.18f };

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float progress = i / (float)sampleCount;

                // A small upward glide is what makes it read as "rising" — recovery, not a chord.
                float glide = Mathf.Lerp(0.97f, 1.06f, progress);

                float value = 0f;
                for (int p = 0; p < partials.Length; p++)
                {
                    value += Mathf.Sin(2f * Mathf.PI * baseHz * partials[p] * glide * time) * gains[p];
                }

                // A soft attack (no sharp transient) keeps it gentle; the slow decay lets it bloom.
                float attack = Mathf.Clamp01(progress / 0.09f);
                float decay = FastDecay(progress, sharpness: 3.2f);

                samples[i] = value * attack * decay * 0.16f;
            }

            return BuildClip(name, samples, peak);
        }

        /// <summary>
        /// Builds a swing whoosh: filtered noise that rises then falls.
        /// </summary>
        /// <remarks>
        /// The rise-and-fall shape is what makes it read as motion past the ear rather than as a
        /// burst. Played on wind-up, it also gives the player an audible telegraph.
        /// </remarks>
        /// <param name="name">Clip name.</param>
        /// <param name="durationSeconds">Total length.</param>
        /// <param name="seed">Seed for reproducibility.</param>
        /// <returns>The generated clip.</returns>
        public static AudioClip CreateWhoosh(
            string name, float durationSeconds = 0.3f, int seed = 7, float peak = 0.45f)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * durationSeconds));
            var samples = new float[sampleCount];

            var random = new System.Random(seed);
            float previous = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float progress = i / (float)sampleCount;

                // Cutoff sweeps up and back down, which is what makes it sound like something
                // passing rather than simply starting and stopping.
                float sweep = Mathf.Sin(progress * Mathf.PI);
                float smoothing = Mathf.Lerp(0.02f, 0.35f, sweep);

                float noise = (float)(random.NextDouble() * 2d - 1d);
                previous += (noise - previous) * smoothing;

                samples[i] = previous * sweep * 0.5f;
            }

            return BuildClip(name, samples, peak);
        }

        /// <summary>
        /// Builds a sharp, alarming sting to warn of a perilous, unblockable attack.
        /// </summary>
        /// <remarks>
        /// A tritone — the most tense interval there is — under a fast tremolo, so it reads instantly
        /// as danger rather than as any ordinary swing. The player hears "do not block this" before
        /// the red telegraph has fully bloomed.
        /// </remarks>
        /// <param name="name">Clip name.</param>
        /// <param name="rootHz">Fundamental of the sting.</param>
        /// <param name="durationSeconds">Total length.</param>
        /// <returns>The generated clip.</returns>
        public static AudioClip CreatePerilWarning(
            string name,
            float rootHz = 300f,
            float durationSeconds = 0.42f,
            float peak = 0.85f)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * durationSeconds));
            var samples = new float[sampleCount];

            float[] partials = { 1f, 1.414f, 2f };
            float[] gains = { 1f, 0.85f, 0.4f };

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float progress = i / (float)sampleCount;

                float value = 0f;
                for (int p = 0; p < partials.Length; p++)
                {
                    value += Mathf.Sin(2f * Mathf.PI * rootHz * partials[p] * time) * gains[p];
                }

                // A fast tremolo makes it pulse like an alarm rather than sit like a chord.
                float tremolo = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 18f * time);
                float envelope = FastDecay(progress, sharpness: 2.4f);

                samples[i] = value * envelope * tremolo * 0.2f;
            }

            return BuildClip(name, samples, peak);
        }

        /// <summary>
        /// Builds an unsteady low lurch for the moment a committed boss swing overbalances it.
        /// </summary>
        /// <remarks>
        /// The pitch sags downward across the sound — the audible equivalent of tipping off balance —
        /// under a slow wobble and slightly detuned partials, so it lands as a heavy stumble the player
        /// can read as an opening rather than as a clean note.
        /// </remarks>
        /// <param name="name">Clip name.</param>
        /// <param name="rootHz">Fundamental. Low, because a large mass losing its footing is low.</param>
        /// <param name="durationSeconds">Total length.</param>
        /// <returns>The generated clip.</returns>
        public static AudioClip CreateStumble(
            string name,
            float rootHz = 165f,
            float durationSeconds = 0.55f,
            float peak = 0.7f)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * durationSeconds));
            var samples = new float[sampleCount];

            // Slightly detuned partials, so the tone never quite settles and reads as unsteady.
            float[] partials = { 1f, 1.5f, 2.02f };
            float[] gains = { 1f, 0.5f, 0.3f };

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float progress = i / (float)sampleCount;

                // The pitch sags across the sound, and a slow wobble makes the fall a lurch, not a slide.
                float glide = Mathf.Lerp(1f, 0.72f, progress);
                float wobble = 1f + 0.04f * Mathf.Sin(2f * Mathf.PI * 6.5f * time);

                float value = 0f;
                for (int p = 0; p < partials.Length; p++)
                {
                    value += Mathf.Sin(2f * Mathf.PI * rootHz * partials[p] * glide * wobble * time) * gains[p];
                }

                // A soft knock in, then a long heavy fall as the boss settles.
                float attack = Mathf.Clamp01(progress / 0.06f);
                float envelope = attack * FastDecay(progress, sharpness: 1.6f);

                samples[i] = value * envelope * 0.22f;
            }

            return BuildClip(name, samples, peak);
        }

        /// <summary>
        /// Builds a low roar for boss phase transitions.
        /// </summary>
        /// <param name="name">Clip name.</param>
        /// <param name="baseHz">Fundamental. Lower reads as larger.</param>
        /// <param name="durationSeconds">Total length.</param>
        /// <param name="seed">Seed for reproducibility.</param>
        /// <returns>The generated clip.</returns>
        public static AudioClip CreateRoar(
            string name,
            float baseHz = 70f,
            float durationSeconds = 1.4f,
            int seed = 13,
            float peak = 0.95f)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * durationSeconds));
            var samples = new float[sampleCount];

            var random = new System.Random(seed);
            float noiseState = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float progress = i / (float)sampleCount;

                // Pitch sags across the roar, which reads as effort and mass.
                float pitch = baseHz * Mathf.Lerp(1.15f, 0.85f, progress);

                float fundamental = Mathf.Sin(2f * Mathf.PI * pitch * time);
                float growl = Mathf.Sin(2f * Mathf.PI * pitch * 1.5f * time) * 0.4f;

                float noise = (float)(random.NextDouble() * 2d - 1d);
                noiseState += (noise - noiseState) * 0.06f;

                float envelope = Mathf.Sin(progress * Mathf.PI);
                samples[i] = (fundamental + growl + noiseState * 0.7f) * envelope * 0.3f;
            }

            return BuildClip(name, samples, peak);
        }

        /// <summary>
        /// Builds a sustained drone for the music bed.
        /// </summary>
        /// <remarks>
        /// Looped and layered by the audio service, with higher layers faded in as the boss
        /// escalates. Tension without melody, which suits an encounter that has no narrative beats
        /// to score.
        /// </remarks>
        /// <param name="name">Clip name.</param>
        /// <param name="rootHz">Fundamental.</param>
        /// <param name="durationSeconds">Loop length.</param>
        /// <returns>The generated clip.</returns>
        public static AudioClip CreateDrone(
            string name, float rootHz = 55f, float durationSeconds = 4f, float peak = 0.4f)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * durationSeconds));
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;

                // Slightly detuned unison, which beats gently and stops the drone sounding synthetic.
                float a = Mathf.Sin(2f * Mathf.PI * rootHz * time);
                float b = Mathf.Sin(2f * Mathf.PI * rootHz * 1.004f * time);
                float fifth = Mathf.Sin(2f * Mathf.PI * rootHz * 1.5f * time) * 0.35f;

                samples[i] = (a + b + fifth) * 0.18f;
            }

            return BuildLoopingClip(name, samples, peak);
        }

        /// <summary>
        /// Builds a rhythmic pulsing tone for the top music layer.
        /// </summary>
        /// <remarks>
        /// Where the drone is sustained tension, this is a heartbeat — a tone gated by a steady pulse.
        /// Faded in only for the final phase, it is what turns "fuller" into "urgent". The loop length
        /// is chosen to hold a whole number of beats so the pulse never stutters at the seam.
        /// </remarks>
        /// <param name="name">Clip name.</param>
        /// <param name="rootHz">Fundamental.</param>
        /// <param name="beatsPerMinute">Pulse rate.</param>
        /// <param name="beats">Number of beats in the loop.</param>
        /// <returns>The generated clip.</returns>
        public static AudioClip CreatePulse(
            string name,
            float rootHz = 110f,
            float beatsPerMinute = 120f,
            int beats = 8,
            float peak = 0.35f)
        {
            float beatSeconds = 60f / Mathf.Max(1f, beatsPerMinute);
            int beatSamples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * beatSeconds));
            int sampleCount = beatSamples * Mathf.Max(1, beats);
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;

                float tone = Mathf.Sin(2f * Mathf.PI * rootHz * time)
                             + Mathf.Sin(2f * Mathf.PI * rootHz * 1.5f * time) * 0.3f;

                // A sharp attack and quick decay within each beat gives the layer its drive.
                float withinBeat = (i % beatSamples) / (float)beatSamples;
                float pulse = FastDecay(withinBeat, sharpness: 6f);

                samples[i] = tone * pulse * 0.16f;
            }

            return BuildLoopingClip(name, samples, peak);
        }

        /// <summary>Exponential decay envelope, normalised to the clip's progress.</summary>
        private static float FastDecay(float progress, float sharpness) =>
            Mathf.Exp(-progress * sharpness);

        /// <summary>
        /// Scales a buffer so its loudest sample sits at a chosen peak.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is what makes a cue's loudness a decision rather than an accident. The one-pole
        /// low-pass used across these generators attenuates in proportion to its cutoff — output
        /// level falls with the square root of the cutoff frequency — so the duller a sound was made,
        /// the quieter it came out. The result was exactly backwards: a light jab at 2200 Hz was
        /// several times louder than the heavy hit, the execution, and the low-health heartbeat,
        /// which at 85 Hz was all but inaudible.
        /// </para>
        /// <para>
        /// Normalising every buffer to an explicit peak removes the coupling entirely, so cutoff
        /// chooses <em>timbre</em> and the peak argument chooses <em>loudness</em>. Relative balance
        /// between cues is therefore still deliberate — a footstep is quiet because it asks to be,
        /// not because of its filter.
        /// </para>
        /// </remarks>
        /// <param name="samples">Buffer to scale in place.</param>
        /// <param name="targetPeak">Desired absolute maximum, from zero to one.</param>
        private static void NormalisePeak(float[] samples, float targetPeak)
        {
            float loudest = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                loudest = Mathf.Max(loudest, Mathf.Abs(samples[i]));
            }

            // A silent buffer has nothing to scale, and dividing by its peak would be a division by
            // zero rather than a useful result.
            if (loudest <= Mathf.Epsilon)
            {
                return;
            }

            float scale = Mathf.Clamp01(targetPeak) / loudest;

            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= scale;
            }
        }

        /// <summary>
        /// Fades the last few milliseconds to silence.
        /// </summary>
        /// <remarks>
        /// An exponential decay never actually reaches zero, so a buffer that simply stops is a step
        /// discontinuity, and a step is heard as a click. The gentler envelopes were the worst
        /// offenders: the overbalance stumble ended at a fifth of its peak amplitude and the peril
        /// warning at a tenth, both cutting off hard. A ramp of a few milliseconds is inaudible as a
        /// fade and removes every one of those ticks at once.
        /// </remarks>
        private static void ApplyReleaseTaper(float[] samples)
        {
            int taper = Mathf.Min(samples.Length, Mathf.RoundToInt(SampleRate * ReleaseSeconds));

            for (int i = 0; i < taper; i++)
            {
                // Runs from the end backwards, so index 0 of the ramp is the final sample.
                samples[samples.Length - 1 - i] *= i / (float)taper;
            }
        }

        /// <summary>
        /// Returns a shortened buffer whose end meets its start, so it loops without a tick.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The buffer is trimmed by the fade length and the discarded tail is blended back over the
        /// head. The last kept sample is then followed — on wrap — by the sample that genuinely came
        /// after it in the original buffer, which is what makes the seam continuous.
        /// </para>
        /// <para>
        /// The previous implementation faded the tail toward the head in place without shortening,
        /// which left the final sample landing on the head's <em>fade-th</em> value while playback
        /// wrapped to its first: the discontinuity survived, merely relocated. It was also applied to
        /// buffers that were already whole numbers of cycles and needed nothing, where it dragged the
        /// first beat's attack in as a ghost a tenth of a second before the loop point.
        /// </para>
        /// </remarks>
        private static float[] MakeSeamless(float[] samples)
        {
            int fade = Mathf.Min(samples.Length / 8, SampleRate / 20);

            if (fade <= 0 || samples.Length <= fade * 2)
            {
                return samples;
            }

            int length = samples.Length - fade;
            var result = new float[length];
            Array.Copy(samples, result, length);

            for (int i = 0; i < fade; i++)
            {
                float blend = i / (float)fade;
                result[i] = Mathf.Lerp(samples[length + i], samples[i], blend);
            }

            return result;
        }

        /// <summary>Wraps a one-shot buffer into a clip, tapered and normalised.</summary>
        /// <param name="name">Clip name.</param>
        /// <param name="samples">Sample buffer.</param>
        /// <param name="peak">Loudness this cue should have, from zero to one.</param>
        private static AudioClip BuildClip(string name, float[] samples, float peak)
        {
            if (samples == null || samples.Length == 0)
            {
                throw new ArgumentException("Cannot build a clip with no samples.", nameof(samples));
            }

            ApplyReleaseTaper(samples);
            NormalisePeak(samples, peak);

            return CreateClip(name, samples);
        }

        /// <summary>
        /// Wraps a looping buffer into a clip, made seamless and normalised.
        /// </summary>
        /// <remarks>
        /// Deliberately does not taper: a fade to silence at the end of a loop is a hole punched in
        /// the sound once per cycle, which is the very artefact the taper exists to prevent elsewhere.
        /// </remarks>
        /// <param name="name">Clip name.</param>
        /// <param name="samples">Sample buffer.</param>
        /// <param name="peak">Loudness this layer should have, from zero to one.</param>
        private static AudioClip BuildLoopingClip(string name, float[] samples, float peak)
        {
            if (samples == null || samples.Length == 0)
            {
                throw new ArgumentException("Cannot build a clip with no samples.", nameof(samples));
            }

            float[] seamless = MakeSeamless(samples);
            NormalisePeak(seamless, peak);

            return CreateClip(name, seamless);
        }

        /// <summary>Hands a finished buffer to Unity.</summary>
        private static AudioClip CreateClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, channels: 1, SampleRate, stream: false);
            clip.SetData(samples, 0);

            return clip;
        }
    }
}
