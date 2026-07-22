using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Resolves the assets the generators produce, for the generators that consume them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lookups load by path first and only fall back to searching. That ordering exists because of a
    /// real failure: when the whole setup chain runs in one batch invocation, assets created moments
    /// earlier are on disk but not yet in the asset database's search index, so
    /// <see cref="AssetDatabase.FindAssets(string)"/> with a name filter silently returns nothing.
    /// The prefabs were then generated with every event channel reference left null, and the only
    /// symptom was a warning in a log nobody reads.
    /// </para>
    /// <para>
    /// Since the generators put their output at known paths, a direct load is both faster and
    /// immune to that problem. The search is kept purely as a fallback for assets someone has moved
    /// by hand.
    /// </para>
    /// </remarks>
    internal static class GeneratedAssets
    {
        /// <summary>Folder the event channel assets are generated into.</summary>
        public const string EventFolder = EditorMenus.GeneratedAssetFolder + "/Events";

        /// <summary>Folder the configuration assets are generated into.</summary>
        public const string ConfigFolder = EditorMenus.GeneratedAssetFolder + "/Config";

        /// <summary>Loads a generated event channel by name.</summary>
        /// <typeparam name="TChannel">Channel type to load.</typeparam>
        /// <param name="assetName">File name without extension.</param>
        /// <returns>The channel, or null with a warning logged.</returns>
        public static TChannel EventChannel<TChannel>(string assetName) where TChannel : Object =>
            LoadByPathOrSearch<TChannel>($"{EventFolder}/{assetName}.asset", assetName);

        /// <summary>
        /// Loads the single asset of a configuration type.
        /// </summary>
        /// <typeparam name="TAsset">Configuration type to load.</typeparam>
        /// <param name="preferredName">
        /// File name the generator uses, tried first so that a project containing several assets of
        /// the type still resolves deterministically.
        /// </param>
        /// <returns>The asset, or null with a warning logged.</returns>
        public static TAsset Config<TAsset>(string preferredName) where TAsset : Object =>
            LoadByPathOrSearch<TAsset>($"{ConfigFolder}/{preferredName}.asset", preferredName);

        /// <summary>Loads from an exact path, forcing an import if needed, then falling back to a search.</summary>
        private static TAsset LoadByPathOrSearch<TAsset>(string expectedPath, string assetName)
            where TAsset : Object
        {
            var direct = AssetDatabase.LoadAssetAtPath<TAsset>(expectedPath);

            if (direct != null)
            {
                return direct;
            }

            direct = ForceImportAndLoad<TAsset>(expectedPath);

            if (direct != null)
            {
                return direct;
            }

            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(TAsset).Name}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var candidate = AssetDatabase.LoadAssetAtPath<TAsset>(path);

                if (candidate != null && candidate.name == assetName)
                {
                    return candidate;
                }
            }

            Debug.LogWarning(
                $"[Adaptive Boss Arena] Could not resolve {typeof(TAsset).Name} '{assetName}'. " +
                $"Expected it at '{expectedPath}'. Run 'Generate Default Assets' and regenerate.");

            return null;
        }

        /// <summary>
        /// Imports an asset synchronously and loads it, for files that exist but are not yet known
        /// to the asset database.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the whole setup chain runs inside one <c>-executeMethod</c> invocation, Unity defers
        /// imports until the batch finishes. An asset written moments earlier is therefore on disk
        /// but absent from the database, and even a direct path load returns null.
        /// </para>
        /// <para>
        /// The consequence was worse than a failed lookup. The generators call this through
        /// <c>CreateOrLoad</c>, so a miss did not merely return nothing — it caused a brand new
        /// asset to be written over the existing one, silently discarding any values that had been
        /// tuned by hand.
        /// </para>
        /// </remarks>
        /// <typeparam name="TAsset">Asset type to load.</typeparam>
        /// <param name="assetPath">Project-relative path.</param>
        /// <returns>The asset, or null when the file genuinely does not exist.</returns>
        public static TAsset ForceImportAndLoad<TAsset>(string assetPath) where TAsset : Object
        {
            // Unity's working directory is the project root, so the project-relative path resolves
            // directly for a filesystem check.
            if (!File.Exists(assetPath))
            {
                return null;
            }

            AssetDatabase.ImportAsset(
                assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            return AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
        }
    }
}
