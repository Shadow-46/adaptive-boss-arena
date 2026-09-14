using UnityEditor;

namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Decides how the third-party stone and brick textures import.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A texture's import type lives in its .meta file, and the defaults are wrong for two of the three
    /// maps in every set. A normal map imported as colour lights every surface as if it were bent the
    /// wrong way; a mask imported as sRGB crushes its occlusion and smoothness toward black, which reads
    /// as stone that is inexplicably wet and dark. Neither fails a test. Setting the import here keeps
    /// it correct however the .meta is lost or regenerated.
    /// </para>
    /// <para>
    /// Capped at 1K on every platform. The arena is seen from a few metres away at most, and the WebGL
    /// download is the budget that matters.
    /// </para>
    /// </remarks>
    public sealed class SurfaceTexturePostprocessor : AssetPostprocessor
    {
        /// <summary>Folder whose textures this postprocessor owns.</summary>
        public const string SurfaceFolder = "Assets/_Project/Art/ThirdParty/PolyHaven/";

        private const int MaximumSize = 1024;

        /// <summary>Bumped whenever the import rules change, so textures already imported pick them up.</summary>
        /// <returns>The version of these import rules.</returns>
        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SurfaceFolder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;

            importer.maxTextureSize = MaximumSize;
            importer.mipmapEnabled = true;
            importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            importer.anisoLevel = 4;

            if (assetPath.Contains("_nor_gl"))
            {
                // Poly Haven's _gl maps are the OpenGL convention, which is the one Unity expects.
                importer.textureType = TextureImporterType.NormalMap;
                return;
            }

            importer.textureType = TextureImporterType.Default;

            // Colour is sRGB; the mask holds data - metallic, occlusion, smoothness - and must stay linear.
            importer.sRGBTexture = !assetPath.Contains("_mask");
            importer.alphaIsTransparency = false;
        }
    }
}
