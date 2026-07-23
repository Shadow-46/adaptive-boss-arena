using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Runs the full first-time setup in the order the steps depend on one another.
    /// </summary>
    /// <remarks>
    /// The three setup commands must run in sequence: the scene builder needs the configuration
    /// assets, and those assets need the project's layers to exist. Exposing a single command
    /// removes the chance of running them out of order and getting a scene with objects on the
    /// default layer that then never collide correctly.
    /// </remarks>
    public static class ProjectSetupRunner
    {
        /// <summary>Runs project configuration, asset generation and scene building in order.</summary>
        [MenuItem(EditorMenus.Setup + "Run Full Setup", priority = EditorMenus.SetupPriorityRunAll)]
        public static void RunFullSetup()
        {
            // A modal dialog cannot be answered on a build machine, so batch runs proceed directly.
            // Anyone invoking this from the command line has already made the decision the dialog
            // asks about.
            if (!Application.isBatchMode)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Adaptive Boss Arena Setup",
                    "This will configure project layers and physics, generate configuration assets, " +
                    "input bindings and both character prefabs, then build the arena scene.\n\n" +
                    "Existing assets are kept. The current scene will be replaced.",
                    "Run Setup",
                    "Cancel");

                if (!proceed)
                {
                    return;
                }
            }

            // Order is a hard dependency chain: assets need layers, the prefab needs both the
            // configuration assets and the input bindings, and the scene needs the prefab.
            ProjectConfigurator.ConfigureProject();

            // Must precede material creation: without an assigned pipeline the shader lookup falls
            // back to the built-in renderer and every generated surface renders magenta.
            RenderPipelineConfigurator.ConfigureRenderPipeline();
            PostProcessingConfigurator.ConfigurePostProcessing();

            DefaultAssetGenerator.GenerateDefaultAssets();
            InputActionsGenerator.GenerateInputActions();
            PlayerPrefabBuilder.GeneratePlayerPrefab();
            BossPrefabBuilder.GenerateBossPrefab();
            ArenaSceneBuilder.BuildArenaScene();

            // Built last so its explicit build-order write — title first, arena second — is the one
            // that stands, rather than being appended after the arena.
            MainMenuSceneBuilder.BuildMainMenuScene();

            IReadOnlyList<string> violations = ArchitectureValidator.FindViolations();

            foreach (string violation in violations)
            {
                Debug.LogError($"[Adaptive Boss Arena] Architecture violation: {violation}");
            }

            // Runs as part of setup because a dangling attack reference produces a game that starts,
            // compiles and passes its tests while being completely unplayable.
            IReadOnlyList<string> broken = AssetIntegrityValidator.FindBrokenReferences();

            foreach (string problem in broken)
            {
                Debug.LogError($"[Adaptive Boss Arena] Broken reference: {problem}");
            }

            if (broken.Count == 0 && violations.Count == 0)
            {
                Debug.Log("[Adaptive Boss Arena] Setup complete. Open the Arena scene and press Play.");
            }
        }
    }
}
