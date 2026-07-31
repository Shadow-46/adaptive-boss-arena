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
        /// <summary>Sample rate used for every generated clip.</summary>
        public const int SampleRate = 44100;

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
        /// <returns>The generated clip.</returns>
        public static AudioClip CreateImpact(
            string name,
            float durationSeconds = 0.18f,
            float lowPassHz = 1400f,
            int seed = 1)
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

            return BuildClip(name, samples);
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
            float durationSeconds = 0.5f)
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

            return BuildClip(name, samples);
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
            float durationSeconds = 0.6f)
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

            return BuildClip(name, samples);
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
        public static AudioClip CreateWhoosh(string name, float durationSeconds = 0.3f, int seed = 7)
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

            return BuildClip(name, samples);
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
            int seed = 13)
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

            return BuildClip(name, samples);
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
        public static AudioClip CreateDrone(string name, float rootHz = 55f, float durationSeconds = 4f)
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

            ApplyLoopCrossfade(samples);
            return BuildClip(name, samples);
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
            string name, float rootHz = 110f, float beatsPerMinute = 120f, int beats = 8)
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

            ApplyLoopCrossfade(samples);
            return BuildClip(name, samples);
        }

        /// <summary>Exponential decay envelope, normalised to the clip's progress.</summary>
        private static float FastDecay(float progress, float sharpness) =>
            Mathf.Exp(-progress * sharpness);

        /// <summary>
        /// Crossfades a clip's ends so it loops without a click.
        /// </summary>
        /// <remarks>
        /// A discontinuity at the loop point is heard as a sharp tick every cycle, which is far more
        /// noticeable than the drone itself.
        /// </remarks>
        private static void ApplyLoopCrossfade(float[] samples)
        {
            int fade = Mathf.Min(samples.Length / 8, SampleRate / 10);

            for (int i = 0; i < fade; i++)
            {
                float blend = i / (float)fade;
                int tail = samples.Length - fade + i;

                samples[tail] = Mathf.Lerp(samples[tail], samples[i], blend);
            }
        }

        /// <summary>Wraps a sample buffer into a clip.</summary>
        private static AudioClip BuildClip(string name, float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                throw new ArgumentException("Cannot build a clip with no samples.", nameof(samples));
            }

            var clip = AudioClip.Create(name, samples.Length, channels: 1, SampleRate, stream: false);
            clip.SetData(samples, 0);

            return clip;
        }
    }
}
