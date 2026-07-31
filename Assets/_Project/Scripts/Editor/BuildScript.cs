using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Headless player builds for the standalone and browser demos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both targets build from the same scene list — whatever is enabled in Build Settings, which the
    /// menu-scene generator already pins to title-first, arena-second — so the two artefacts can never
    /// disagree about which scenes ship. Driven from the command line with
    /// <c>-executeMethod AdaptiveBossArena.Editor.BuildScript.BuildWindows</c> (or <c>BuildWebGL</c>).
    /// </para>
    /// <para>
    /// The WebGL settings are chosen for GitHub Pages specifically: Pages serves static files without
    /// a <c>Content-Encoding</c> header, so a gzip-compressed build only loads if Unity's JavaScript
    /// decompression fallback is enabled. With it on, the build is small and still loads from a plain
    /// static host.
    /// </para>
    /// </remarks>
    public static class BuildScript
    {
        private const string WindowsOutput = "Build/Windows/AdaptiveBossArena.exe";
        private const string WebGLOutput = "Build/WebGL";

        /// <summary>Builds the 64-bit Windows standalone player.</summary>
        [MenuItem(EditorMenus.Setup + "Build Windows (x64)")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, WindowsOutput);

        /// <summary>Builds the WebGL player, configured to run from GitHub Pages.</summary>
        [MenuItem(EditorMenus.Setup + "Build WebGL")]
        public static void BuildWebGL()
        {
            // Gzip plus the JS decompression fallback keeps the download small while still loading
            // from a static host that sends no Content-Encoding header.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            // The demo should keep simulating when the tab loses focus mid-fight rather than freezing.
            PlayerSettings.runInBackground = true;

            Build(BuildTarget.WebGL, WebGLOutput);
        }

        /// <summary>Runs a player build and fails the process loudly when it does not succeed.</summary>
        private static void Build(BuildTarget target, string outputPath)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Fail("No enabled scenes in Build Settings — run 'Run Full Setup' first.");
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"{target} build {summary.result} with {summary.totalErrors} error(s).");
                return;
            }

            Debug.Log(
                $"[Build] {target} succeeded → '{outputPath}' " +
                $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0}s).");
        }

        /// <summary>Logs the failure and, in batch mode, exits non-zero so CI notices.</summary>
        private static void Fail(string message)
        {
            Debug.LogError($"[Build] {message}");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
