namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// Why a combatant was interrupted.
    /// </summary>
    /// <remarks>
    /// Carried alongside the duration because the stagger state announces itself, and announcing the
    /// wrong thing is worse than announcing nothing: a poise break throws the loudest burst and cue
    /// in the game, and firing that for a parry recoil would tell the player their guard had broken
    /// when it had not. Ordered by severity so a request arriving during an existing stagger can be
    /// merged rather than overwriting what is already playing.
    /// </remarks>
    public enum StaggerReason
    {
        /// <summary>An ordinary hit that carried enough force to interrupt.</summary>
        Hit = 0,

        /// <summary>A swing met on the beat and refused, recoiling the attacker.</summary>
        Parried = 1,

        /// <summary>The poise pool emptied and the guard broke outright.</summary>
        PoiseBreak = 2
    }
}
