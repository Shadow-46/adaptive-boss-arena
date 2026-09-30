using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Builds the licensed characters' own textured materials for URP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mixamo embeds each character's diffuse, normal and specular maps in its FBX, and Unity imports them as
    /// nothing until they are extracted. The imported materials also target the built-in renderer, so the
    /// characters rendered white until the generator painted them a flat colour. Here the maps are extracted
    /// once beside the characters and wired into URP Lit in the specular workflow, which is how Mixamo's
    /// specular maps are authored.
    /// </para>
    /// <para>
    /// Everything this writes lives in the licensed folder, which is not in the repository, because the
    /// textures are Mixamo's. It is rebuilt by setup wherever the licensed art is present.
    /// </para>
    /// </remarks>
    public static class LicensedArtMaterials
    {
        /// <summary>Folder the extracted textures are written to.</summary>
        public const string TextureFolder = LicensedArtPostprocessor.MixamoFolder + "Characters/Textures";

        private const string MaterialFolder = LicensedArtPostprocessor.MixamoFolder + "Materials";

        /// <summary>The knight's plate: polished steel with its own normal detail.</summary>
        /// <remarks>
        /// The Paladin's specular map averages a quarter reflectance, which is what a painted surface reflects, not
        /// steel; over its dark bronze diffuse the plate rendered as dull brown cloth in the moonlit nave. Scaled up
        /// and made glossier, the plate catches the moon, the fires and the character light as metal does. Not as far
        /// as 2.5: under the character light that turned the helm and knees into white spots, and the plate glowed.
        /// </remarks>
        /// <returns>The material, or null when the licensed art is absent.</returns>
        public static Material Knight() =>
            Build("KnightBody", LicensedArtPostprocessor.KnightCharacter, "Paladin", smoothness: 0.6f, specularScale: 1.8f);

        /// <summary>The brute's hide: rough, with the specular map picking out horn and armour.</summary>
        /// <returns>The material, or null when the licensed art is absent.</returns>
        public static Material Brute() =>
            Build("BruteBody", LicensedArtPostprocessor.BossCharacter, "bear", smoothness: 0.35f);

        private static Material Build(string name, string character, string texturePrefix, float smoothness, float specularScale = 1f)
        {
            if (!LicensedArtPostprocessor.Available)
            {
                return null;
            }

            string diffusePath = $"{TextureFolder}/{texturePrefix}_diffuse.png";

            if (!File.Exists(diffusePath))
            {
                ExtractTextures(character);
            }

            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{texturePrefix}_normal.png");
            var specular = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{texturePrefix}_specular.png");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (diffuse == null || shader == null)
            {
                Debug.LogWarning("[Adaptive Boss Arena] Could not build " + name + "; its textures or the URP Lit shader are missing.");
                return null;
            }

            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;

            if (created)
            {
                material = new Material(shader) { name = name };
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", diffuse);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", smoothness);

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            if (specular != null)
            {
                material.SetFloat("_WorkflowMode", 0f);
                material.SetTexture("_SpecGlossMap", specular);
                material.SetColor("_SpecColor", new Color(specularScale, specularScale, specularScale, 1f));
                material.EnableKeyword("_SPECULAR_SETUP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            if (created)
            {
                AssetAuthoring.EnsureFolderExists(MaterialFolder);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static void ExtractTextures(string character)
        {
            AssetAuthoring.EnsureFolderExists(TextureFolder);

            var importer = (ModelImporter)AssetImporter.GetAtPath(character);

            if (importer == null || !importer.ExtractTextures(TextureFolder))
            {
                Debug.LogWarning("[Adaptive Boss Arena] Could not extract the textures embedded in " + character + ".");
            }

            AssetDatabase.Refresh();
        }
    }
}
