using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Builds the heads-up display hierarchy from code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Interface hierarchies are the worst thing to hand-author into a scene: dozens of nested
    /// transforms with anchor values that mean nothing in a diff, and every reference a GUID that
    /// breaks silently. Generating it means the layout is readable, reviewable and reproducible.
    /// </para>
    /// <para>
    /// Text uses the built-in legacy font rather than TextMeshPro. TMP needs its essential resources
    /// imported through a dialog before any text renders at all, which a generated scene cannot
    /// rely on having happened. The legacy font is always present. Swapping to TMP is a sensible
    /// polish-phase job once the setup is a one-time manual step rather than part of the chain.
    /// </para>
    /// </remarks>
    internal static class HudBuilder
    {
        private const int ReferenceWidth = 1920;
        private const int ReferenceHeight = 1080;

        /// <summary>
        /// Vertical centres of the end screen's four bands, top to bottom.
        /// </summary>
        /// <remarks>
        /// Kept together so the gaps between them can be checked at a glance. With the sizes assigned
        /// in <c>BuildEndScreen</c> the occupied ranges are headline [330, 430], summary [80, 310],
        /// dossier [-255, 45] and the buttons [-328, -272] — every band clear of its neighbour.
        /// </remarks>
        private const float HeadlineCentreY = 380f;

        private const float SummaryCentreY = 195f;

        private const float DossierCentreY = -105f;

        private const float OutcomeButtonY = -300f;

        /// <summary>
        /// Title scene the pause menu returns to.
        /// </summary>
        /// <remarks>
        /// Derived from the generated scene path rather than typed again, so renaming the scene in
        /// one place cannot leave the Main Menu button pointing at a scene that no longer exists.
        /// </remarks>
        private static readonly string MainMenuSceneName =
            System.IO.Path.GetFileNameWithoutExtension(EditorMenus.MainMenuScenePath);

        private static readonly Color PlayerHealthColor = new Color(0.45f, 0.85f, 0.5f);
        private static readonly Color StaminaColor = new Color(0.95f, 0.8f, 0.35f);
        private static readonly Color BossHealthColor = new Color(0.9f, 0.3f, 0.32f);
        private static readonly Color PostureColor = new Color(0.75f, 0.8f, 1f);
        private static readonly Color FocusColor = new Color(1f, 0.6f, 0.9f);
        private static readonly Color TrailColor = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.72f);

        /// <summary>Builds the whole interface and returns the objects the director needs.</summary>
        /// <param name="actionsAsset">Input actions asset, for the pause control.</param>
        /// <param name="channels">
        /// Channels resolved before the scene was replaced. They are passed in rather than looked up
        /// here because creating a new scene unloads assets, after which a lookup by path returns
        /// null even though the file is present and correct.
        /// </param>
        /// <returns>References to the generated components.</returns>
        public static HudReferences Build(
            UnityEngine.InputSystem.InputActionAsset actionsAsset,
            HudChannels channels)
        {
            GameObject canvasObject = CreateCanvas();
            Transform root = canvasObject.transform;

            var references = new HudReferences();

            BuildPlayerBars(root, references, channels);
            BuildWeaponDisplay(root, channels);
            BuildBossBar(root, references, channels);
            BuildAdaptationTell(root, references, channels);

            // Built before the pause menu so the pause menu can wire a button to open it.
            references.SettingsMenu = SettingsMenuBuilder.Build(root, actionsAsset);

            BuildPauseMenu(root, references, actionsAsset, channels);
            BuildEndScreen(root, references);
            BuildRoundIntro(root, references);

            // Raised last so the settings panel draws over the pause menu it is opened from, rather
            // than under it — later siblings win in Unity's UI.
            references.SettingsMenu.RaiseToTop();

            return references;
        }

        /// <summary>
        /// Resolves every channel the interface needs.
        /// </summary>
        /// <remarks>
        /// Must be called before the scene is replaced. Resolving once up front and injecting the
        /// result is also simply better than scattering lookups through the build.
        /// </remarks>
        /// <returns>The resolved channels.</returns>
        public static HudChannels ResolveChannels() => new HudChannels
        {
            PlayerHealth = GeneratedAssets.EventChannel<FloatEventChannel>("OnPlayerHealthChanged"),
            PlayerStamina = GeneratedAssets.EventChannel<FloatEventChannel>("OnPlayerStaminaChanged"),
            BossHealth = GeneratedAssets.EventChannel<FloatEventChannel>("OnBossHealthChanged"),
            PlayerPosture = GeneratedAssets.EventChannel<FloatEventChannel>("OnPlayerPostureChanged"),
            PlayerFocus = GeneratedAssets.EventChannel<FloatEventChannel>("OnPlayerFocusChanged"),
            BossPosture = GeneratedAssets.EventChannel<FloatEventChannel>("OnBossPostureChanged"),
            BossPhase = GeneratedAssets.EventChannel<IntEventChannel>("OnBossPhaseChanged"),
            AdaptationAdopted = GeneratedAssets.EventChannel<StringEventChannel>("OnAdaptationAdopted"),
            WeaponDrawn = GeneratedAssets.EventChannel<StringEventChannel>("OnWeaponDrawn"),
            PauseState = GeneratedAssets.EventChannel<BoolEventChannel>("OnPauseStateChanged"),
            PlayerDied = GeneratedAssets.EventChannel<VoidEventChannel>("OnPlayerDied"),
            BossDefeated = GeneratedAssets.EventChannel<VoidEventChannel>("OnBossDefeated"),
            Deflect = GeneratedAssets.EventChannel<VoidEventChannel>("OnDeflect"),
            PerfectDodge = GeneratedAssets.EventChannel<VoidEventChannel>("OnPerfectDodge"),
            Overbalance = GeneratedAssets.EventChannel<VoidEventChannel>("OnBossOverbalanced")
        };

        /// <summary>Creates the scaling canvas and its event system.</summary>
        private static GameObject CreateCanvas()
        {
            var canvasObject = new GameObject("--- HUD ---", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);

            // Matching height keeps the bars a constant fraction of the screen on ultrawide
            // monitors, where matching width would shrink them to nothing.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject(
                    "EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            return canvasObject;
        }

        /// <summary>Creates the player's health and stamina bars, bottom left.</summary>
        private static void BuildPlayerBars(Transform root, HudReferences references, HudChannels channels)
        {
            GameObject group = CreatePanel(root, "PlayerVitals", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f, 40f), new Vector2(420f, 92f), Color.clear);

            references.PlayerHealth = CreateBar(
                group.transform, "Health", new Vector2(0f, 56f), new Vector2(420f, 26f),
                PlayerHealthColor,
                channels.PlayerHealth);

            references.PlayerStamina = CreateBar(
                group.transform, "Stamina", new Vector2(0f, 20f), new Vector2(420f, 14f),
                StaminaColor,
                channels.PlayerStamina);

            // A slim bar tucked between health and stamina. It fills as the player deflects and dodges
            // perfectly, and a full bar is the cue that the next special will land empowered.
            references.PlayerFocus = CreateBar(
                group.transform, "Focus", new Vector2(0f, 38f), new Vector2(420f, 7f),
                FocusColor,
                channels.PlayerFocus);

            // Posture sits closest to the character's feet, because it is the bar the player watches
            // while under pressure and deciding whether they can afford another block.
            references.PlayerPosture = CreateBar(
                group.transform, "Posture", new Vector2(0f, 2f), new Vector2(420f, 10f),
                PostureColor,
                channels.PlayerPosture);
        }

        /// <summary>Creates the boss bar and phase pips, top centre.</summary>
        private static void BuildBossBar(Transform root, HudReferences references, HudChannels channels)
        {
            GameObject group = CreatePanel(root, "BossVitals", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -46f), new Vector2(900f, 78f), Color.clear);

            references.BossHealth = CreateBar(
                group.transform, "Health", new Vector2(0f, -14f), new Vector2(900f, 22f),
                BossHealthColor,
                channels.BossHealth);

            // Directly beneath the health bar so the two read as one gauge. Watching this empty is
            // how the player knows a riposte is coming, which is the payoff of every deflect.
            references.BossPosture = CreateBar(
                group.transform, "Posture", new Vector2(0f, -32f), new Vector2(900f, 10f),
                PostureColor,
                channels.BossPosture);

            var pipsRoot = new GameObject("PhasePips", typeof(RectTransform));
            pipsRoot.transform.SetParent(group.transform, false);

            var pips = new Image[BossPhaseIndicator.ExpectedPipCount];
            const float pipWidth = 44f;
            const float pipSpacing = 8f;
            float totalWidth = pips.Length * pipWidth + (pips.Length - 1) * pipSpacing;

            for (int i = 0; i < pips.Length; i++)
            {
                float x = -totalWidth * 0.5f + pipWidth * 0.5f + i * (pipWidth + pipSpacing);

                GameObject pip = CreatePanel(pipsRoot.transform, $"Pip_{i}",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, 14f), new Vector2(pipWidth, 6f), Color.white);

                pips[i] = pip.GetComponent<Image>();
            }

            var indicator = pipsRoot.AddComponent<BossPhaseIndicator>();
            indicator.Bind(channels.BossPhase, pips);
        }

        /// <summary>Creates the adaptation tell banner, upper third.</summary>
        private static void BuildAdaptationTell(Transform root, HudReferences references, HudChannels channels)
        {
            GameObject panel = CreatePanel(root, "AdaptationTell",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -150f), new Vector2(1100f, 60f), Color.clear);

            var group = panel.AddComponent<CanvasGroup>();
            Text label = CreateText(panel.transform, "Label", string.Empty, 34, TextAnchor.MiddleCenter);
            label.color = new Color(1f, 0.85f, 0.55f);

            var display = panel.AddComponent<AdaptationTellDisplay>();
            display.Bind(channels.AdaptationAdopted, label, group);

            references.AdaptationTell = display;
        }

        /// <summary>Creates the pause panel.</summary>
        private static void BuildPauseMenu(
            Transform root,
            HudReferences references,
            UnityEngine.InputSystem.InputActionAsset actionsAsset,
            HudChannels channels)
        {
            GameObject panel = CreateFullScreenPanel(root, "PausePanel");

            CreateText(panel.transform, "Title", "PAUSED", 64, TextAnchor.MiddleCenter)
                .rectTransform.anchoredPosition = new Vector2(0f, 140f);

            var menu = root.gameObject.AddComponent<PauseMenu>();

            CreateButton(panel.transform, "Resume", "Resume", new Vector2(0f, 90f), menu.Resume);
            CreateButton(panel.transform, "Restart", "Restart Fight", new Vector2(0f, 20f), menu.Restart);

            if (references.SettingsMenu != null)
            {
                CreateButton(panel.transform, "Settings", "Settings", new Vector2(0f, -50f),
                    references.SettingsMenu.Open);
            }

            // Without this the title screen — and so the challenge modifiers — is unreachable for the
            // rest of the session once the first fight starts.
            CreateButton(panel.transform, "MainMenu", "Main Menu", new Vector2(0f, -120f),
                menu.ReturnToMenu);

            // Built unconditionally and hidden at runtime where quitting is meaningless: this
            // generator has no idea which platform the build will target.
            Button quit = CreateButton(panel.transform, "Quit", "Quit", new Vector2(0f, -190f), menu.Quit);

            menu.Bind(
                actionsAsset,
                panel,
                channels.PauseState,
                references.SettingsMenu,
                quit != null ? quit.gameObject : null,
                MainMenuSceneName);

            panel.SetActive(false);
            references.PauseMenu = menu;
        }

        /// <summary>Creates the outcome panel.</summary>
        private static void BuildEndScreen(Transform root, HudReferences references)
        {
            GameObject panel = CreateFullScreenPanel(root, "EndPanel");
            var group = panel.AddComponent<CanvasGroup>();

            // Laid out as explicit non-overlapping bands, because every text created by UiBuilder has
            // a centre pivot: a rect at y spans [y - height/2, y + height/2], and a top-anchored
            // block therefore starts drawing at its rect's TOP edge, not at y. Missing that put the
            // summary's first line exactly on the headline's centre line and drew them over
            // each other. Each band below leaves a visible gap to the next.
            Text headline = CreateText(panel.transform, "Headline", string.Empty, 72, TextAnchor.MiddleCenter);
            headline.rectTransform.anchoredPosition = new Vector2(0f, HeadlineCentreY);

            // Tall enough to contain 72pt glyphs. The default 80 was shorter than the line itself, so
            // it only rendered at all because overflow is permitted.
            headline.rectTransform.sizeDelta = new Vector2(1000f, 100f);

            // Sized for the longest summary the end screen can produce — seven lines on a run that
            // sets a personal best — so it cannot spill into the dossier below it.
            Text summary = CreateText(panel.transform, "Summary", string.Empty, 28, TextAnchor.UpperCenter);
            summary.rectTransform.anchoredPosition = new Vector2(0f, SummaryCentreY);
            summary.rectTransform.sizeDelta = new Vector2(900f, 230f);

            // The dossier: the boss's read on the player. Left-aligned and roomy because it is a list
            // of sentences, and it is the payoff of the whole "it studied you" mechanic.
            Text dossier = CreateText(panel.transform, "Dossier", string.Empty, 24, TextAnchor.UpperLeft);
            dossier.rectTransform.anchoredPosition = new Vector2(0f, DossierCentreY);
            dossier.rectTransform.sizeDelta = new Vector2(760f, 300f);
            dossier.color = new Color(0.82f, 0.86f, 0.94f);

            var screen = root.gameObject.AddComponent<EndScreen>();
            CreateButton(
                panel.transform, "Retry", "Try Again", new Vector2(-170f, OutcomeButtonY), screen.Restart);

            // The fight is over, so this is the natural moment to change the challenge modifiers —
            // which live on the title screen and were previously unreachable from here.
            if (references.PauseMenu != null)
            {
                CreateButton(panel.transform, "EndMainMenu", "Main Menu", new Vector2(170f, OutcomeButtonY),
                    references.PauseMenu.ReturnToMenu);
            }

            screen.Bind(panel, group, headline, summary, dossier);
            panel.SetActive(false);

            references.EndScreen = screen;
        }

        /// <summary>Creates the small current-weapon label above the player's vitals.</summary>
        private static void BuildWeaponDisplay(Transform root, HudChannels channels)
        {
            Text label = CreateText(root, "WeaponName", "Blade", 24, TextAnchor.LowerLeft);

            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(44f, 140f);
            rect.sizeDelta = new Vector2(320f, 32f);
            label.color = new Color(0.85f, 0.88f, 0.95f);

            var display = root.gameObject.AddComponent<WeaponDisplay>();
            display.Bind(channels.WeaponDrawn, label);
        }

        /// <summary>Creates the ready–fight banner shown at the start of each attempt.</summary>
        /// <remarks>
        /// Unlike the end screen this has no dark backing: it is a banner over the frozen fight, not a
        /// panel that hides it. It carries no Image and its group blocks no raycasts, so it can never
        /// swallow a click meant for the buttons beneath it.
        /// </remarks>
        private static void BuildRoundIntro(Transform root, HudReferences references)
        {
            var panel = new GameObject("RoundIntroPanel", typeof(RectTransform));
            panel.transform.SetParent(root, false);
            Stretch(panel.GetComponent<RectTransform>());

            var group = panel.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            Text banner = CreateText(panel.transform, "Banner", string.Empty, 96, TextAnchor.MiddleCenter);
            banner.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            banner.fontStyle = FontStyle.Bold;

            var intro = root.gameObject.AddComponent<RoundIntro>();
            intro.Bind(panel, group, banner);
            panel.SetActive(false);

            references.RoundIntro = intro;
        }

        /// <summary>Creates a bar with a trailing fill behind its main fill.</summary>
        private static ResourceBar CreateBar(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color,
            FloatEventChannel channel)
        {
            GameObject barRoot = CreatePanel(parent, name, new Vector2(0f, 0f), new Vector2(0f, 0f),
                anchoredPosition, size, new Color(0f, 0f, 0f, 0.6f));

            Image trail = CreateFillImage(barRoot.transform, "Trail", TrailColor);
            Image fill = CreateFillImage(barRoot.transform, "Fill", color);

            var bar = barRoot.AddComponent<ResourceBar>();
            bar.Bind(channel, fill, trail);
            bar.SetImmediate(1f);

            return bar;
        }

        /// <summary>Creates a horizontally filled image stretched to its parent.</summary>
        private static Image CreateFillImage(Transform parent, string name, Color color)
        {
            var imageObject = new GameObject(name, typeof(Image));
            imageObject.transform.SetParent(parent, false);

            var rect = imageObject.GetComponent<RectTransform>();
            Stretch(rect);

            // Left as a simple untextured quad. The bar is sized by stretching this rect, not by
            // Image.fillAmount, which has no effect without a sprite.
            var image = imageObject.GetComponent<Image>();
            image.color = color;
            image.type = Image.Type.Simple;

            return image;
        }

        /// <summary>Creates a positioned panel with an optional background.</summary>
        private static GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Color background)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            if (background.a > 0f)
            {
                panel.AddComponent<Image>().color = background;
            }

            return panel;
        }

        /// <summary>Creates a panel covering the whole screen.</summary>
        private static GameObject CreateFullScreenPanel(Transform parent, string name)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = PanelColor;

            return panel;
        }

        /// <summary>Creates a text element using the always-present built-in font.</summary>
        private static Text CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            TextAnchor alignment) =>
            UiBuilder.CreateText(parent, name, content, fontSize, alignment);

        /// <summary>Creates a labelled button wired to a callback.</summary>
        /// <returns>The created button, for callers that need to reference it afterwards.</returns>
        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction onClick) =>
            UiBuilder.CreateButton(parent, name, label, anchoredPosition, onClick);

        /// <summary>Stretches a rect to fill its parent.</summary>
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// The event channels the interface binds to, resolved before the scene is replaced.
    /// </summary>
    /// <remarks>
    /// This type exists because of a specific and unobvious failure. Replacing the open scene
    /// unloads assets, and afterwards <c>AssetDatabase.LoadAssetAtPath</c> returns null for assets
    /// that are present and perfectly valid on disk. Resolving everything beforehand and carrying it
    /// across sidesteps the problem, and reads better than lookups sprinkled through the build.
    /// </remarks>
    internal sealed class HudChannels
    {
        /// <summary>Normalised player health.</summary>
        public FloatEventChannel PlayerHealth { get; set; }

        /// <summary>Normalised player stamina.</summary>
        public FloatEventChannel PlayerStamina { get; set; }

        /// <summary>Normalised player focus.</summary>
        public FloatEventChannel PlayerFocus { get; set; }

        /// <summary>Normalised boss health.</summary>
        public FloatEventChannel BossHealth { get; set; }

        /// <summary>Normalised player posture.</summary>
        public FloatEventChannel PlayerPosture { get; set; }

        /// <summary>Normalised boss posture.</summary>
        public FloatEventChannel BossPosture { get; set; }

        /// <summary>Zero-based boss phase index.</summary>
        public IntEventChannel BossPhase { get; set; }

        /// <summary>Tell message for a newly adopted counter-strategy.</summary>
        public StringEventChannel AdaptationAdopted { get; set; }

        /// <summary>Name of a newly drawn weapon.</summary>
        public StringEventChannel WeaponDrawn { get; set; }

        /// <summary>Whether the game is paused.</summary>
        public BoolEventChannel PauseState { get; set; }

        /// <summary>Raised when the player is defeated.</summary>
        public VoidEventChannel PlayerDied { get; set; }

        /// <summary>Raised when the boss is defeated.</summary>
        public VoidEventChannel BossDefeated { get; set; }

        /// <summary>Raised on a clean deflect.</summary>
        public VoidEventChannel Deflect { get; set; }

        /// <summary>Raised on a perfect dodge.</summary>
        public VoidEventChannel PerfectDodge { get; set; }

        /// <summary>Raised when the boss overbalances on a committed whiff.</summary>
        public VoidEventChannel Overbalance { get; set; }
    }

    /// <summary>The generated interface components the scene builder needs to wire up.</summary>
    internal sealed class HudReferences
    {
        /// <summary>Player health bar.</summary>
        public ResourceBar PlayerHealth { get; set; }

        /// <summary>Player stamina bar.</summary>
        public ResourceBar PlayerStamina { get; set; }

        /// <summary>Player focus bar.</summary>
        public ResourceBar PlayerFocus { get; set; }

        /// <summary>Boss health bar.</summary>
        public ResourceBar BossHealth { get; set; }

        /// <summary>Player posture bar.</summary>
        public ResourceBar PlayerPosture { get; set; }

        /// <summary>Boss posture bar.</summary>
        public ResourceBar BossPosture { get; set; }

        /// <summary>Adaptation tell banner.</summary>
        public AdaptationTellDisplay AdaptationTell { get; set; }

        /// <summary>Pause menu.</summary>
        public PauseMenu PauseMenu { get; set; }

        /// <summary>Outcome screen.</summary>
        public EndScreen EndScreen { get; set; }

        /// <summary>Ready–fight intro banner.</summary>
        public RoundIntro RoundIntro { get; set; }

        /// <summary>Settings panel and its live-applying menu.</summary>
        public SettingsMenu SettingsMenu { get; set; }
    }
}
