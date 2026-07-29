using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdaptiveBossArena.Learning
{
    /// <summary>
    /// Turns the boss's picture of the player into plain sentences for the post-fight report.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole encounter is about the boss studying you, but until now that story lived only in the
    /// developer overlay behind F1. This renders the same profile as a dossier the player is shown
    /// when the fight ends — "here is what it worked out about you" — which is the payoff the mechanic
    /// has been earning the entire fight.
    /// </para>
    /// <para>
    /// Pure and static, like <see cref="AdaptiveBossArena.Combat.DefenceResolver"/>, so the choice of
    /// which habits are worth mentioning and in what order can be pinned by an edit-mode test rather
    /// than eyeballed on a results screen. It deliberately reports only <em>strong</em> habits: a
    /// reading that is confident but near zero ("you almost never whiffed") is not something the boss
    /// leaned on, so listing it would misrepresent what actually happened in the fight.
    /// </para>
    /// </remarks>
    public static class ProfileReport
    {
        /// <summary>Least confidence a habit needs before it is worth telling the player about.</summary>
        public const float ConfidenceFloor = 0.35f;

        /// <summary>Least strength a habit needs to count as a habit rather than its absence.</summary>
        public const float ValueFloor = 0.4f;

        /// <summary>
        /// The habits the boss grew confident in, phrased for the player and ordered strongest first.
        /// </summary>
        /// <param name="profile">The boss's picture of the player at the end of the fight.</param>
        /// <param name="confidenceFloor">Least confidence a habit needs to be included.</param>
        /// <param name="valueFloor">Least strength a habit needs to be included.</param>
        /// <returns>Ordered sentences, empty when the boss never read the player clearly.</returns>
        public static IReadOnlyList<string> HabitLines(
            BehaviorProfile profile,
            float confidenceFloor = ConfidenceFloor,
            float valueFloor = ValueFloor)
        {
            var scored = new List<(float weighted, string line)>();

            foreach (BehaviorFeature feature in Enum.GetValues(typeof(BehaviorFeature)))
            {
                FeatureReading reading = profile.Get(feature);

                if (reading.Confidence < confidenceFloor || reading.Value < valueFloor)
                {
                    continue;
                }

                int percent = Mathf.RoundToInt(reading.Confidence * 100f);
                scored.Add((reading.Weighted, $"{Describe(feature)}  ({percent}% sure)"));
            }

            // Strongest evidence first, so the dossier opens with what the boss was most certain of.
            scored.Sort((a, b) => b.weighted.CompareTo(a.weighted));

            var lines = new List<string>(scored.Count);
            foreach ((float _, string line) in scored)
            {
                lines.Add(line);
            }

            return lines;
        }

        /// <summary>The player-facing sentence for a habit read at strength.</summary>
        /// <remarks>
        /// Each phrases the <em>high</em>-value meaning of its feature, because only strong readings
        /// reach this point. The absence of a habit (passivity, say) is told instead through the
        /// counter the boss adopted for it, which carries its own tell.
        /// </remarks>
        private static string Describe(BehaviorFeature feature) => feature switch
        {
            BehaviorFeature.AttackFrequency => "You kept up a relentless offence.",
            BehaviorFeature.HeavyAttackRatio => "You leaned on heavy swings.",
            BehaviorFeature.PostDodgeAggression => "You struck the instant a dodge ended.",
            BehaviorFeature.DashDirectionBias => "You favoured one way to roll.",
            BehaviorFeature.PreferredDistance => "You fought from range.",
            BehaviorFeature.Aggression => "You pressed forward and refused to give ground.",
            BehaviorFeature.WhiffRatio => "You swung at empty air often.",
            BehaviorFeature.HealFrequency => "You broke off to heal often.",
            BehaviorFeature.DodgeTimingConsistency => "Your perfect dodges fell on the same beat.",
            BehaviorFeature.EvasionSuccess => "You slipped almost everything it threw.",
            _ => feature.ToString()
        };
    }
}
