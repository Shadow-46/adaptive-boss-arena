using System.Collections.Generic;
using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveBossArena.UI
{
    /// <summary>
    /// Announces on screen that the boss has changed how it fights.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the single most important element in the interface, and the reason every
    /// <see cref="AdaptiveBossArena.Learning.CounterStrategy"/> is required to carry a tell.
    /// </para>
    /// <para>
    /// The boss's adaptations are, by design, gradual and subtle: a slightly longer pause before it
    /// commits, a slightly tighter preferred range. A player who notices the effect without being
    /// told the cause will not conclude "it has learned that I always attack after dodging". They
    /// will conclude it is reading their controller. Announcing the change is what converts a
    /// suspicion of cheating into the intended experience of being outplayed.
    /// </para>
    /// <para>
    /// The presentation is deliberately restrained: a brief line near the top of the screen, then
    /// gone. A modal popup would stop the fight to explain itself, which would be worse than saying
    /// nothing.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class AdaptationTellDisplay : MonoBehaviour
    {
        /// <summary>Longest a queued tell waits before being shown, so it cannot arrive stale.</summary>
        private const float MaxQueueAgeSeconds = 6f;

        [Header("Source")]
        [SerializeField]
        [Tooltip("Channel carrying the tell message for a newly adopted counter-strategy.")]
        private StringEventChannel _adaptationChannel;

        [Header("Presentation")]
        [SerializeField]
        [Tooltip("Text element the message is written into.")]
        private Text _label;

        [SerializeField]
        [Tooltip("Group faded in and out to show and hide the message.")]
        private CanvasGroup _group;

        [Header("Timing")]
        [SerializeField]
        [Range(1f, 8f)]
        [Tooltip("How long a message stays fully visible.")]
        private float _holdSeconds = 3f;

        [SerializeField]
        [Range(0.1f, 1.5f)]
        [Tooltip("Fade duration at each end.")]
        private float _fadeSeconds = 0.4f;

        private readonly Queue<QueuedTell> _pending = new Queue<QueuedTell>();

        private float _visibleFor;
        private bool _isShowing;

        /// <summary>A tell waiting its turn, with the time it was raised.</summary>
        private readonly struct QueuedTell
        {
            public QueuedTell(string message, float raisedAt)
            {
                Message = message;
                RaisedAt = raisedAt;
            }

            public string Message { get; }

            public float RaisedAt { get; }
        }

        /// <summary>Multiplies the hold duration, read from the global accessibility setting.</summary>
        private static float HoldMultiplier => Mathf.Max(1f, FeedbackSettings.TellHoldMultiplier);

        private void OnEnable()
        {
            if (_adaptationChannel != null)
            {
                _adaptationChannel.Raised += OnAdaptationAdopted;
            }

            if (_group != null)
            {
                _group.alpha = 0f;
            }
        }

        private void OnDisable()
        {
            if (_adaptationChannel != null)
            {
                _adaptationChannel.Raised -= OnAdaptationAdopted;
            }
        }

        private void Update()
        {
            // Unscaled, so a tell that lands during a hit-stop or a perfect-dodge slow-motion still
            // reads at a normal pace.
            float deltaTime = Time.unscaledDeltaTime;

            if (_isShowing)
            {
                AdvanceVisible(deltaTime);
                return;
            }

            TryShowNext();
        }

        /// <summary>Assigns the channel and presentation elements. Used by the interface generator.</summary>
        /// <param name="channel">Channel carrying tell messages.</param>
        /// <param name="label">Text element to write into.</param>
        /// <param name="group">Group to fade.</param>
        public void Bind(StringEventChannel channel, Text label, CanvasGroup group)
        {
            _adaptationChannel = channel;
            _label = label;
            _group = group;
        }

        private void OnAdaptationAdopted(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            // Queued rather than shown immediately. Two adaptations landing close together would
            // otherwise have the second overwrite the first before it had been read.
            _pending.Enqueue(new QueuedTell(message, Time.unscaledTime));
        }

        private void TryShowNext()
        {
            while (_pending.Count > 0)
            {
                QueuedTell next = _pending.Dequeue();

                // A tell that waited too long describes a change the player has already lived
                // through, so showing it would be actively confusing.
                if (Time.unscaledTime - next.RaisedAt > MaxQueueAgeSeconds)
                {
                    continue;
                }

                if (_label != null)
                {
                    _label.text = next.Message;
                }

                _isShowing = true;
                _visibleFor = 0f;
                return;
            }
        }

        private void AdvanceVisible(float deltaTime)
        {
            _visibleFor += deltaTime;

            float totalDuration = _fadeSeconds * 2f + _holdSeconds * HoldMultiplier;

            if (_visibleFor >= totalDuration)
            {
                _isShowing = false;

                if (_group != null)
                {
                    _group.alpha = 0f;
                }

                return;
            }

            if (_group != null)
            {
                _group.alpha = ResolveAlpha(totalDuration);
            }
        }

        /// <summary>Fades in, holds, then fades out.</summary>
        private float ResolveAlpha(float totalDuration)
        {
            if (_visibleFor < _fadeSeconds)
            {
                return MathUtil.Remap01(_visibleFor, 0f, _fadeSeconds);
            }

            float fadeOutStart = totalDuration - _fadeSeconds;

            return _visibleFor > fadeOutStart
                ? 1f - MathUtil.Remap01(_visibleFor, fadeOutStart, totalDuration)
                : 1f;
        }
    }
}
