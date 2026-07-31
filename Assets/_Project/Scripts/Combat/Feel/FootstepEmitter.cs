using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Plays a footstep every stride of actual travel, giving movement an audible weight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Driven by <em>distance moved</em> rather than by speed or a timer, so steps land in cadence
    /// with the character's real motion and stop the instant it does — including during a hit-stop,
    /// when the transform simply is not moving, so no clock needs consulting.
    /// </para>
    /// <para>
    /// Presentation only: it reads its own transform and plays a sound, and knows nothing of combat.
    /// The player and boss use the same component with different cues and stride lengths, so the boss
    /// lands heavier, slower footfalls than the player without any special-casing.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FootstepEmitter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Audio cue played on each footstep.")]
        private string _cueId = "step.player";

        [SerializeField]
        [Tooltip("Distance travelled between footsteps. Longer strides read as a larger character.")]
        private float _strideLength = 1.6f;

        private IAudioService _audio;
        private Vector3 _lastPosition;
        private float _accumulated;

        /// <summary>Assigns the cue and stride. Used by the prefab generator.</summary>
        /// <param name="cueId">Footstep cue to play.</param>
        /// <param name="strideLength">Distance between steps.</param>
        public void Configure(string cueId, float strideLength)
        {
            _cueId = cueId;
            _strideLength = Mathf.Max(0.2f, strideLength);
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;

            // Primed so the first footstep lands almost as soon as the character starts moving,
            // rather than after a full silent stride.
            _accumulated = _strideLength * 0.6f;
        }

        private void Update()
        {
            _audio ??= ServiceRegistry.Current != null &&
                       ServiceRegistry.Current.TryGet(out IAudioService audio)
                ? audio
                : null;

            Vector3 position = transform.position;
            Vector3 delta = position - _lastPosition;
            delta.y = 0f;
            _lastPosition = position;

            float moved = delta.magnitude;

            // A jump larger than a stride is a teleport (a retry respawn), not a step — skip it so the
            // reset does not fire a footstep at the spawn point.
            if (moved <= 0f || moved > _strideLength)
            {
                return;
            }

            _accumulated += moved;

            if (_accumulated >= _strideLength)
            {
                _accumulated -= _strideLength;
                _audio?.PlayCue(_cueId, position);
            }
        }
    }
}
