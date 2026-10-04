using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Decides how the licensed Mixamo characters and animations import.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The files are downloaded by whoever sets the project up and are not in the repository, so their
    /// import settings must come from code: a .meta written by hand would not exist on the next machine.
    /// </para>
    /// <para>
    /// Characters import as Humanoid with their own avatar. Every animation file copies its avatar from the
    /// character it was made for - the knight's sword-and-shield set and the shared clips from the Paladin,
    /// the boss's great sword set from the Warrok - so a clip drives the skeleton it was authored on. Clips
    /// are renamed after their file, because Mixamo names every take "mixamo.com", and their root motion is
    /// baked into the pose: the motors move characters, never a clip.
    /// </para>
    /// <para>
    /// Unity imports <c>Animations</c> before <c>Characters</c>, so on a fresh import an animation's source
    /// avatar does not exist yet. When a character finishes importing, the animations still without an
    /// avatar are imported again, which is the one ordering that always ends correct.
    /// </para>
    /// </remarks>
    public sealed class LicensedArtPostprocessor : AssetPostprocessor
    {
        /// <summary>Root of the licensed art.</summary>
        public const string MixamoFolder = "Assets/_Project/Art/Licensed/Mixamo/";

        /// <summary>Folder of the animation files.</summary>
        public const string AnimationFolder = MixamoFolder + "Animations";

        /// <summary>The knight's character file.</summary>
        public const string KnightCharacter = MixamoFolder + "Characters/Paladin J Nordstrom.fbx";

        /// <summary>
        /// The skeleton the boss's animations were authored on, and its body when the corrupted knight is absent.
        /// </summary>
        /// <remarks>
        /// Kept even once the corrupted knight is the body. A clip is read through the skeleton it was made for, and
        /// every boss clip is a Mixamo file; read through the knight's Biped skeleton they would not import at all.
        /// Imported as humanoid motion, they then play on any humanoid body.
        /// </remarks>
        public const string BossCharacter = MixamoFolder + "Characters/Warrok W Kurniawan.fbx";

        /// <summary>The corrupted knight the boss now is, when this checkout has it.</summary>
        /// <remarks>
        /// A free Sketchfab model rigged with a 3ds Max Biped skeleton, which Unity's humanoid mapping reads bone for
        /// bone - so it needs no auto-rigging, which refused it for already having a skeleton.
        /// </remarks>
        public const string CorruptedKnightBody = MixamoFolder + "Characters/Shadow_Igris_Boss.fbx";

        /// <summary>
        /// Import scale for the corrupted knight. Its file declares centimetres but was authored a hundredth too
        /// small besides; at 1 it stood two centimetres tall.
        /// </summary>
        private const float CorruptedKnightImportScale = 100f;

        /// <summary>
        /// Scale that stands the corrupted knight half as tall again as the knight: its head at 1.98 m as imported,
        /// the knight's at 1.54 m, so 1.54 x 1.5 / 1.98.
        /// </summary>
        private const float CorruptedKnightScale = 1.17f;

        /// <summary>The brute's scale, a head taller than the knight.</summary>
        private const float BruteScale = 1.05f;

        /// <summary>The boss's body: the corrupted knight when present, else the brute.</summary>
        public static string BossBody => File.Exists(CorruptedKnightBody) ? CorruptedKnightBody : BossCharacter;

        /// <summary>The scale that sizes <see cref="BossBody"/>.</summary>
        public static float BossBodyScale => File.Exists(CorruptedKnightBody) ? CorruptedKnightScale : BruteScale;

        /// <summary>Folder the corrupted knight's own maps were unpacked to.</summary>
        private const string CorruptedKnightMaps = "Assets/_Project/Art/Licensed/Igris/";

        private static readonly string[] LoopingWords = { "idle", "walk", "run", "strafe" };

        /// <summary>Whether the licensed characters are present in this checkout.</summary>
        public static bool Available => File.Exists(KnightCharacter) && File.Exists(BossCharacter);

        /// <summary>Bumped whenever the import rules change, so files already imported pick them up.</summary>
        /// <returns>The version of these import rules.</returns>
        public override uint GetVersion() => 5;

        /// <summary>
        /// A clip's name: its file name, with the character Mixamo previewed it on removed.
        /// </summary>
        /// <remarks>
        /// Mixamo names a download either "walk.fbx" or "Warrok W Kurniawan@Mutant Roaring.fbx" depending on how
        /// it was fetched, and which one a clip arrives as is not worth encoding in every clip table. The
        /// character in that prefix is the preview body, not the skeleton the clip is retargeted onto here.
        /// </remarks>
        /// <param name="assetPath">The animation file's asset path.</param>
        /// <returns>The clip's name.</returns>
        public static string ClipNameFor(string assetPath)
        {
            string name = Path.GetFileNameWithoutExtension(assetPath);
            int character = name.LastIndexOf('@');

            return character >= 0 && character + 1 < name.Length ? name.Substring(character + 1) : name;
        }

        /// <summary>The character whose skeleton an animation file drives.</summary>
        /// <param name="animationPath">The animation file's asset path.</param>
        /// <returns>The character file's asset path.</returns>
        public static string CharacterFor(string animationPath) =>
            animationPath.Replace('\\', '/').Contains("/Animations/Boss/") ? BossCharacter : KnightCharacter;

        /// <summary>Whether a clip loops, from Mixamo's naming.</summary>
        /// <remarks>
        /// Idles, walks, runs and strafes loop, including a held guard ("block idle"). Turns, attacks, hits,
        /// falls and deaths play once. Matched on whole words, so "180 turn" is not mistaken for a loop.
        /// </remarks>
        /// <param name="clipName">The clip's name, which is its file name.</param>
        /// <returns>True for a looping clip.</returns>
        public static bool IsLooping(string clipName)
        {
            string[] words = clipName.ToLowerInvariant().Split(' ', '(', ')');

            // Matched on the start of a word, so "Mutant Walking" and "Mutant Run" loop alongside "great sword
            // walk", while "180 turn" and "Sprinting Forward Roll" still play once.
            return words.Any(word => LoopingWords.Any(word.StartsWith));
        }

        private bool IsLicensed =>
            assetPath.StartsWith(MixamoFolder) && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        private bool IsCharacter => assetPath.Contains("/Characters/");

        private void OnPreprocessModel()
        {
            if (!IsLicensed)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;

            if (IsCharacter)
            {
                if (assetPath == CorruptedKnightBody)
                {
                    importer.globalScale = CorruptedKnightImportScale;
                }

                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.importAnimation = false;
                return;
            }

            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;

            // Null on the first import of a fresh checkout; OnPostprocessAllAssets imports this file again
            // once the character exists.
            importer.sourceAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(CharacterFor(assetPath));
        }

        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(CorruptedKnightMaps))
            {
                PrepareCorruptedKnightMap((TextureImporter)assetImporter);
                return;
            }

            if (!assetPath.StartsWith(MixamoFolder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 2048;

            // A normal map imported as colour lights the armour as if every dent faced the wrong way.
            if (assetPath.Contains("_normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
        }

        /// <summary>
        /// Makes the corrupted knight's maps readable, so its material can be generated from them, and imports each
        /// as what it is: the normal map as one, the packed mask as data rather than colour.
        /// </summary>
        private void PrepareCorruptedKnightMap(TextureImporter importer)
        {
            importer.isReadable = true;
            importer.maxTextureSize = 2048;

            if (assetPath.Contains("_NM"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (assetPath.Contains("_DT"))
            {
                importer.sRGBTexture = false;
            }
        }

        private void OnPreprocessAnimation()
        {
            if (!IsLicensed || IsCharacter)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            string name = ClipNameFor(assetPath);
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;

            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = name;
                clip.loopTime = IsLooping(name);

                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;

                // Travel across the floor is extracted as root motion, which the game discards, rather than baked
                // into the pose. Baked, a slide or a charge carried the body metres away from the root the motor
                // moves and the hurtbox rides on - it lunged twice, drifted off its own hitbox, and snapped back
                // when the clip ended. Extracted, every clip plays over its root, and the motor alone moves it.
                // The locomotion clips are in place already, so they are unchanged.
                clip.lockRootPositionXZ = false;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clips;
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!importedAssets.Any(path => path == KnightCharacter || path == BossCharacter) ||
                !Directory.Exists(AnimationFolder))
            {
                return;
            }

            foreach (string animation in Directory.GetFiles(AnimationFolder, "*.fbx", SearchOption.AllDirectories))
            {
                string path = animation.Replace('\\', '/');

                if (AssetImporter.GetAtPath(path) is ModelImporter importer && importer.sourceAvatar == null)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
        }
    }
}
