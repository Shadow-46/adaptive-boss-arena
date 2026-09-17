using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests that the arena's post-processing survives being saved and reloaded.
    /// </summary>
    /// <remarks>
    /// The profile's effects were added in memory and never saved into its asset. The editor session that
    /// ran setup looked graded, and every build and every later session loaded a profile of nulls: no bloom,
    /// no tonemapping, no vignette, no grade.
    /// </remarks>
    [TestFixture]
    public sealed class VolumeProfileAssetTests
    {
        [Test]
        public void EveryEffectIsSavedInsideTheProfileAsset()
        {
            const string path = "Assets/_Project/Settings/ArenaVolumeProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            Assume.That(profile != null, "Setup has not generated the profile.");

            Assert.Greater(profile.components.Count, 0, "The profile carries no effects.");

            foreach (VolumeComponent component in profile.components)
            {
                Assert.IsNotNull(component, "The profile holds an effect that was never saved.");
                Assert.AreEqual(path, AssetDatabase.GetAssetPath(component), component.GetType().Name + " is not saved in the profile.");
            }

            Assert.IsTrue(profile.Has<Bloom>(), "No bloom.");
            Assert.IsTrue(profile.Has<Tonemapping>(), "No tonemapping.");
            Assert.IsTrue(profile.Has<ColorAdjustments>(), "No grade.");
        }

        [Test]
        public void TheBrowserProfileKeepsTheGradeButDropsTheExpensivePasses()
        {
            var web = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AdaptiveBossArena.Editor.PostProcessingConfigurator.WebProfilePath);
            Assume.That(web != null, "Setup has not generated the browser profile.");

            foreach (VolumeComponent component in web.components)
            {
                Assert.IsNotNull(component, "The browser profile holds an effect that was never saved.");
            }

            Assert.IsFalse(web.Has<FilmGrain>(), "The browser pays for film grain.");
            Assert.IsFalse(web.Has<ChromaticAberration>(), "The browser pays for chromatic aberration.");
            Assert.IsTrue(web.Has<ColorAdjustments>(), "The browser lost the grade.");
            Assert.IsTrue(web.Has<Tonemapping>(), "The browser lost tonemapping.");

            Assert.IsTrue(web.TryGet(out Bloom bloom), "The browser lost bloom.");
            Assert.IsFalse(bloom.highQualityFiltering.value, "The browser's bloom uses high-quality filtering.");
            Assert.AreEqual(BloomDownscaleMode.Quarter, bloom.downscale.value, "The browser's bloom is not quarter resolution.");
        }

        [TestCase("Assets/_Project/Settings/UniversalRenderer.asset")]
        [TestCase("Assets/_Project/Settings/UniversalRenderer_Desktop.asset")]
        public void EveryRendererCarriesThePostProcessingResources(string path)
        {
            // Without them URP skips every effect for that renderer, whatever the camera and volume say.
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            Assume.That(renderer != null, "Setup has not generated the renderer.");

            Assert.IsNotNull(renderer.postProcessData, path + " has no post-processing resources.");
        }
    }
}
