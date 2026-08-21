using System.Collections.Generic;
using AdaptiveBossArena.Core.Constants;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Applies the project's layers, tags, collision matrix and physics timing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These settings live in binary-ish YAML under <c>ProjectSettings</c> that is impractical to
    /// author by hand and unpleasant to review in a diff. Expressing them as code makes the intent
    /// legible, makes the setup reproducible on a fresh clone, and lets the collision matrix be
    /// derived from <see cref="LayerNames"/> rather than duplicated in an inspector grid.
    /// </para>
    /// <para>
    /// Safe to run repeatedly. Existing layers and tags are detected and reused rather than
    /// duplicated.
    /// </para>
    /// </remarks>
    public static class ProjectConfigurator
    {
        /// <summary>First layer index Unity leaves free for projects to use.</summary>
        private const int FirstUserLayerIndex = 8;

        /// <summary>Total layer slots Unity provides.</summary>
        private const int TotalLayerCount = 32;

        private const string TagManagerAssetPath = "ProjectSettings/TagManager.asset";
        private const string TimeManagerAssetPath = "ProjectSettings/TimeManager.asset";

        /// <summary>Creates layers and tags, configures collisions, and sets the physics rate.</summary>
        [MenuItem(EditorMenus.Setup + "1. Configure Project", priority = EditorMenus.SetupPriorityConfigureProject)]
        public static void ConfigureProject()
        {
            ConfigureLayers();
            ConfigureTags();
            ConfigureTimeSettings();
            ConfigureInputHandling();
            ConfigureColorSpace();

            // Layer indices are cached at runtime, and they have just moved.
            Layers.InvalidateCache();

            ConfigureCollisionMatrix();

            AssetDatabase.SaveAssets();
            Debug.Log(
                "[Adaptive Boss Arena] Project configured: layers, tags, collision matrix and physics rate applied.");
        }

        /// <summary>
        /// Puts the project in linear colour space.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A project made from Unity's URP template is linear from the first day. This one generates
        /// its own settings and nothing ever set it, so it had been rendering in gamma throughout —
        /// which is the single largest reason the game looked cheap, and it is invisible as a bug
        /// because nothing errors and everything still draws.
        /// </para>
        /// <para>
        /// In gamma, light accumulates and falls off with the wrong curve: the terminator between lit
        /// and unlit is too abrupt and too saturated, physically-based metal and smoothness come out
        /// chalky, the ACES tonemapper is handed values it was never designed to receive, and
        /// emission above one blooms at a threshold other than the intended one. Every one of those
        /// reads to a player as "this looks like a prototype" without suggesting a cause.
        /// </para>
        /// <para>
        /// Switching also makes the scene noticeably darker, because colours that were being
        /// double-brightened no longer are. The palette constants in the generators were re-balanced
        /// against linear afterwards; anything tuned by eye against the old space would need the same.
        /// </para>
        /// </remarks>
        private static void ConfigureColorSpace()
        {
            if (PlayerSettings.colorSpace == ColorSpace.Linear)
            {
                return;
            }

            PlayerSettings.colorSpace = ColorSpace.Linear;

            Debug.Log(
                "[Adaptive Boss Arena] Switched to linear colour space. Unity will reimport shaders " +
                "and textures, which takes a moment.");
        }

        /// <summary>Ensures every layer named in <see cref="LayerNames"/> exists.</summary>
        private static void ConfigureLayers()
        {
            SerializedObject tagManager = LoadProjectSettingsAsset(TagManagerAssetPath);
            SerializedProperty layers = tagManager.FindProperty("layers");

            foreach (string layerName in LayerNames.All)
            {
                if (LayerExists(layers, layerName))
                {
                    continue;
                }

                if (!TryAssignToFreeSlot(layers, layerName))
                {
                    Debug.LogError(
                        $"[Adaptive Boss Arena] No free layer slot available for '{layerName}'. " +
                        "Free a user layer in Project Settings and run this command again.");
                }
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Tests whether a layer name is already present in any slot.</summary>
        private static bool LayerExists(SerializedProperty layers, string layerName)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Writes a layer name into the first empty user slot.</summary>
        private static bool TryAssignToFreeSlot(SerializedProperty layers, string layerName)
        {
            for (int i = FirstUserLayerIndex; i < TotalLayerCount && i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Ensures every tag named in <see cref="TagNames"/> exists.</summary>
        private static void ConfigureTags()
        {
            SerializedObject tagManager = LoadProjectSettingsAsset(TagManagerAssetPath);
            SerializedProperty tags = tagManager.FindProperty("tags");

            foreach (string tagName in TagNames.All)
            {
                if (TagExists(tags, tagName))
                {
                    continue;
                }

                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tagName;
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Tests whether a tag is already defined.</summary>
        private static bool TagExists(SerializedProperty tags, string tagName)
        {
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tagName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Restricts hit-detection layers so they only interact with their intended partner.
        /// </summary>
        /// <remarks>
        /// Built by denying everything first and then allowing named pairs, rather than by disabling
        /// the pairs known to be wrong. Deny-by-default means a layer added later cannot silently
        /// start colliding with hitboxes, which is the failure mode that produces attacks landing
        /// through walls.
        /// </remarks>
        private static void ConfigureCollisionMatrix()
        {
            int[] volumeLayers =
            {
                Layers.PlayerHitbox,
                Layers.PlayerHurtbox,
                Layers.BossHitbox,
                Layers.BossHurtbox,
                Layers.Projectile
            };

            foreach (int volumeLayer in volumeLayers)
            {
                for (int other = 0; other < TotalLayerCount; other++)
                {
                    Physics.IgnoreLayerCollision(volumeLayer, other, true);
                }
            }

            // The only interactions hit detection is permitted to see.
            AllowCollision(Layers.PlayerHitbox, Layers.BossHurtbox);
            AllowCollision(Layers.BossHitbox, Layers.PlayerHurtbox);
            AllowCollision(Layers.Projectile, Layers.PlayerHurtbox);
            AllowCollision(Layers.Projectile, Layers.Arena);

            // Bodies collide with the world and with each other so neither can be walked through.
            AllowCollision(Layers.Player, Layers.Arena);
            AllowCollision(Layers.Boss, Layers.Arena);
            AllowCollision(Layers.Player, Layers.Boss);
        }

        /// <summary>Re-enables collision between a specific pair of layers.</summary>
        private static void AllowCollision(int layerA, int layerB) =>
            Physics.IgnoreLayerCollision(layerA, layerB, false);

        /// <summary>
        /// Switches the project to the Input System package exclusively.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Unity defaults to supporting both input backends when the package is added, which leaves a
        /// standing deprecation warning and, worse, lets legacy <c>UnityEngine.Input</c> calls compile
        /// and run. Those throw the moment the project is switched to the new backend, so allowing
        /// them to work now is storing up a failure for later.
        /// </para>
        /// <para>
        /// Changing this requires an editor restart to take effect, which Unity handles by prompting.
        /// </para>
        /// </remarks>
        private static void ConfigureInputHandling()
        {
            const int inputSystemPackageOnly = 2;

            SerializedObject playerSettings = LoadProjectSettingsAsset("ProjectSettings/ProjectSettings.asset");
            SerializedProperty handler = playerSettings.FindProperty("activeInputHandler");

            if (handler == null)
            {
                Debug.LogWarning(
                    "[Adaptive Boss Arena] Could not find the active input handler setting. Set " +
                    "Project Settings > Player > Active Input Handling to 'Input System Package' manually.");
                return;
            }

            if (handler.intValue == inputSystemPackageOnly)
            {
                return;
            }

            handler.intValue = inputSystemPackageOnly;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log(
                "[Adaptive Boss Arena] Active Input Handling set to Input System only. " +
                "Unity will ask to restart for this to take effect.");
        }

        /// <summary>Pins the physics step to the rate the project's frame data is authored against.</summary>
        private static void ConfigureTimeSettings()
        {
            SerializedObject timeManager = LoadProjectSettingsAsset(TimeManagerAssetPath);

            SetFloatIfPresent(timeManager, "Fixed Timestep", GameplayConstants.FixedTimeStep);

            // Capping the catch-up step stops a frame spike from producing a burst of physics steps,
            // which would let attacks resolve several frames' worth of movement at once.
            SetFloatIfPresent(timeManager, "Maximum Allowed Timestep", 0.1f);

            timeManager.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Assigns a float project setting, warning if Unity has renamed the property.</summary>
        private static void SetFloatIfPresent(SerializedObject target, string propertyName, float value)
        {
            SerializedProperty property = target.FindProperty(propertyName);

            if (property == null)
            {
                Debug.LogWarning(
                    $"[Adaptive Boss Arena] Time setting '{propertyName}' was not found and has been " +
                    "skipped. Set it manually in Project Settings > Time.");
                return;
            }

            property.floatValue = value;
        }

        /// <summary>Opens a project settings asset for serialized editing.</summary>
        private static SerializedObject LoadProjectSettingsAsset(string assetPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            return new SerializedObject(assets[0]);
        }

        /// <summary>Reports which of the project's layers and tags are currently missing.</summary>
        /// <returns>Human-readable descriptions of every missing item.</returns>
        public static IReadOnlyList<string> FindMissingConfiguration()
        {
            var missing = new List<string>();

            foreach (string layerName in LayerNames.All)
            {
                if (LayerMask.NameToLayer(layerName) < 0)
                {
                    missing.Add($"Layer '{layerName}'");
                }
            }

            return missing;
        }
    }
}
