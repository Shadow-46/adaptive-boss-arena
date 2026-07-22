using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Creates and populates the post-processing volume profile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With the arena built from untextured primitives, post-processing is doing most of the work
    /// that art would normally do. Bloom gives the attack overlays and the boss's weak point a sense
    /// of emission rather than flatness; vignette focuses attention toward the centre where the duel
    /// happens; a slight tonemap and colour grade stop everything reading as raw untextured grey.
    /// </para>
    /// <para>
    /// The values are deliberately restrained. Heavy post-processing on abstract shapes reads as a
    /// screensaver; the goal is for the arena to look considered rather than processed.
    /// </para>
    /// </remarks>
    public static class PostProcessingConfigurator
    {
        private const string ProfilePath = "Assets/_Project/Settings/ArenaVolumeProfile.asset";

        /// <summary>Creates the profile if absent and fills in the effect overrides.</summary>
        [MenuItem(EditorMenus.Setup + "Configure Post Processing",
            priority = EditorMenus.SetupPriorityConfigureProject + 2)]
        public static void ConfigurePostProcessing()
        {
            AssetAuthoring.EnsureFolderExists("Assets/_Project/Settings");

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            bool created = false;

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                created = true;
            }

            ConfigureBloom(profile);
            ConfigureVignette(profile);
            ConfigureColorAdjustments(profile);
            ConfigureTonemapping(profile);
            ConfigureChromaticAberration(profile);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[Adaptive Boss Arena] Post-processing profile {(created ? "created" : "updated")} " +
                $"at '{ProfilePath}'.");
        }

        /// <summary>Loads the generated profile, for the scene builder.</summary>
        /// <returns>The profile, or null when it has not been generated yet.</returns>
        public static VolumeProfile LoadProfile() =>
            GeneratedAssets.ForceImportAndLoad<VolumeProfile>(ProfilePath);

        /// <summary>
        /// Makes bright surfaces glow.
        /// </summary>
        /// <remarks>
        /// The single highest-value effect here. Attack overlays, the boss's weak point and the
        /// deflect flash all rely on reading as emissive, and without bloom they are simply flat
        /// coloured shapes.
        /// </remarks>
        private static void ConfigureBloom(VolumeProfile profile)
        {
            Bloom bloom = GetOrAdd<Bloom>(profile);

            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(1f, 0.96f, 0.92f));
        }

        /// <summary>Darkens the edges, pulling the eye toward the duel.</summary>
        private static void ConfigureVignette(VolumeProfile profile)
        {
            Vignette vignette = GetOrAdd<Vignette>(profile);

            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.02f, 0.01f, 0.04f));
        }

        /// <summary>Lifts contrast and cools the palette so untextured surfaces read as deliberate.</summary>
        private static void ConfigureColorAdjustments(VolumeProfile profile)
        {
            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);

            color.postExposure.Override(0.15f);
            color.contrast.Override(18f);
            color.saturation.Override(-6f);
            color.colorFilter.Override(new Color(0.94f, 0.96f, 1f));
        }

        /// <summary>Applies a filmic curve so bright areas roll off rather than clipping.</summary>
        private static void ConfigureTonemapping(VolumeProfile profile)
        {
            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);
        }

        /// <summary>
        /// A trace of lens fringing at the edges.
        /// </summary>
        /// <remarks>
        /// Kept very low. This is the effect most often overdone, and past a few percent it reads as
        /// a fault in the display rather than as character.
        /// </remarks>
        private static void ConfigureChromaticAberration(VolumeProfile profile)
        {
            ChromaticAberration aberration = GetOrAdd<ChromaticAberration>(profile);
            aberration.intensity.Override(0.08f);
        }

        /// <summary>Returns an override from the profile, adding it when absent.</summary>
        private static TComponent GetOrAdd<TComponent>(VolumeProfile profile)
            where TComponent : VolumeComponent
        {
            if (profile.TryGet(out TComponent existing))
            {
                existing.active = true;
                return existing;
            }

            return profile.Add<TComponent>(overrides: true);
        }
    }
}
