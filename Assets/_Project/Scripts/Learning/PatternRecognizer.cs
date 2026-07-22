using UnityEngine;

namespace AdaptiveBossArena.Learning
{
    /// <summary>
    /// Turns the tracker's raw statistics into the habits the boss reasons about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs on a slow interval, not per frame. That cadence is a design constraint rather than an
    /// optimisation: a boss that re-derived its picture of the player continuously could respond to
    /// a single action, and the whole encounter is built on it responding to trends instead.
    /// </para>
    /// <para>
    /// Every habit is normalised to zero through one and carries a confidence derived from how many
    /// observations it rests on. The half-confidence counts below are judgements about how much
    /// evidence each conclusion deserves: a dash-direction bias needs a good many dashes before it
    /// means anything, whereas a player's preferred distance is apparent from a handful of samples.
    /// </para>
    /// <para>
    /// This class is deliberately pure. It reads a tracker and a memory and writes a profile, with
    /// no engine dependencies at all, so the boss's conclusions can be tested by feeding synthetic
    /// fights through it.
    /// </para>
    /// </remarks>
    public sealed class PatternRecognizer
    {
        private const int AttackSamplesForConfidence = 8;
        private const int DodgeSamplesForConfidence = 10;
        private const int DirectionSamplesForConfidence = 12;
        private const int DistanceSamplesForConfidence = 25;
        private const int EvasionSamplesForConfidence = 6;
        private const int HealSamplesForConfidence = 2;

        /// <summary>Heals in a fight above which healing is treated as a defining habit.</summary>
        private const float HealFrequencyCeiling = 4f;

        /// <summary>
        /// Recomputes every habit from the current statistics.
        /// </summary>
        /// <param name="tracker">Running statistics.</param>
        /// <param name="memory">Witnessed occurrences and their running counts.</param>
        /// <param name="profile">Profile to write the conclusions into.</param>
        /// <param name="timeNow">Combat-clock time, recorded on the profile.</param>
        public void Analyse(
            BehaviorTracker tracker,
            CombatMemory memory,
            BehaviorProfile profile,
            float timeNow)
        {
            AnalyseOffence(tracker, memory, profile);
            AnalyseMovement(tracker, profile);
            AnalyseEvasion(tracker, memory, profile);
            AnalyseSustain(memory, profile);

            profile.MarkUpdated(timeNow);
        }

        /// <summary>Derives how the player attacks.</summary>
        private static void AnalyseOffence(
            BehaviorTracker tracker,
            CombatMemory memory,
            BehaviorProfile profile)
        {
            profile.Set(
                BehaviorFeature.AttackFrequency,
                tracker.NormalizedAttackFrequency(),
                memory.PlayerAttacksThrown,
                AttackSamplesForConfidence);

            profile.Set(
                BehaviorFeature.HeavyAttackRatio,
                Ratio(memory.PlayerHeavyAttacksThrown, memory.PlayerAttacksThrown),
                memory.PlayerAttacksThrown,
                AttackSamplesForConfidence);

            // A high whiff ratio marks a player swinging at where the boss was rather than where it
            // is, which is an opening worth pressing rather than one worth respecting.
            profile.Set(
                BehaviorFeature.WhiffRatio,
                Ratio(memory.PlayerAttacksWhiffed, memory.PlayerAttacksThrown),
                memory.PlayerAttacksThrown,
                AttackSamplesForConfidence);

            // The habit the brief names outright: attacking on reflex the moment a dodge ends.
            profile.Set(
                BehaviorFeature.PostDodgeAggression,
                Ratio(tracker.PostDodgeAttacks, tracker.DodgeOpportunities),
                tracker.DodgeOpportunities,
                DodgeSamplesForConfidence);
        }

        /// <summary>Derives how the player moves and where they like to stand.</summary>
        private static void AnalyseMovement(BehaviorTracker tracker, BehaviorProfile profile)
        {
            profile.Set(
                BehaviorFeature.PreferredDistance,
                tracker.NormalizedPreferredDistance(),
                tracker.DistanceSampleCount,
                DistanceSamplesForConfidence);

            profile.Set(
                BehaviorFeature.Aggression,
                tracker.NormalizedAggression(),
                tracker.DistanceSampleCount,
                DistanceSamplesForConfidence);

            // Predictability is already an entropy measure over the direction histogram, so it is a
            // habit strength directly rather than something needing further normalisation.
            profile.Set(
                BehaviorFeature.DashDirectionBias,
                tracker.DashDirections.Predictability,
                Mathf.RoundToInt(tracker.DashDirections.TotalWeight),
                DirectionSamplesForConfidence);
        }

        /// <summary>Derives how well the player avoids what the boss throws.</summary>
        private static void AnalyseEvasion(
            BehaviorTracker tracker,
            CombatMemory memory,
            BehaviorProfile profile)
        {
            int bossAttacksResolved =
                memory.BossAttacksLanded + memory.BossAttacksEvaded + memory.BossAttacksWhiffed;

            // The boss's overall read on how much trouble this player is. A high value is what
            // eventually pushes it toward attacks that cover ground instead of attacks that aim.
            profile.Set(
                BehaviorFeature.EvasionSuccess,
                Ratio(memory.BossAttacksEvaded + memory.BossAttacksWhiffed, bossAttacksResolved),
                bossAttacksResolved,
                EvasionSamplesForConfidence);

            profile.Set(
                BehaviorFeature.DodgeTimingConsistency,
                Ratio(memory.PlayerPerfectDodges, tracker.DodgeOpportunities),
                tracker.DodgeOpportunities,
                DodgeSamplesForConfidence);
        }

        /// <summary>Derives how readily the player disengages to recover.</summary>
        private static void AnalyseSustain(CombatMemory memory, BehaviorProfile profile) =>
            profile.Set(
                BehaviorFeature.HealFrequency,
                Mathf.Clamp01(memory.PlayerHealsStarted / HealFrequencyCeiling),
                memory.PlayerHealsStarted,
                HealSamplesForConfidence);

        /// <summary>Safe division that treats "no observations" as zero rather than as a habit.</summary>
        private static float Ratio(int numerator, int denominator) =>
            denominator <= 0 ? 0f : Mathf.Clamp01((float)numerator / denominator);
    }
}
