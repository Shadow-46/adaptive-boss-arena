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

            BuildBackdrop(root);

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

            Button fight = UiBuilder.CreateButton(root, "Start", "Fight", new Vector2(0f, -70f), titleScreen.StartGame);
            HowToFightPanel guide = BuildHowToFight(root, canvasObject);
            UiBuilder.CreateButton(root, "HowToFight", "How to Fight", new Vector2(0f, -140f), guide.Open);
            UiBuilder.CreateButton(root, "Settings", "Settings", new Vector2(0f, -210f), settings.Open);

            // Built unconditionally and hidden at runtime in a browser, where a page cannot close its
            // own tab and the button would otherwise sit on the front door doing nothing.
            Button quit = UiBuilder.CreateButton(
                root, "Quit", "Quit", new Vector2(0f, -280f), titleScreen.Quit);

            titleScreen.Bind(
                Path.GetFileNameWithoutExtension(EditorMenus.ArenaScenePath),
                quit != null ? quit.gameObject : null,
                fight != null ? fight.gameObject : null);

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
            Toggle ascended = ModifierToggle(root, "Ascended", ref y, out Text ascendedLabel);

            var menu = canvasObject.AddComponent<RunModifierMenu>();
            menu.Bind(fastLearner, noHealing, fragile, training);

            // The difficulty sits above Fight, where it is chosen just before starting; its line says what it changes.
            Button difficulty = UiBuilder.CreateButton(
                root, "Difficulty", "Difficulty: Knight", new Vector2(0f, 40f), menu.CycleDifficulty);
            Text blurb = UiBuilder.CreateText(root, "DifficultyBlurb", string.Empty, 20, TextAnchor.MiddleCenter);
            blurb.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            blurb.rectTransform.sizeDelta = new Vector2(900f, 30f);
            blurb.color = SubtitleColor;
            blurb.fontStyle = FontStyle.Italic;

            y -= ModifierRowSpacing * 0.5f;
            Button tint = UiBuilder.CreateButton(
                root, "Armour", "Armour: Steel", new Vector2(ModifierColumnX + ArmourButtonOffset, y), menu.CycleTint);

            Text records = BuildRecordsPanel(root);

            menu.BindProgression(
                difficulty.GetComponentInChildren<Text>(), blurb, tint.GetComponentInChildren<Text>(), ascended,
                ascendedLabel, records);

            UnityEventTools.AddPersistentListener(ascended.onValueChanged, menu.SetAscended);

            UnityEventTools.AddPersistentListener(fastLearner.onValueChanged, menu.SetFastLearner);
            UnityEventTools.AddPersistentListener(noHealing.onValueChanged, menu.SetNoHealing);
            UnityEventTools.AddPersistentListener(fragile.onValueChanged, menu.SetFragile);
            UnityEventTools.AddPersistentListener(training.onValueChanged, menu.SetTraining);
        }

        /// <summary>Creates a left-aligned caption and its toggle, advancing the running y.</summary>
        private static Toggle ModifierToggle(Transform parent, string caption, ref float y) =>
            ModifierToggle(parent, caption, ref y, out _);

        /// <summary>A modifier row, also handing back its caption so the menu can rewrite it.</summary>
        private static Toggle ModifierToggle(Transform parent, string caption, ref float y, out Text captionText)
        {
            Text label = UiBuilder.CreateText(parent, $"{caption} Label", caption, 24, TextAnchor.MiddleLeft);
            label.rectTransform.anchoredPosition = new Vector2(ModifierColumnX, y);
            label.rectTransform.sizeDelta = new Vector2(300f, 34f);
            label.color = SubtitleColor;
            captionText = label;

            Toggle toggle = UiBuilder.CreateToggle(
                parent, $"{caption} Toggle", new Vector2(ModifierColumnX + ModifierToggleOffset, y), ModifierAccent);

            y -= ModifierRowSpacing;
            return toggle;
        }

        /// <summary>Creates the scaling canvas and an event system that reads the new Input System.</summary>
        private static readonly Color GuidePanelColor = new Color(0.02f, 0.015f, 0.015f, 0.97f);

        /// <summary>
        /// The How to Fight guide: a full-screen panel with the controls and the fight's mechanics, hidden until
        /// opened (and opened by itself on a first launch).
        /// </summary>
        private static HowToFightPanel BuildHowToFight(Transform root, GameObject canvasObject)
        {
            var panel = new GameObject("HowToFightPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = GuidePanelColor;

            Text header = UiBuilder.CreateText(panel.transform, "Header", "H O W   T O   F I G H T", 44, TextAnchor.MiddleCenter);
            header.rectTransform.anchoredPosition = new Vector2(0f, 400f);
            header.rectTransform.sizeDelta = new Vector2(1400f, 70f);
            header.color = TitleColor;

            Text body = UiBuilder.CreateText(panel.transform, "Guide", HowToFightPanel.Guide, 24, TextAnchor.UpperLeft);
            body.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            body.rectTransform.sizeDelta = new Vector2(1300f, 720f);
            body.color = new Color(0.82f, 0.76f, 0.68f);
            body.supportRichText = true;
            body.lineSpacing = 1.15f;

            var guide = canvasObject.AddComponent<HowToFightPanel>();
            guide.Bind(panel);

            UiBuilder.CreateButton(panel.transform, "Close", "Ready", new Vector2(0f, -440f), guide.Close);
            panel.SetActive(false);

            return guide;
        }

        /// <summary>How far right of the challenge column the armour button's centre sits.</summary>
        private const float ArmourButtonOffset = 130f;

        /// <summary>Where the records column's left edge sits.</summary>
        private const float RecordsColumnX = 420f;

        /// <summary>
        /// The right-hand column: every mode's record and every trial, so the reasons to fight again are on
        /// the front door rather than buried after a win.
        /// </summary>
        private static Text BuildRecordsPanel(Transform root)
        {
            Text header = UiBuilder.CreateText(root, "RecordsHeader", "THE RECORD", 24, TextAnchor.MiddleLeft);
            header.rectTransform.pivot = new Vector2(0f, 0.5f);
            header.rectTransform.anchoredPosition = new Vector2(RecordsColumnX, 120f);
            header.rectTransform.sizeDelta = new Vector2(520f, 34f);
            header.fontStyle = FontStyle.Bold;
            header.color = ModifierHeaderColor;

            Text body = UiBuilder.CreateText(root, "Records", string.Empty, 19, TextAnchor.UpperLeft);
            body.rectTransform.pivot = new Vector2(0f, 1f);
            body.rectTransform.anchoredPosition = new Vector2(RecordsColumnX, 96f);
            body.rectTransform.sizeDelta = new Vector2(540f, 520f);
            body.color = SubtitleColor;
            body.supportRichText = true;
            body.lineSpacing = 1.1f;

            return body;
        }

        /// <summary>A still of the fight itself, captured from the game, behind the whole title.</summary>
        public const string BackdropPath = "Assets/_Project/Art/Title/TitleBackdrop.png";

        /// <summary>How bright the backdrop is drawn: dim enough that the lettering and buttons own the screen.</summary>
        private static readonly Color BackdropTint = new Color(0.42f, 0.4f, 0.4f, 1f);

        /// <summary>
        /// Lays a captured still of Igris in his hall behind the title, dimmed, under the ember glow.
        /// </summary>
        /// <remarks>
        /// The title was red and bone lettering on black: the first thing anyone saw said nothing about the fight.
        /// The still is a frame of the game's own showcase capture, the boss's back to the camera in the moonlit
        /// hall, so the front door shows the place and the enemy without a second camera or scene to render them.
        /// Skipped quietly when the image is missing, and the title falls back to black.
        /// </remarks>
        private static void BuildBackdrop(Transform root)
        {
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropPath);

            if (image == null)
            {
                return;
            }

            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(RawImage));
            backdrop.transform.SetParent(root, false);
            var rect = backdrop.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var raw = backdrop.GetComponent<RawImage>();
            raw.texture = image;
            raw.color = BackdropTint;
            raw.raycastTarget = false;

            // Kept to the screen's shape: a wider or taller window crops the still rather than stretching Igris.
            var fitter = backdrop.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = image.width / (float)Mathf.Max(1, image.height);
        }

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
