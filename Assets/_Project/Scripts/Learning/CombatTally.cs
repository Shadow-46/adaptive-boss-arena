namespace AdaptiveBossArena.Learning
{
    /// <summary>
    /// The running counts and durations the boss keeps of the fight, each fading with time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Faded rather than summed over the whole fight, so every ratio built on them describes how the
    /// player is fighting now. Counted from the first second, a habit the player dropped two minutes ago
    /// still weighed as much as the one they have now, and late in the fight no new habit could ever
    /// outweigh the old ones.
    /// </para>
    /// <para>
    /// The durations are seconds, the rest are occurrences. Both fade at the same rate, so the ratios
    /// between them stay honest.
    /// </para>
    /// </remarks>
    public enum CombatTally
    {
        /// <summary>Attacks the player started.</summary>
        PlayerAttacks = 0,

        /// <summary>Of those, the heavy ones.</summary>
        PlayerHeavyAttacks = 1,

        /// <summary>Player attacks that touched nothing.</summary>
        PlayerWhiffs = 2,

        /// <summary>Dodges the player made.</summary>
        PlayerDodges = 3,

        /// <summary>Dodges timed to negate a blow outright.</summary>
        PlayerPerfectDodges = 4,

        /// <summary>Heals the player began.</summary>
        PlayerHeals = 5,

        /// <summary>Heals begun within the boss's reach.</summary>
        PlayerHealsUnderPressure = 6,

        /// <summary>Boss blows turned aside on the beat.</summary>
        PlayerDeflects = 7,

        /// <summary>Boss blows caught late on the guard.</summary>
        PlayerBlocks = 8,

        /// <summary>Boss swings that connected.</summary>
        BossLanded = 9,

        /// <summary>Boss swings the player dodged through.</summary>
        BossEvaded = 10,

        /// <summary>Boss swings that met nothing at all: no body, no dodge, no guard.</summary>
        BossWhiffed = 11,

        /// <summary>Dodges made into one of the boss's wind-ups.</summary>
        DodgesIntoSwings = 12,

        /// <summary>Of those, the ones made well before the blade could arrive.</summary>
        EarlyDodges = 13,

        /// <summary>Dodges followed by an attack within the follow-up window.</summary>
        PostDodgeAttacks = 14,

        /// <summary>Seconds of the fight the boss has watched.</summary>
        ObservedSeconds = 15,

        /// <summary>Seconds the player spent pressed inside the boss's reach.</summary>
        CrowdingSeconds = 16,

        /// <summary>Seconds the player spent keeping well away.</summary>
        KitingSeconds = 17,

        /// <summary>Seconds the player spent behind a raised guard, near enough to be struck.</summary>
        GuardingSeconds = 18,

        /// <summary>Seconds the player spent close enough that guarding was a choice.</summary>
        NearSeconds = 19,

        /// <summary>Seconds spent circling the boss to its right.</summary>
        CirclingRightSeconds = 20,

        /// <summary>Seconds spent circling the boss to its left.</summary>
        CirclingLeftSeconds = 21
    }
}
