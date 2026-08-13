using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Creates and reuses the flat-coloured materials the generated placeholder art needs.
    /// </summary>
    /// <remarks>
    /// Shared by the scene and prefab generators so a colour is defined once and every object using
    /// it references the same asset. Creating a material per object would leave the project full of
    /// near-identical assets and break batching for no benefit.
    /// </remarks>
    internal static class MaterialLibrary
    {
        private const string UniversalLitShader = "Universal Render Pipeline/Lit";
        private const string FallbackShader = "Standard";

        private const string UniversalColorProperty = "_BaseColor";
        private const string FallbackColorProperty = "_Color";

        /// <summary>Loads a generated material by name, creating it if it does not exist.</summary>
        /// <param name="materialName">File name, without extension.</param>
        /// <param name="color">Base colour to assign when creating.</param>
        /// <returns>The material, or null when no lit shader could be resolved.</returns>
        public static Material GetOrCreate(string materialName, Color color)
        {
            string path = $"{EditorMenus.GeneratedMaterialFolder}/{materialName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                return existing;
            }

            Shader shader = ResolveLitShader();

            if (shader == null)
            {
                Debug.LogWarning(
                    "[Adaptive Boss Arena] No lit shader could be resolved, so generated objects will " +
                    "use the default material. This usually means package import has not finished.");
                return null;
            }

            var material = new Material(shader) { name = materialName };
            material.SetColor(ColorPropertyFor(shader), color);

            AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedMaterialFolder);
            AssetDatabase.CreateAsset(material, path);

            return material;
        }

        /// <summary>
        /// Loads or creates a surface material with metal, roughness and optional glow.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Everything generated so far has been flat, default-rough plastic — a single base colour and
        /// nothing else — which is why one directional light had nothing to catch and the whole scene
        /// read as untextured primitives. Metal and smoothness give the light something to do; the
        /// silhouette stops being a flat shape.
        /// </para>
        /// <para>
        /// Emission is the cheapest drama available here. The bloom threshold is 0.9, so any emission
        /// colour brighter than that blooms without further work — which is what makes braziers, rune
        /// inlays and a boss's weak point glow rather than merely being pale.
        /// </para>
        /// </remarks>
        /// <param name="materialName">File name, without extension.</param>
        /// <param name="color">Base colour.</param>
        /// <param name="metallic">Zero for cloth or stone, near one for armour and blades.</param>
        /// <param name="smoothness">Zero for rough, one for polished.</param>
        /// <param name="emission">Glow colour; pass black for a surface that does not emit.</param>
        /// <returns>The material, or null when no lit shader could be resolved.</returns>
        public static Material GetOrCreateSurface(
            string materialName,
            Color color,
            float metallic = 0f,
            float smoothness = 0.35f,
            Color emission = default)
        {
            string path = $"{EditorMenus.GeneratedMaterialFolder}/{materialName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                return existing;
            }

            Shader shader = ResolveLitShader();

            if (shader == null)
            {
                Debug.LogWarning(
                    "[Adaptive Boss Arena] No lit shader could be resolved, so generated surfaces will " +
                    "use the default material. This usually means package import has not finished.");
                return null;
            }

            var material = new Material(shader) { name = materialName };
            material.SetColor(ColorPropertyFor(shader), color);

            // Named through property strings because the pipeline and built-in shaders disagree about
            // which exist; setting one that is absent is a no-op rather than an error.
            material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            material.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));

            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedMaterialFolder);
            AssetDatabase.CreateAsset(material, path);

            return material;
        }

        /// <summary>
        /// Loads or creates the shared transparent material used for attack overlays.
        /// </summary>
        /// <remarks>
        /// Built on the sprite shader rather than a pipeline lit or unlit shader. It is unlit and
        /// alpha-blended by default in every render pipeline, whereas making a URP unlit material
        /// transparent means setting several surface keywords that differ between versions. The
        /// overlay only ever needs a flat translucent colour, so the simplest reliable option wins.
        /// </remarks>
        /// <returns>The overlay material, or null when no suitable shader exists.</returns>
        public static Material GetOrCreateAttackOverlay()
        {
            const string materialName = "AttackOverlay";
            string path = $"{EditorMenus.GeneratedMaterialFolder}/{materialName}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                return existing;
            }

            Material material = CreateTransparentUnlit(materialName);

            if (material == null)
            {
                Debug.LogWarning(
                    "[Adaptive Boss Arena] Could not resolve a transparent shader; attack overlays " +
                    "will not render.");
                return null;
            }

            AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedMaterialFolder);
            AssetDatabase.CreateAsset(material, path);

            return material;
        }

        /// <summary>
        /// Loads or creates the shared material used for weapon trails.
        /// </summary>
        /// <remarks>
        /// Built on the sprite shader specifically because it honours vertex colours. A trail's shape
        /// and fade come from <see cref="TrailRenderer.colorGradient"/>, which reaches the shader as
        /// vertex colour; the pipeline's unlit shader ignores it, so a trail authored against that
        /// would render as a flat unfading ribbon.
        /// </remarks>
        /// <returns>The trail material, or null when no suitable shader exists.</returns>
        public static Material GetOrCreateWeaponTrail()
        {
            const string materialName = "WeaponTrail";
            string path = $"{EditorMenus.GeneratedMaterialFolder}/{materialName}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning(
                    "[Adaptive Boss Arena] Could not resolve a sprite shader; weapon trails will " +
                    "not render.");
                return null;
            }

            var material = new Material(shader) { name = materialName };

            AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedMaterialFolder);
            AssetDatabase.CreateAsset(material, path);

            return material;
        }

        /// <summary>
        /// Builds an unlit alpha-blended material for the render pipeline in use.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A URP unlit material does not become transparent by setting its colour's alpha. The
        /// surface mode, blend mode, depth write and render queue all have to be configured
        /// together, and the shader keyword toggled, or the material silently renders fully opaque
        /// or not at all.
        /// </para>
        /// <para>
        /// Falls back to the built-in sprite shader, which is unlit and alpha-blended by default,
        /// for projects not running URP.
        /// </para>
        /// </remarks>
        private static Material CreateTransparentUnlit(string materialName)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
            {
                Shader fallback = Shader.Find("Sprites/Default");
                return fallback == null ? null : new Material(fallback) { name = materialName };
            }

            var material = new Material(shader) { name = materialName };

            const float transparentSurface = 1f;
            const float alphaBlend = 0f;

            material.SetFloat("_Surface", transparentSurface);
            material.SetFloat("_Blend", alphaBlend);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            return material;
        }

        /// <summary>Assigns a generated material to a primitive, tolerating a failed shader lookup.</summary>
        /// <param name="target">Object whose renderer should be assigned.</param>
        /// <param name="materialName">File name, without extension.</param>
        /// <param name="color">Base colour to assign when creating.</param>
        public static void Apply(GameObject target, string materialName, Color color)
        {
            Material material = GetOrCreate(materialName, color);
            var renderer = target.GetComponent<MeshRenderer>();

            if (material != null && renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        /// <summary>
        /// Resolves the render pipeline's lit shader, falling back to the built-in one.
        /// </summary>
        /// <remarks>
        /// The fallback matters when this runs before package import completes. Producing an
        /// oddly-lit scene is a far easier problem to notice and correct than a generator that
        /// aborts partway through.
        /// </remarks>
        private static Shader ResolveLitShader()
        {
            Shader shader = Shader.Find(UniversalLitShader);
            return shader != null ? shader : Shader.Find(FallbackShader);
        }

        /// <summary>Resolves the base colour property name, which differs between pipelines.</summary>
        private static string ColorPropertyFor(Shader shader) =>
            shader.name.StartsWith(UniversalLitShader) ? UniversalColorProperty : FallbackColorProperty;
    }
}
