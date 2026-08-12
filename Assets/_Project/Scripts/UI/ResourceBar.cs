using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveBossArena.UI
{
    /// <summary>
    /// A bar that displays a normalised resource, with a delayed trailing fill.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The trailing fill is the whole point of this component rather than a decoration. A bar that
    /// snaps to its new value tells the player their health changed; a bar with a chase behind it
    /// tells them <em>how much</em> it changed, which is the thing they actually need mid-fight and
    /// cannot get by reading a number.
    /// </para>
    /// <para>
    /// It listens to an event channel rather than polling a combatant, so the heads-up display holds
    /// no reference to the player or the boss and works for either.
    /// </para>
    /// <para>
    /// Ticks on unscaled time so the chase keeps animating during hit-stop, when the player is most
    /// likely to be looking at it.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ResourceBar : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField]
        [Tooltip("Channel carrying the normalised value, from zero to one.")]
        private FloatEventChannel _channel;

        [Header("Fills")]
        [SerializeField]
        [Tooltip("Image that tracks the value immediately.")]
        private Image _fill;

        [SerializeField]
        [Tooltip("Image that eases toward the value behind the main fill, showing recent loss. Optional.")]
        private Image _trailingFill;

        [Header("Feel")]
        [SerializeField]
        [Range(0.05f, 1.5f)]
        [Tooltip("Half-life of the trailing fill's chase. Longer makes a big hit read as more dramatic.")]
        private float _trailHalfLife = 0.35f;

        [SerializeField]
        [Range(0f, 1.5f)]
        [Tooltip("Delay before the trailing fill starts chasing, so the gap is legible first.")]
        private float _trailDelaySeconds = 0.4f;

        private float _current = 1f;
        private float _trailing = 1f;

        /// <summary>
        /// The fraction this bar is currently showing.
        /// </summary>
        /// <remarks>
        /// Exposed so an automated test can assert that a change in the underlying pool actually
        /// reaches the interface. A bar that was never wired and a bar with nothing to report look
        /// identical on screen, which is exactly the confusion this makes testable.
        /// </remarks>
        public float DisplayedFraction => _current;

        /// <summary>Whether this bar has a channel to listen to at all. For diagnostics.</summary>
        public bool HasChannel => _channel != null;
        private float _trailHoldRemaining;

        private void OnEnable()
        {
            if (_channel != null)
            {
                _channel.Raised += OnValueChanged;
            }
        }

        private void OnDisable()
        {
            if (_channel != null)
            {
                _channel.Raised -= OnValueChanged;
            }
        }

        private void Update()
        {
            if (_trailingFill == null)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;

            if (_trailHoldRemaining > 0f)
            {
                _trailHoldRemaining -= deltaTime;
                return;
            }

            _trailing = MathUtil.Damp(_trailing, _current, _trailHalfLife, deltaTime);
            SetWidthFraction(_trailingFill, Mathf.Max(_trailing, _current));
        }

        /// <summary>Sets the displayed value, snapping both fills. Used when a fight resets.</summary>
        /// <param name="normalized">Value from zero to one.</param>
        public void SetImmediate(float normalized)
        {
            _current = Mathf.Clamp01(normalized);
            _trailing = _current;
            _trailHoldRemaining = 0f;

            ApplyFills();
        }

        /// <summary>Assigns the channel and images. Used by the interface generator.</summary>
        /// <param name="channel">Channel carrying the normalised value.</param>
        /// <param name="fill">Immediate fill image.</param>
        /// <param name="trailingFill">Trailing fill image, may be null.</param>
        public void Bind(FloatEventChannel channel, Image fill, Image trailingFill)
        {
            _channel = channel;
            _fill = fill;
            _trailingFill = trailingFill;
        }

        private void OnValueChanged(float normalized)
        {
            float previous = _current;
            _current = Mathf.Clamp01(normalized);

            // The hold only applies to losses. Gaining resource should show immediately, because
            // the player is usually deciding whether they can afford another action.
            if (_current < previous)
            {
                _trailHoldRemaining = _trailDelaySeconds;
            }
            else
            {
                _trailing = _current;
            }

            ApplyFills();
        }

        /// <summary>
        /// Sizes both fills by stretching their rects rather than using <see cref="Image.fillAmount"/>.
        /// </summary>
        /// <remarks>
        /// <c>fillAmount</c> is silently ignored when an Image has no sprite, and these bars are
        /// plain coloured quads. The symptom is a bar that renders permanently full while every
        /// value behind it updates correctly, which looks like a broken event wiring and is not.
        /// Driving the anchor works regardless of whether a sprite is ever assigned.
        /// </remarks>
        private void ApplyFills()
        {
            SetWidthFraction(_fill, _current);
            SetWidthFraction(_trailingFill, Mathf.Max(_trailing, _current));
        }

        /// <summary>Stretches an image's rect to a fraction of its parent's width.</summary>
        private static void SetWidthFraction(Image image, float fraction)
        {
            if (image == null)
            {
                return;
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
