using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Learning;
using UnityEngine;

namespace AdaptiveBossArena.AI
{
    /// <summary>
    /// Chooses which attack the boss throws next.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Selection is weighted and random rather than a rule chain. A boss that always answered a
    /// given distance with a given attack would be a lookup table, and a player who learned the
    /// table would stop having to think. Weighting means distance strongly influences the choice
    /// without ever fully determining it.
    /// </para>
    /// <para>
    /// This is also where adaptation becomes visible in what the boss does rather than merely in how
    /// fast it does it. A boss that has learned the player kites will weight its gap-closer heavily;
    /// one that has learned they dodge everything will start reaching for attacks that cover ground
    /// instead of attacks that aim.
    /// </para>
    /// </remarks>
    public sealed class BossAttackSelector
    {
        /// <summary>Weight floor, so no attack in the phase's catalogue becomes impossible.</summary>
        private const float MinimumWeight = 0.05f;

        /// <summary>Weight given to an attack whose reach matches the current distance exactly.</summary>
        private const float IdealRangeWeight = 1f;

        /// <summary>How sharply weight falls away as an attack's reach mismatches the distance.</summary>
        private const float RangeFalloff = 0.6f;

        /// <summary>Extra weight the phase's signature attack carries, so each phase has a face.</summary>
        /// <remarks>
        /// A bonus rather than an override: the signature is markedly more likely but never certain,
        /// so it reads as this phase's threat without becoming a rhythm the player can set a metronome
        /// to. Its distinct telegraph is what makes the raised frequency land as "here it comes".
        /// </remarks>
        private const float SignatureWeightBonus = 1.1f;

        private readonly IRandomProvider _random;
        private float[] _weights = new float[8];

        /// <summary>Creates a selector.</summary>
        /// <param name="random">Seedable randomness, so a seed reproduces the same fight.</param>
        public BossAttackSelector(IRandomProvider random) => _random = random;

        /// <summary>
        /// Picks an attack from the phase's catalogue.
        /// </summary>
        /// <param name="context">Boss context supplying distance, tuning and the current phase.</param>
        /// <returns>The chosen attack, or null when the phase has no usable attacks.</returns>
        public AttackDefinition Select(BossContext context)
        {
            AttackDefinition[] catalogue = context.CurrentPhase.Attacks;

            if (catalogue == null || catalogue.Length == 0)
            {
                return null;
            }

            EnsureWeightCapacity(catalogue.Length);

            float totalWeight = ScoreCatalogue(context, catalogue);

            return totalWeight <= 0f
                ? catalogue[_random.NextInt(0, catalogue.Length)]
                : PickWeighted(catalogue, totalWeight);
        }

        /// <summary>Scores every attack in the catalogue and returns the total weight.</summary>
        private float ScoreCatalogue(BossContext context, AttackDefinition[] catalogue)
        {
            float distance = context.DistanceToPlayer;
            float gapCloserBias = context.Tuning.Get(BossTuningParameter.GapCloserWeight);
            float areaDenialBias = context.Tuning.Get(BossTuningParameter.AreaDenialWeight);
            AttackDefinition signature = context.CurrentPhase.SignatureAttack;

            float total = 0f;

            for (int i = 0; i < catalogue.Length; i++)
            {
                AttackDefinition attack = catalogue[i];

                if (attack == null)
                {
                    _weights[i] = 0f;
                    continue;
                }

                float weight = RangeSuitability(attack, distance);
                weight += gapCloserBias * GapCloserAffinity(attack);
                weight += areaDenialBias * AreaCoverageAffinity(attack);

                if (signature != null && attack == signature)
                {
                    weight += SignatureWeightBonus;
                }

                _weights[i] = Mathf.Max(MinimumWeight, weight);
                total += _weights[i];
            }

            return total;
        }

        /// <summary>
        /// How well an attack's reach matches the current distance.
        /// </summary>
        /// <remarks>
        /// A lunging attack is credited with the ground its lunge covers, so a gap-closer scores well
        /// at exactly the distances a stationary attack of the same reach would score badly.
        /// </remarks>
        private static float RangeSuitability(AttackDefinition attack, float distance)
        {
            float effectiveReach = attack.Range + attack.LungeSpeed * attack.StartupSeconds;
            float mismatch = Mathf.Abs(effectiveReach - distance);

            return Mathf.Max(0f, IdealRangeWeight - mismatch * RangeFalloff);
        }

        /// <summary>How much an attack behaves like a gap-closer.</summary>
        private static float GapCloserAffinity(AttackDefinition attack) =>
            attack.LungeSpeed > 0f ? 1f : 0f;

        /// <summary>How much ground an attack covers, favouring wide arcs and long reach.</summary>
        private static float AreaCoverageAffinity(AttackDefinition attack)
        {
            float arcFraction = Mathf.Clamp01(attack.ArcDegrees / 360f);
            float reachFraction = Mathf.Clamp01(attack.Range / 8f);

            return (arcFraction + reachFraction) * 0.5f;
        }

        /// <summary>Draws one attack in proportion to the scored weights.</summary>
        private AttackDefinition PickWeighted(AttackDefinition[] catalogue, float totalWeight)
        {
            float roll = _random.NextFloat(0f, totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < catalogue.Length; i++)
            {
                cumulative += _weights[i];

                if (roll <= cumulative && catalogue[i] != null)
                {
                    return catalogue[i];
                }
            }

            // Floating-point accumulation can leave the roll marginally past the total. Falling back
            // to the last valid entry is correct and costs nothing.
            for (int i = catalogue.Length - 1; i >= 0; i--)
            {
                if (catalogue[i] != null)
                {
                    return catalogue[i];
                }
            }

            return null;
        }

        /// <summary>Grows the reusable weight buffer when a phase has more attacks than before.</summary>
        private void EnsureWeightCapacity(int required)
        {
            if (_weights.Length < required)
            {
                _weights = new float[required];
            }
        }
    }
}
