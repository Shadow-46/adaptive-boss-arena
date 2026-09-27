using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AdaptiveBossArena.Game;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Points each sound cue at the recorded files that should play for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every sound used to be synthesised from tones and noise, and the player heard them as random and
    /// inaccurate. The recordings live under <see cref="SoundFolder"/>, downloaded from free (CC0) sources: the
    /// Kenney impact and RPG packs, and individual Freesound clips kept under their Freesound names, which begin
    /// with the sound's id.
    /// </para>
    /// <para>
    /// A cue is matched by file name, so each one names the moment it belongs to rather than a path that moves
    /// when a pack is unzipped differently. Several matching files become several takes. A cue with no matching
    /// file keeps its synthesised sound, so the game sounds complete before every download is in.
    /// </para>
    /// </remarks>
    public static class AudioAssetBuilder
    {
        /// <summary>Where the downloaded sounds are kept.</summary>
        public const string SoundFolder = "Assets/_Project/Audio/ThirdParty";

        private static readonly string[] SoundExtensions = { ".ogg", ".wav", ".mp3", ".aif", ".aiff", ".flac" };

        /// <summary>
        /// Which recordings play for each cue, and at what pitch.
        /// </summary>
        /// <remarks>
        /// The knight's shield is painted wood, so a block is wood; a deflect rings off the blade as metal; a parry
        /// is the brighter, bell-like clang, so the two are never confused. The brute's footfalls reuse the heavy
        /// body thud a good deal lower, for the mass.
        /// </remarks>
        public static readonly (string Cue, string Pattern, float Pitch)[] Mapping =
        {
            (AudioService.Cues.Block, @"^impactWood_heavy_\d+$", 1f),
            (AudioService.Cues.Deflect, @"^impactMetal_heavy_\d+$", 1f),
            (AudioService.Cues.Parry, @"^impactBell_heavy_\d+$", 1f),
            (AudioService.Cues.HitLight, @"^impactPunch_medium_\d+$", 1f),
            (AudioService.Cues.HitHeavy, @"^impactPunch_heavy_\d+$", 0.9f),
            (AudioService.Cues.PostureBreak, @"^impactPlate_heavy_\d+$", 0.85f),
            (AudioService.Cues.FootstepPlayer, @"^footstep_concrete_\d+$", 1f),
            (AudioService.Cues.FootstepBoss, @"^impactSoft_heavy_\d+$", 0.7f),
            (AudioService.Cues.WeaponDraw, @"^drawKnife\d*$", 1f),
            (AudioService.Cues.Execution, @"^knifeSlice\d*$", 0.9f),
            (AudioService.Cues.GuardRaise, @"^cloth\d+$", 1f),
            (AudioService.Cues.PlayerDeath, @"^impactSoft_heavy_\d+$", 0.85f),

            // Freesound, matched by id: picked up as soon as they are downloaded.
            (AudioService.Cues.BossRoar, @"^(489901|132874)__", 1f),
            (AudioService.Cues.BossDeath, @"^497056__", 0.9f),
            (AudioService.Cues.SwingGreatsword, @"^507470__", 1f),
            (AudioService.Cues.SwingBlade, @"^(317849|471097)__", 1f),
            (AudioService.Cues.Whoosh, @"^(317849|471097)__", 1.1f)
        };

        /// <summary>Every sound file in the downloads folder, by file name without extension.</summary>
        private static List<(string Name, string Path)> Sounds()
        {
            if (!Directory.Exists(SoundFolder))
            {
                return new List<(string, string)>();
            }

            return Directory.GetFiles(SoundFolder, "*.*", SearchOption.AllDirectories)
                .Where(path => SoundExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                .Select(path => (Path.GetFileNameWithoutExtension(path), path.Replace('\\', '/')))
                .OrderBy(sound => sound.Item1)
                .ToList();
        }

        /// <summary>The recordings found for one cue, in name order.</summary>
        /// <param name="pattern">The file-name pattern the cue plays.</param>
        /// <returns>The clips.</returns>
        public static AudioClip[] ClipsMatching(string pattern)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);

            return Sounds()
                .Where(sound => regex.IsMatch(sound.Name))
                .Select(sound => AssetDatabase.LoadAssetAtPath<AudioClip>(sound.Path))
                .Where(clip => clip != null)
                .ToArray();
        }

        /// <summary>Writes the recordings found onto an audio service's cue overrides.</summary>
        /// <param name="audio">The scene's audio service.</param>
        /// <returns>How many cues now play recordings.</returns>
        public static int Assign(AudioService audio)
        {
            var found = new List<(string Cue, AudioClip[] Clips, float Pitch)>();

            foreach ((string cue, string pattern, float pitch) in Mapping)
            {
                AudioClip[] clips = ClipsMatching(pattern);

                if (clips.Length > 0)
                {
                    found.Add((cue, clips, pitch));
                }
            }

            var serialized = new SerializedObject(audio);
            SerializedProperty overrides = serialized.FindProperty("_cueOverrides");
            overrides.arraySize = found.Count;

            for (int i = 0; i < found.Count; i++)
            {
                SerializedProperty entry = overrides.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("CueId").stringValue = found[i].Cue;
                entry.FindPropertyRelative("Clip").objectReferenceValue = found[i].Clips[0];
                entry.FindPropertyRelative("Pitch").floatValue = found[i].Pitch;

                SerializedProperty takes = entry.FindPropertyRelative("Clips");
                takes.arraySize = found[i].Clips.Length;

                for (int t = 0; t < found[i].Clips.Length; t++)
                {
                    takes.GetArrayElementAtIndex(t).objectReferenceValue = found[i].Clips[t];
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Adaptive Boss Arena] {found.Count} sound cues play recordings from {SoundFolder}.");

            return found.Count;
        }
    }
}
