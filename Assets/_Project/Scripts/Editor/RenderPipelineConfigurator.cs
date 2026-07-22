using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Creates the Universal Render Pipeline assets and makes them the active pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A project created from Unity's own URP template arrives with these assets already made and
    /// assigned. This project's structure is generated rather than templated, so nothing had ever
    /// created them, and the symptom was severe and completely non-obvious: the package was
    /// installed and the materials correctly used <c>Universal Render Pipeline/Lit</c>, but with no
    /// pipeline assigned Unity fell back to the built-in renderer, which cannot compile those
    /// shaders. Every surface in the arena rendered solid magenta.
    /// </para>
    /// <para>
    /// Worth noting that neither compilation nor the test suite catches this. It is only visible by
    /// looking at the game.
    /// </para>
    /// <para>
    /// Safe to run repeatedly; existing assets are reused rather than replaced, so quality settings
    /// tuned by hand survive.
    /// </para>
    /// </remarks>
    public static class RenderPipelineConfigurator
    {
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const string PipelineAssetPath = SettingsFolder + "/UniversalRenderPipeline.asset";
        private const string RendererAssetPath = SettingsFolder + "/UniversalRenderer.asset";

        /// <summary>Creates the pipeline assets if absent and assigns them globally.</summary>
        [MenuItem(EditorMenus.Setup + "Configure Render Pipeline",
            priority = EditorMenus.SetupPriorityConfigureProject + 1)]
        public static void ConfigureRenderPipeline()
        {
            AssetAuthoring.EnsureFolderExists(SettingsFolder);

            UniversalRenderPipelineAsset pipeline = LoadOrCreatePipelineAsset();

            if (pipeline == null)
            {
                Debug.LogError(
                    "[Adaptive Boss Arena] Could not create the Universal Render Pipeline asset. " +
                    "Everything will render magenta until a pipeline is assigned.");
                return;
            }

            // Both assignments are needed. The graphics setting is the project-wide default; the
            // quality setting overrides it per quality level and, left empty, silently wins.
            GraphicsSettings.defaultRenderPipeline = pipeline;
            AssignToAllQualityLevels(pipeline);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[Adaptive Boss Arena] Universal Render Pipeline configured and assigned " +
                $"('{PipelineAssetPath}').");
        }

        /// <summary>Loads the pipeline asset, creating it and its renderer on first run.</summary>
        private static UniversalRenderPipelineAsset LoadOrCreatePipelineAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);

            if (existing != null)
            {
                return existing;
            }

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);

            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererAssetPath);
            }

            UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(rendererData);

            if (pipeline == null)
            {
                return null;
            }

            pipeline.name = Path.GetFileNameWithoutExtension(PipelineAssetPath);
            AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);

            return pipeline;
        }

        /// <summary>
        /// Assigns the pipeline to every quality level.
        /// </summary>
        /// <remarks>
        /// A quality level with no pipeline assigned falls back to the built-in renderer regardless
        /// of the project-wide default, so leaving even one unset produces a build that renders
        /// magenta only at that quality setting.
        /// </remarks>
        private static void AssignToAllQualityLevels(UniversalRenderPipelineAsset pipeline)
        {
            int originalLevel = QualitySettings.GetQualityLevel();
            string[] levels = QualitySettings.names;

            for (int i = 0; i < levels.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                QualitySettings.renderPipeline = pipeline;
            }

            QualitySettings.SetQualityLevel(originalLevel, applyExpensiveChanges: false);
        }
    }
}
