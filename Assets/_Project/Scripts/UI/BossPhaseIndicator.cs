using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Events;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveBossArena.UI
{
    /// <summary>
    /// Shows how far through its phases the boss has been driven.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pips rather than a number, because the useful information is not "phase two" but "one of
    /// three done, two to go". A boss health bar alone does not convey that, since the phases are
    /// where the fight actually changes character.
    /// </para>
    /// <para>
    /// It also gives the escalation a moment of acknowledgement. Crossing into a new phase is the
    /// clearest beat in the encounter and deserves to be marked.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BossPhaseIndicator : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField]
        [Tooltip("Channel carrying the zero-based phase index.")]
        private IntEventChannel _phaseChannel;

        [Header("Pips")]
        [SerializeField]
        [Tooltip("One image per phase, in order.")]
        private Image[] _pips = new Image[0];

        [SerializeField]
        [Tooltip("Colour of a phase the boss has not yet reached.")]
        private Color _pendingColor = new Color(1f, 1f, 1f, 0.25f);

        [SerializeField]
        [Tooltip("Colour of the phase currently in effect.")]
        private Color _activeColor = new Color(1f, 0.35f, 0.3f, 1f);

        [SerializeField]
        [Tooltip("Colour of a phase already driven through.")]
        private Color _clearedColor = new Color(0.6f, 0.6f, 0.65f, 0.8f);

        private void OnEnable()
        {
            if (_phaseChannel != null)
            {
                _phaseChannel.Raised += OnPhaseChanged;
            }

            OnPhaseChanged(0);
        }

        private void OnDisable()
        {
            if (_phaseChannel != null)
            {
                _phaseChannel.Raised -= OnPhaseChanged;
            }
        }

        /// <summary>Assigns the channel and pip images. Used by the interface generator.</summary>
        /// <param name="channel">Channel carrying the phase index.</param>
        /// <param name="pips">Pip images in phase order.</param>
        public void Bind(IntEventChannel channel, Image[] pips)
        {
            _phaseChannel = channel;
            _pips = pips ?? new Image[0];
        }

        private void OnPhaseChanged(int phaseIndex)
        {
            for (int i = 0; i < _pips.Length; i++)
            {
                if (_pips[i] == null)
                {
                    continue;
                }

                _pips[i].color = ResolveColor(i, phaseIndex);
            }
        }

        private Color ResolveColor(int pipIndex, int phaseIndex)
        {
            if (pipIndex < phaseIndex)
            {
                return _clearedColor;
            }

            return pipIndex == phaseIndex ? _activeColor : _pendingColor;
        }

        /// <summary>Number of pips the indicator expects, matching the configured phase count.</summary>
        public static int ExpectedPipCount => GameplayConstants.BossPhaseCount;
    }
}
