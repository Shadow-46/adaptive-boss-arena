using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Builds the corrupted knight's material - charred iron with ember seams - from the Igris model's own maps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The model was authored for a stylised game shader, so its maps are not the albedo, normal and emission URP
    /// expects. Its colour map is almost entirely black, carrying only the glowing seams in blue; its "DT" map packs
    /// edge lines (red), a mask (green), a rim term (blue) and the armour's shade (alpha). Unchanged in URP Lit the
    /// knight rendered as a black silhouette with blue lines.
    /// </para>
    /// <para>
    /// So two textures are generated from them. The albedo is charred iron: the shade channel sets how dark each
    /// plate is, and the edge lines lighten worn edges, so the armour has form under the moon and the fires. The
    /// emission is the seams alone, white, so the material's emission colour decides their hue - ember rather than
    /// the original blue - and its intensity can be raised as the fight escalates without a second texture.
    /// </para>
    /// <para>
    /// Saved as texture assets rather than images, because image encoding lives in an engine module this project
    /// does not include. Everything here sits in the licensed folder, which is not in the repository.
    /// </para>
    /// </remarks>
    public static class CorruptedKnightMaterials
    {
        /// <summary>Folder the model's original maps were unpacked to.</summary>
        public const string SourceFolder = "Assets/_Project/Art/Licensed/Igris/source/Igris-Boss (Solo Leveling Arsie)/";

        /// <summary>Folder the generated maps and material are written to.</summary>
        private const string GeneratedFolder = "Assets/_Project/Art/Licensed/Igris/Generated";

        /// <summary>Size of the generated maps: the colour map's own resolution.</summary>
        private const int Size = 1024;

        /// <summary>The darkest and lightest the plate's shade channel maps to: soot to worn iron.</summary>
        private const float DarkestPlate = 0.025f;
        private const float LightestPlate = 0.2f;

        /// <summary>How much an edge line lightens the plate it crosses, as wear catching the light.</summary>
        private const float EdgeWear = 0.12f;

        /// <summary>Brightness in the colour map below which nothing glows, and at which a seam glows fully.</summary>
        private const float SeamFloor = 0.3f;
        private const float SeamFull = 0.75f;

        /// <summary>The iron's colour cast: a faint warmth, as metal that has been through fire.</summary>
        private static readonly Color IronTint = new Color(1f, 0.93f, 0.88f);

        /// <summary>The seams' opening glow, above one so it blooms. The phase raises it from here.</summary>
        /// <remarks>Deep red-orange: a brighter orange is washed toward white by the tonemapper and read as gold.</remarks>
        public static readonly Color EmberGlow = new Color(2.4f, 0.55f, 0.1f);

        /// <summary>The plume's dye: deep crimson, the one colour on the black plate besides the fire.</summary>
        private static readonly Color PlumeCrimson = new Color(0.32f, 0.02f, 0.02f);

        /// <summary>Whether the model's maps are in this checkout.</summary>
        public static bool Available => File.Exists(SourceFolder + "ShadowIgris_Body0_DT.png");

        /// <summary>Builds or refreshes the material.</summary>
        /// <returns>The material, or null when the maps are absent.</returns>
        public static Material Build()
        {
            if (!Available)
            {
                return null;
            }

            var shade = AssetDatabase.LoadAssetAtPath<Texture2D>(SourceFolder + "ShadowIgris_Body0_DT.png");
            var colour = AssetDatabase.LoadAssetAtPath<Texture2D>(SourceFolder + "ShadowIgris_Body0_CO.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(SourceFolder + "IgrisBoss_Body0_NM.png");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shade == null || colour == null || shader == null || !shade.isReadable || !colour.isReadable)
            {
                Debug.LogWarning("[Adaptive Boss Arena] The corrupted knight's maps are missing or unreadable; it keeps the default material.");
                return null;
            }

            AssetAuthoring.EnsureFolderExists(GeneratedFolder);
            float[] plume = PlumeMask();
            Texture2D albedo = SaveTexture("CorruptedKnightAlbedo", Albedo(shade, plume), true);
            Color[] seamPixels = Seams(colour, plume);
            Texture2D seams = SaveTexture("CorruptedKnightSeams", seamPixels, true);
            CrackLevels = BuildCrackLevels(seams, seamPixels, shade, plume);

            string path = GeneratedFolder + "/CorruptedKnight.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader) { name = "CorruptedKnight" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0.75f);
            material.SetFloat("_Smoothness", 0.42f);

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            material.SetTexture("_EmissionMap", seams);
            material.SetColor("_EmissionColor", EmberGlow);
            material.EnableKeyword("_EMISSION");
            // Realtime emissive, as the generated surfaces are: with no emissive flag at all, URP's material validation
            // reads the emission as switched off and strips the keyword, and the seams never glowed.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Charred iron: the shade channel's tone, lightened along the edge lines; the plume dyed crimson.</summary>
        private static Color[] Albedo(Texture2D shade, float[] plume)
        {
            var pixels = new Color[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Color s = shade.GetPixelBilinear((x + 0.5f) / Size, (y + 0.5f) / Size);
                    float tone = Mathf.Lerp(DarkestPlate, LightestPlate, s.a) + EdgeWear * s.r;
                    Color c = Color.Lerp(IronTint * tone, PlumeCrimson * (0.6f + tone * 2f), plume[y * Size + x]);
                    c.a = 1f;
                    pixels[y * Size + x] = c;
                }
            }

            return pixels;
        }

        /// <summary>The glowing seams alone, as white, so the material's emission colour decides their hue.</summary>
        private static Color[] Seams(Texture2D colour, float[] plume)
        {
            var pixels = new Color[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Color c = colour.GetPixelBilinear((x + 0.5f) / Size, (y + 0.5f) / Size);

                    // The seams are the map's bright strokes; its alpha is nearly empty, so it is not used. Below the
                    // lower edge is the faint blue the plates are washed with, which must stay dark.
                    float heat = Ramp(SeamFloor, SeamFull, Mathf.Max(c.r, Mathf.Max(c.g, c.b))) * (1f - plume[y * Size + x]);
                    pixels[y * Size + x] = new Color(heat, heat, heat, 1f);
                }
            }

            return pixels;
        }

        /// <summary>
        /// Where on the maps the plume lies: every triangle the plume's bones carry, filled in texture space.
        /// </summary>
        /// <remarks>
        /// The plume shares the maps with the armour and was painted as one of the bright glowing panels, so as a seam
        /// it burned as a pale ponytail. It is found through the skeleton rather than painted by hand: the triangles
        /// weighted to the "Ponytail" bones are the plume, wherever the maps happen to put them.
        /// </remarks>
        /// <returns>A Size x Size mask, one where the plume is.</returns>
        private static float[] PlumeMask()
        {
            var mask = new float[Size * Size];
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(LicensedArtPostprocessor.CorruptedKnightBody);
            SkinnedMeshRenderer body = model != null ? FindBody(model) : null;

            if (body == null || body.sharedMesh == null)
            {
                return mask;
            }

            Mesh mesh = body.sharedMesh;
            Vector2[] uvs = mesh.uv;
            BoneWeight[] weights = mesh.boneWeights;
            Transform[] bones = body.bones;
            int[] triangles = mesh.triangles;

            for (int t = 0; t < triangles.Length; t += 3)
            {
                if (IsPlume(weights, bones, triangles[t]) && IsPlume(weights, bones, triangles[t + 1]) &&
                    IsPlume(weights, bones, triangles[t + 2]))
                {
                    Fill(mask, uvs[triangles[t]], uvs[triangles[t + 1]], uvs[triangles[t + 2]]);
                }
            }

            return mask;
        }

        private static SkinnedMeshRenderer FindBody(GameObject model)
        {
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!SpareMeshes.IsSpare(renderer.name))
                {
                    return renderer;
                }
            }

            return null;
        }

        /// <summary>Whether a vertex is carried mostly by the plume's bones.</summary>
        private static bool IsPlume(BoneWeight[] weights, Transform[] bones, int vertex)
        {
            if (vertex >= weights.Length)
            {
                return false;
            }

            BoneWeight w = weights[vertex];
            float plume = PlumeWeight(bones, w.boneIndex0, w.weight0) + PlumeWeight(bones, w.boneIndex1, w.weight1) +
                          PlumeWeight(bones, w.boneIndex2, w.weight2) + PlumeWeight(bones, w.boneIndex3, w.weight3);

            return plume > 0.5f;
        }

        private static float PlumeWeight(Transform[] bones, int index, float weight) =>
            index >= 0 && index < bones.Length && bones[index] != null && bones[index].name.Contains("Ponytail") ? weight : 0f;

        /// <summary>Fills a texture-space triangle into the mask.</summary>
        private static void Fill(float[] mask, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 pa = Wrap(a) * Size, pb = Wrap(b) * Size, pc = Wrap(c) * Size;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))) - 1);
            int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))) + 1);
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))) - 1);
            int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))) + 1);
            float area = Cross(pb - pa, pc - pa);

            if (Mathf.Abs(area) < 1e-6f)
            {
                return;
            }

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Cross(pc - pb, p - pb) / area, w1 = Cross(pa - pc, p - pc) / area, w2 = Cross(pb - pa, p - pa) / area;

                    // A pixel's width of margin, so the plume's edge does not keep a rim of glow from the panel beneath.
                    const float Margin = -0.02f;

                    if (w0 >= Margin && w1 >= Margin && w2 >= Margin)
                    {
                        mask[y * Size + x] = 1f;
                    }
                }
            }
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static Vector2 Wrap(Vector2 uv) => new Vector2(uv.x - Mathf.Floor(uv.x), uv.y - Mathf.Floor(uv.y));

        /// <summary>
        /// The glow map for each phase, from the seams alone to plate split open with fire. Set by <see cref="Build"/>.
        /// </summary>
        public static Texture2D[] CrackLevels { get; private set; } = new Texture2D[0];

        /// <summary>Loads the glow maps <see cref="Build"/> wrote, whichever session wrote them.</summary>
        /// <returns>One per phase, or an empty array when they have not been generated.</returns>
        public static Texture2D[] LoadCrackLevels()
        {
            var levels = new Texture2D[CrackWidthByPhase.Length];

            for (int level = 0; level < levels.Length; level++)
            {
                string name = level == 0 ? "CorruptedKnightSeams" : "CorruptedKnightCracks" + level;
                levels[level] = AssetDatabase.LoadAssetAtPath<Texture2D>(GeneratedFolder + "/" + name + ".asset");

                if (levels[level] == null)
                {
                    return new Texture2D[0];
                }
            }

            return levels;
        }

        /// <summary>
        /// How wide the cracks have opened in each phase, as a fraction of the noise's ridge: none, hairlines, open,
        /// and in the Last Stand a web across every plate.
        /// </summary>
        private static readonly float[] CrackWidthByPhase = { 0f, 0.035f, 0.07f, 0.12f };

        /// <summary>A crack's glow against a seam's: a little dimmer, so the original seams still lead the eye.</summary>
        private const float CrackGlow = 0.85f;

        /// <summary>The shade below which a texel is not plate - the gaps between plates, where nothing cracks.</summary>
        private const float PlateShade = 0.15f;

        /// <summary>
        /// Builds a glow map per phase: the seams, plus cracks that widen phase by phase across the plate.
        /// </summary>
        /// <remarks>
        /// The cracks follow ridged Perlin noise in texture space - the lines where the noise crosses its middle -
        /// which run as long meandering fractures rather than speckle. One pattern, widened each phase, so the same
        /// cracks open further rather than new ones appearing at random. Never on the plume, and only on plate.
        /// </remarks>
        private static Texture2D[] BuildCrackLevels(Texture2D seamMap, Color[] seams, Texture2D shade, float[] plume)
        {
            var levels = new Texture2D[CrackWidthByPhase.Length];
            levels[0] = seamMap;

            var ridge = new float[Size * Size];
            var plate = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                    float n = 0.65f * Mathf.PerlinNoise(u * 6f + 11.3f, v * 6f + 4.7f) +
                              0.35f * Mathf.PerlinNoise(u * 13f + 2.1f, v * 13f + 9.4f);
                    ridge[y * Size + x] = 1f - Mathf.Abs(2f * n - 1f);
                    plate[y * Size + x] = shade.GetPixelBilinear(u, v).a > PlateShade ? 1f - plume[y * Size + x] : 0f;
                }
            }

            for (int level = 1; level < levels.Length; level++)
            {
                float width = CrackWidthByPhase[level];
                var pixels = new Color[Size * Size];

                for (int i = 0; i < pixels.Length; i++)
                {
                    float crack = Ramp(1f - width, 1f - width * 0.3f, ridge[i]) * plate[i] * CrackGlow;
                    float heat = Mathf.Max(seams[i].r, crack);
                    pixels[i] = new Color(heat, heat, heat, 1f);
                }

                levels[level] = SaveTexture("CorruptedKnightCracks" + level, pixels, true);
            }

            return levels;
        }

        /// <summary>A smooth 0-to-1 ramp as a value crosses from one edge to the other.</summary>
        /// <remarks>Not <see cref="Mathf.SmoothStep"/> on the value, whose arguments are the ends, not edges.</remarks>
        private static float Ramp(float edge0, float edge1, float x) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, x));

        private static Texture2D SaveTexture(string name, Color[] pixels, bool mips)
        {
            string path = GeneratedFolder + "/" + name + ".asset";
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mips) { name = name, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(pixels);
            texture.Apply(mips);

            // Compressed: uncompressed, each map is four megabytes the browser must download and hold.
            EditorUtility.CompressTexture(texture, TextureFormat.DXT1, TextureCompressionQuality.Normal);

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (existing != null)
            {
                EditorUtility.CopySerialized(texture, existing);
                Object.DestroyImmediate(texture);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }
    }
}
