namespace AdaptiveBossArena.Learning
{
    /// <summary>
    /// The measurable habits the boss builds its picture of the player from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every value here is derived from events the boss could legitimately witness: attacks it saw
    /// thrown, dashes it saw performed, distances it could measure by looking. None is read from
    /// player input or internal state.
    /// </para>
    /// <para>
    /// Each feature is normalised to the range zero to one and carries a confidence, so a
    /// counter-strategy can require both that a habit is strong and that it has been seen often
    /// enough to be believed.
    /// </para>
    /// </remarks>
    public enum BehaviorFeature
    {
        /// <summary>How often the player attacks, relative to a sustainable maximum.</summary>
        AttackFrequency = 0,

        /// <summary>Share of the player's attacks that are heavy. High values invite parrying.</summary>
        HeavyAttackRatio = 1,

        /// <summary>
        /// Probability the player attacks immediately after a dodge.
        /// </summary>
        /// <remarks>The habit the design calls out by name: the boss answers it by delaying its attacks.</remarks>
        PostDodgeAggression = 2,

        /// <summary>How lopsided the player's dash directions are. High values make movement predictable.</summary>
        DashDirectionBias = 3,

        /// <summary>
        /// Preferred engagement distance, normalised across the arena's usable range.
        /// </summary>
        /// <remarks>High values indicate a player who keeps away, which the boss answers with gap-closers.</remarks>
        PreferredDistance = 4,

        /// <summary>Balance between closing and retreating. High values indicate a pressuring player.</summary>
        Aggression = 5,

        /// <summary>Share of the player's attacks that whiff. High values indicate panicked swinging.</summary>
        WhiffRatio = 6,

        /// <summary>How readily the player commits to healing rather than continuing to fight.</summary>
        HealFrequency = 7,

        /// <summary>How reliably the player dodges at the same moment within an attack's wind-up.</summary>
        DodgeTimingConsistency = 8,

        /// <summary>Share of the boss's attacks the player avoids cleanly. The overall read on player skill.</summary>
        EvasionSuccess = 9,

        /// <summary>
        /// How much the player answers attacks with the guard rather than by moving.
        /// </summary>
        /// <remarks>
        /// A turtle stands their ground and lets the guard do the work. The honest answer is not a
        /// harder attack but an unblockable one, which has to be dodged — a capability the boss
        /// already has and merely reaches for more often.
        /// </remarks>
        GuardReliance = 10,

        /// <summary>
        /// Of the attacks the player meets with the guard, the share turned aside on the exact beat.
        /// </summary>
        /// <remarks>
        /// The read on a parry master, as distinct from someone merely holding block:
        /// <see cref="GuardReliance"/> says how often they guard, this says how well. It is the habit
        /// feints exist to punish, since a player this precise is reading a rhythm.
        /// </remarks>
        DeflectSkill = 11
    }

    /// <summary>
    /// The boss behaviours a counter-strategy is allowed to adjust.
    /// </summary>
    /// <remarks>
    /// Adaptation changes numbers, never rules. The boss cannot gain a new capability it did not
    /// already possess, only lean harder on one it had. That constraint is what keeps the fight
    /// readable: everything the boss does in its final phase was visible, in weaker form, in its
    /// first.
    /// </remarks>
    public enum BossTuningParameter
    {
        /// <summary>Extra delay inserted before attacking, to beat players who pre-empt.</summary>
        AttackDelay = 0,

        /// <summary>Distance the boss tries to hold from the player.</summary>
        PreferredRange = 1,

        /// <summary>Likelihood of answering an incoming heavy attack with a parry.</summary>
        ParryChance = 2,

        /// <summary>Weighting applied to gap-closing attacks when choosing what to throw.</summary>
        GapCloserWeight = 3,

        /// <summary>How strongly the boss leads its attacks toward the player's favoured dodge direction.</summary>
        DodgePredictionStrength = 4,

        /// <summary>Overall willingness to press the attack rather than reposition.</summary>
        Aggression = 5,

        /// <summary>Likelihood of extending a single attack into a longer combo.</summary>
        ComboExtensionChance = 6,

        /// <summary>Likelihood of feinting: showing a telegraph and cancelling it.</summary>
        FeintChance = 7,

        /// <summary>Weighting applied to attacks that cover a wide area, against evasive players.</summary>
        AreaDenialWeight = 8
    }

    /// <summary>How a feature's value is compared against a strategy's threshold.</summary>
    public enum FeatureComparison
    {
        /// <summary>The strategy applies when the feature is at or above the threshold.</summary>
        AtOrAbove = 0,

        /// <summary>The strategy applies when the feature is at or below the threshold.</summary>
        AtOrBelow = 1
    }
}
