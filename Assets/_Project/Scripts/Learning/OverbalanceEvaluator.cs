using UnityEngine;

namespace AdaptiveBossArena.Learning
{
    /// <summary>
    /// Decides whether — and how badly — the boss overbalances when a committed swing of its own
    /// misses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the mechanical half of "adapting back". The boss reads a habit and leans into pressing
    /// it (raising the committing tuning parameters — see
    /// <see cref="BossTuning.CommitmentIntensity"/>). When the player then changes that habit, the
    /// boss's committed attacks start hitting empty air, and the harder it had leaned in, the more a
    /// miss costs it: a longer stumble and a stance briefly wide open to a posture break. Out-reading
    /// the read is what buys the opening.
    /// </para>
    /// <para>
    /// It is a <see langword="readonly"/> struct of pure functions over its tuning, with no engine or
    /// player dependencies, so the whole mechanic is exercised in edit-mode tests. It reacts only to
    /// the boss's own whiff and its own commitment — never to anything about the player — which is why
    /// it needs no perception delay to stay honest.
    /// </para>
    /// <para>
    /// Crucially it is <em>dormant</em> until the boss has actually adapted: at zero commitment the
    /// chance is exactly zero, so a fight in which the boss learns nothing plays exactly as before.
    /// </para>
    /// </remarks>
    public readonly struct OverbalanceEvaluator
    {
        /// <summary>
        /// Smallest share of the full window a triggered overbalance still lasts, so a stumble that
        /// fires at the edge of the threshold is still long enough to punish rather than a flicker.
        /// </summary>
        private const float MinRecoveryFraction = 0.4f;

        private readonly float _maxChance;
        private readonly float _intensityThreshold;
        private readonly float _recoverySecondsAtFull;
        private readonly float _poiseMultiplierAtFull;

        /// <summary>Creates an evaluator from its tuning.</summary>
        /// <param name="maxChance">Chance a committed whiff overbalances at full commitment (0..1).</param>
        /// <param name="intensityThreshold">
        /// Commitment below which nothing overbalances, giving a dead zone so minor adaptation does not
        /// trigger it.
        /// </param>
        /// <param name="recoverySecondsAtFull">Stumble length at full commitment, in seconds.</param>
        /// <param name="poiseMultiplierAtFull">
        /// How much incoming poise damage is multiplied while fully overbalanced. At least one, since a
        /// stumble may only ever make the boss more vulnerable, never less.
        /// </param>
        public OverbalanceEvaluator(
            float maxChance,
            float intensityThreshold,
            float recoverySecondsAtFull,
            float poiseMultiplierAtFull)
        {
            _maxChance = Mathf.Clamp01(maxChance);
            _intensityThreshold = Mathf.Clamp01(intensityThreshold);
            _recoverySecondsAtFull = Mathf.Max(0f, recoverySecondsAtFull);
            _poiseMultiplierAtFull = Mathf.Max(1f, poiseMultiplierAtFull);
        }

        /// <summary>
        /// How far past the dead zone the boss's commitment sits, on a 0..1 scale.
        /// </summary>
        /// <param name="commitmentIntensity">The boss's current commitment (0..1).</param>
        /// <returns>Zero at or below the threshold, one at full commitment.</returns>
        public float Severity(float commitmentIntensity)
        {
            if (_intensityThreshold >= 1f)
            {
                return 0f;
            }

            return Mathf.Clamp01((commitmentIntensity - _intensityThreshold) / (1f - _intensityThreshold));
        }

        /// <summary>The probability a committed whiff overbalances at this commitment.</summary>
        /// <param name="commitmentIntensity">The boss's current commitment (0..1).</param>
        /// <returns>Zero at or below the threshold, rising to the maximum chance at full commitment.</returns>
        public float Chance(float commitmentIntensity) => _maxChance * Severity(commitmentIntensity);

        /// <summary>Decides whether a whiff overbalances the boss.</summary>
        /// <param name="wasCommittedAttack">
        /// Whether the swing that missed was a committed one — a lunge, a combo follow-up or a phase
        /// signature. A light poke leaves no opening worth punishing and never overbalances.
        /// </param>
        /// <param name="commitmentIntensity">The boss's current commitment (0..1).</param>
        /// <param name="roll">A uniform sample in [0, 1), drawn from the injected random provider.</param>
        /// <returns>True when the boss should stumble into an open stance.</returns>
        public bool ShouldOverbalance(bool wasCommittedAttack, float commitmentIntensity, float roll) =>
            wasCommittedAttack && roll < Chance(commitmentIntensity);

        /// <summary>How long the resulting stumble lasts.</summary>
        /// <param name="commitmentIntensity">The boss's current commitment (0..1).</param>
        /// <returns>A window that grows with commitment, never shorter than a punishable minimum.</returns>
        public float ExtraRecoverySeconds(float commitmentIntensity) =>
            _recoverySecondsAtFull * Mathf.Lerp(MinRecoveryFraction, 1f, Severity(commitmentIntensity));

        /// <summary>How much incoming poise damage is amplified during the stumble.</summary>
        /// <param name="commitmentIntensity">The boss's current commitment (0..1).</param>
        /// <returns>
        /// One when nothing is at stake, rising toward the full multiplier as commitment grows, so a
        /// hit landed in the window rushes the posture break that opens the execution.
        /// </returns>
        public float PoiseVulnerabilityMultiplier(float commitmentIntensity) =>
            Mathf.Lerp(1f, _poiseMultiplierAtFull, Severity(commitmentIntensity));
    }
}
