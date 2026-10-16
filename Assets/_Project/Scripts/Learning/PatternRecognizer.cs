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

        /// <summary>Seconds of the attack window watched before the attack rate is half-trusted.</summary>
        private const int AttackWindowSecondsForConfidence = 10;

        /// <summary>Seconds watched before where the player stands is half-trusted.</summary>
        private const int PositionSecondsForConfidence = 8;

        /// <summary>Seconds of circling seen before the side is half-trusted.</summary>
        private const int CirclingSecondsForConfidence = 4;

        /// <summary>Seconds within reach before the guard habit is half-trusted.</summary>
        private const int NearSecondsForConfidence = 6;

        /// <summary>Rolls into wind-ups before their timing is half-trusted.</summary>
        private const int SwingDodgesForConfidence = 8;

        /// <summary>
        /// Guarded exchanges before a defensive read is trusted.
        /// </summary>
        /// <remarks>
        /// Higher than the evasion count because a guard answer is a deliberate, repeated choice
        /// rather than a one-off reaction, and because the counters it invites — unblockables and
        /// feints — are among the most punishing the boss has. It should be sure first.
        /// </remarks>
        private const int GuardSamplesForConfidence = 8;

        /// <summary>Recent heals above which healing is treated as a defining habit.</summary>
        private const float HealFrequencyCeiling = 4f;

        /// <summary>
        /// Recomputes every habit from the current statistics.
        /// </summary>
        /// <remarks>
        /// Every ratio is over faded tallies, and every confidence over the faded count it rests on, so a
        /// habit the player has dropped loses both its strength and the boss's certainty about it.
        /// </remarks>
        /// <param name="tracker">Running statistics.</param>
        /// <param name="memory">Witnessed occurrences and their running tallies.</param>
        /// <param name="profile">Profile to write the conclusions into.</param>
        /// <param name="timeNow">Combat-clock time, recorded on the profile.</param>
        public void Analyse(
            BehaviorTracker tracker,
            CombatMemory memory,
            BehaviorProfile profile,
            float timeNow)
        {
            AnalyseOffence(tracker, memory, profile);
            AnalyseMovement(tracker, memory, profile);
            AnalyseEvasion(memory, profile);
            AnalyseDefence(memory, profile);
            AnalyseSustain(memory, profile);

            profile.MarkUpdated(timeNow);
        }

        /// <summary>Derives how the player attacks.</summary>
        private static void AnalyseOffence(BehaviorTracker tracker, CombatMemory memory, BehaviorProfile profile)
        {
            float attacks = memory.Recent(CombatTally.PlayerAttacks);
            float dodges = memory.Recent(CombatTally.PlayerDodges);

            // Rests on time watched, not on attacks counted: a passive player throws few attacks, and a
            // count-based confidence could never be sure of the very habit it is meant to see.
            profile.Set(
                BehaviorFeature.AttackFrequency,
                tracker.NormalizedAttackFrequency(),
                Mathf.RoundToInt(tracker.AttackWindowWatched),
                AttackWindowSecondsForConfidence);

            profile.Set(
                BehaviorFeature.HeavyAttackRatio,
                Ratio(memory.Recent(CombatTally.PlayerHeavyAttacks), attacks),
                Count(attacks),
                AttackSamplesForConfidence);

            // A high whiff ratio marks a player swinging at where the boss was rather than where it
            // is, which is an opening worth pressing rather than one worth respecting.
            profile.Set(
                BehaviorFeature.WhiffRatio,
                Ratio(memory.Recent(CombatTally.PlayerWhiffs), attacks),
                Count(attacks),
                AttackSamplesForConfidence);

            // The habit the brief names outright: attacking on reflex the moment a dodge ends.
            profile.Set(
                BehaviorFeature.PostDodgeAggression,
                Ratio(memory.Recent(CombatTally.PostDodgeAttacks), dodges),
                Count(dodges),
                DodgeSamplesForConfidence);
        }

        /// <summary>Derives how the player moves and where they like to stand.</summary>
        private static void AnalyseMovement(BehaviorTracker tracker, CombatMemory memory, BehaviorProfile profile)
        {
            float observed = memory.Recent(CombatTally.ObservedSeconds);

            profile.Set(
                BehaviorFeature.PreferredDistance,
                Ratio(memory.Recent(CombatTally.KitingSeconds), observed),
                Count(observed),
                PositionSecondsForConfidence);

            profile.Set(
                BehaviorFeature.Crowding,
                Ratio(memory.Recent(CombatTally.CrowdingSeconds), observed),
                Count(observed),
                PositionSecondsForConfidence);

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

            float circling = memory.Recent(CombatTally.CirclingRightSeconds) +
                             memory.Recent(CombatTally.CirclingLeftSeconds);

            profile.Set(
                BehaviorFeature.CirclingBias,
                Mathf.Abs(tracker.CirclingSide),
                Count(circling),
                CirclingSecondsForConfidence);
        }

        /// <summary>Derives how well, and how, the player avoids what the boss throws.</summary>
        private static void AnalyseEvasion(CombatMemory memory, BehaviorProfile profile)
        {
            float landed = memory.Recent(CombatTally.BossLanded);
            float avoided = memory.Recent(CombatTally.BossEvaded) + memory.Recent(CombatTally.BossWhiffed);
            float dodges = memory.Recent(CombatTally.PlayerDodges);
            float intoSwings = memory.Recent(CombatTally.DodgesIntoSwings);

            // The boss's overall read on how much trouble this player is. A high value is what
            // eventually pushes it toward attacks that cover ground instead of attacks that aim.
            profile.Set(
                BehaviorFeature.EvasionSuccess,
                Ratio(avoided, landed + avoided),
                Count(landed + avoided),
                EvasionSamplesForConfidence);

            profile.Set(
                BehaviorFeature.DodgeTimingConsistency,
                Ratio(memory.Recent(CombatTally.PlayerPerfectDodges), dodges),
                Count(dodges),
                DodgeSamplesForConfidence);

            profile.Set(
                BehaviorFeature.DodgeEarliness,
                Ratio(memory.Recent(CombatTally.EarlyDodges), intoSwings),
                Count(intoSwings),
                SwingDodgesForConfidence);
        }

        /// <summary>
        /// Derives how the player defends: whether they hold ground behind the guard, and how
        /// precisely they use it.
        /// </summary>
        /// <remarks>
        /// Reliance is measured against everything the boss threw, so it answers "do they stand and
        /// block?"; skill is measured only against the attacks they actually met with the guard, so it
        /// answers "and are they good at it?"; turtling is measured against time, so it sees the guard held
        /// up and waiting even when nothing is thrown at it. A cautious novice scores high on the first and
        /// low on the second, and the habits deserve different answers — unblockables and the boot for the
        /// turtle, feints for the metronome.
        /// </remarks>
        private static void AnalyseDefence(CombatMemory memory, BehaviorProfile profile)
        {
            float guarded = memory.Recent(CombatTally.PlayerDeflects) + memory.Recent(CombatTally.PlayerBlocks);
            float resolved = memory.Recent(CombatTally.BossLanded) + memory.Recent(CombatTally.BossEvaded) +
                             memory.Recent(CombatTally.BossWhiffed) + guarded;
            float near = memory.Recent(CombatTally.NearSeconds);

            profile.Set(
                BehaviorFeature.GuardReliance,
                Ratio(guarded, resolved),
                Count(resolved),
                GuardSamplesForConfidence);

            // Rested on the guarded answers alone, so a player who blocks twice and deflects both
            // does not read as a master until they have done it often enough to mean something.
            profile.Set(
                BehaviorFeature.DeflectSkill,
                Ratio(memory.Recent(CombatTally.PlayerDeflects), guarded),
                Count(guarded),
                GuardSamplesForConfidence);

            profile.Set(
                BehaviorFeature.Turtling,
                Ratio(memory.Recent(CombatTally.GuardingSeconds), near),
                Count(near),
                NearSecondsForConfidence);
        }

        /// <summary>Derives how readily, and where, the player disengages to recover.</summary>
        private static void AnalyseSustain(CombatMemory memory, BehaviorProfile profile)
        {
            float heals = memory.Recent(CombatTally.PlayerHeals);

            profile.Set(
                BehaviorFeature.HealFrequency,
                Mathf.Clamp01(heals / HealFrequencyCeiling),
                Count(heals),
                HealSamplesForConfidence);

            profile.Set(
                BehaviorFeature.HealUnderPressure,
                Ratio(memory.Recent(CombatTally.PlayerHealsUnderPressure), heals),
                Count(heals),
                HealSamplesForConfidence);
        }

        /// <summary>A faded tally as the whole-number evidence count a confidence is built on.</summary>
        private static int Count(float tally) => Mathf.RoundToInt(tally);

        /// <summary>Safe division that treats "no observations" as zero rather than as a habit.</summary>
        private static float Ratio(float numerator, float denominator) =>
            denominator <= 0.001f ? 0f : Mathf.Clamp01(numerator / denominator);
    }
}
