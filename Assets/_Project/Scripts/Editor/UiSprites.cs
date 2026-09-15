using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Sprites the interface is drawn with, generated rather than imported.
    /// </summary>
    public static class UiSprites
    {
        private const string Folder = "Assets/_Project/Materials/UI";

        /// <summary>
        /// Vertical shading for a gauge's fill: a thin lit edge on top, darkening toward the bottom.
        /// </summary>
        /// <remarks>
        /// A flat colour read as a coloured rectangle; shaded, the same fill reads as a filled channel. White,
        /// so each gauge's own colour tints it.
        /// </remarks>
        /// <returns>The sprite asset.</returns>
        public static Sprite GaugeShading()
        {
            const int Height = 32;
            string path = Folder + "/GaugeShading.asset";

            Sprite existing = LoadSprite(path);

            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(4, Height, TextureFormat.RGBA32, false)
            {
                name = "GaugeShadingTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < Height; y++)
            {
                float up = (y + 0.5f) / Height;
                float shade = Mathf.Lerp(0.55f, 1f, up);
                float lit = up > 0.84f && up < 0.94f ? 0.35f : 0f;
                float value = Mathf.Clamp01(shade + lit);

                for (int x = 0; x < 4; x++)
                {
                    texture.SetPixel(x, y, new Color(value, value, value, 1f));
                }
            }

            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, Height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "GaugeShading";

            // The texture is the main asset and the sprite rides inside it, so the sprite's reference to its
            // texture is to a saved object rather than to one that existed only in memory.
            AssetAuthoring.EnsureFolderExists(Folder);
            AssetDatabase.CreateAsset(texture, path);
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.SaveAssets();

            return LoadSprite(path);
        }

        /// <summary>A soft white glow fading from the centre to nothing at the edges, tinted by the image using it.</summary>
        /// <returns>The sprite asset.</returns>
        public static Sprite RadialGlow()
        {
            const int Size = 256;
            string path = Folder + "/RadialGlow.asset";

            Sprite existing = LoadSprite(path);

            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "RadialGlowTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f) / Size * 2f - 1f, dy = (y + 0.5f) / Size * 2f - 1f;
                    float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, falloff * falloff * falloff));
                }
            }

            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "RadialGlow";

            AssetAuthoring.EnsureFolderExists(Folder);
            AssetDatabase.CreateAsset(texture, path);
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.SaveAssets();

            return LoadSprite(path);
        }

        private static Sprite LoadSprite(string path)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                {
                    return sprite;
                }
            }

            return null;
        }
    }
}
