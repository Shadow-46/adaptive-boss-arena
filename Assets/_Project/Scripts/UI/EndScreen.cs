using System;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveBossArena.UI
{
    /// <summary>
    /// The panel shown once the fight is decided, whichever way it went.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One component covers both outcomes, because the two screens differ only in wording and
    /// colour. Splitting them would duplicate the fade, the summary layout and the restart wiring
    /// for no benefit.
    /// </para>
    /// <para>
    /// The summary deliberately leads with how many strategies the boss developed rather than with
    /// the clock. In this game that number is the more meaningful score: it says how readable the
    /// player was, which is the thing the whole encounter is built to test.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EndScreen : MonoBehaviour
    {
        [Header("Presentation")]
        [SerializeField]
        [Tooltip("Root object containing the panel.")]
        private GameObject _panel;

        [SerializeField]
        [Tooltip("Group faded in when the fight ends.")]
        private CanvasGroup _group;

        [SerializeField]
        [Tooltip("Headline text, such as victory or defeat.")]
        private Text _headline;

        [SerializeField]
        [Tooltip("Summary of how the attempt went.")]
        private Text _summary;

        [SerializeField]
        [Tooltip("The boss's dossier: what it read in the player and how it answered.")]
        private Text _dossier;

        [SerializeField]
        [Tooltip("The button selected when the report appears, so a gamepad can retry without a mouse.")]
        private GameObject _firstSelected;

        private bool _selectedOnReport;

        [Header("Outcome Card")]
        [SerializeField]
        [Tooltip("The band across the screen that carries the one-line verdict before the report.")]
        private CanvasGroup _card;

        [SerializeField]
        [Tooltip("The verdict's lettering.")]
        private Text _cardText;

        [Header("Timing")]
        [SerializeField]
        [Range(0.2f, 3f)]
        [Tooltip("Pause before the screen appears, so the final blow can land and be seen.")]
        private float _delaySeconds = 1.1f;

        [SerializeField]
        [Range(0.1f, 2f)]
        [Tooltip("Fade-in duration.")]
        private float _fadeSeconds = 0.6f;

        // Tarnished gold for a victory and old blood for a defeat, spaced like an inscription. Pale blue and
        // pink capitals read as an arcade's game-over card.
        private static readonly Color VictoryColor = new Color(0.84f, 0.72f, 0.46f);
        private static readonly Color DefeatColor = new Color(0.66f, 0.09f, 0.06f);

        /// <summary>When the card starts to fade in, after the last blow has landed.</summary>
        public const float CardInStart = 0.35f;

        /// <summary>When the card is fully in.</summary>
        public const float CardInEnd = 1.05f;

        /// <summary>When the card starts to fade out.</summary>
        public const float CardOutStart = 2.75f;

        /// <summary>When the card has gone, and the report can follow.</summary>
        public const float CardOutEnd = 3.25f;

        /// <summary>How far the lettering spreads as it arrives, as a fraction of its width.</summary>
        private const float CardSpread = 0.08f;

        /// <summary>The verdict on a win: Igris is the Blood-Red Commander, and he has been felled.</summary>
        public const string VictoryVerdict = "Commander felled";

        /// <summary>The verdict on a death.</summary>
        public const string DefeatVerdict = "Fallen";

        private bool _isRunning;
        private float _elapsed;

        /// <summary>Raised when the player asks to try again.</summary>
        public event Action RestartRequested;

        /// <summary>True once the fight has been decided and this screen has taken over.</summary>
        public bool IsShown => _isRunning;

        private void Awake() => Hide();

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            UpdateCard();

            float delay = ReportDelay;

            if (_elapsed < delay)
            {
                return;
            }

            if (_group != null)
            {
                _group.alpha = Mathf.Clamp01((_elapsed - delay) / _fadeSeconds);
            }

            if (!_selectedOnReport && _firstSelected != null && UnityEngine.EventSystems.EventSystem.current != null)
            {
                _selectedOnReport = true;
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(_firstSelected);
            }
        }

        /// <summary>Sets the button selected when the report appears. Used by the interface generator.</summary>
        /// <param name="button">Usually Try Again.</param>
        public void SetFirstSelected(GameObject button) => _firstSelected = button;

        /// <summary>
        /// Seconds after the end before the report appears: after the card when there is one.
        /// </summary>
        /// <remarks>
        /// The card is the moment; the report is the reading. Shown together, the report's paragraphs pulled the
        /// eye off the one line that matters at the instant the fight was decided.
        /// </remarks>
        private float ReportDelay => _card != null ? Mathf.Max(_delaySeconds, CardOutEnd) : _delaySeconds;

        /// <summary>Whether the verdict card is on screen now. For tests.</summary>
        public bool IsCardShowing => _card != null && _card.alpha > 0f;

        /// <summary>The card's opacity at a moment after the end: in, held, out.</summary>
        /// <param name="elapsed">Seconds since the fight was decided.</param>
        /// <returns>Zero to one.</returns>
        public static float CardAlpha(float elapsed)
        {
            if (elapsed <= CardInStart || elapsed >= CardOutEnd)
            {
                return 0f;
            }

            if (elapsed < CardInEnd)
            {
                return Mathf.SmoothStep(0f, 1f, (elapsed - CardInStart) / (CardInEnd - CardInStart));
            }

            return elapsed < CardOutStart
                ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, (elapsed - CardOutStart) / (CardOutEnd - CardOutStart));
        }

        /// <summary>Fades the card and spreads its lettering as it arrives, like breath let out.</summary>
        private void UpdateCard()
        {
            if (_card == null)
            {
                return;
            }

            _card.alpha = CardAlpha(_elapsed);

            if (_cardText != null)
            {
                float arrival = Mathf.Clamp01((_elapsed - CardInStart) / (CardOutEnd - CardInStart));
                float spread = 1f - CardSpread + CardSpread * Mathf.Sqrt(arrival);
                _cardText.rectTransform.localScale = new Vector3(spread, 1f, 1f);
            }
        }

        /// <summary>
        /// Shows the outcome.
        /// </summary>
        /// <param name="won">Whether the boss was defeated.</param>
        /// <param name="durationSeconds">How long the attempt lasted.</param>
        /// <param name="adaptationsAllowed">Counter-strategies the boss developed.</param>
        /// <param name="isNewRecord">Whether this attempt set a personal best.</param>
        /// <param name="dossier">The boss's read on the player, or empty for none.</param>
        public void Show(bool won, float durationSeconds, int adaptationsAllowed, bool isNewRecord,
            string dossier = "")
        {
            _isRunning = true;
            _elapsed = 0f;
            _selectedOnReport = false;

            if (_panel != null)
            {
                _panel.SetActive(true);
            }

            if (_group != null)
            {
                _group.alpha = 0f;
            }

            if (_card != null)
            {
                _card.alpha = 0f;
                _card.gameObject.SetActive(true);
            }

            if (_cardText != null)
            {
                _cardText.text = Core.Constants.BossIdentity.Spaced(won ? VictoryVerdict : DefeatVerdict);
                _cardText.color = won ? VictoryColor : DefeatColor;
            }

            if (_headline != null)
            {
                _headline.text = Core.Constants.BossIdentity.Spaced(won ? $"{Core.Constants.BossIdentity.Name} falls" : "You fall");
                _headline.color = won ? VictoryColor : DefeatColor;
            }

            if (_summary != null)
            {
                _summary.text = BuildSummary(won, durationSeconds, adaptationsAllowed, isNewRecord);
            }

            if (_dossier != null)
            {
                _dossier.text = dossier ?? string.Empty;
            }
        }

        /// <summary>Hides the panel and returns to the neutral state.</summary>
        public void Hide()
        {
            _isRunning = false;
            _elapsed = 0f;

            if (_group != null)
            {
                _group.alpha = 0f;
            }

            if (_panel != null)
            {
                _panel.SetActive(false);
            }

            if (_card != null)
            {
                _card.alpha = 0f;
                _card.gameObject.SetActive(false);
            }
        }

        /// <summary>Asks for another attempt. Bound to the panel's retry button.</summary>
        public void Restart()
        {
            Hide();
            RestartRequested?.Invoke();
        }

        /// <summary>Assigns the references. Used by the interface generator.</summary>
        /// <param name="panel">Root object containing the panel.</param>
        /// <param name="group">Group to fade.</param>
        /// <param name="headline">Headline text.</param>
        /// <param name="summary">Summary text.</param>
        /// <param name="dossier">The boss's read-on-the-player text.</param>
        /// <summary>Assigns the verdict card. Used by the interface generator.</summary>
        /// <param name="card">The card's group.</param>
        /// <param name="cardText">Its lettering.</param>
        public void BindCard(CanvasGroup card, Text cardText)
        {
            _card = card;
            _cardText = cardText;
        }

        public void Bind(GameObject panel, CanvasGroup group, Text headline, Text summary, Text dossier)
        {
            _panel = panel;
            _group = group;
            _headline = headline;
            _summary = summary;
            _dossier = dossier;
        }

        /// <summary>Composes the outcome summary.</summary>
        private static string BuildSummary(
            bool won,
            float durationSeconds,
            int adaptationsAllowed,
            bool isNewRecord)
        {
            var minutes = Mathf.FloorToInt(durationSeconds / 60f);
            var seconds = Mathf.FloorToInt(durationSeconds % 60f);

            string readAsText = adaptationsAllowed == 0
                ? "It never worked you out."
                : adaptationsAllowed == 1
                    ? "It worked out one of your habits."
                    : $"It worked out {adaptationsAllowed} of your habits.";

            string record = isNewRecord ? "\n\nNew personal best." : string.Empty;

            string closing = won
                ? "\n\nTry winning while giving it less to learn from."
                : "\n\nChange something. It only counters what you repeat.";

            return $"{readAsText}\n\nTime: {minutes:00}:{seconds:00}{record}{closing}";
        }
    }
}
