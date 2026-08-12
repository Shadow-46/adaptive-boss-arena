using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// The project's shared generators for the two UI primitives every screen needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Extracted so the heads-up display and the title screen produce identical text and buttons
    /// rather than each carrying its own near-copy. Both are built from code for the same reason the
    /// scenes are: a hand-authored canvas is a wall of anchor values that means nothing in a diff.
    /// </para>
    /// <para>
    /// Text uses the always-present legacy runtime font. TextMeshPro needs its essential resources
    /// imported through a dialog before anything renders, which a generated scene cannot assume has
    /// happened.
    /// </para>
    /// </remarks>
    internal static class UiBuilder
    {
        /// <summary>Creates a centred text element using the built-in font.</summary>
        /// <param name="parent">Transform to parent the text under.</param>
        /// <param name="name">Object name.</param>
        /// <param name="content">Initial text.</param>
        /// <param name="fontSize">Font size in reference pixels.</param>
        /// <param name="alignment">Text alignment.</param>
        /// <returns>The created text component.</returns>
        public static Text CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(Text));
            textObject.transform.SetParent(parent, false);

            var text = textObject.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1000f, 80f);

            return text;
        }

        /// <summary>Creates a labelled button wired to a callback.</summary>
        /// <param name="parent">Transform to parent the button under.</param>
        /// <param name="name">Object name.</param>
        /// <param name="label">Button caption.</param>
        /// <param name="anchoredPosition">Centre-relative position.</param>
        /// <param name="onClick">Method invoked on click. Must be a public method on a serialised object.</param>
        /// <returns>The created button, for callers that need to reference it afterwards.</returns>
        public static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(name, typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(320f, 56f);

            buttonObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);

            Text text = CreateText(buttonObject.transform, "Label", label, 26, TextAnchor.MiddleCenter);
            text.rectTransform.sizeDelta = new Vector2(320f, 56f);

            // AddPersistentListener, not AddListener. This runs at edit time while the scene is being
            // baked to disk, and a plain AddListener registers a runtime-only callback that is never
            // serialised — so on reload every generated button would have an empty onClick and do
            // nothing, however correct the wiring looked in code. Persistent listeners are the ones
            // stored in the scene, exactly as if the method had been dragged into the inspector.
            var button = buttonObject.GetComponent<Button>();
            UnityEventTools.AddPersistentListener(button.onClick, onClick);

            return button;
        }

        /// <summary>
        /// Creates a horizontal slider with a solid, sprite-free look.
        /// </summary>
        /// <remarks>
        /// Built through <see cref="DefaultControls"/> rather than by hand: a slider's fill and handle
        /// have a fiddly nest of anchors that the component drives at runtime, and getting one wrong
        /// yields a control that looks right and does not move. Letting Unity assemble the known-good
        /// hierarchy and then only recolouring it removes that whole class of mistake.
        /// </remarks>
        /// <param name="parent">Transform to parent under.</param>
        /// <param name="name">Object name.</param>
        /// <param name="anchoredPosition">Centre-relative position.</param>
        /// <param name="accent">Colour of the filled portion.</param>
        /// <param name="width">Slider width.</param>
        /// <returns>The slider, ranged zero to one.</returns>
        public static Slider CreateSlider(
            Transform parent, string name, Vector2 anchoredPosition, Color accent, float width = 340f)
        {
            GameObject sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = name;
            sliderObject.transform.SetParent(parent, false);

            var rect = (RectTransform)sliderObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(width, 22f);

            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            Recolour(sliderObject.transform, "Fill Area/Fill", accent);
            Recolour(sliderObject.transform, "Background", new Color(1f, 1f, 1f, 0.16f));
            Recolour(sliderObject.transform, "Handle Slide Area/Handle", Color.white);

            return slider;
        }

        /// <summary>
        /// Creates a checkbox toggle with a solid, sprite-free look and no built-in label.
        /// </summary>
        /// <param name="parent">Transform to parent under.</param>
        /// <param name="name">Object name.</param>
        /// <param name="anchoredPosition">Centre-relative position.</param>
        /// <param name="accent">Colour of the checkmark when on.</param>
        /// <returns>The toggle.</returns>
        public static Toggle CreateToggle(
            Transform parent, string name, Vector2 anchoredPosition, Color accent)
        {
            GameObject toggleObject = DefaultControls.CreateToggle(new DefaultControls.Resources());
            toggleObject.name = name;
            toggleObject.transform.SetParent(parent, false);

            var rect = (RectTransform)toggleObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(30f, 30f);

            // The default toggle ships with its own Text label; the settings rows supply their own,
            // so it is disabled rather than left showing an empty box beside the real label.
            Transform label = toggleObject.transform.Find("Label");
            if (label != null)
            {
                label.gameObject.SetActive(false);
            }

            Recolour(toggleObject.transform, "Background", new Color(1f, 1f, 1f, 0.2f));
            Recolour(toggleObject.transform, "Background/Checkmark", accent);

            return toggleObject.GetComponent<Toggle>();
        }

        /// <summary>Recolours a named descendant image, tolerating its absence.</summary>
        private static void Recolour(Transform root, string path, Color color)
        {
            Transform target = root.Find(path);
            var image = target != null ? target.GetComponent<Image>() : null;

            if (image != null)
            {
                image.color = color;
            }
        }
    }
}
