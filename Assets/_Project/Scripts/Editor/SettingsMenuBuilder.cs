using AdaptiveBossArena.UI;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Builds the settings panel and its live-applying menu component.
    /// </summary>
    /// <remarks>
    /// Shared by the heads-up display and the title screen so the settings look and behave identically
    /// wherever they are opened from, and so the panel is only described once. Every control is wired
    /// to its menu method with a persistent listener, the only kind that survives being baked into a
    /// scene from an editor script.
    /// </remarks>
    internal static class SettingsMenuBuilder
    {
        // Very nearly opaque. At the 0.85 it started on, the bright title and buttons behind it bled
        // through and made the panel look like a bug rather than a screen.
        private static readonly Color PanelColor = new Color(0.03f, 0.03f, 0.05f, 0.985f);
        private static readonly Color Accent = new Color(0.45f, 0.85f, 0.95f);
        private static readonly Color ToggleAccent = new Color(0.55f, 0.9f, 0.6f);
        private static readonly Color MutedText = new Color(0.72f, 0.76f, 0.82f);

        private const float LabelX = -150f;
        private const float ControlX = 150f;
        private const float RowSpacing = 58f;

        /// <summary>Builds the panel under a canvas and returns the wired menu.</summary>
        /// <param name="root">Canvas transform to build under.</param>
        /// <param name="actions">Shared input actions asset, for rebinding overrides.</param>
        /// <returns>The settings menu, with its panel hidden.</returns>
        public static SettingsMenu Build(Transform root, InputActionAsset actions)
        {
            GameObject panel = CreateFullScreenPanel(root, "SettingsPanel");

            Text title = UiBuilder.CreateText(panel.transform, "Title", "SETTINGS", 56, TextAnchor.MiddleCenter);
            title.rectTransform.anchoredPosition = new Vector2(0f, 460f);
            title.fontStyle = FontStyle.Bold;

            float y = 320f;

            Slider master = LabelledSlider(panel.transform, "Master Volume", ref y);
            Slider music = LabelledSlider(panel.transform, "Music Volume", ref y);
            Slider effects = LabelledSlider(panel.transform, "Effects Volume", ref y);
            Slider shake = LabelledSlider(panel.transform, "Screen Shake", ref y);
            Toggle reducedFlashing = LabelledToggle(panel.transform, "Reduced Flashing", ref y);
            Toggle extendedTell = LabelledToggle(panel.transform, "Longer Boss Tells", ref y);

            // The menu must exist before the rebind rows, because each row holds a reference to it and
            // the sliders' change callbacks are wired to it here.
            var menu = root.gameObject.AddComponent<SettingsMenu>();
            menu.Bind(panel, master, music, effects, shake, reducedFlashing, extendedTell, actions);

            WireSlider(master, menu.SetMasterVolume);
            WireSlider(music, menu.SetMusicVolume);
            WireSlider(effects, menu.SetEffectsVolume);
            WireSlider(shake, menu.SetShakeIntensity);
            WireToggle(reducedFlashing, menu.SetReducedFlashing);
            WireToggle(extendedTell, menu.SetExtendedTell);

            BuildRebindSection(panel.transform, menu, ref y);

            UiBuilder.CreateButton(panel.transform, "Back", "Back", new Vector2(0f, -470f), menu.Close);

            panel.SetActive(false);
            return menu;
        }

        /// <summary>Creates a right-aligned label and a slider on one row, advancing the cursor.</summary>
        private static Slider LabelledSlider(Transform parent, string caption, ref float y)
        {
            RowLabel(parent, caption, y);
            Slider slider = UiBuilder.CreateSlider(parent, caption, new Vector2(ControlX, y), Accent);
            y -= RowSpacing;
            return slider;
        }

        /// <summary>Creates a right-aligned label and a toggle on one row, advancing the cursor.</summary>
        private static Toggle LabelledToggle(Transform parent, string caption, ref float y)
        {
            RowLabel(parent, caption, y);
            Toggle toggle = UiBuilder.CreateToggle(parent, caption, new Vector2(ControlX - 150f, y), ToggleAccent);
            y -= RowSpacing;
            return toggle;
        }

        /// <summary>Places a right-aligned caption for a row.</summary>
        private static void RowLabel(Transform parent, string caption, float y)
        {
            Text label = UiBuilder.CreateText(parent, $"{caption} Label", caption, 26, TextAnchor.MiddleRight);
            label.rectTransform.anchoredPosition = new Vector2(LabelX, y);
            label.rectTransform.sizeDelta = new Vector2(280f, 40f);
        }

        /// <summary>Builds the rebindable-control rows, a fixed-control reference, and a reset button.</summary>
        private static void BuildRebindSection(Transform parent, SettingsMenu menu, ref float y)
        {
            y -= 22f;

            Text header = UiBuilder.CreateText(
                parent, "ControlsHeader", "CONTROLS  —  click a key to rebind", 24, TextAnchor.MiddleCenter);
            header.rectTransform.anchoredPosition = new Vector2(0f, y);
            header.fontStyle = FontStyle.Bold;
            header.color = MutedText;
            y -= 46f;

            RebindRow(parent, "Dash", "Dash", menu, ref y);
            RebindRow(parent, "Guard / Deflect", "Guard", menu, ref y);
            RebindRow(parent, "Special", "Special", menu, ref y);
            RebindRow(parent, "Heal", "Heal", menu, ref y);
            RebindRow(parent, "Swap Weapon", "SwapWeapon", menu, ref y);

            // The controls that are not rebindable — movement, the mouse attacks and the camera keys —
            // shown for reference so the panel is a complete controls list, not only a rebind menu.
            Text fixedControls = UiBuilder.CreateText(
                parent, "FixedControls",
                "Move  WASD        Light  Left Mouse        Heavy  Right Mouse        " +
                "Camera  C        Lock On  Tab        Pause  Esc",
                19, TextAnchor.MiddleCenter);
            fixedControls.rectTransform.anchoredPosition = new Vector2(0f, y - 4f);
            fixedControls.rectTransform.sizeDelta = new Vector2(1040f, 28f);
            fixedControls.color = MutedText;
            y -= 44f;

            UiBuilder.CreateButton(parent, "ResetControls", "Reset Controls", new Vector2(0f, y - 4f), menu.ResetControls);
            y -= 44f;
        }

        /// <summary>Builds one rebindable row: a caption and a button whose label shows the current key.</summary>
        private static void RebindRow(
            Transform parent, string caption, string actionName, SettingsMenu menu, ref float y)
        {
            Text label = UiBuilder.CreateText(parent, $"{caption} Label", caption, 22, TextAnchor.MiddleRight);
            label.rectTransform.anchoredPosition = new Vector2(LabelX, y);
            label.rectTransform.sizeDelta = new Vector2(280f, 32f);

            var buttonObject = new GameObject($"{caption} Rebind", typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(ControlX, y);
            rect.sizeDelta = new Vector2(190f, 32f);

            buttonObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            Text key = UiBuilder.CreateText(buttonObject.transform, "Key", "—", 22, TextAnchor.MiddleCenter);
            key.rectTransform.sizeDelta = new Vector2(190f, 32f);

            var rebind = buttonObject.AddComponent<RebindButton>();

            // Binding index 0 is the keyboard binding: the generator adds keyboard first, then gamepad.
            rebind.Configure(actionName, 0, key, menu);

            UnityEventTools.AddPersistentListener(buttonObject.GetComponent<Button>().onClick, rebind.OnClick);

            y -= 44f;
        }

        /// <summary>Creates a panel covering the whole screen with a dark backing.</summary>
        private static GameObject CreateFullScreenPanel(Transform parent, string name)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            panel.GetComponent<Image>().color = PanelColor;
            return panel;
        }

        private static void WireSlider(Slider slider, UnityEngine.Events.UnityAction<float> onChanged) =>
            UnityEventTools.AddPersistentListener(slider.onValueChanged, onChanged);

        private static void WireToggle(Toggle toggle, UnityEngine.Events.UnityAction<bool> onChanged) =>
            UnityEventTools.AddPersistentListener(toggle.onValueChanged, onChanged);
    }
}
