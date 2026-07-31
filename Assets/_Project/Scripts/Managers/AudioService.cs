using System.Collections.Generic;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Utilities.Audio;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Plays every sound in the game, from procedurally generated clips.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements the <see cref="IAudioService"/> contract that was designed in the first phase and
    /// left unimplemented until the game turned out to be unpleasant to play in silence. Combat
    /// feedback is at least as much auditory as visual: without a sound on the exact frame of
    /// contact, a hit reads as not having landed however much the screen shakes.
    /// </para>
    /// <para>
    /// Sources are pooled and voice-limited. A three-hit combo landing inside a fifth of a second
    /// would otherwise stack three copies of the same impact and produce a harsh cluster instead of
    /// three distinct hits.
    /// </para>
    /// <para>
    /// Pitch is randomised slightly per playback. Repeating an identical sample is the single
    /// clearest tell that a sound is synthetic, and a few percent of variation removes almost all of
    /// that impression for no cost.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        /// <summary>Simultaneous effect voices. Beyond this the oldest is recycled.</summary>
        private const int VoiceCount = 12;

        /// <summary>Shortest gap between two playbacks of the same cue, in seconds.</summary>
        private const float SameCueCooldown = 0.035f;

        /// <summary>Pitch variation applied per playback.</summary>
        private const float PitchJitter = 0.08f;

        /// <summary>Stacked music loops: a base bed, a tension layer, and a driving pulse.</summary>
        private static readonly string[] MusicLayerClips = { "music.layer0", "music.layer1", "music.layer2" };

        /// <summary>
        /// Target volume of each layer at each intensity level (row = level, column = layer).
        /// </summary>
        /// <remarks>
        /// Level zero is the bare bed. Each step fades another layer in; at the top the bed ducks
        /// slightly so the driving pulse cuts through rather than muddying it.
        /// </remarks>
        private static readonly float[][] IntensityTargets =
        {
            new[] { 1f, 0f, 0f },
            new[] { 1f, 0.85f, 0f },
            new[] { 0.9f, 0.9f, 0.85f }
        };

        /// <summary>How fast a music layer fades toward its target volume, per second.</summary>
        private const float MusicFadePerSecond = 0.8f;

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> _lastPlayedAt = new Dictionary<string, float>();
        private readonly Dictionary<AudioBus, float> _busVolumes = new Dictionary<AudioBus, float>();

        private AudioSource[] _voices;
        private AudioSource[] _musicLayers;
        private float[] _musicTargets;
        private int _nextVoice;

        /// <summary>Cue identifiers, kept as constants so call sites cannot mistype them.</summary>
        public static class Cues
        {
            /// <summary>A light attack connecting.</summary>
            public const string HitLight = "hit.light";

            /// <summary>A heavy attack connecting.</summary>
            public const string HitHeavy = "hit.heavy";

            /// <summary>A swing passing through the air.</summary>
            public const string Whoosh = "swing.whoosh";

            /// <summary>A clean deflect.</summary>
            public const string Deflect = "guard.deflect";

            /// <summary>A late block.</summary>
            public const string Block = "guard.block";

            /// <summary>A guard or poise breaking.</summary>
            public const string PostureBreak = "guard.break";

            /// <summary>The boss escalating to a new phase.</summary>
            public const string BossRoar = "boss.roar";

            /// <summary>A perfect dodge.</summary>
            public const string PerfectDodge = "dodge.perfect";

            /// <summary>A dash launching.</summary>
            public const string Dash = "dodge.whoosh";

            /// <summary>A heal being channelled.</summary>
            public const string Heal = "player.heal";

            /// <summary>A weapon being drawn.</summary>
            public const string WeaponDraw = "weapon.draw";

            /// <summary>An execution — the riposte into a broken guard.</summary>
            public const string Execution = "player.execution";

            /// <summary>A player footstep.</summary>
            public const string FootstepPlayer = "step.player";

            /// <summary>A boss footstep — heavier and lower.</summary>
            public const string FootstepBoss = "step.boss";

            /// <summary>The focus meter reaching full.</summary>
            public const string FocusFull = "focus.full";

            /// <summary>A ground hazard erupting.</summary>
            public const string Hazard = "hazard.erupt";

            /// <summary>A guard being raised.</summary>
            public const string GuardRaise = "guard.raise";

            /// <summary>A single heartbeat, pulsed while the player is near death.</summary>
            public const string Heartbeat = "player.heartbeat";
        }

        private void Awake()
        {
            BuildVoices();
            GenerateClips();

            _busVolumes[AudioBus.Master] = 0.8f;
            _busVolumes[AudioBus.Music] = 0.5f;
            _busVolumes[AudioBus.Effects] = 0.9f;
            _busVolumes[AudioBus.Interface] = 0.8f;

            ServiceRegistry.Current.RegisterOrReplace<IAudioService>(this);
        }

        /// <inheritdoc />
        public void PlayCue(string cueId, Vector3 worldPosition) => Play(cueId, worldPosition, spatial: true);

        /// <inheritdoc />
        public void PlayCue2D(string cueId) => Play(cueId, Vector3.zero, spatial: false);

        /// <inheritdoc />
        public void PlayMusic(string trackId, float fadeSeconds = 1f)
        {
            if (_musicLayers == null)
            {
                return;
            }

            // The layered score is the game's only music, so a null track means fade to silence.
            if (string.IsNullOrEmpty(trackId))
            {
                for (int i = 0; i < _musicLayers.Length; i++)
                {
                    _musicTargets[i] = 0f;
                }

                return;
            }

            for (int i = 0; i < _musicLayers.Length; i++)
            {
                if (_musicLayers[i] != null && _clips.TryGetValue(MusicLayerClips[i], out AudioClip clip))
                {
                    _musicLayers[i].clip = clip;

                    if (!_musicLayers[i].isPlaying)
                    {
                        _musicLayers[i].Play();
                    }
                }
            }

            SetMusicIntensity(0);
        }

        /// <inheritdoc />
        public void SetMusicIntensity(int level)
        {
            if (_musicTargets == null)
            {
                return;
            }

            int clamped = Mathf.Clamp(level, 0, IntensityTargets.Length - 1);

            for (int i = 0; i < _musicTargets.Length; i++)
            {
                _musicTargets[i] = IntensityTargets[clamped][i];
            }
        }

        /// <inheritdoc />
        public void SetBusVolume(AudioBus bus, float linearVolume) =>
            _busVolumes[bus] = Mathf.Clamp01(linearVolume);

        /// <summary>Eases each music layer toward its target, so intensity and volume changes glide.</summary>
        private void Update()
        {
            if (_musicLayers == null)
            {
                return;
            }

            float effectiveMusic = EffectiveVolume(AudioBus.Music);
            float step = MusicFadePerSecond * Time.unscaledDeltaTime;

            for (int i = 0; i < _musicLayers.Length; i++)
            {
                if (_musicLayers[i] != null)
                {
                    _musicLayers[i].volume =
                        Mathf.MoveTowards(_musicLayers[i].volume, _musicTargets[i] * effectiveMusic, step);
                }
            }
        }

        /// <inheritdoc />
        public float GetBusVolume(AudioBus bus) =>
            _busVolumes.TryGetValue(bus, out float volume) ? volume : 1f;

        /// <summary>
        /// Plays a cue on the next free voice.
        /// </summary>
        /// <remarks>
        /// The per-cue cooldown is what keeps a flurry sounding like a flurry. Without it, several
        /// hits landing on nearly the same frame sum into one loud transient that reads as
        /// distortion rather than as impacts.
        /// </remarks>
        private void Play(string cueId, Vector3 worldPosition, bool spatial)
        {
            if (!_clips.TryGetValue(cueId, out AudioClip clip) || clip == null)
            {
                return;
            }

            float now = Time.unscaledTime;

            if (_lastPlayedAt.TryGetValue(cueId, out float lastPlayed) &&
                now - lastPlayed < SameCueCooldown)
            {
                return;
            }

            _lastPlayedAt[cueId] = now;

            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;

            voice.transform.position = worldPosition;
            voice.spatialBlend = spatial ? 0.65f : 0f;
            voice.clip = clip;
            voice.volume = EffectiveVolume(AudioBus.Effects);
            voice.pitch = 1f + Random.Range(-PitchJitter, PitchJitter);

            voice.Play();
        }

        /// <summary>Creates the pooled voices and the music source.</summary>
        private void BuildVoices()
        {
            _voices = new AudioSource[VoiceCount];

            for (int i = 0; i < VoiceCount; i++)
            {
                var voiceObject = new GameObject($"Voice_{i:D2}");
                voiceObject.transform.SetParent(transform, false);

                AudioSource source = voiceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.maxDistance = 40f;

                // Effects deliberately ignore the time scale, so impacts still sound during the
                // hit-stop they cause rather than being pitched down into a groan.
                source.ignoreListenerPause = true;

                _voices[i] = source;
            }

            _musicLayers = new AudioSource[MusicLayerClips.Length];
            _musicTargets = new float[MusicLayerClips.Length];

            for (int i = 0; i < MusicLayerClips.Length; i++)
            {
                var layerObject = new GameObject($"Music_{i}");
                layerObject.transform.SetParent(transform, false);

                AudioSource source = layerObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;

                _musicLayers[i] = source;
            }
        }

        /// <summary>Synthesises every clip the game uses.</summary>
        private void GenerateClips()
        {
            _clips[Cues.HitLight] = ToneGenerator.CreateImpact("hit.light", 0.14f, 2200f, seed: 3);
            _clips[Cues.HitHeavy] = ToneGenerator.CreateImpact("hit.heavy", 0.26f, 900f, seed: 5);
            _clips[Cues.Block] = ToneGenerator.CreateImpact("guard.block", 0.2f, 600f, seed: 11);
            _clips[Cues.Whoosh] = ToneGenerator.CreateWhoosh("swing.whoosh");

            // The deflect is the highest, brightest sound in the game on purpose: it is the moment
            // the player most needs to know they got it exactly right.
            _clips[Cues.Deflect] = ToneGenerator.CreateMetallicRing("guard.deflect", 1180f, 0.45f);
            _clips[Cues.PostureBreak] = ToneGenerator.CreateMetallicRing("guard.break", 320f, 0.9f);
            _clips[Cues.PerfectDodge] = ToneGenerator.CreateMetallicRing("dodge.perfect", 1560f, 0.3f);

            _clips[Cues.BossRoar] = ToneGenerator.CreateRoar("boss.roar");

            // A shorter, breathier whoosh than a swing, so a dash reads as the player moving rather
            // than attacking.
            _clips[Cues.Dash] = ToneGenerator.CreateWhoosh("dodge.whoosh", 0.2f, seed: 21);

            // Warm and rising for the heal; a bright, short chime the instant focus fills.
            _clips[Cues.Heal] = ToneGenerator.CreateShimmer("player.heal", 520f, 0.7f);
            _clips[Cues.FocusFull] = ToneGenerator.CreateShimmer("focus.full", 1040f, 0.4f);

            // A short high metallic 'shing' for drawing a weapon.
            _clips[Cues.WeaponDraw] = ToneGenerator.CreateMetallicRing("weapon.draw", 1500f, 0.2f);

            // The execution is the heaviest, lowest impact the player can make — the payoff of a
            // broken guard, so it hits harder than any ordinary blow.
            _clips[Cues.Execution] = ToneGenerator.CreateImpact("player.execution", 0.38f, 520f, seed: 29);

            // Footsteps: a soft tap for the player, a heavier and much lower thud for the boss.
            _clips[Cues.FootstepPlayer] = ToneGenerator.CreateImpact("step.player", 0.08f, 520f, seed: 31);
            _clips[Cues.FootstepBoss] = ToneGenerator.CreateImpact("step.boss", 0.16f, 220f, seed: 37);

            // A low, broadband rumble for a hazard erupting from the ground.
            _clips[Cues.Hazard] = ToneGenerator.CreateImpact("hazard.erupt", 0.5f, 340f, seed: 41);

            // A short, soft shift for raising a guard, and a low resonant thud for the heartbeat.
            _clips[Cues.GuardRaise] = ToneGenerator.CreateImpact("guard.raise", 0.09f, 900f, seed: 47);
            _clips[Cues.Heartbeat] = ToneGenerator.CreateImpact("player.heartbeat", 0.2f, 85f, seed: 43);

            // Three stacked loops, harmonically related so they layer without dissonance: a low bed,
            // a fifth above it for tension, and a rhythmic pulse an octave up for the final phase.
            _clips[MusicLayerClips[0]] = ToneGenerator.CreateDrone(MusicLayerClips[0], 55f);
            _clips[MusicLayerClips[1]] = ToneGenerator.CreateDrone(MusicLayerClips[1], 82.5f);
            _clips[MusicLayerClips[2]] = ToneGenerator.CreatePulse(MusicLayerClips[2], 110f, 120f, 8);
        }

        /// <summary>Combines a bus level with the master level.</summary>
        private float EffectiveVolume(AudioBus bus) =>
            GetBusVolume(bus) * GetBusVolume(AudioBus.Master);
    }
}
