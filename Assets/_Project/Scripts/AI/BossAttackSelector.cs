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
        /// <remarks>
        /// Gentle, with a plateau around the ideal reach, so at any distance two to four blows genuinely
        /// compete. A steep falloff handed each distance to a single move, which is how the boss came to throw
        /// the same blow again and again.
        /// </remarks>
        private const float RangeFalloff = 0.35f;

        /// <summary>Mismatch, in metres, within which an attack counts as perfectly in range.</summary>
        private const float RangePlateauMetres = 0.75f;

        /// <summary>Extra weight the phase's signature attack carries, so each phase has a face.</summary>
        /// <remarks>
        /// A bonus rather than an override: the signature is markedly more likely but never certain,
        /// so it reads as this phase's threat without becoming a rhythm the player can set a metronome
        /// to. Its distinct telegraph is what makes the raised frequency land as "here it comes".
        /// Trimmed from 1.1 once players reported the boss throwing the same blow again and again: at long range
        /// the opening phase's leaping slam, being both the signature and the only blow that reaches, came up in
        /// nearly half of all picks.
        /// </remarks>
        private const float SignatureWeightBonus = 0.85f;

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
        public AttackDefinition Select(BossContext context) =>
            Select(
                context.CurrentPhase.Attacks,
                context.CurrentPhase.SignatureAttack,
                context.DistanceToPlayer,
                context.Tuning,
                context.RecentMoves);

        /// <summary>
        /// Picks an attack from a catalogue, given everything the choice depends on.
        /// </summary>
        /// <remarks>
        /// Free of the boss context so the mix of blows the boss throws can be pinned by an edit-mode test
        /// rather than judged by watching it fight.
        /// </remarks>
        /// <param name="catalogue">The phase's moves.</param>
        /// <param name="signature">The phase's signature move, or null.</param>
        /// <param name="distance">Perceived distance to the player, in metres.</param>
        /// <param name="tuning">The boss's learned tuning.</param>
        /// <param name="recent">The blows thrown lately.</param>
        /// <returns>The chosen attack, or null when the catalogue is empty.</returns>
        public AttackDefinition Select(
            AttackDefinition[] catalogue, AttackDefinition signature, float distance, BossTuning tuning,
            RecentMoves recent)
        {
            if (catalogue == null || catalogue.Length == 0)
            {
                return null;
            }

            EnsureWeightCapacity(catalogue.Length);

            float totalWeight = ScoreCatalogue(catalogue, signature, distance, tuning, recent);

            return totalWeight <= 0f
                ? catalogue[_random.NextInt(0, catalogue.Length)]
                : PickWeighted(catalogue, totalWeight);
        }

        /// <summary>Scores every attack in the catalogue and returns the total weight.</summary>
        private float ScoreCatalogue(
            AttackDefinition[] catalogue, AttackDefinition signature, float distance, BossTuning tuning,
            RecentMoves recent)
        {
            float gapCloserBias = tuning.Get(BossTuningParameter.GapCloserWeight);
            float areaDenialBias = tuning.Get(BossTuningParameter.AreaDenialWeight);
            float unblockableBias = tuning.Get(BossTuningParameter.UnblockableWeight);

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
                weight += unblockableBias * UnblockableAffinity(attack);
                weight += AnswerBias(tuning, attack.Answers);

                if (signature != null && attack == signature)
                {
                    weight += SignatureWeightBonus;
                }

                // Recency is applied last, to the whole score, so a learned answer is cut along with
                // everything else when it was just thrown: still the likeliest pick over a few blows, never
                // the only one. Only a move thrown twice running drops to nothing.
                _weights[i] = Mathf.Max(MinimumWeight, weight) * (recent?.WeightFor(attack) ?? 1f);
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
            float effectiveReach = attack.EffectiveReach;
            float mismatch = Mathf.Max(0f, Mathf.Abs(effectiveReach - distance) - RangePlateauMetres);

            return Mathf.Max(0f, IdealRangeWeight - mismatch * RangeFalloff);
        }

        /// <summary>How much an attack behaves like a gap-closer.</summary>
        /// <summary>
        /// The weight learning adds to an attack for the habits it answers.
        /// </summary>
        /// <remarks>
        /// Sized against the rest of the scoring, where an attack in range scores about one and the phase's
        /// signature another one: a learned answer of two makes the blow that beats the player's habit the
        /// likeliest pick without making it the only one, so the boss still reads as a fighter rather than a
        /// lookup table.
        /// </remarks>
        /// <param name="tuning">The boss's learned tuning.</param>
        /// <param name="answers">The habits the attack answers.</param>
        /// <returns>The added weight.</returns>
        public static float AnswerBias(BossTuning tuning, HabitAnswer answers)
        {
            if (answers == HabitAnswer.None)
            {
                return 0f;
            }

            float bias = 0f;

            if ((answers & HabitAnswer.Guard) != 0)
            {
                bias += tuning.Get(BossTuningParameter.AnswerGuardWeight);
            }

            if ((answers & HabitAnswer.EarlyDodge) != 0)
            {
                bias += tuning.Get(BossTuningParameter.AnswerEarlyDodgeWeight);
            }

            if ((answers & HabitAnswer.Circling) != 0)
            {
                bias += tuning.Get(BossTuningParameter.AnswerCirclingWeight);
            }

            if ((answers & HabitAnswer.Crowding) != 0)
            {
                bias += tuning.Get(BossTuningParameter.AnswerCrowdingWeight);
            }

            if ((answers & HabitAnswer.Retreat) != 0)
            {
                bias += tuning.Get(BossTuningParameter.AnswerRetreatWeight);
            }

            return Mathf.Max(0f, bias);
        }

        /// <summary>
        /// The quickest blow this phase owns that reaches the player from here, for taking an opening.
        /// </summary>
        /// <remarks>
        /// An opening is short, so speed beats power: the fastest wind-up whose reach, lunge included, covers
        /// the distance. Nothing in reach falls back to the ordinary choice.
        /// </remarks>
        /// <param name="context">The boss context.</param>
        /// <returns>The attack, or null when the phase has none.</returns>
        public AttackDefinition SelectQuickestReaching(BossContext context)
        {
            AttackDefinition[] catalogue = context.CurrentPhase.Attacks;
            AttackDefinition best = null;

            if (catalogue != null)
            {
                float distance = context.DistanceToPlayer;

                foreach (AttackDefinition attack in catalogue)
                {
                    if (attack == null || attack.Unblockable || attack.EffectiveReach < distance ||
                        context.RecentMoves.IsThirdInARow(attack))
                    {
                        continue;
                    }

                    if (best == null || attack.StartupSeconds < best.StartupSeconds)
                    {
                        best = attack;
                    }
                }
            }

            return best != null ? best : Select(context);
        }

        private static float GapCloserAffinity(AttackDefinition attack) =>
            attack.LungeSpeed > 0f ? 1f : 0f;

        /// <summary>
        /// Whether an attack goes straight through a raised guard.
        /// </summary>
        /// <remarks>
        /// Binary, like the gap-closer affinity: an attack either has to be dodged or it does not.
        /// Against a player who answers everything with the guard, this is what turns their habit
        /// into a liability — and it stays fair, because a perilous attack is the most heavily
        /// telegraphed thing the boss throws.
        /// </remarks>
        private static float UnblockableAffinity(AttackDefinition attack) =>
            attack.Unblockable ? 1f : 0f;

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
