using System;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveBossArena.UI
{
    /// <summary>
    /// The short "ready… fight!" beat that opens a fight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A cold open — where the boss is already swinging the instant control returns — reads as the
    /// game having started without you. This holds the fight for a moment so the player can settle,
    /// then releases it on an unmistakable cue. The director freezes time around this; the overlay
    /// itself only decides what to show and for how long.
    /// </para>
    /// <para>
    /// Everything here runs on unscaled time, because the world it sits in front of is paused. It
    /// reports completion through a callback rather than a coroutine so the director stays the single
    /// owner of when the fight actually begins.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RoundIntro : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Root object containing the banner.")]
        private GameObject _panel;

        [SerializeField]
        [Tooltip("Group faded out as the fight is released.")]
        private CanvasGroup _group;

        [SerializeField]
        [Tooltip("The single large banner line.")]
        private Text _label;

        [Header("Timing")]
        [SerializeField]
        [Range(0.2f, 2f)]
        [Tooltip("How long the 'ready' cue holds before the release.")]
        private float _readySeconds = 0.9f;

        [SerializeField]
        [Range(0.2f, 1.5f)]
        [Tooltip("How long the 'fight' cue stays up as it fades.")]
        private float _fightSeconds = 0.6f;

        private static readonly Color ReadyColor = new Color(0.85f, 0.9f, 1f);
        private static readonly Color FightColor = new Color(1f, 0.55f, 0.3f);

        private Action _onComplete;
        private bool _isRunning;
        private float _elapsed;
        private bool _releasedShown;

        /// <summary>True while the intro is still playing.</summary>
        public bool IsRunning => _isRunning;

        private void Awake() => Hide();

        /// <summary>Starts the intro, calling back once it finishes.</summary>
        /// <param name="onComplete">Invoked when the fight should be released. Always called once.</param>
        public void Begin(Action onComplete)
        {
            _onComplete = onComplete;
            _isRunning = true;
            _elapsed = 0f;
            _releasedShown = false;

            if (_panel != null)
            {
                _panel.SetActive(true);
            }

            ShowReady();
        }

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;

            if (!_releasedShown && _elapsed >= _readySeconds)
            {
                ShowFight();
            }

            if (_releasedShown && _group != null)
            {
                // Fade the banner out across the 'fight' window so the last thing the player sees
                // before acting is the cue thinning away rather than blinking off.
                float fightElapsed = _elapsed - _readySeconds;
                _group.alpha = Mathf.Clamp01(1f - fightElapsed / _fightSeconds);
            }

            if (_elapsed >= _readySeconds + _fightSeconds)
            {
                Complete();
            }
        }

        /// <summary>Hides the banner and clears any run in progress.</summary>
        public void Hide()
        {
            _isRunning = false;

            if (_group != null)
            {
                _group.alpha = 0f;
            }

            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void ShowReady()
        {
            if (_group != null)
            {
                _group.alpha = 1f;
            }

            if (_label != null)
            {
                _label.text = "READY?";
                _label.color = ReadyColor;
            }
        }

        private void ShowFight()
        {
            _releasedShown = true;

            if (_label != null)
            {
                _label.text = "FIGHT!";
                _label.color = FightColor;
            }
        }

        private void Complete()
        {
            Action callback = _onComplete;
            _onComplete = null;

            Hide();

            // Invoked last, after this component is fully back at rest, so the director is free to
            // begin the attempt — including anything that inspects this intro — without re-entrancy.
            callback?.Invoke();
        }

        /// <summary>Assigns the references. Used by the interface generator.</summary>
        /// <param name="panel">Root object containing the banner.</param>
        /// <param name="group">Group to fade.</param>
        /// <param name="label">The banner line.</param>
        public void Bind(GameObject panel, CanvasGroup group, Text label)
        {
            _panel = panel;
            _group = group;
            _label = label;
        }
    }
}
