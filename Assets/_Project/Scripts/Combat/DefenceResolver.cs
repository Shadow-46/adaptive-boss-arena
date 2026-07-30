namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// The rules that make a weapon's defence style change how an exchange resolves.
    /// </summary>
    /// <remarks>
    /// Split out from the player as pure functions, because these rules are the entire reason the
    /// three weapons feel different on defence — and a rule that decides whether a swing is
    /// interrupted is exactly the kind of thing that should be pinned by an edit-mode test rather
    /// than discovered in a play test. Whether a weapon can deflect at all lives on
    /// <see cref="WeaponDefinition.CanDeflect"/>; the situational decisions live here.
    /// </remarks>
    public static class DefenceResolver
    {
        /// <summary>Fraction of the normal late-block posture cost a sustained guard pays.</summary>
        /// <remarks>
        /// Below one so the guard can actually be <em>held</em> against a flurry rather than breaking
        /// almost as fast as a weapon built to parry instead of block.
        /// </remarks>
        private const float SustainedGuardBlockFraction = 0.6f;

        /// <summary>
        /// Whether a committed swing carries through an incoming hit instead of being staggered.
        /// </summary>
        /// <remarks>
        /// Hyper-armour protects the wind-up and the live frames — the part the player has committed
        /// to and cannot take back — but never the recovery, so a whiffed heavy is still punished.
        /// The damage is unaffected; only the interruption is refused. That asymmetry is what makes
        /// hyper-armour a real choice rather than a strictly better option: you trade the ability to
        /// react for the certainty that your swing lands.
        /// </remarks>
        /// <param name="defence">The equipped weapon's defence style.</param>
        /// <param name="attackPhase">The phase of the swing in progress.</param>
        /// <returns>True when the stagger should be refused.</returns>
        public static bool ResistsStagger(DefenceStyle defence, AttackPhase attackPhase) =>
            defence == DefenceStyle.HyperArmour &&
            (attackPhase == AttackPhase.Startup || attackPhase == AttackPhase.Active);

        /// <summary>
        /// Multiplier on the posture cost of a late block, so a sustained guard bleeds slower.
        /// </summary>
        /// <param name="defence">The equipped weapon's defence style.</param>
        /// <returns>A multiplier applied to the block posture cost.</returns>
        public static float BlockPostureMultiplier(DefenceStyle defence) =>
            defence == DefenceStyle.SustainedGuard ? SustainedGuardBlockFraction : 1f;

        /// <summary>
        /// Whether a raised guard actually stops an incoming hit.
        /// </summary>
        /// <remarks>
        /// A guard stops everything except a perilous, unblockable attack, which is the entire point
        /// of marking one unblockable: it turns the guard from the safe default into the wrong answer,
        /// forcing the player to read block-versus-dodge rather than only timing a deflect.
        /// </remarks>
        /// <param name="isGuarding">Whether the defender currently has a guard raised.</param>
        /// <param name="unblockable">Whether the incoming attack ignores guards.</param>
        /// <returns>True when the guard branch should resolve the hit.</returns>
        public static bool GuardStopsHit(bool isGuarding, bool unblockable) => isGuarding && !unblockable;
    }
}
