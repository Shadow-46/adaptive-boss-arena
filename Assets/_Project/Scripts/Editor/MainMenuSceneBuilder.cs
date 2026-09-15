using System.IO;
using AdaptiveBossArena.Game;
using AdaptiveBossArena.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Builds the title scene procedurally and puts it first in the build order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game's front door is its own scene rather than a panel over the arena, so the fight only
    /// ever constructs itself when the player actually chooses to fight. Generated from code like
    /// every other scene, so it is reviewable and reproducible instead of an opaque YAML blob.
    /// </para>
    /// <para>
    /// This step also fixes the build order: the title must load first, the arena second. It sets
    /// the whole build-settings list explicitly rather than appending, because "first" is the one
    /// thing appending cannot guarantee.
    /// </para>
    /// </remarks>
    public static class MainMenuSceneBuilder
    {
        private const int ReferenceWidth = 1920;
        private const int ReferenceHeight = 1080;

        private static readonly Color BackgroundColor = new Color(0.012f, 0.01f, 0.01f);
        private static readonly Color TitleColor = new Color(0.84f, 0.77f, 0.66f);
        private static readonly Color SubtitleColor = new Color(0.6f, 0.54f, 0.47f);
        private static readonly Color EmberColor = new Color(0.36f, 0.08f, 0.035f);

        /// <summary>Creates the title scene, replacing any previously generated one.</summary>
        [MenuItem(EditorMenus.Setup + "Build Title Scene", priority = EditorMenus.SetupPriorityBuildScene + 1)]
        public static void BuildMainMenuScene()
        {
            // The save prompt is modal and cannot be answered on a build machine.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildServices();
            BuildCamera();
            BuildInterface();

            AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedSceneFolder);
            EditorSceneManager.SaveScene(scene, EditorMenus.MainMenuScenePath);

            RegisterSceneOrder();

            Debug.Log($"[Adaptive Boss Arena] Title scene built at '{EditorMenus.MainMenuScenePath}'.");
        }

        /// <summary>
        /// Adds the service host so the title can load and save settings.
        /// </summary>
        /// <remarks>
        /// The title is a real scene, not a panel over the fight, so it does not inherit the fight's
        /// services. It needs its own <see cref="GameBootstrapper"/> for the save service the settings
        /// menu persists through. The combat services it also registers go unused here, which costs
        /// nothing.
        /// </remarks>
        private static void BuildServices()
        {
            var managers = new GameObject("--- Managers ---");
            managers.AddComponent<GameBootstrapper>();
        }

        /// <summary>Adds a plain camera so the overlay canvas has something to clear behind it.</summary>
        private static void BuildCamera()
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.orthographic = true;
        }

        /// <summary>Builds the canvas, title, and the start and quit buttons.</summary>
        private static void BuildInterface()
        {
            GameObject canvasObject = CreateCanvas();
            Transform root = canvasObject.transform;

            // A dying ember glow behind the lettering instead of a flat dark fill: the title is the first
            // thing anyone sees, and red capitals on black read as a placeholder.
            var glow = new GameObject("EmberGlow", typeof(RectTransform), typeof(Image));
            glow.transform.SetParent(root, false);
            var glowRect = glow.GetComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-200f, -300f);
            glowRect.offsetMax = new Vector2(200f, 100f);
            var glowImage = glow.GetComponent<Image>();
            glowImage.sprite = UiSprites.RadialGlow();
            glowImage.color = EmberColor;
            glowImage.raycastTarget = false;

            Text title = UiBuilder.CreateText(root, "Title", "A D A P T I V E   B O S S   A R E N A", 72, TextAnchor.MiddleCenter);
            title.rectTransform.anchoredPosition = new Vector2(0f, 220f);
            title.rectTransform.sizeDelta = new Vector2(1800f, 100f);
            title.color = TitleColor;
            title.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            var rule = new GameObject("TitleRule", typeof(RectTransform), typeof(Image));
            rule.transform.SetParent(root, false);
            var ruleRect = rule.GetComponent<RectTransform>();
            ruleRect.anchoredPosition = new Vector2(0f, 168f);
            ruleRect.sizeDelta = new Vector2(760f, 2f);
            rule.GetComponent<Image>().color = new Color(0.5f, 0.12f, 0.06f, 0.9f);

            Text subtitle = UiBuilder.CreateText(
                root, "Subtitle", "It learns what you repeat.", 30, TextAnchor.MiddleCenter);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, 130f);
            subtitle.color = SubtitleColor;
            subtitle.fontStyle = FontStyle.Italic;

            // The component must exist before the buttons are wired, because the persistent listeners
            // baked into them serialise a reference to it.
            var titleScreen = canvasObject.AddComponent<TitleScreen>();

            // The same settings panel the fight uses, so preferences set here carry straight in.
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsGenerator.AssetPath);
            SettingsMenu settings = SettingsMenuBuilder.Build(root, actions);

            UiBuilder.CreateButton(root, "Start", "Fight", new Vector2(0f, -20f), titleScreen.StartGame);
            UiBuilder.CreateButton(root, "Settings", "Settings", new Vector2(0f, -90f), settings.Open);

            // Built unconditionally and hidden at runtime in a browser, where a page cannot close its
            // own tab and the button would otherwise sit on the front door doing nothing.
            Button quit = UiBuilder.CreateButton(
                root, "Quit", "Quit", new Vector2(0f, -160f), titleScreen.Quit);

            titleScreen.Bind(
                Path.GetFileNameWithoutExtension(EditorMenus.ArenaScenePath),
                quit != null ? quit.gameObject : null);

            BuildModifiers(root, canvasObject);

            // Raised after the buttons exist. Unity draws later siblings on top, so a panel built
            // before them would otherwise have the title's buttons showing straight through it.
            settings.RaiseToTop();
        }

        private const float ModifierColumnX = -640f;
        private const float ModifierToggleOffset = 232f;
        private const float ModifierRowSpacing = 52f;
        private static readonly Color ModifierAccent = new Color(0.72f, 0.2f, 0.08f);
        private static readonly Color ModifierHeaderColor = new Color(0.78f, 0.7f, 0.6f);

        /// <summary>
        /// Builds the challenge-modifier column: a labelled toggle per rule, saved as it is changed.
        /// </summary>
        /// <remarks>
        /// Shown directly on the title rather than behind a button, so a player browsing the front
        /// door discovers that the fight can be made harder — or turned into a place to practise —
        /// without going looking for it.
        /// </remarks>
        private static void BuildModifiers(Transform root, GameObject canvasObject)
        {
            float y = 120f;

            Text header = UiBuilder.CreateText(root, "ChallengesHeader", "CHALLENGES", 24, TextAnchor.MiddleLeft);
            header.rectTransform.anchoredPosition = new Vector2(ModifierColumnX, y);
            header.rectTransform.sizeDelta = new Vector2(360f, 34f);
            header.fontStyle = FontStyle.Bold;
            header.color = ModifierHeaderColor;
            y -= ModifierRowSpacing;

            Toggle fastLearner = ModifierToggle(root, "Fast Learner", ref y);
            Toggle noHealing = ModifierToggle(root, "No Healing", ref y);
            Toggle fragile = ModifierToggle(root, "Fragile", ref y);
            Toggle training = ModifierToggle(root, "Training", ref y);

            var menu = canvasObject.AddComponent<RunModifierMenu>();
            menu.Bind(fastLearner, noHealing, fragile, training);

            UnityEventTools.AddPersistentListener(fastLearner.onValueChanged, menu.SetFastLearner);
            UnityEventTools.AddPersistentListener(noHealing.onValueChanged, menu.SetNoHealing);
            UnityEventTools.AddPersistentListener(fragile.onValueChanged, menu.SetFragile);
            UnityEventTools.AddPersistentListener(training.onValueChanged, menu.SetTraining);
        }

        /// <summary>Creates a left-aligned caption and its toggle, advancing the running y.</summary>
        private static Toggle ModifierToggle(Transform parent, string caption, ref float y)
        {
            Text label = UiBuilder.CreateText(parent, $"{caption} Label", caption, 24, TextAnchor.MiddleLeft);
            label.rectTransform.anchoredPosition = new Vector2(ModifierColumnX, y);
            label.rectTransform.sizeDelta = new Vector2(300f, 34f);
            label.color = SubtitleColor;

            Toggle toggle = UiBuilder.CreateToggle(
                parent, $"{caption} Toggle", new Vector2(ModifierColumnX + ModifierToggleOffset, y), ModifierAccent);

            y -= ModifierRowSpacing;
            return toggle;
        }

        /// <summary>Creates the scaling canvas and an event system that reads the new Input System.</summary>
        private static GameObject CreateCanvas()
        {
            var canvasObject = new GameObject(
                "Title Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            // The new Input System needs its own UI module or no button ever receives a click, a
            // failure that looks exactly like dead wiring.
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject(
                    "EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            return canvasObject;
        }

        /// <summary>
        /// Sets the build-settings order so the title loads first and the arena second.
        /// </summary>
        /// <remarks>
        /// Written whole rather than appended. The arena builder appends itself, but only this step
        /// can guarantee the title comes first, which is the entire point of having a title.
        /// </remarks>
        private static void RegisterSceneOrder()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(EditorMenus.MainMenuScenePath, true),
                new EditorBuildSettingsScene(EditorMenus.ArenaScenePath, true)
            };
        }
    }
}
