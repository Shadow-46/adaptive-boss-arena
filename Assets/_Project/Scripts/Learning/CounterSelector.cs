using System.Collections.Generic;
using AdaptiveBossArena.Core.Services;

namespace AdaptiveBossArena.Learning
{
    /// <summary>
    /// Chooses which answer the boss develops to the habits it has observed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Selection is weighted-random over the eligible strategies rather than "strongest habit wins".
    /// A deterministic mapping from habit to counter would be a lookup table, and a player who
    /// learned it would be able to steer the boss deliberately. Weighting means a strong habit is
    /// very likely to be answered, without which answer being certain.
    /// </para>
    /// <para>
    /// Each candidate's weight is its authored weight multiplied by the confidence behind the habit
    /// that triggered it. That is the mechanism that keeps the boss from countering a coincidence:
    /// a strategy triggered by two observations competes at a fraction of the weight of one
    /// triggered by thirty.
    /// </para>
    /// <para>
    /// Randomness comes from an injected provider, so a seed reproduces the boss's decisions
    /// exactly and the whole system stays testable despite being probabilistic.
    /// </para>
    /// </remarks>
    public sealed class CounterSelector
    {
        private readonly IRandomProvider _random;
        private readonly List<CounterStrategy> _candidates = new List<CounterStrategy>();
        private readonly List<float> _weights = new List<float>();

        /// <summary>Creates a selector.</summary>
        /// <param name="random">Seedable randomness, so selection is reproducible.</param>
        public CounterSelector(IRandomProvider random) => _random = random;

        /// <summary>Strategies eligible at the most recent selection. Exposed for the debug overlay.</summary>
        public IReadOnlyList<CounterStrategy> LastCandidates => _candidates;

        /// <summary>
        /// Picks a strategy to adopt, or returns null when nothing is worth adopting.
        /// </summary>
        /// <param name="catalogue">Every strategy the boss could develop.</param>
        /// <param name="profile">The boss's current picture of the player.</param>
        /// <param name="currentPhase">One-based phase currently in effect.</param>
        /// <param name="alreadyAdopted">Strategies already in effect, skipped unless repeatable.</param>
        /// <returns>The chosen strategy, or null.</returns>
        public CounterStrategy Select(
            IReadOnlyList<CounterStrategy> catalogue,
            BehaviorProfile profile,
            int currentPhase,
            ICollection<CounterStrategy> alreadyAdopted)
        {
            GatherCandidates(catalogue, profile, currentPhase, alreadyAdopted);

            if (_candidates.Count == 0)
            {
                return null;
            }

            return PickWeighted();
        }

        /// <summary>Collects every strategy whose trigger is currently satisfied.</summary>
        private void GatherCandidates(
            IReadOnlyList<CounterStrategy> catalogue,
            BehaviorProfile profile,
            int currentPhase,
            ICollection<CounterStrategy> alreadyAdopted)
        {
            _candidates.Clear();
            _weights.Clear();

            if (catalogue == null)
            {
                return;
            }

            for (int i = 0; i < catalogue.Count; i++)
            {
                CounterStrategy strategy = catalogue[i];

                if (strategy == null)
                {
                    continue;
                }

                if (!strategy.IsRepeatable && alreadyAdopted != null && alreadyAdopted.Contains(strategy))
                {
                    continue;
                }

                FeatureReading reading = profile.Get(strategy.TriggerFeature);

                if (!strategy.IsEligible(reading.Value, reading.Confidence, currentPhase))
                {
                    continue;
                }

                _candidates.Add(strategy);

                // Confidence scales the weight rather than merely gating it, so among several
                // eligible answers the boss leans toward the one it has the best evidence for.
                _weights.Add(strategy.SelectionWeight * reading.Confidence);
            }
        }

        /// <summary>Draws one candidate in proportion to its weight.</summary>
        private CounterStrategy PickWeighted()
        {
            float total = 0f;
            for (int i = 0; i < _weights.Count; i++)
            {
                total += _weights[i];
            }

            if (total <= 0f)
            {
                return _candidates[_random.NextInt(0, _candidates.Count)];
            }

            float roll = _random.NextFloat(0f, total);
            float cumulative = 0f;

            for (int i = 0; i < _candidates.Count; i++)
            {
                cumulative += _weights[i];

                if (roll <= cumulative)
                {
                    return _candidates[i];
                }
            }

            // Floating-point accumulation can leave the roll marginally past the total.
            return _candidates[_candidates.Count - 1];
        }
    }
}
