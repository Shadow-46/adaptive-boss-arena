using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// A ribbon that follows the weapon through a swing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The single cheapest way to make an attack look like it travelled somewhere. Without it a
    /// swing is a pose change between two frames; with it the arc is visible after the fact, which is
    /// what lets a player read range and direction from an attack they only half saw.
    /// </para>
    /// <para>
    /// The trail is emitted only during the swing and then left to fade on its own. Clearing it on
    /// stop would cut the ribbon off mid-air at the exact moment the eye is following it.
    /// </para>
    /// <para>
    /// Time is unscaled deliberately. During hit-stop the swing freezes, and a trail that kept
    /// ageing would evaporate while the frozen frame was still on screen.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrailRenderer))]
    public sealed class WeaponTrail : MonoBehaviour
    {
        [SerializeField]
        [Range(0.1f, 1.5f)]
        [Tooltip("How long the ribbon is emitted for after a swing begins. Covers wind-up through " +
                 "recovery without bleeding into the next attack.")]
        private float _emitSeconds = 0.45f;

        private TrailRenderer _trail;
        private float _remaining;

        private void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
            _trail.emitting = false;
        }

        /// <summary>Starts emitting, or extends emission if a swing is already under way.</summary>
        public void Begin()
        {
            // Restarting rather than accumulating: a combo's second hit should get a full ribbon, not
            // an ever-lengthening one.
            _remaining = _emitSeconds;
            _trail.emitting = true;
        }

        private void Update()
        {
            if (_remaining <= 0f)
            {
                return;
            }

            _remaining -= Time.unscaledDeltaTime;

            if (_remaining <= 0f)
            {
                _trail.emitting = false;
            }
        }
    }
}
