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

        /// <summary>Fade rate used when a caller asks for no fade at all.</summary>
        private const float ImmediateMusicFade = 100f;

        /// <summary>Distance within which a world sound plays at full volume, in metres.</summary>
        /// <remarks>
        /// Roughly the range the fight is actually conducted at, so ordinary combat is not quietened
        /// by its own distance falloff — the falloff exists to place a sound, not to hide it.
        /// </remarks>
        private const float NearFieldMetres = 4f;

        /// <summary>Distance beyond which a world sound is effectively inaudible, in metres.</summary>
        private const float ArenaAudibleMetres = 22f;

        /// <summary>How far the score is pulled down under a loud effect.</summary>
        private const float DuckDepth = 0.45f;

        /// <summary>How long a duck lasts before the score returns.</summary>
        private const float DuckSeconds = 0.35f;

        /// <summary>Cue gain above which a sound is loud enough to duck the score.</summary>
        private const float DuckThresholdGain = 0.9f;

        /// <summary>Seconds remaining in the current duck.</summary>
        private float _duckRemaining;

        /// <summary>
        /// Relative loudness per cue, on top of the peak each clip was generated at.
        /// </summary>
        /// <remarks>
        /// The clip peak decides how a sound is shaped; this decides how much of it belongs in the
        /// mix. Keeping the two separate means the balance can be adjusted without regenerating any
        /// audio, and it is the only place the relative importance of the cues is written down.
        /// Anything absent plays at unity.
        /// </remarks>
        private static readonly Dictionary<string, float> CueGains = new Dictionary<string, float>
        {
            // Constant background. Loud footsteps are the fastest way to make a mix tiring.
            { Cues.FootstepPlayer, 0.5f },
            { Cues.FootstepBoss, 0.7f },
            { Cues.Whoosh, 0.7f },
            { Cues.SwingBlade, 0.7f },
            { Cues.SwingGreatsword, 0.8f },
            { Cues.SwingEnergy, 0.65f },
            { Cues.Whiff, 0.55f },
            { Cues.GuardRaise, 0.6f },

            // The moments the fight turns on.
            { Cues.Deflect, 1f },
            { Cues.PostureBreak, 1f },
            { Cues.Execution, 1f },
            { Cues.Peril, 1f },
            { Cues.BossRoar, 1f },
            { Cues.PlayerDeath, 1f },
            { Cues.BossDeath, 1f },

            // Interface, deliberately quiet enough to sit under a fight in progress.
            { Cues.UiClick, 0.45f },
            { Cues.UiHover, 0.3f }
        };

        /// <summary>Current fade rate, set by the fade length the caller asked for.</summary>
        private float _musicFadePerSecond = MusicFadePerSecond;

        /// <summary>Whether the score has been started, so intensity is only seeded once.</summary>
        private bool _hasMusicStarted;

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

            /// <summary>The warning sting on a perilous, unblockable wind-up.</summary>
            public const string Peril = "peril.warning";

            /// <summary>The unsteady stagger when a committed boss swing overbalances it.</summary>
            public const string Overbalance = "boss.overbalance";

            /// <summary>The player falling. The fight's ending had no sound of its own.</summary>
            public const string PlayerDeath = "player.death";

            /// <summary>The boss falling: lower and longer, because it is the larger thing.</summary>
            public const string BossDeath = "boss.death";

            /// <summary>A swing that touched nothing.</summary>
            public const string Whiff = "swing.whiff";

            /// <summary>A heal that ran to completion rather than being interrupted.</summary>
            public const string HealComplete = "player.healed";

            /// <summary>Swing of the standard blade.</summary>
            public const string SwingBlade = "swing.blade";

            /// <summary>Swing of the greatsword: slow, low and airy.</summary>
            public const string SwingGreatsword = "swing.greatsword";

            /// <summary>Swing of the energy blade: thin and quick.</summary>
            public const string SwingEnergy = "swing.energy";

            /// <summary>A menu button being pressed.</summary>
            public const string UiClick = "ui.click";

            /// <summary>A menu button being highlighted.</summary>
            public const string UiHover = "ui.hover";
        }

        /// <summary>
        /// One recorded clip standing in for a synthesised cue.
        /// </summary>
        /// <remarks>
        /// The route for real audio. Everything here is generated at startup from code, which ships
        /// nothing and costs nothing, but it will never beat a recorded sound. Naming a cue and
        /// dropping a file beside it replaces that one sound and leaves every caller untouched, so
        /// the bank can be replaced a cue at a time rather than all at once.
        /// </remarks>
        [System.Serializable]
        private struct CueOverride
        {
            [Tooltip("Cue identifier to replace, as listed in AudioService.Cues.")]
            public string CueId;

            [Tooltip("Clip to play instead of the synthesised one.")]
            public AudioClip Clip;
        }

        [SerializeField]
        [Tooltip("Recorded clips that replace individual synthesised cues. Leave empty to use the " +
                 "generated bank.")]
        private CueOverride[] _cueOverrides = new CueOverride[0];

        private void Awake()
        {
            BuildVoices();
            GenerateClips();
            ApplyCueOverrides();

            // These must match SettingsData's defaults, or the game plays at one volume until the
            // settings menu is opened for the first time and pushes the saved values down.
            _busVolumes[AudioBus.Master] = SettingsData.DefaultMasterVolume;
            _busVolumes[AudioBus.Music] = SettingsData.DefaultMusicVolume;
            _busVolumes[AudioBus.Effects] = SettingsData.DefaultEffectsVolume;
            _busVolumes[AudioBus.Interface] = SettingsData.DefaultEffectsVolume;

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

            // The requested fade is honoured rather than ignored, and the bed is only introduced when
            // nothing is playing yet. Resetting unconditionally meant any later call — a restart, say
            // — silently dropped the score back to the opening layer while the boss was still in a
            // late phase.
            _musicFadePerSecond = fadeSeconds > 0f ? 1f / fadeSeconds : ImmediateMusicFade;

            if (!_hasMusicStarted)
            {
                _hasMusicStarted = true;
                SetMusicIntensity(0);
            }
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

            _duckRemaining = Mathf.Max(0f, _duckRemaining - Time.unscaledDeltaTime);

            // Recovers over the whole duck rather than snapping back, so a run of impacts holds the
            // score down and it rises again only once the exchange is over.
            float duck = Mathf.Lerp(1f, 1f - DuckDepth, _duckRemaining / DuckSeconds);
            float effectiveMusic = EffectiveVolume(AudioBus.Music) * duck;
            float step = _musicFadePerSecond * Time.unscaledDeltaTime;

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

            AudioSource voice = ClaimVoice();

            voice.transform.position = worldPosition;

            // Fully spatialised, not the previous two-thirds. Leaving a third of every world sound
            // unpanned meant even a correctly-placed impact leaked out of the centre of the mix.
            voice.spatialBlend = spatial ? 1f : 0f;
            voice.clip = clip;
            voice.volume = EffectiveVolume(AudioBus.Effects) * GainFor(cueId);
            voice.pitch = 1f + Random.Range(-PitchJitter, PitchJitter);

            voice.Play();

            // A loud moment ducks the score rather than fighting it. Nothing else in the game
            // arbitrates between the two, so without this the music simply adds to the pile.
            RequestDuck(GainFor(cueId));
        }

        /// <summary>
        /// Picks the voice least costly to interrupt.
        /// </summary>
        /// <remarks>
        /// The previous strict round-robin stole the next voice in sequence whether or not it was
        /// still playing, so anything long was cut off mid-tail by whatever came next: the roar runs
        /// 1.4 seconds and a posture break 0.9, while footsteps from two characters alone can turn
        /// the pool over in far less than that. An idle voice is always taken first; when they are
        /// all busy the one nearest its end loses the least.
        /// </remarks>
        private AudioSource ClaimVoice()
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                int index = (_nextVoice + i) % _voices.Length;

                if (!_voices[index].isPlaying)
                {
                    _nextVoice = (index + 1) % _voices.Length;
                    return _voices[index];
                }
            }

            int bestIndex = 0;
            float leastRemaining = float.MaxValue;

            for (int i = 0; i < _voices.Length; i++)
            {
                AudioSource candidate = _voices[i];
                float length = candidate.clip != null ? candidate.clip.length : 0f;
                float remaining = length - candidate.time;

                if (remaining < leastRemaining)
                {
                    leastRemaining = remaining;
                    bestIndex = i;
                }
            }

            _nextVoice = (bestIndex + 1) % _voices.Length;
            return _voices[bestIndex];
        }

        /// <summary>
        /// Swaps recorded clips in over the synthesised ones.
        /// </summary>
        /// <remarks>
        /// Runs after generation so an override always wins, and warns rather than failing on a cue
        /// id that matches nothing — a typo in an identifier would otherwise be a sound that silently
        /// never plays, which is the hardest kind of audio bug to notice.
        /// </remarks>
        private void ApplyCueOverrides()
        {
            if (_cueOverrides == null)
            {
                return;
            }

            foreach (CueOverride replacement in _cueOverrides)
            {
                if (replacement.Clip == null || string.IsNullOrWhiteSpace(replacement.CueId))
                {
                    continue;
                }

                if (!_clips.ContainsKey(replacement.CueId))
                {
                    Debug.LogWarning(
                        $"[Adaptive Boss Arena] Audio override names '{replacement.CueId}', which is " +
                        "not a known cue. It will never play. Check the identifiers in " +
                        "AudioService.Cues.",
                        this);
                    continue;
                }

                _clips[replacement.CueId] = replacement.Clip;
            }
        }

        /// <summary>Relative loudness for a cue, defaulting to unity when none is listed.</summary>
        private float GainFor(string cueId) =>
            CueGains.TryGetValue(cueId, out float gain) ? gain : 1f;

        /// <summary>Deepens the current duck if this sound is loud enough to warrant one.</summary>
        private void RequestDuck(float gain)
        {
            if (gain < DuckThresholdGain)
            {
                return;
            }

            _duckRemaining = DuckSeconds;
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

                // Logarithmic, and scaled to the arena. The previous setup rolled off linearly from
                // one metre to forty across a sixteen-metre floor, so a sound five metres away was at
                // ninety per cent and one across the arena at seventy-five: nothing ever read as near
                // or far. Full volume out to the usual fighting distance, then a real falloff.
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = NearFieldMetres;
                source.maxDistance = ArenaAudibleMetres;

                // Nothing here moves fast enough for Doppler to be anything but an unwanted pitch
                // wobble on footsteps and swings, layered on top of the deliberate jitter.
                source.dopplerLevel = 0f;

                // Effects deliberately ignore the time scale, so impacts still sound during the
                // hit-stop they cause rather than being pitched down into a groan.
                source.ignoreListenerPause = true;

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
            // Impacts are layered rather than filtered noise: a crack for the contact and a
            // pitch-dropping body for the mass behind it. The peaks are the mix — a heavy blow is
            // louder than a light one because it is told to be, not because of its filter.
            _clips[Cues.HitLight] =
                ToneGenerator.CreateWeightedImpact("hit.light", 150f, 2400f, 0.22f, 0.62f, 0.4f, seed: 3);

            _clips[Cues.HitHeavy] =
                ToneGenerator.CreateWeightedImpact("hit.heavy", 68f, 1000f, 0.42f, 0.92f, 0.6f, seed: 5);

            _clips[Cues.Block] =
                ToneGenerator.CreateWeightedImpact("guard.block", 115f, 700f, 0.26f, 0.58f, 0.35f, seed: 11);

            // Per-weapon swings. One moveset, three silhouettes of sound: the greatsword is slow and
            // airy, the blade neutral, the energy blade thin and quick.
            _clips[Cues.Whoosh] = ToneGenerator.CreateWhoosh("swing.whoosh");
            _clips[Cues.SwingBlade] = ToneGenerator.CreateWhoosh("swing.blade", 0.28f, 7, 0.42f, 1f);
            _clips[Cues.SwingGreatsword] =
                ToneGenerator.CreateWhoosh("swing.greatsword", 0.46f, 13, 0.5f, 0.45f);
            _clips[Cues.SwingEnergy] =
                ToneGenerator.CreateWhoosh("swing.energy", 0.2f, 17, 0.38f, 2.1f);

            // The deflect is the highest, brightest sound in the game on purpose: it is the moment
            // the player most needs to know they got it exactly right.
            _clips[Cues.Deflect] = ToneGenerator.CreateMetallicRing("guard.deflect", 1180f, 0.45f, 0.85f);
            _clips[Cues.PostureBreak] = ToneGenerator.CreateMetallicRing("guard.break", 320f, 0.9f, 0.9f);
            _clips[Cues.PerfectDodge] = ToneGenerator.CreateMetallicRing("dodge.perfect", 1560f, 0.3f, 0.72f);

            _clips[Cues.BossRoar] = ToneGenerator.CreateRoar("boss.roar", peak: 1f);

            // The fight's two endings. Neither had a sound at all, so the single most important
            // moment in the encounter passed in silence. The boss's knell is lower and longer,
            // because it is the larger thing falling.
            _clips[Cues.PlayerDeath] = ToneGenerator.CreateDeathKnell("player.death", 104f, 1.9f, 0.95f);
            _clips[Cues.BossDeath] = ToneGenerator.CreateDeathKnell("boss.death", 68f, 2.6f, 1f);

            // A shorter, breathier whoosh than a swing, so a dash reads as the player moving rather
            // than attacking.
            _clips[Cues.Dash] = ToneGenerator.CreateWhoosh("dodge.whoosh", 0.2f, seed: 21);

            // Warm and rising for the heal; a bright, short chime the instant focus fills, and a
            // softer one when the heal actually completes rather than being interrupted.
            _clips[Cues.Heal] = ToneGenerator.CreateShimmer("player.heal", 520f, 0.7f, 0.55f);
            _clips[Cues.HealComplete] = ToneGenerator.CreateShimmer("player.healed", 700f, 0.5f, 0.5f);
            _clips[Cues.FocusFull] = ToneGenerator.CreateShimmer("focus.full", 1040f, 0.4f, 0.68f);

            // A short high metallic 'shing' for drawing a weapon.
            _clips[Cues.WeaponDraw] = ToneGenerator.CreateMetallicRing("weapon.draw", 1500f, 0.2f, 0.5f);

            // The execution is the heaviest, lowest blow the player can land — the payoff of a
            // broken guard, so it hits harder than anything else they have.
            _clips[Cues.Execution] =
                ToneGenerator.CreateWeightedImpact("player.execution", 52f, 620f, 0.6f, 1f, 0.8f, seed: 29);

            // Footsteps: a soft tap for the player, a heavier and much lower thud for the boss. Both
            // sit well back in the mix — they play constantly and must never compete with a hit.
            _clips[Cues.FootstepPlayer] =
                ToneGenerator.CreateImpact("step.player", 0.08f, 520f, seed: 31, peak: 0.26f);

            _clips[Cues.FootstepBoss] =
                ToneGenerator.CreateWeightedImpact("step.boss", 58f, 300f, 0.22f, 0.44f, 0.3f, seed: 37);

            // A low, broadband rumble for a hazard erupting from the ground.
            _clips[Cues.Hazard] =
                ToneGenerator.CreateWeightedImpact("hazard.erupt", 44f, 380f, 0.62f, 0.8f, 0.75f, seed: 41);

            // A short, soft shift for raising a guard.
            _clips[Cues.GuardRaise] =
                ToneGenerator.CreateImpact("guard.raise", 0.09f, 900f, seed: 47, peak: 0.34f);

            // The heartbeat is the clearest case of the old bug: at 85 Hz the filter left it at four
            // per cent of full scale, so the game's low-health warning was effectively silent.
            _clips[Cues.Heartbeat] =
                ToneGenerator.CreateWeightedImpact("player.heartbeat", 46f, 140f, 0.3f, 0.7f, 0.25f, seed: 43);

            // The perilous-attack warning: a tense tritone sting under a fast tremolo.
            _clips[Cues.Peril] = ToneGenerator.CreatePerilWarning("peril.warning", peak: 0.9f);

            // The boss overbalancing: an unsteady low lurch whose pitch sags as it tips.
            _clips[Cues.Overbalance] = ToneGenerator.CreateStumble("boss.overbalance", peak: 0.78f);

            // A swing that hits nothing: air, and no contact at all. Quiet, but its absence made a
            // whiffed attack feel like the game had missed the input rather than the target.
            _clips[Cues.Whiff] = ToneGenerator.CreateWhoosh("swing.whiff", 0.34f, 53, 0.3f, 0.7f);

            // Menu feedback, on the interface bus that existed and was never used by anything.
            _clips[Cues.UiClick] = ToneGenerator.CreateMetallicRing("ui.click", 1320f, 0.12f, 0.4f);
            _clips[Cues.UiHover] = ToneGenerator.CreateMetallicRing("ui.hover", 1760f, 0.07f, 0.22f);

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
