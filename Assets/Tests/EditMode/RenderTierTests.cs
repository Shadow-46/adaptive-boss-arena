using AdaptiveBossArena.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Pins the split between the browser's render tier and the desktop build's.
    /// </summary>
    /// <remarks>
    /// The one property that matters most here is invisible from the desktop: ambient occlusion broke
    /// the WebGL build outright, and it passed every test and ran a full fight as a Windows player
    /// before dying on load in a browser. A desktop tier that leaked into WebGL would repeat that
    /// failure with nothing but a browser to notice it. These assert the leak is structurally
    /// impossible rather than hoping it does not happen.
    /// </remarks>
    [TestFixture]
    public sealed class RenderTierTests
    {
        private static bool HasAmbientOcclusion(string rendererPath)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            Assert.IsNotNull(renderer, $"Renderer '{rendererPath}' is missing.");

            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
            {
                if (feature is ScreenSpaceAmbientOcclusion)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void BothTiersHaveAPipeline()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                RenderPipelineConfigurator.PipelineAssetPath), "The web tier pipeline is missing.");

            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                RenderPipelineConfigurator.DesktopPipelineAssetPath), "The desktop tier pipeline is missing.");
        }

        [Test]
        public void OnlyTheDesktopTierCarriesAmbientOcclusion()
        {
            Assert.IsFalse(
                HasAmbientOcclusion(RenderPipelineConfigurator.RendererAssetPath),
                "The web tier has ambient occlusion, which broke the WebGL build.");

            Assert.IsTrue(
                HasAmbientOcclusion(RenderPipelineConfigurator.DesktopRendererAssetPath),
                "The desktop tier lost its ambient occlusion.");
        }

        [Test]
        public void EveryDesktopLevelIsExcludedFromWebGLAndEveryWebLevelFromDesktop()
        {
            SerializedObject quality = RenderPipelineConfigurator.LoadQualitySettings();
            SerializedProperty levels = quality.FindProperty("m_QualitySettings");
            Assert.IsNotNull(levels);

            var web = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                RenderPipelineConfigurator.PipelineAssetPath);
            var desktop = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                RenderPipelineConfigurator.DesktopPipelineAssetPath);

            int firstDesktop = RenderPipelineConfigurator.FirstDesktopLevel(levels.arraySize);

            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                bool isDesktop = i >= firstDesktop;
                string name = level.FindPropertyRelative("name").stringValue;

                Assert.AreSame(
                    isDesktop ? desktop : web,
                    level.FindPropertyRelative("customRenderPipeline").objectReferenceValue,
                    $"Quality level '{name}' uses the wrong tier's pipeline.");

                string excluded = isDesktop
                    ? RenderPipelineConfigurator.WebGLPlatform
                    : RenderPipelineConfigurator.StandalonePlatform;

                Assert.IsTrue(
                    Excludes(level, excluded),
                    $"Quality level '{name}' is not excluded from {excluded}.");
            }
        }

        [Test]
        public void EachPlatformStartsInItsOwnTier()
        {
            SerializedObject quality = RenderPipelineConfigurator.LoadQualitySettings();
            int levelCount = quality.FindProperty("m_QualitySettings").arraySize;
            int firstDesktop = RenderPipelineConfigurator.FirstDesktopLevel(levelCount);

            Assert.Less(
                DefaultLevel(quality, RenderPipelineConfigurator.WebGLPlatform), firstDesktop,
                "WebGL starts in the desktop tier.");

            Assert.GreaterOrEqual(
                DefaultLevel(quality, RenderPipelineConfigurator.StandalonePlatform), firstDesktop,
                "The Windows build starts in the web tier and would never show the full graphics.");
        }

        [Test]
        public void BothBuildTargetsPassTheirTierCheck()
        {
            // The same check the build script runs before building. If it ever fails here, a build
            // would refuse to start - which is the intended outcome, but should be caught now.
            Assert.IsNull(BuildScript.TierProblemFor(BuildTarget.WebGL));
            Assert.IsNull(BuildScript.TierProblemFor(BuildTarget.StandaloneWindows64));
        }

        private static bool Excludes(SerializedProperty level, string platform)
        {
            SerializedProperty excluded = level.FindPropertyRelative("excludedTargetPlatforms");

            for (int i = 0; i < excluded.arraySize; i++)
            {
                if (excluded.GetArrayElementAtIndex(i).stringValue == platform)
                {
                    return true;
                }
            }

            return false;
        }

        private static int DefaultLevel(SerializedObject quality, string platform)
        {
            SerializedProperty defaults = quality.FindProperty("m_PerPlatformDefaultQuality");

            for (int i = 0; i < defaults.arraySize; i++)
            {
                SerializedProperty pair = defaults.GetArrayElementAtIndex(i);

                if (pair.FindPropertyRelative("first").stringValue == platform)
                {
                    return pair.FindPropertyRelative("second").intValue;
                }
            }

            Assert.Fail($"No default quality level is set for {platform}.");
            return -1;
        }
    }
}
