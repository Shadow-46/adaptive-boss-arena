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
        /// Loads or creates the arena's sky.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The camera cleared to a flat dark colour before this, so the world simply stopped at the
        /// wall tops with nothing beyond them. A sky costs one material and gives the arena a horizon
        /// to sit under.
        /// </para>
        /// <para>
        /// Unity's procedural sky is used rather than a cubemap because there is no image to ship and
        /// none needs generating: it is driven entirely by numbers, and its tint can therefore be
        /// pushed toward the boss's current mood at runtime.
        /// </para>
        /// </remarks>
        /// <param name="materialName">File name, without extension.</param>
        /// <param name="skyTint">Overall colour of the sky.</param>
        /// <param name="groundColor">Colour below the horizon.</param>
        /// <param name="exposure">Overall brightness. Low values keep the mood dark.</param>
        /// <returns>The material, or null when the sky shader could not be resolved.</returns>
        public static Material GetOrCreateSkybox(
            string materialName, Color skyTint, Color groundColor, float exposure)
        {
            string path = $"{EditorMenus.GeneratedMaterialFolder}/{materialName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Skybox/Procedural");

            if (shader == null)
            {
                Debug.LogWarning(
                    "[Adaptive Boss Arena] The procedural sky shader could not be resolved, so the " +
                    "camera will keep clearing to a flat colour.");
                return null;
            }

            var material = new Material(shader) { name = materialName };
            material.SetColor("_SkyTint", skyTint);
            material.SetColor("_GroundColor", groundColor);
            material.SetFloat("_Exposure", exposure);

            // Thicker than default, which reddens the horizon and suits a sky the arena is meant to
            // feel oppressed by.
            material.SetFloat("_AtmosphereThickness", 1.7f);

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
        /// Loads or creates a lit material from a colour map, a normal map and a URP mask map.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The mask is packed the way URP Lit reads it - metallic in red, occlusion in green, smoothness
        /// in alpha - so one texture fills both the metallic and the occlusion slot. Shipping the
        /// published ARM map as-is would have put roughness where occlusion is read and made every stone
        /// look lit from inside.
        /// </para>
        /// <para>
        /// Tiling lives on the mesh UVs, not here, so one material serves a column and a wall of any size.
        /// Refreshed on every run rather than only when missing, because unlike the flat surfaces these
        /// carry no hand-tuned values, and a texture regenerated underneath would otherwise be orphaned.
        /// </para>
        /// </remarks>
        /// <param name="materialName">File name, without extension.</param>
        /// <param name="textureSet">Folder and file prefix of the set, e.g. <c>stone_tiles_02</c>.</param>
        /// <param name="tint">Multiplied into the colour map, to pull a set toward the arena's palette.</param>
        /// <returns>The material, or null when the textures or the lit shader are missing.</returns>
        public static Material GetOrCreateTexturedLit(string materialName, string textureSet, Color tint)
        {
            string folder = "Assets/_Project/Art/ThirdParty/PolyHaven/" + textureSet + "/" + textureSet;
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "_diff_1k.jpg");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "_nor_gl_1k.jpg");
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "_mask_1k.png");
            Shader shader = ResolveLitShader();

            if (albedo == null || shader == null)
            {
                Debug.LogWarning("[Adaptive Boss Arena] Texture set " + textureSet + " is missing; a flat surface stands in.");
                return GetOrCreateSurface(materialName, tint, 0f, 0.2f);
            }

            string path = $"{EditorMenus.GeneratedMaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;

            if (created)
            {
                material = new Material(shader) { name = materialName };
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", tint);

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }

            if (mask != null)
            {
                material.SetTexture("_MetallicGlossMap", mask);
                material.SetTexture("_OcclusionMap", mask);
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_OcclusionStrength", 1f);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_OCCLUSIONMAP");
            }

            if (created)
            {
                AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedMaterialFolder);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

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

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (shader == null)
            {
                Debug.LogWarning(
                    "[Adaptive Boss Arena] Could not resolve a transparent shader; attack overlays " +
                    "will not render.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader) { name = materialName };
                AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedMaterialFolder);
                AssetDatabase.CreateAsset(material, path);
            }

            // The particle shader, not the plain unlit one, because it multiplies in vertex colour: the overlay
            // mesh carries its rim-bright, faint-inside falloff that way. Reapplied to an existing asset.
            material.shader = shader;
            ConfigureParticleBlend(material, additive: false);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.SetFloat("_ColorMode", 0f);
            EditorUtility.SetDirty(material);

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
        /// Gets or creates the additive material impact sparks are drawn with.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A real asset rather than a material built at runtime, which is what it used to be. A runtime
        /// material finds its shader by name, and a build only includes shaders something in it
        /// references: nothing referenced the particle shader, so a WebGL build was free to strip it
        /// and every spark would have silently failed to render. Referenced from the scene through
        /// this asset, the shader is guaranteed to ship.
        /// </para>
        /// <para>
        /// Additive, so sparks read as light rather than as coloured paper and pick up bloom.
        /// </para>
        /// </remarks>
        /// <returns>The spark material, or null when no suitable shader exists.</returns>
        public static Material GetOrCreateImpactSparks()
        {
            const string materialName = "ImpactSparks";
            string path = EditorMenus.GeneratedMaterialFolder + "/" + materialName + ".mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                return DressSparks(existing);
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (shader == null)
            {
                Debug.LogWarning("[Adaptive Boss Arena] The URP particle shader is missing; impact sparks will not render.");
                return null;
            }

            var material = new Material(shader) { name = materialName };

            ConfigureParticleBlend(material, additive: true);
            DressSparks(material);

            AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedMaterialFolder);
            AssetDatabase.CreateAsset(material, path);

            return material;
        }

        /// <summary>Loads or creates the material blood and dust are drawn with.</summary>
        /// <remarks>
        /// Alpha-blended, unlike the sparks: blood and dust are matter that covers what is behind it. Drawn
        /// additively they glowed, which made a blow landing on a body look like a firework.
        /// </remarks>
        /// <returns>The matter material, or null when the particle shader is missing.</returns>
        public static Material GetOrCreateImpactMatter()
        {
            const string materialName = "ImpactMatter";
            string path = EditorMenus.GeneratedMaterialFolder + "/" + materialName + ".mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

                if (shader == null)
                {
                    return null;
                }

                material = new Material(shader) { name = materialName };
                ConfigureParticleBlend(material, additive: false);
                AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedMaterialFolder);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", SoftParticle());
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);

            return material;
        }

        /// <summary>Gives the spark material its round falloff and a tint hot enough to bloom.</summary>
        /// <remarks>
        /// Untextured, a spark was a stretched square; tinted at one it never reached the bloom threshold and
        /// read as orange paint. Refreshed on every run so existing assets pick the change up.
        /// </remarks>
        private static Material DressSparks(Material material)
        {
            material.SetTexture("_BaseMap", SoftParticle());
            material.SetColor("_BaseColor", new Color(2.4f, 1.5f, 0.7f, 1f));
            EditorUtility.SetDirty(material);

            return material;
        }

        /// <summary>Sets a particle material transparent, either adding light or covering what is behind.</summary>
        private static void ConfigureParticleBlend(Material material, bool additive)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 1f : 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive
                ? UnityEngine.Rendering.BlendMode.One
                : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        /// <summary>A white disc with a soft falloff, shared by every particle material.</summary>
        private static Texture2D SoftParticle()
        {
            const int Size = 64;
            const string Name = "SoftParticle";
            string path = EditorMenus.GeneratedMaterialFolder + "/" + Name + ".asset";

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = Name, wrapMode = TextureWrapMode.Clamp };

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float r = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f).magnitude;
                    float alpha = 1f - Environment.CathedralBuilder.Edge(0.25f, 1f, r);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }

            texture.Apply(true);
            AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedMaterialFolder);
            AssetDatabase.CreateAsset(texture, path);

            return texture;
        }

        /// <summary>
        /// Gets or creates the alpha-blended material ground hazards are drawn with.
        /// </summary>
        /// <remarks>
        /// An asset for the same reason as <see cref="GetOrCreateImpactSparks"/>: a shader found by
        /// name at runtime is one a build may strip. Alpha-blended rather than additive, so a scar
        /// reads as a stain darkening the floor rather than as light.
        /// </remarks>
        /// <returns>The hazard material, or null when no suitable shader exists.</returns>
        public static Material GetOrCreateHazardDisc()
        {
            const string materialName = "HazardDisc";
            string path = EditorMenus.GeneratedMaterialFolder + "/" + materialName + ".mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = CreateTransparentUnlit(materialName);

                if (material == null)
                {
                    return null;
                }

                AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedMaterialFolder);
                AssetDatabase.CreateAsset(material, path);
            }

            // A scorched crater with ember cracks rather than a flat red disc: the disc read as a UI marker lying
            // on the floor, not as ground a slam had broken. Refreshed on every run so a change reaches the asset.
            material.SetTexture("_BaseMap", ScorchCracks());
            EditorUtility.SetDirty(material);

            return material;
        }

        /// <summary>
        /// A ground scar: a scorched centre fading to its rim, split by glowing cracks radiating from the impact.
        /// </summary>
        /// <remarks>
        /// Colour carries the embers and alpha the extent, so the hazard's tint and fade still come from the
        /// zone's per-instance colour. Cracks are grown from a fixed seed, so every regeneration draws the same scar.
        /// </remarks>
        private static Texture2D ScorchCracks()
        {
            const int Size = 384;
            const string Name = "HazardScorchCracks";
            string path = EditorMenus.GeneratedMaterialFolder + "/" + Name + ".asset";

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool created = texture == null;

            if (created)
            {
                texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = Name };
            }

            texture.wrapMode = TextureWrapMode.Clamp;

            var segments = new System.Collections.Generic.List<(Vector2 A, Vector2 B, float Width)>();
            var random = new Core.Services.XorShiftRandomProvider(9173u);

            // Main cracks run outward from the impact, wandering as they go; some fork once.
            for (int crack = 0; crack < 9; crack++)
            {
                float angle = crack * (Mathf.PI * 2f / 9f) + random.NextFloat(-0.25f, 0.25f);
                Vector2 point = Vector2.zero;
                float width = random.NextFloat(0.012f, 0.02f);

                for (int step = 0; step < 11; step++)
                {
                    angle += random.NextFloat(-0.35f, 0.35f);
                    Vector2 next = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * random.NextFloat(0.06f, 0.1f);
                    segments.Add((point, next, width * (1f - step / 12f)));

                    if (step == 4 && random.NextBool(0.6f))
                    {
                        float forkAngle = angle + random.NextFloat(0.5f, 0.9f) * (random.NextBool() ? 1f : -1f);
                        Vector2 fork = next;

                        for (int f = 0; f < 5; f++)
                        {
                            forkAngle += random.NextFloat(-0.3f, 0.3f);
                            Vector2 forkNext = fork + new Vector2(Mathf.Cos(forkAngle), Mathf.Sin(forkAngle)) * 0.06f;
                            segments.Add((fork, forkNext, width * 0.6f * (1f - f / 6f)));
                            fork = forkNext;
                        }
                    }

                    point = next;
                }
            }

            var scorch = new Color(0.05f, 0.03f, 0.02f);
            var ember = new Color(1f, 0.42f, 0.1f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                    float r = p.magnitude;

                    float crackStrength = 0f;

                    foreach ((Vector2 a, Vector2 b, float width) in segments)
                    {
                        Vector2 ab = b - a;
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                        float distance = Vector2.Distance(p, a + ab * t);
                        crackStrength = Mathf.Max(crackStrength, Mathf.Exp(-(distance * distance) / (width * width)));
                    }

                    float body = 1f - Environment.CathedralBuilder.Edge(0.3f, 1f, r);
                    float cracks = crackStrength * (1f - Environment.CathedralBuilder.Edge(0.7f, 1f, r));
                    float heart = Mathf.Exp(-(r * r) / 0.02f);
                    float glow = Mathf.Max(cracks, heart * 0.8f);

                    Color colour = Color.Lerp(scorch, ember, glow);
                    colour.a = Mathf.Clamp01(Mathf.Max(body * 0.7f, glow));
                    texture.SetPixel(x, y, colour);
                }
            }

            texture.Apply(true);

            if (created)
            {
                AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedMaterialFolder);
                AssetDatabase.CreateAsset(texture, path);
            }
            else
            {
                EditorUtility.SetDirty(texture);
            }

            return texture;
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
