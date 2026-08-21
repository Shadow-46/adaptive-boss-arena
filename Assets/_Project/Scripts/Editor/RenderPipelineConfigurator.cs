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

        /// <summary>Shadow range, pulled in from the default 50 m to suit a ~15 m arena.</summary>
        private const float ShadowDistanceMetres = 30f;

        /// <summary>Cascades across that range. One was giving the fighters very few texels.</summary>
        private const int ShadowCascades = 4;

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

            TunePipeline(pipeline);
            EnsureAmbientOcclusion();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[Adaptive Boss Arena] Universal Render Pipeline configured and assigned " +
                $"('{PipelineAssetPath}').");
        }

        /// <summary>
        /// Writes the quality settings the pipeline ships without.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>UniversalRenderPipelineAsset.Create</c> produces factory defaults, and this configurator
        /// used to accept every one of them. The worst consequence was soft shadows: the arena asks
        /// for <c>LightShadows.Soft</c> when it builds the directional light, the pipeline refused,
        /// and so every shadow edge in the game was hard and aliased while the code read as though it
        /// were not.
        /// </para>
        /// <para>
        /// Shadow distance is the other one worth naming. The default 50 metres spread a 2048 map
        /// over an area a hundred times larger than the roughly 15-metre arena, across a single
        /// cascade, so the fighters were lit by a handful of shadow texels. Pulling the distance in
        /// and splitting it into four cascades costs nothing and multiplies the effective resolution.
        /// </para>
        /// <para>
        /// Written through <see cref="SerializedObject"/> rather than the public API, because most of
        /// these are serialized fields of a package type with no public setter.
        /// </para>
        /// </remarks>
        private static void TunePipeline(UniversalRenderPipelineAsset pipeline)
        {
            var serialized = new SerializedObject(pipeline);

            // Shadows: soft, close, and split so the fighters get real resolution.
            SetIfPresent(serialized, "m_SoftShadowsSupported", true);
            SetIfPresent(serialized, "m_SoftShadowQuality", 2);
            SetIfPresent(serialized, "m_ShadowDistance", ShadowDistanceMetres);
            SetIfPresent(serialized, "m_ShadowCascadeCount", ShadowCascades);
            SetIfPresent(serialized, "m_MainLightShadowmapResolution", 2048);

            // The braziers could not cast at all: the light asks for shadows, the pipeline forbade
            // them regardless.
            SetIfPresent(serialized, "m_AdditionalLightsRenderingMode", 1);
            SetIfPresent(serialized, "m_AdditionalLightShadowsSupported", true);
            SetIfPresent(serialized, "m_AdditionalLightsShadowmapResolution", 1024);

            // Four braziers, the boss's phase aura and the flash on every impact can easily want more
            // than four lights on one surface at once, and the ones past the limit simply vanish.
            SetIfPresent(serialized, "m_AdditionalLightsPerObjectLimit", 8);

            // Ambient occlusion and every depth-reading effect need this. It was off, so no renderer
            // feature could have worked even if one had been added.
            SetIfPresent(serialized, "m_RequireDepthTexture", true);

            // Grade in HDR so the ACES curve and the above-one emission behave as intended, rather
            // than being clipped to display range first.
            SetIfPresent(serialized, "m_ColorGradingMode", 1);
            SetIfPresent(serialized, "m_ColorGradingLutSize", 32);

            // Metal reflects the probe rather than only the flat sky.
            SetIfPresent(serialized, "m_ReflectionProbeBlending", true);
            SetIfPresent(serialized, "m_ReflectionProbeBoxProjection", true);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        /// <summary>
        /// Adds screen-space ambient occlusion to the renderer.
        /// </summary>
        /// <remarks>
        /// The renderer shipped with an empty feature list, so the game had no ambient occlusion of
        /// any kind: no screen-space pass, nothing baked, no occlusion maps. Contact darkening is how
        /// the eye reads one object as resting on another, and without it everything in the arena
        /// floats very slightly — a large part of why generated geometry reads as a prototype.
        /// </remarks>
        private static void EnsureAmbientOcclusion()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);

            if (rendererData == null)
            {
                return;
            }

            foreach (ScriptableRendererFeature existing in rendererData.rendererFeatures)
            {
                if (existing is ScreenSpaceAmbientOcclusion)
                {
                    return;
                }
            }

            var ambientOcclusion = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ambientOcclusion.name = "ScreenSpaceAmbientOcclusion";

            // Stored as a sub-asset of the renderer, which is how the inspector adds one too.
            AssetDatabase.AddObjectToAsset(ambientOcclusion, rendererData);

            var serialized = new SerializedObject(rendererData);
            SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = serialized.FindProperty("m_RendererFeatureMap");

            if (features == null || featureMap == null)
            {
                Debug.LogWarning(
                    "[Adaptive Boss Arena] The renderer's feature list could not be found, so ambient " +
                    "occlusion was not added. Surfaces will render without contact darkening.");
                return;
            }

            features.arraySize += 1;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ambientOcclusion;

            // The map holds each feature's local file id, which is how the renderer keeps its
            // references straight when the list is reordered.
            featureMap.arraySize = features.arraySize;
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue =
                LocalFileIdOf(ambientOcclusion);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            TuneAmbientOcclusion(ambientOcclusion);

            EditorUtility.SetDirty(rendererData);
        }

        /// <summary>Sets the occlusion strength, kept subtle enough to read as contact, not grime.</summary>
        private static void TuneAmbientOcclusion(ScreenSpaceAmbientOcclusion ambientOcclusion)
        {
            var serialized = new SerializedObject(ambientOcclusion);
            SerializedProperty settings = serialized.FindProperty("m_Settings");

            if (settings != null)
            {
                SetChildIfPresent(settings, "Intensity", 1.1f);
                SetChildIfPresent(settings, "Radius", 0.28f);
                SetChildIfPresent(settings, "Falloff", 90f);
                SetChildIfPresent(settings, "Downsample", true);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Reads back the local file id Unity assigned to a newly added sub-asset.</summary>
        private static long LocalFileIdOf(Object subAsset) =>
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(subAsset, out string _, out long localId)
                ? localId
                : 0L;

        /// <summary>
        /// Writes a serialized field, skipping it when this pipeline version does not have it.
        /// </summary>
        /// <remarks>
        /// These are private fields of a package type, so their names are not a compile-time
        /// contract. Skipping a missing one with a warning keeps a package upgrade from turning
        /// project setup into a hard failure.
        /// </remarks>
        private static void SetIfPresent(SerializedObject serialized, string fieldName, object value)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogWarning(
                    $"[Adaptive Boss Arena] Render pipeline field '{fieldName}' was not found and has " +
                    "been skipped. The render pipeline package may have renamed it.");
                return;
            }

            Assign(property, value);
        }

        /// <summary>Writes a child field of a serialized struct, skipping it when absent.</summary>
        private static void SetChildIfPresent(SerializedProperty parent, string fieldName, object value)
        {
            SerializedProperty property = parent.FindPropertyRelative(fieldName);

            if (property != null)
            {
                Assign(property, value);
            }
        }

        /// <summary>Assigns a boxed value to whichever serialized type the property holds.</summary>
        private static void Assign(SerializedProperty property, object value)
        {
            switch (value)
            {
                case bool flag:
                    property.boolValue = flag;
                    break;

                case int number:
                    property.intValue = number;
                    break;

                case float number:
                    property.floatValue = number;
                    break;
            }
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
