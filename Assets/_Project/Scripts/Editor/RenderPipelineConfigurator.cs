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

        /// <summary>The web tier. Kept at its original path so every existing reference to it survives.</summary>
        public const string PipelineAssetPath = SettingsFolder + "/UniversalRenderPipeline.asset";

        /// <summary>The web tier's renderer.</summary>
        public const string RendererAssetPath = SettingsFolder + "/UniversalRenderer.asset";

        /// <summary>The desktop tier: the full-graphics Windows build.</summary>
        public const string DesktopPipelineAssetPath = SettingsFolder + "/UniversalRenderPipeline_Desktop.asset";

        /// <summary>The desktop tier's renderer, which carries ambient occlusion.</summary>
        public const string DesktopRendererAssetPath = SettingsFolder + "/UniversalRenderer_Desktop.asset";

        /// <summary>How many quality levels, counted from the top, belong to the desktop tier.</summary>
        /// <remarks>
        /// Unity's default six levels are kept rather than replaced with two new ones, because the
        /// quality settings asset has no public API for adding or removing levels. The top two become
        /// the desktop tier and the rest the web tier.
        /// </remarks>
        public const int DesktopTierLevelCount = 2;

        /// <summary>Build-target group name used in quality-level exclusions and defaults.</summary>
        public const string StandalonePlatform = "Standalone";

        /// <summary>Build-target group name used in quality-level exclusions and defaults.</summary>
        public const string WebGLPlatform = "WebGL";

        /// <summary>What a render tier asks of its pipeline.</summary>
        private readonly struct TierSettings
        {
            public float ShadowDistance { get; init; }
            public int ShadowCascades { get; init; }
            public int MainShadowResolution { get; init; }
            public int AdditionalShadowResolution { get; init; }
            public int SoftShadowQuality { get; init; }
            public int Msaa { get; init; }
        }

        /// <summary>
        /// The web tier, exactly as the game shipped before tiers existed.
        /// </summary>
        /// <remarks>
        /// Deliberately unchanged. Splitting the tiers was to give the desktop build more, not the
        /// browser less, and the browser build's frame time cannot be measured from the automation
        /// pane, so cutting it blind would be a guess.
        /// </remarks>
        private static readonly TierSettings WebTier = new TierSettings
        {
            ShadowDistance = 30f,
            ShadowCascades = 4,
            MainShadowResolution = 2048,
            AdditionalShadowResolution = 1024,
            SoftShadowQuality = 2,
            Msaa = 1
        };

        /// <summary>The desktop tier: sharper shadows further out, and multisampled edges.</summary>
        private static readonly TierSettings DesktopTier = new TierSettings
        {
            ShadowDistance = 40f,
            ShadowCascades = 4,
            MainShadowResolution = 4096,
            AdditionalShadowResolution = 2048,
            SoftShadowQuality = 3,
            Msaa = 4
        };

        /// <summary>
        /// Whether screen-space ambient occlusion is added to the renderer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Off, because adding it broke the WebGL build outright: the page died on load with
        /// "InternalError: too much recursion" and a stack that repeated the same handful of wasm
        /// frames in a cycle — infinite recursion rather than merely deep recursion, so a larger
        /// stack would not have helped. The same build ran perfectly as a Windows player with
        /// graphics, a full fight through to a riposte and a posture break, which places the fault in
        /// the browser's render path rather than in the game.
        /// </para>
        /// <para>
        /// Ambient occlusion is a real loss — contact darkening is what stops objects looking like
        /// they float — so this is a deliberately reversible switch rather than a deletion. Turning it
        /// back on should be paired with running the SSAO pass after opaque geometry instead of
        /// before it, which removes its dependency on the depth-normals prepass and is the most
        /// likely source of a cycle in the render graph. It must be verified in a browser before
        /// being shipped again; the desktop player will not reproduce it.
        /// </para>
        /// </remarks>
        private const bool EnableAmbientOcclusion = false;

        /// <summary>Creates the pipeline assets if absent and assigns them globally.</summary>
        [MenuItem(EditorMenus.Setup + "Configure Render Pipeline",
            priority = EditorMenus.SetupPriorityConfigureProject + 1)]
        public static void ConfigureRenderPipeline()
        {
            AssetAuthoring.EnsureFolderExists(SettingsFolder);

            UniversalRenderPipelineAsset web = LoadOrCreatePipelineAsset(PipelineAssetPath, RendererAssetPath);
            UniversalRenderPipelineAsset desktop =
                LoadOrCreatePipelineAsset(DesktopPipelineAssetPath, DesktopRendererAssetPath);

            if (web == null || desktop == null)
            {
                Debug.LogError(
                    "[Adaptive Boss Arena] Could not create the Universal Render Pipeline assets. " +
                    "Everything will render magenta until a pipeline is assigned.");
                return;
            }

            // Both assignments are needed. The graphics setting is the project-wide default; the
            // quality setting overrides it per quality level and, left empty, silently wins. The
            // default is the web tier, the conservative one: anything that falls back to it runs
            // everywhere.
            GraphicsSettings.defaultRenderPipeline = web;
            AssignQualityTiers(web, desktop);

            TunePipeline(web, WebTier);
            TunePipeline(desktop, DesktopTier);
            EnsureAmbientOcclusion(RendererAssetPath, EnableAmbientOcclusion);
            EnsureAmbientOcclusion(DesktopRendererAssetPath, true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[Adaptive Boss Arena] Universal Render Pipeline configured: web tier " +
                $"'{PipelineAssetPath}', desktop tier '{DesktopPipelineAssetPath}'.");
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
        private static void TunePipeline(UniversalRenderPipelineAsset pipeline, TierSettings tier)
        {
            var serialized = new SerializedObject(pipeline);

            // Shadows: soft, close, and split so the fighters get real resolution.
            SetIfPresent(serialized, "m_SoftShadowsSupported", true);
            SetIfPresent(serialized, "m_SoftShadowQuality", tier.SoftShadowQuality);
            SetIfPresent(serialized, "m_ShadowDistance", tier.ShadowDistance);
            SetIfPresent(serialized, "m_ShadowCascadeCount", tier.ShadowCascades);
            SetIfPresent(serialized, "m_MainLightShadowmapResolution", tier.MainShadowResolution);
            SetIfPresent(serialized, "m_MSAA", tier.Msaa);

            // The braziers could not cast at all: the light asks for shadows, the pipeline forbade
            // them regardless.
            SetIfPresent(serialized, "m_AdditionalLightsRenderingMode", 1);
            SetIfPresent(serialized, "m_AdditionalLightShadowsSupported", true);
            SetIfPresent(serialized, "m_AdditionalLightsShadowmapResolution", tier.AdditionalShadowResolution);

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
        private static void EnsureAmbientOcclusion(string rendererAssetPath, bool enabled)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererAssetPath);

            if (rendererData == null)
            {
                return;
            }

            bool alreadyPresent = false;

            foreach (ScriptableRendererFeature existing in rendererData.rendererFeatures)
            {
                if (existing is ScreenSpaceAmbientOcclusion)
                {
                    alreadyPresent = true;
                    break;
                }
            }

            // Removal has to be handled as well as addition. The renderer asset is loaded rather than
            // recreated, so simply skipping the add would leave a feature added by an earlier run in
            // place for ever.
            if (!enabled)
            {
                if (alreadyPresent)
                {
                    RemoveAmbientOcclusion(rendererData);
                }

                return;
            }

            if (alreadyPresent)
            {
                return;
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

        /// <summary>
        /// Strips ambient occlusion back out of the renderer.
        /// </summary>
        /// <remarks>
        /// Both the feature list and the id map have to be cleared together, and the sub-asset itself
        /// deleted — leaving an orphaned feature object inside the renderer asset would keep it in
        /// the build even with nothing referencing it.
        /// </remarks>
        private static void RemoveAmbientOcclusion(UniversalRendererData rendererData)
        {
            var serialized = new SerializedObject(rendererData);
            SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = serialized.FindProperty("m_RendererFeatureMap");

            if (features == null || featureMap == null)
            {
                return;
            }

            for (int i = features.arraySize - 1; i >= 0; i--)
            {
                var feature =
                    features.GetArrayElementAtIndex(i).objectReferenceValue as ScreenSpaceAmbientOcclusion;

                if (feature == null)
                {
                    continue;
                }

                features.DeleteArrayElementAtIndex(i);

                if (i < featureMap.arraySize)
                {
                    featureMap.DeleteArrayElementAtIndex(i);
                }

                AssetDatabase.RemoveObjectFromAsset(feature);
                Object.DestroyImmediate(feature, allowDestroyingAssets: true);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);

            Debug.Log(
                "[Adaptive Boss Arena] Ambient occlusion removed from the renderer. It breaks the " +
                "WebGL build; see EnableAmbientOcclusion for the detail.");
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

        /// <summary>Loads a pipeline asset, creating it and its renderer on first run.</summary>
        private static UniversalRenderPipelineAsset LoadOrCreatePipelineAsset(
            string pipelineAssetPath, string rendererAssetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelineAssetPath);

            if (existing != null)
            {
                return existing;
            }

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererAssetPath);

            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, rendererAssetPath);
            }

            UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(rendererData);

            if (pipeline == null)
            {
                return null;
            }

            pipeline.name = Path.GetFileNameWithoutExtension(pipelineAssetPath);
            AssetDatabase.CreateAsset(pipeline, pipelineAssetPath);

            return pipeline;
        }

        /// <summary>
        /// Splits the quality levels into a web tier and a desktop tier.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every level still gets a pipeline — a level left empty falls back to the built-in renderer
        /// and renders magenta at that setting alone. The top <see cref="DesktopTierLevelCount"/>
        /// levels get the desktop pipeline and are excluded from WebGL builds; the rest get the web
        /// pipeline and are excluded from desktop builds.
        /// </para>
        /// <para>
        /// The exclusion is what makes this safe. Ambient occlusion broke the WebGL build outright,
        /// and a quality level that merely went unused in a browser would still carry its renderer —
        /// and its occlusion pass — into the build. Excluding the desktop levels from WebGL keeps that
        /// renderer out of the browser entirely.
        /// </para>
        /// <para>
        /// Written through <see cref="SerializedObject"/> because per-platform exclusion and defaults
        /// have no public API.
        /// </para>
        /// </remarks>
        private static void AssignQualityTiers(
            UniversalRenderPipelineAsset web, UniversalRenderPipelineAsset desktop)
        {
            SerializedObject quality = LoadQualitySettings();
            SerializedProperty levels = quality?.FindProperty("m_QualitySettings");

            if (levels == null)
            {
                Debug.LogError(
                    "[Adaptive Boss Arena] Quality levels could not be read, so the render tiers were not " +
                    "assigned. Levels without a pipeline render magenta.");
                return;
            }

            int firstDesktopLevel = FirstDesktopLevel(levels.arraySize);

            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                bool isDesktop = i >= firstDesktopLevel;

                SerializedProperty pipeline = level.FindPropertyRelative("customRenderPipeline");

                if (pipeline != null)
                {
                    pipeline.objectReferenceValue = isDesktop ? desktop : web;
                }

                SetExcludedPlatforms(
                    level.FindPropertyRelative("excludedTargetPlatforms"),
                    isDesktop ? WebGLPlatform : StandalonePlatform);
            }

            SetPlatformDefault(quality, WebGLPlatform, firstDesktopLevel - 1);
            SetPlatformDefault(quality, StandalonePlatform, levels.arraySize - 1);

            quality.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The first quality level that belongs to the desktop tier.</summary>
        /// <param name="levelCount">How many quality levels exist.</param>
        /// <returns>The index of the first desktop-tier level, never below one.</returns>
        public static int FirstDesktopLevel(int levelCount) =>
            Mathf.Max(1, levelCount - DesktopTierLevelCount);

        /// <summary>Loads the project's quality settings for serialized editing.</summary>
        /// <returns>The settings, or null if the asset could not be loaded.</returns>
        public static SerializedObject LoadQualitySettings()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");

            return assets != null && assets.Length > 0 ? new SerializedObject(assets[0]) : null;
        }

        private static void SetExcludedPlatforms(SerializedProperty excluded, string platform)
        {
            if (excluded == null)
            {
                return;
            }

            excluded.arraySize = 1;
            excluded.GetArrayElementAtIndex(0).stringValue = platform;
        }

        /// <summary>Sets which quality level a platform starts at.</summary>
        /// <remarks>
        /// The per-platform defaults are a serialized map, which appears as an array of pairs.
        /// </remarks>
        private static void SetPlatformDefault(SerializedObject quality, string platform, int level)
        {
            SerializedProperty defaults = quality.FindProperty("m_PerPlatformDefaultQuality");

            if (defaults == null)
            {
                Debug.LogWarning(
                    $"[Adaptive Boss Arena] Per-platform quality defaults not found; '{platform}' keeps its " +
                    "current default quality level.");
                return;
            }

            for (int i = 0; i < defaults.arraySize; i++)
            {
                SerializedProperty pair = defaults.GetArrayElementAtIndex(i);

                if (pair.FindPropertyRelative("first")?.stringValue == platform)
                {
                    pair.FindPropertyRelative("second").intValue = level;
                    return;
                }
            }

            defaults.arraySize += 1;
            SerializedProperty added = defaults.GetArrayElementAtIndex(defaults.arraySize - 1);
            added.FindPropertyRelative("first").stringValue = platform;
            added.FindPropertyRelative("second").intValue = level;
        }
    }
}
