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

        /// <summary>The boss's character file.</summary>
        public const string BossCharacter = MixamoFolder + "Characters/Warrok W Kurniawan.fbx";

        private static readonly string[] LoopingWords = { "idle", "walk", "run", "strafe" };

        /// <summary>Whether the licensed characters are present in this checkout.</summary>
        public static bool Available => File.Exists(KnightCharacter) && File.Exists(BossCharacter);

        /// <summary>Bumped whenever the import rules change, so files already imported pick them up.</summary>
        /// <returns>The version of these import rules.</returns>
        public override uint GetVersion() => 1;

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
            return LoopingWords.Any(words.Contains);
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

        private void OnPreprocessAnimation()
        {
            if (!IsLicensed || IsCharacter)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;

            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = name;
                clip.loopTime = IsLooping(name);

                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
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
