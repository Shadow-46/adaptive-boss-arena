using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

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
        private const string WindowsFolder = "Build/Windows";
        private const string WebGLOutput = "Build/WebGL";
        private const string ReleaseFolder = "Build/Release";

        /// <summary>Builds the 64-bit Windows standalone player.</summary>
        [MenuItem(EditorMenus.Setup + "Build Windows (x64)")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, WindowsOutput);

        /// <summary>
        /// Builds the Windows player and packs it into a zip ready to attach to a release.
        /// </summary>
        /// <remarks>
        /// Named after the commit it was built from, so a downloaded zip can always be traced back to
        /// the exact code it contains. Publishing the zip is deliberately not done here: a release is
        /// an outward-facing act and is taken separately.
        /// </remarks>
        [MenuItem(EditorMenus.Setup + "Build Windows Release Zip")]
        public static void BuildWindowsRelease()
        {
            if (!Build(BuildTarget.StandaloneWindows64, WindowsOutput))
            {
                return;
            }

            Directory.CreateDirectory(ReleaseFolder);

            string zipPath = Path.Combine(ReleaseFolder, $"AdaptiveBossArena-{ShortCommit()}-win64.zip");

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(
                WindowsFolder, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);

            Debug.Log(
                $"[Build] Windows release packed -> {zipPath} ({new FileInfo(zipPath).Length / (1024 * 1024)} MB).");
        }

        /// <summary>
        /// Explains why a target's default quality level would render with the wrong tier, or null.
        /// </summary>
        /// <remarks>
        /// A build that silently started in the other tier would either ship ambient occlusion to a
        /// browser, which crashed it on load, or ship the desktop build with none of the graphics it
        /// exists for. Both failures are invisible until someone runs the result, so the build refuses
        /// to start instead.
        /// </remarks>
        /// <param name="target">The build target.</param>
        /// <returns>A description of the problem, or null when the tier is correct.</returns>
        public static string TierProblemFor(BuildTarget target)
        {
            bool isWeb = target == BuildTarget.WebGL;
            string platform = isWeb
                ? RenderPipelineConfigurator.WebGLPlatform
                : RenderPipelineConfigurator.StandalonePlatform;

            string expectedPipeline = isWeb
                ? RenderPipelineConfigurator.PipelineAssetPath
                : RenderPipelineConfigurator.DesktopPipelineAssetPath;

            SerializedObject quality = RenderPipelineConfigurator.LoadQualitySettings();
            SerializedProperty levels = quality?.FindProperty("m_QualitySettings");
            SerializedProperty defaults = quality?.FindProperty("m_PerPlatformDefaultQuality");

            if (levels == null || defaults == null)
            {
                return "quality settings could not be read";
            }

            int defaultLevel = -1;

            for (int i = 0; i < defaults.arraySize; i++)
            {
                SerializedProperty pair = defaults.GetArrayElementAtIndex(i);

                if (pair.FindPropertyRelative("first").stringValue == platform)
                {
                    defaultLevel = pair.FindPropertyRelative("second").intValue;
                }
            }

            if (defaultLevel < 0 || defaultLevel >= levels.arraySize)
            {
                return $"{platform} has no valid default quality level";
            }

            SerializedProperty level = levels.GetArrayElementAtIndex(defaultLevel);
            Object pipeline = level.FindPropertyRelative("customRenderPipeline").objectReferenceValue;
            string pipelinePath = pipeline != null ? AssetDatabase.GetAssetPath(pipeline) : "(none)";

            if (pipelinePath != expectedPipeline)
            {
                string levelName = level.FindPropertyRelative("name").stringValue;

                return $"{platform} starts on quality level {levelName}, which renders with {pipelinePath} " +
                       $"instead of {expectedPipeline}";
            }

            return ShippedPipelineProblem(platform, expectedPipeline);
        }

        /// <summary>
        /// Checks the pipeline assets the engine will actually ship for a platform.
        /// </summary>
        /// <remarks>
        /// The default-level check alone passed on two builds that shipped the wrong tier, because the
        /// settings were right and the engine still packed the other platform's assets. This asks the
        /// engine the question it answers when it builds, so a wrong-tier asset about to ship is caught
        /// before the build rather than discovered in a browser.
        /// </remarks>
        private static string ShippedPipelineProblem(string platform, string expectedPipeline)
        {
            var shipped = new System.Collections.Generic.List<UnityEngine.Rendering.RenderPipelineAsset>();
            QualitySettings.GetAllRenderPipelineAssetsForPlatform(platform, ref shipped);

            foreach (UnityEngine.Rendering.RenderPipelineAsset asset in shipped)
            {
                string path = asset != null ? AssetDatabase.GetAssetPath(asset) : "(none)";

                if (path != expectedPipeline)
                {
                    return $"a {platform} build would ship {path}, which belongs to the other render tier";
                }
            }

            return shipped.Count == 0 ? $"a {platform} build would ship no render pipeline at all" : null;
        }

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
        /// <returns>True when the build succeeded.</returns>
        private static bool Build(BuildTarget target, string outputPath)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Fail("No enabled scenes in Build Settings — run 'Run Full Setup' first.");
                return false;
            }

            // Switched before anything else, not left to BuildPlayer. The render pipeline decides
            // which pipeline assets ship from the editor's active platform before BuildPlayer gets
            // round to switching it, so a WebGL build started from a Windows-active editor shipped
            // the desktop tier - ambient occlusion and all - and the next Windows build, started from
            // WebGL, shipped the web tier. Every build carried the previous platform's graphics.
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);

            if (EditorUserBuildSettings.activeBuildTarget != target &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
            {
                Fail($"Could not switch the active platform to {target} before building.");
                return false;
            }

            string tierProblem = TierProblemFor(target);

            if (tierProblem != null)
            {
                Fail($"Refusing to build {target}: {tierProblem}. Run 'Run Full Setup' first.");
                return false;
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
                return false;
            }

            Debug.Log(
                $"[Build] {target} succeeded → '{outputPath}' " +
                $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0}s).");

            return true;
        }

        /// <summary>The current commit's short hash, or "local" when git is unavailable.</summary>
        private static string ShortCommit()
        {
            try
            {
                using Process git = Process.Start(new ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (git == null)
                {
                    return "local";
                }

                string hash = git.StandardOutput.ReadToEnd().Trim();
                git.WaitForExit();

                return string.IsNullOrEmpty(hash) ? "local" : hash;
            }
            catch (Exception)
            {
                return "local";
            }
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
