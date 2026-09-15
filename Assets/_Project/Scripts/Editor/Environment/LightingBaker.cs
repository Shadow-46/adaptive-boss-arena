using System.Collections.Generic;
using System.Linq;
using AdaptiveBossArena.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdaptiveBossArena.Editor.Environment
{
    /// <summary>
    /// Bakes the cathedral's reflections and bounce light, and re-links what was baked whenever the scene is rebuilt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two things are baked. The reflection probe, so the knight's plate and the brute's shoulders reflect
    /// the stone and the sky rather than nothing. And a grid of light probes, so a fighter standing on a
    /// sunlit floor picks up the warm light bouncing off it - the difference between a body lit and a body
    /// placed in a room.
    /// </para>
    /// <para>
    /// Baking needs a GPU, and the setup runs headless without one, so it is its own step:
    /// <see cref="BakeArenaLighting"/>, run from the menu or from a batch editor started without
    /// <c>-nographics</c>. The scene itself is rebuilt from code on every setup, which would drop the baked
    /// data each time, so <see cref="PrepareScene"/> lays out the probes identically and re-links whatever
    /// has already been baked. A setup on a machine with no GPU therefore keeps the last bake.
    /// </para>
    /// <para>
    /// Lights are Mixed in Baked Indirect mode: direct light and shadows stay realtime, so the phase
    /// colours still reach every surface directly, and only the bounce is baked.
    /// </para>
    /// </remarks>
    public static class LightingBaker
    {
        /// <summary>Folder the lighting settings and baked reflection live in.</summary>
        public const string LightingFolder = "Assets/_Project/Lighting";

        /// <summary>The baked reflection cubemap.</summary>
        public const string ReflectionPath = LightingFolder + "/ArenaReflection.exr";

        private const string SettingsPath = LightingFolder + "/ArenaLighting.lighting";

        /// <summary>The lighting data Unity writes beside the scene when it bakes.</summary>
        private const string LightingDataPath = EditorMenus.GeneratedSceneFolder + "/Arena/LightingData.asset";

        /// <summary>Spacing of the probe grid over the fighting floor, in metres.</summary>
        private const float ProbeSpacing = 4f;

        /// <summary>Probe heights: at a fighter's knees and at head height, which is where bodies sample.</summary>
        private static readonly float[] ProbeHeights = { 0.6f, 2.4f };

        /// <summary>Lays the probe grid out, sets the lights and re-links any earlier bake. Called by the scene builder.</summary>
        /// <param name="config">Arena dimensions.</param>
        public static void PrepareScene(ArenaConfig config)
        {
            Lightmapping.lightingSettings = LoadOrCreateSettings();

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                light.lightmapBakeType = LightmapBakeType.Mixed;
            }

            // Stone contributes bounce light to the bake, but is not lit by the probes afterwards. The floor lies
            // below the probe grid and the walls and columns beyond it, where the interpolated probes are about
            // two and a half times darker than the room's ambient light - measured - and letting the stone sample
            // them turned the whole ruin dark. Probes are for the fighters, who stand inside the grid.
            GameObject cathedral = GameObject.Find(CathedralBuilder.RootName);

            if (cathedral != null)
            {
                foreach (MeshRenderer renderer in cathedral.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.sharedMaterial != null && renderer.sharedMaterial.renderQueue >= (int)RenderQueue.Transparent)
                    {
                        continue;
                    }

                    GameObject stone = renderer.gameObject;
                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(stone);
                    GameObjectUtility.SetStaticEditorFlags(stone, flags | StaticEditorFlags.ContributeGI);
                    renderer.receiveGI = ReceiveGI.LightProbes;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                }
            }

            var probes = new GameObject("Light Probes").AddComponent<LightProbeGroup>();
            probes.probePositions = ProbeGrid(config.Radius).ToArray();

            RelinkReflection();

            var data = AssetDatabase.LoadAssetAtPath<LightingDataAsset>(LightingDataPath);

            if (data != null)
            {
                Lightmapping.lightingDataAsset = data;
            }
        }

        /// <summary>Bakes the arena's light probes and reflection probe. Needs a GPU.</summary>
        [MenuItem(EditorMenus.Setup + "4. Bake Arena Lighting", priority = EditorMenus.SetupPriorityBuildScene + 1)]
        public static void BakeArenaLighting()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError("[Adaptive Boss Arena] Baking lighting needs a GPU. Run the editor without -nographics.");

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(2);
                }

                return;
            }

            EditorSceneManager.OpenScene(EditorMenus.ArenaScenePath, OpenSceneMode.Single);

            if (!Lightmapping.Bake())
            {
                Debug.LogError("[Adaptive Boss Arena] The light probe bake failed.");
                return;
            }

            ReflectionProbe probe = Object.FindAnyObjectByType<ReflectionProbe>();

            if (probe != null)
            {
                AssetAuthoring.EnsureFolderExists(LightingFolder);

                probe.mode = ReflectionProbeMode.Baked;

                if (!Lightmapping.BakeReflectionProbe(probe, ReflectionPath))
                {
                    Debug.LogError("[Adaptive Boss Arena] The reflection probe bake failed.");
                    return;
                }

                AssetDatabase.ImportAsset(ReflectionPath);
                RelinkReflection();
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            int probeCount = LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0;
            Debug.Log($"[Adaptive Boss Arena] Arena lighting baked: {probeCount} light probes, reflection at '{ReflectionPath}'.");
        }

        /// <summary>The probe positions: a square grid clipped to the fighting floor, at each sampling height.</summary>
        /// <param name="radius">Radius of the fighting floor.</param>
        /// <returns>World positions, identical on every call.</returns>
        public static IEnumerable<Vector3> ProbeGrid(float radius)
        {
            int steps = Mathf.CeilToInt(radius / ProbeSpacing);

            foreach (float height in ProbeHeights)
            {
                for (int x = -steps; x <= steps; x++)
                {
                    for (int z = -steps; z <= steps; z++)
                    {
                        var point = new Vector3(x * ProbeSpacing, height, z * ProbeSpacing);

                        if (point.x * point.x + point.z * point.z <= radius * radius)
                        {
                            yield return point;
                        }
                    }
                }
            }
        }

        /// <summary>Points the reflection probe at the baked cubemap, if one exists.</summary>
        private static void RelinkReflection()
        {
            var cubemap = AssetDatabase.LoadAssetAtPath<Texture>(ReflectionPath);
            ReflectionProbe probe = Object.FindAnyObjectByType<ReflectionProbe>();

            if (cubemap == null || probe == null)
            {
                return;
            }

            // Custom rather than Baked: a Baked probe's texture lives in the scene's lighting data, which the
            // scene rebuild discards. A custom texture is an ordinary asset reference and survives it.
            probe.mode = ReflectionProbeMode.Custom;
            probe.customBakedTexture = cubemap;
        }

        private static LightingSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);

            if (settings == null)
            {
                settings = new LightingSettings { name = "ArenaLighting" };
                AssetAuthoring.EnsureFolderExists(LightingFolder);
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
            settings.indirectSampleCount = 256;
            settings.maxBounces = 2;
            settings.autoGenerate = false;
            EditorUtility.SetDirty(settings);

            return settings;
        }
    }
}
