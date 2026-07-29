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

        [Header("Timing")]
        [SerializeField]
        [Range(0.2f, 3f)]
        [Tooltip("Pause before the screen appears, so the final blow can land and be seen.")]
        private float _delaySeconds = 1.1f;

        [SerializeField]
        [Range(0.1f, 2f)]
        [Tooltip("Fade-in duration.")]
        private float _fadeSeconds = 0.6f;

        private static readonly Color VictoryColor = new Color(0.6f, 0.9f, 1f);
        private static readonly Color DefeatColor = new Color(1f, 0.45f, 0.45f);

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

            if (_elapsed < _delaySeconds)
            {
                return;
            }

            if (_group != null)
            {
                _group.alpha = Mathf.Clamp01((_elapsed - _delaySeconds) / _fadeSeconds);
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

            if (_panel != null)
            {
                _panel.SetActive(true);
            }

            if (_group != null)
            {
                _group.alpha = 0f;
            }

            if (_headline != null)
            {
                _headline.text = won ? "THE BOSS FALLS" : "YOU FALL";
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
