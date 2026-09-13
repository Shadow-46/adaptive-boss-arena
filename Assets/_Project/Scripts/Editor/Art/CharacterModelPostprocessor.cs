using UnityEditor;

namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Decides how third-party character models and their animations import.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Import settings normally live only in a model's .meta file, set by hand in the inspector. That
    /// breaks the rule this project runs on - everything is generated from code - and it is fragile in
    /// a specific way: delete or regenerate a .meta and the rig silently becomes Generic, clips stop
    /// looping, and root motion starts dragging characters around, with nothing in any test to notice.
    /// Setting the import here means reimporting always produces the same result.
    /// </para>
    /// <para>
    /// Scoped to the third-party art folder, so no other model in the project is affected.
    /// </para>
    /// </remarks>
    public sealed class CharacterModelPostprocessor : AssetPostprocessor
    {
        /// <summary>Folder whose models this postprocessor owns.</summary>
        public const string ThirdPartyFolder = "Assets/_Project/Art/ThirdParty/";

        /// <summary>
        /// Bumped whenever the import rules change.
        /// </summary>
        /// <remarks>
        /// Unity only reimports a model when something it tracks has changed, and a postprocessor's code
        /// is not one of those things. Without a version bump an edited rule applies to newly imported
        /// models only, while every model already in the project keeps its old import.
        /// </remarks>
        /// <returns>The version of these import rules.</returns>
        public override uint GetVersion() => 2;

        private bool IsThirdPartyModel => assetPath.StartsWith(ThirdPartyFolder) && assetPath.EndsWith(".fbx");

        private void OnPreprocessModel()
        {
            if (!IsThirdPartyModel)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;

            // Humanoid, so clips from one library drive a body from another, and so the game can find
            // the hand and chest bones by role rather than by the author's bone names.
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            // The embedded materials are placeholders; surfaces come from MaterialLibrary, which knows
            // the render pipeline and the art direction.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;

            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsThirdPartyModel)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;

            foreach (ModelImporterClipAnimation clip in clips)
            {
                // The exporter prefixes every take with its armature, and the two libraries use
                // different armature names. Stripped, one clip name means one thing everywhere, and
                // nothing downstream has to know which library a clip came from.
                clip.name = CleanClipName(clip.name);
                clip.loopTime = IsLoopingClip(clip.name);

                // Movement is the motors' job, never the clip's: hitbox reach and the boss's range logic
                // are authored against where the code moves a character. Every clip's root motion is
                // baked into the pose so a clip can never carry its character anywhere.
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clips;
        }

        /// <summary>Removes the exporter's armature prefix from a take name.</summary>
        /// <param name="rawName">Name as exported, such as <c>Rig|Walk_Loop</c>.</param>
        /// <returns>The clip's own name, such as <c>Walk_Loop</c>.</returns>
        public static string CleanClipName(string rawName)
        {
            int separator = rawName.LastIndexOf('|');

            return separator >= 0 ? rawName.Substring(separator + 1) : rawName;
        }

        /// <summary>Whether a clip should loop, from the library's own naming.</summary>
        /// <param name="clipName">The clip's name.</param>
        /// <returns>True for idles and locomotion.</returns>
        public static bool IsLoopingClip(string clipName) =>
            clipName.EndsWith("_Loop") || clipName == "Sword_Idle";
    }
}
