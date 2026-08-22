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
        /// Decides how a defence meets an incoming hit.
        /// </summary>
        /// <remarks>
        /// <para>
        /// One function for both combatants, because a parry is the same mechanic pointed in two
        /// directions and the two sides must not be able to drift apart. What differs between them
        /// is capability, not logic: the player can fall back on a guard, the boss cannot, and that
        /// is expressed by <see cref="DefenceQuery.CanBlock"/> rather than by a second copy of these
        /// rules living somewhere else.
        /// </para>
        /// <para>
        /// It replaces a split decision. A guard used to be gated in one place and its timing judged
        /// in another, so "is this hit defended" was encoded twice and could disagree with itself.
        /// </para>
        /// </remarks>
        /// <param name="query">Everything known about the defence and the incoming hit.</param>
        /// <returns>How the hit resolves against the defence.</returns>
        public static DefenceOutcome ResolveDefence(in DefenceQuery query)
        {
            if (!query.IsDefending)
            {
                return DefenceOutcome.None;
            }

            // A perilous attack ignores a guard and a parry alike. That is the entire point of
            // marking one unblockable: it turns defending from the safe default into the wrong
            // answer, forcing a read of block-versus-dodge rather than only the timing of a deflect.
            if (query.Unblockable)
            {
                return DefenceOutcome.None;
            }

            // Unparryable suppresses only the deflect, then falls through. A hit the defender has
            // earned the right to land - a riposte thrown into a broken guard - must not simply be
            // refused, but it need not be a free hit either: against a defender who can block, it
            // degrades to an honest block at the block's usual cost.
            bool deflectable = query.CanDeflect && !query.Unparryable;

            if (deflectable && query.TimeInDefenceSeconds <= query.DeflectWindowSeconds)
            {
                return DefenceOutcome.Deflected;
            }

            return query.CanBlock ? DefenceOutcome.Blocked : DefenceOutcome.None;
        }
    }

    /// <summary>How a defence met an incoming hit.</summary>
    public enum DefenceOutcome
    {
        /// <summary>The hit was not defended and resolves normally.</summary>
        None = 0,

        /// <summary>Refused on the beat by a timed defence, at no cost to the defender.</summary>
        Deflected = 1,

        /// <summary>Absorbed late, at a cost in chip damage and posture.</summary>
        Blocked = 2
    }

    /// <summary>
    /// Everything <see cref="DefenceResolver.ResolveDefence"/> needs to judge one exchange.
    /// </summary>
    /// <remarks>
    /// A struct rather than seven parameters because the call sites sit in the middle of a damage
    /// path, where an argument silently transposed with its neighbour would be a fairness bug that
    /// still compiles. Named initialisers make the call read as what it means.
    /// </remarks>
    public readonly struct DefenceQuery
    {
        /// <summary>Whether the defender has a guard raised or a parry stance committed.</summary>
        public bool IsDefending { get; init; }

        /// <summary>Whether the defence is capable of a timed deflect at all.</summary>
        public bool CanDeflect { get; init; }

        /// <summary>
        /// Whether the defence can absorb a hit it failed to time.
        /// </summary>
        /// <remarks>
        /// False for the boss, which commits to a stance rather than holding a guard: a boss that
        /// chipped would be strictly harder to punish, and the punishable tail of a missed parry is
        /// the whole reason its stance is worth baiting out.
        /// </remarks>
        public bool CanBlock { get; init; }

        /// <summary>How long the defence has been held when the hit arrives.</summary>
        public float TimeInDefenceSeconds { get; init; }

        /// <summary>How long after committing a hit is deflected rather than absorbed.</summary>
        public float DeflectWindowSeconds { get; init; }

        /// <summary>Whether the incoming attack ignores defences entirely.</summary>
        public bool Unblockable { get; init; }

        /// <summary>Whether the incoming attack refuses to be deflected.</summary>
        public bool Unparryable { get; init; }
    }
}
