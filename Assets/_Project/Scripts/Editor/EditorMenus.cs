namespace AdaptiveBossArena.Editor
{
    /// <summary>Menu paths and asset locations shared by the project's editor tooling.</summary>
    /// <remarks>
    /// Centralised so that the ordering of the setup commands, which must be run in sequence, is
    /// expressed once and stays consistent as tooling is added.
    /// </remarks>
    internal static class EditorMenus
    {
        /// <summary>Root menu all project tooling lives under.</summary>
        public const string Root = "Adaptive Boss Arena/";

        /// <summary>Submenu for one-time project setup commands.</summary>
        public const string Setup = Root + "Setup/";

        /// <summary>Submenu for validation commands.</summary>
        public const string Validate = Root + "Validate/";

        /// <summary>Priority of the first setup command, establishing the run order.</summary>
        public const int SetupPriorityConfigureProject = 0;

        /// <summary>Priority of the asset generation command.</summary>
        public const int SetupPriorityGenerateAssets = 1;

        /// <summary>Priority of the scene building command.</summary>
        public const int SetupPriorityBuildScene = 2;

        /// <summary>Priority of the combined run-everything command.</summary>
        public const int SetupPriorityRunAll = 20;

        /// <summary>Folder generated configuration assets are written to.</summary>
        public const string GeneratedAssetFolder = "Assets/_Project/ScriptableObjects";

        /// <summary>Folder generated materials are written to.</summary>
        public const string GeneratedMaterialFolder = "Assets/_Project/Materials";

        /// <summary>Folder generated scenes are written to.</summary>
        public const string GeneratedSceneFolder = "Assets/_Project/Scenes";

        /// <summary>Path of the arena scene the builder produces.</summary>
        public const string ArenaScenePath = GeneratedSceneFolder + "/Arena.unity";

        /// <summary>Path of the title scene the builder produces.</summary>
        public const string MainMenuScenePath = GeneratedSceneFolder + "/MainMenu.unity";
    }
}
