using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Renders every licensed clip as a strip of poses, so clips are chosen by looking at them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pack's file names say almost nothing: <c>slash</c>, <c>slash (2)</c> and <c>slash (5)</c> are a quick
    /// cut, a three-and-a-half-second string and a wide sweep, and the difference decides whether a light
    /// attack reads as light. Each clip is posed on its own character at five points and rendered from the
    /// front-left, which shows both the wind-up and the reach.
    /// </para>
    /// <para>
    /// Needs a GPU, so it runs from a batch editor started without <c>-nographics</c>. Frames are written as
    /// raw RGBA - an 8-byte width and height header, then rows - because image encoding lives in an engine
    /// module this project does not include; a script outside Unity stitches them into sheets.
    /// </para>
    /// </remarks>
    public static class ClipContactSheet
    {
        private const int Size = 256;
        private static readonly float[] Samples = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        /// <summary>Renders every clip. The output folder is the argument after <c>-contactSheetOut</c>.</summary>
        public static void Render()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError("[ART] The contact sheet needs a GPU. Run the editor without -nographics.");
                EditorApplication.Exit(2);
                return;
            }

            string[] args = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-contactSheetOut");
            string output = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetTempPath();
            Directory.CreateDirectory(output);

            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var readback = new Texture2D(Size, Size, TextureFormat.RGBA32, false);

            var cameraObject = new GameObject("ContactSheetCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.18f, 0.2f);
            camera.fieldOfView = 30f;

            var lightObject = new GameObject("ContactSheetLight");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            lightObject.transform.rotation = Quaternion.Euler(40f, 150f, 0f);

            int rendered = 0;

            try
            {
                foreach (string file in Directory.GetFiles(LicensedArtPostprocessor.AnimationFolder, "*.fbx", SearchOption.AllDirectories).OrderBy(f => f))
                {
                    string path = file.Replace('\\', '/');
                    AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                        .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

                    if (clip == null)
                    {
                        continue;
                    }

                    string set = Path.GetFileName(Path.GetDirectoryName(path));
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(LicensedArtPostprocessor.CharacterFor(path));
                    GameObject body = UnityEngine.Object.Instantiate(model);

                    try
                    {
                        float height = set == "Boss" ? 2.45f : 1.82f;
                        Transform hips = body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips);

                        for (int i = 0; i < Samples.Length; i++)
                        {
                            clip.SampleAnimation(body, Samples[i] * clip.length);

                            // Sampling a clip directly plays its root motion, which the game bakes out, so a lunge
                            // walks the body out of frame. The camera follows the hips instead: this is about the
                            // pose, not the travel. Three-quarter view from the front-left, close enough to read it.
                            var focus = new Vector3(hips.position.x, hips.position.y * 0.9f, hips.position.z);
                            cameraObject.transform.position = focus + new Vector3(-height * 1.1f, height * 0.3f, height * 1.6f);
                            cameraObject.transform.LookAt(focus);

                            camera.Render();

                            RenderTexture.active = target;
                            readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                            readback.Apply();
                            RenderTexture.active = null;

                            WriteRaw(Path.Combine(output, $"{set}__{clip.name}__{i}.raw"), readback);
                        }

                        rendered++;
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(body);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(lightObject);
                UnityEngine.Object.DestroyImmediate(readback);
                target.Release();
            }

            Debug.Log($"[ART] Contact sheet: {rendered} clips written to {output}");
        }

        private static void WriteRaw(string path, Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            var bytes = new byte[8 + pixels.Length * 4];

            BitConverter.GetBytes(texture.width).CopyTo(bytes, 0);
            BitConverter.GetBytes(texture.height).CopyTo(bytes, 4);

            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[8 + i * 4] = pixels[i].r;
                bytes[9 + i * 4] = pixels[i].g;
                bytes[10 + i * 4] = pixels[i].b;
                bytes[11 + i * 4] = pixels[i].a;
            }

            File.WriteAllBytes(path, bytes);
        }
    }
}
