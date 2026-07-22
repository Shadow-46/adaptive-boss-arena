using AdaptiveBossArena.Core.ScriptableObjects;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Core.Perception
{
    /// <summary>
    /// Human-plausibility limits applied to every decision the boss makes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Restricting the boss to visible information is necessary but not sufficient. A boss that acts
    /// on visible information with zero delay and perfect consistency still reads as a cheat. This
    /// asset supplies the two remaining ingredients: a lag before a perceived event can be acted
    /// on, and variance so identical situations do not produce identical responses.
    /// </para>
    /// <para>
    /// Defaults are drawn from human reaction research: roughly 250 milliseconds for a simple
    /// visual stimulus, and longer when a choice between responses is involved. Later boss phases
    /// tighten these toward the fast end rather than below it, so escalation never crosses into
    /// superhuman territory.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "ReactionProfile",
        menuName = "Adaptive Boss Arena/AI/Reaction Profile",
        order = 0)]
    public sealed class ReactionProfile : IdentifiableSO
    {
        [Header("Perception")]
        [SerializeField]
        [Range(0.05f, 0.5f)]
        [Tooltip("How stale the player observations handed to the AI are. This is what makes feints " +
                 "and baits genuinely work against the boss.")]
        private float _perceptionLatencySeconds = 0.14f;

        [Header("Reaction")]
        [SerializeField]
        [Range(0.1f, 1f)]
        [Tooltip("Fastest the boss may respond to a perceived event.")]
        private float _minReactionSeconds = 0.22f;

        [SerializeField]
        [Range(0.1f, 1.5f)]
        [Tooltip("Slowest the boss will take to respond to a perceived event.")]
        private float _maxReactionSeconds = 0.42f;

        [Header("Fallibility")]
        [SerializeField]
        [Range(0f, 0.5f)]
        [Tooltip("Chance the boss simply fails to act on a perceived opening, as a human would. " +
                 "Set to zero only for a deliberately mechanical boss.")]
        private float _missedOpportunityChance = 0.12f;

        /// <summary>Delay applied to all player observations before the AI may see them.</summary>
        public float PerceptionLatencySeconds => _perceptionLatencySeconds;

        /// <summary>Fastest permitted reaction, in seconds.</summary>
        public float MinReactionSeconds => _minReactionSeconds;

        /// <summary>Slowest permitted reaction, in seconds.</summary>
        public float MaxReactionSeconds => Mathf.Max(_maxReactionSeconds, _minReactionSeconds);

        /// <summary>Probability that the boss overlooks an opening it could have punished.</summary>
        public float MissedOpportunityChance => _missedOpportunityChance;

        /// <summary>Rolls a reaction delay for a single decision.</summary>
        /// <param name="random">Seedable randomness source, so tests stay deterministic.</param>
        /// <returns>A delay in seconds within the configured band.</returns>
        public float RollReactionDelay(IRandomProvider random) =>
            random.NextFloat(MinReactionSeconds, MaxReactionSeconds);

        /// <summary>Rolls whether the boss fails to notice an opening this time.</summary>
        /// <param name="random">Seedable randomness source.</param>
        /// <returns>True when the opening should be ignored.</returns>
        public bool RollMissedOpportunity(IRandomProvider random) =>
            random.NextFloat01() < _missedOpportunityChance;

#if UNITY_EDITOR
        /// <inheritdoc />
        protected override void OnValidate()
        {
            base.OnValidate();

            // A profile whose minimum exceeds its maximum would silently invert the reaction band.
            if (_maxReactionSeconds < _minReactionSeconds)
            {
                _maxReactionSeconds = _minReactionSeconds;
            }
        }
#endif
    }
}
