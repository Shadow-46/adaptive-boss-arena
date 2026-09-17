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

        /// <summary>
        /// The browser's profile: the same grade, without the effects that cost a full-screen pass each.
        /// </summary>
        /// <remarks>
        /// Once post-processing actually rendered, the browser build on a laptop's integrated graphics stuttered.
        /// Film grain and chromatic aberration each add a full-resolution pass for a look the eye barely separates
        /// from the grade itself, and bloom at half resolution with high-quality filtering is the most expensive
        /// bloom there is. The grade - exposure, contrast, split tone, white balance, tonemapping - all folds into
        /// one lookup table, so it costs the same either way and is kept.
        /// </remarks>
        public const string WebProfilePath = "Assets/_Project/Settings/ArenaVolumeProfile_Web.asset";

        /// <summary>Creates both profiles if absent and fills in the effect overrides.</summary>
        [MenuItem(EditorMenus.Setup + "Configure Post Processing",
            priority = EditorMenus.SetupPriorityConfigureProject + 2)]
        public static void ConfigurePostProcessing()
        {
            AssetAuthoring.EnsureFolderExists("Assets/_Project/Settings");

            VolumeProfile desktop = LoadOrCreate(ProfilePath);
            ConfigureBloom(desktop);
            ConfigureVignette(desktop);
            ConfigureColorAdjustments(desktop);
            ConfigureTonemapping(desktop);
            ConfigureChromaticAberration(desktop);
            ConfigureFilmGrain(desktop);
            ConfigureSplitToning(desktop);
            ConfigureWhiteBalance(desktop);
            EditorUtility.SetDirty(desktop);

            VolumeProfile web = LoadOrCreate(WebProfilePath);
            ConfigureBloom(web);
            ConfigureVignette(web);
            ConfigureColorAdjustments(web);
            ConfigureTonemapping(web);
            ConfigureSplitToning(web);
            ConfigureWhiteBalance(web);
            MakeCheapForTheBrowser(web);
            EditorUtility.SetDirty(web);

            AssetDatabase.SaveAssets();

            Debug.Log("[Adaptive Boss Arena] Post-processing profiles written: desktop and web.");
        }

        /// <summary>Loads the generated profile, for the scene builder.</summary>
        /// <returns>The profile, or null when it has not been generated yet.</returns>
        public static VolumeProfile LoadProfile() =>
            GeneratedAssets.ForceImportAndLoad<VolumeProfile>(ProfilePath);

        /// <summary>Loads the browser's profile, for the scene builder.</summary>
        /// <returns>The profile, or null when it has not been generated yet.</returns>
        public static VolumeProfile LoadWebProfile() =>
            GeneratedAssets.ForceImportAndLoad<VolumeProfile>(WebProfilePath);

        private static VolumeProfile LoadOrCreate(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            // Entries left null by earlier runs, which added effects without saving them; see GetOrAdd.
            profile.components.RemoveAll(component => component == null);

            return profile;
        }

        /// <summary>
        /// Makes the browser's bloom the cheap kind: quarter resolution, simple filtering, fewer passes.
        /// </summary>
        /// <remarks>
        /// Film grain and chromatic aberration are simply never added to this profile. They are not removed
        /// here, because a component removed from a profile's list leaves its saved sub-asset behind.
        /// </remarks>
        private static void MakeCheapForTheBrowser(VolumeProfile profile)
        {
            if (profile.TryGet(out Bloom bloom))
            {
                bloom.highQualityFiltering.Override(false);
                bloom.downscale.Override(BloomDownscaleMode.Quarter);
                bloom.maxIterations.Override(5);
            }
        }

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

            vignette.intensity.Override(0.36f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.02f, 0.01f, 0.04f));
        }

        /// <summary>Drains colour and deepens contrast toward a grim, overcast palette.</summary>
        /// <remarks>
        /// With textured characters and stone the image no longer needs lifting; it needs weight. At nearly
        /// full saturation the green hide, bright bars and orange light read as a cartoon. Exposure comes down
        /// only a little, because a darker grade than this lost the fighters against the floor.
        /// </remarks>
        private static void ConfigureColorAdjustments(VolumeProfile profile)
        {
            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);

            color.postExposure.Override(1.3f);
            color.contrast.Override(20f);
            color.saturation.Override(-22f);
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

        /// <summary>
        /// A fine layer of grain over everything.
        /// </summary>
        /// <remarks>
        /// Cheap, and unusually valuable in this project specifically. Untextured generated surfaces
        /// produce perfectly smooth gradients, which is one of the strongest signals that what you
        /// are looking at was not photographed or painted. Grain breaks those gradients up and reads
        /// as film rather than as noise, provided it stays quiet enough not to be noticed directly.
        /// </remarks>
        private static void ConfigureFilmGrain(VolumeProfile profile)
        {
            FilmGrain grain = GetOrAdd<FilmGrain>(profile);

            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.28f);

            // Lets the grain fade out of bright areas, which is how it behaves on real stock.
            grain.response.Override(0.8f);
        }

        /// <summary>
        /// Cools the shadows and warms the highlights.
        /// </summary>
        /// <remarks>
        /// The difference between a graded image and one that has had a contrast slider pushed.
        /// Cool shadows against warm light is the oldest trick there is for making a dark scene feel
        /// deliberate, and it suits an arena lit by one cold key and four fires.
        /// </remarks>
        private static void ConfigureSplitToning(VolumeProfile profile)
        {
            ShadowsMidtonesHighlights grading = GetOrAdd<ShadowsMidtonesHighlights>(profile);

            // The fourth channel of each is the overall weight of that band, not alpha.
            grading.shadows.Override(new Vector4(0.9f, 0.95f, 1.05f, 0f));
            grading.midtones.Override(new Vector4(1f, 0.98f, 0.95f, 0f));
            grading.highlights.Override(new Vector4(1.06f, 1f, 0.9f, 0f));
        }

        /// <summary>
        /// Shifts the whole image cold.
        /// </summary>
        /// <remarks>
        /// A genuine chromatic adaptation rather than the colour filter previously used to fake one.
        /// A filter multiplies every channel and dims as it tints; white balance shifts what counts
        /// as white and keeps the exposure.
        /// </remarks>
        private static void ConfigureWhiteBalance(VolumeProfile profile)
        {
            WhiteBalance balance = GetOrAdd<WhiteBalance>(profile);

            balance.temperature.Override(-5f);
            balance.tint.Override(2f);
        }

        /// <summary>Returns an override from the profile, adding it when absent.</summary>
        /// <remarks>
        /// <see cref="VolumeProfile.Add{T}"/> only creates the effect in memory. Unless it is also saved into
        /// the profile's asset, the profile reloads with a null in its place - which is how every build shipped
        /// with no bloom, tonemapping, vignette or grade at all, while the editor session that ran setup
        /// looked right.
        /// </remarks>
        private static TComponent GetOrAdd<TComponent>(VolumeProfile profile)
            where TComponent : VolumeComponent
        {
            if (!profile.TryGet(out TComponent component))
            {
                component = profile.Add<TComponent>(overrides: true);
            }

            component.active = true;

            if (!AssetDatabase.Contains(component))
            {
                component.name = typeof(TComponent).Name;
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            return component;
        }
    }
}
