using System;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// The player habits an attack is the honest answer to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A boss that has learned a habit should reach for the blow that beats it, the way a human opponent
    /// would: the boot for someone hiding behind a shield, the held overhead for someone who rolls as soon as
    /// the arms go up, the spin for someone circling, the shove for someone standing in its face, the lunge
    /// for someone backing away. Tagging the attacks lets learning weigh them without a rule per habit.
    /// </para>
    /// <para>
    /// Every tagged attack is one the boss throws anyway; learning only makes it likelier. That is the line
    /// all adaptation stays on - numbers, never new rules.
    /// </para>
    /// </remarks>
    [Flags]
    public enum HabitAnswer
    {
        /// <summary>Answers nothing in particular.</summary>
        None = 0,

        /// <summary>Beats a raised guard: breaks it, or cannot be blocked.</summary>
        Guard = 1 << 0,

        /// <summary>Beats a roll made before the blade can be judged: comes late, or is held.</summary>
        EarlyDodge = 1 << 1,

        /// <summary>Beats circling: covers the flank.</summary>
        Circling = 1 << 2,

        /// <summary>Beats standing inside the boss's reach: throws the player off.</summary>
        Crowding = 1 << 3,

        /// <summary>Beats backing away: closes the distance as it strikes.</summary>
        Retreat = 1 << 4
    }
}
