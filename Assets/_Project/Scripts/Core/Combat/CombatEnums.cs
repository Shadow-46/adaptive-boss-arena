namespace AdaptiveBossArena.Core.Combat
{
    /// <summary>Which side of the fight a combatant belongs to.</summary>
    /// <remarks>
    /// Hit resolution filters on team in addition to physics layers. Layers alone are a broad
    /// instrument, and the redundant check keeps a mis-assigned prefab layer from letting the boss
    /// damage itself with its own shockwave.
    /// </remarks>
    public enum CombatantTeam
    {
        /// <summary>Unassigned. Never damages or is damaged by anything.</summary>
        None = 0,

        /// <summary>The player character.</summary>
        Player = 1,

        /// <summary>The boss and anything it spawns.</summary>
        Boss = 2
    }

    /// <summary>Classification of an incoming hit.</summary>
    /// <remarks>
    /// The learning system reads this to distinguish tactics the player leans on. A boss that keeps
    /// getting opened by <see cref="Heavy"/> hits should start parrying; one losing chip health to
    /// <see cref="Light"/> pokes should reach for spacing tools instead.
    /// </remarks>
    public enum DamageType
    {
        /// <summary>Fast primary attack. Low commitment, low reward.</summary>
        Light = 0,

        /// <summary>Slow committed attack. High damage and high poise damage.</summary>
        Heavy = 1,

        /// <summary>Player special ability damage.</summary>
        Special = 2,

        /// <summary>Boss melee contact damage.</summary>
        BossMelee = 3,

        /// <summary>Boss ranged or area damage.</summary>
        BossProjectile = 4,

        /// <summary>Environmental or arena hazard damage.</summary>
        Hazard = 5
    }

    /// <summary>What happened when damage was offered to a target.</summary>
    /// <remarks>
    /// This return value is the single source of truth the learning system uses to score the fight.
    /// <see cref="Missed"/> and <see cref="PerfectDodged"/> in particular are what let the boss
    /// notice that its current pattern is being read, without ever inspecting player input.
    /// </remarks>
    public enum DamageOutcome
    {
        /// <summary>Damage was applied to health.</summary>
        Applied = 0,

        /// <summary>The target was invulnerable, typically mid-dash.</summary>
        Invulnerable = 1,

        /// <summary>The target dodged inside the perfect-dodge window and should be rewarded.</summary>
        PerfectDodged = 2,

        /// <summary>The target blocked or parried the hit.</summary>
        Blocked = 3,

        /// <summary>The hit never connected with a valid target.</summary>
        Missed = 4,

        /// <summary>The target was already dead or otherwise not a valid recipient.</summary>
        Ignored = 5
    }

    /// <summary>How forcefully a hit interrupts what the victim was doing.</summary>
    public enum StaggerStrength
    {
        /// <summary>No interruption; the victim continues its action.</summary>
        None = 0,

        /// <summary>Brief flinch that does not cancel the current action.</summary>
        Flinch = 1,

        /// <summary>Cancels the current action and applies a short recovery.</summary>
        Interrupt = 2,

        /// <summary>Full poise break, opening a long punish window.</summary>
        Break = 3
    }
}
