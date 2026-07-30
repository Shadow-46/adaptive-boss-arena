using UnityEngine;

namespace AdaptiveBossArena.Core.Combat
{
    /// <summary>
    /// Immutable description of a single hit, produced by an attacker and consumed by a target.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Declared as a <c>readonly struct</c> and passed by <c>in</c> everywhere, so hit resolution
    /// stays allocation-free even during a dense flurry. Being readonly also means <c>in</c>
    /// parameters do not trigger defensive copies when properties are read.
    /// </para>
    /// <para>
    /// Every field a receiver might need travels inside this one value. Nothing in the hit pipeline
    /// reaches back to the attacker's components, which is what allows the player and the boss to
    /// damage each other while living in assemblies that do not reference one another.
    /// </para>
    /// </remarks>
    public readonly struct DamageInfo
    {
        /// <summary>Raw health to remove before any target-side modifiers.</summary>
        public float Amount { get; init; }

        /// <summary>Category of the hit, used for both resistances and behaviour analysis.</summary>
        public DamageType Type { get; init; }

        /// <summary>Team the attack originated from. Hits against the same team are discarded.</summary>
        public CombatantTeam SourceTeam { get; init; }

        /// <summary>
        /// Instance identifier of the attacking object, for attribution and self-hit rejection.
        /// </summary>
        /// <remarks>
        /// Deliberately an <c>int</c> rather than a component reference: an identifier carries no
        /// assembly dependency and cannot be used to reach into the attacker and read its state.
        /// </remarks>
        public int SourceInstanceId { get; init; }

        /// <summary>World position where the hit registered, used to place impact effects.</summary>
        public Vector3 HitPoint { get; init; }

        /// <summary>
        /// Normalised direction the hit travelled, from attacker toward target.
        /// </summary>
        public Vector3 HitDirection { get; init; }

        /// <summary>Impulse applied to the target on landing, in world units per second.</summary>
        public float KnockbackSpeed { get; init; }

        /// <summary>Poise damage dealt. Accumulating enough of it staggers the target.</summary>
        public float PoiseDamage { get; init; }

        /// <summary>How hard this hit should interrupt the target's current action.</summary>
        public StaggerStrength Stagger { get; init; }

        /// <summary>
        /// Seconds of hit-stop to freeze both parties for on a successful hit.
        /// </summary>
        /// <remarks>
        /// The single highest-leverage game-feel parameter in the project. Carried per-hit rather
        /// than globally so a heavy attack can land noticeably heavier than a light one.
        /// </remarks>
        public float HitStopSeconds { get; init; }

        /// <summary>When true, the hit lands even through dash invincibility frames.</summary>
        /// <remarks>Reserved for unavoidable arena hazards; boss attacks must never set this.</remarks>
        public bool IgnoresInvulnerability { get; init; }

        /// <summary>When true, a raised guard does not stop the hit — it must be dodged instead.</summary>
        /// <remarks>
        /// The "perilous" attack of the genre. It ignores the block and the deflect window alike, so a
        /// guarding player takes it in full; a dash still avoids it, because <see cref="IgnoresInvulnerability"/>
        /// stays false. That asymmetry is the whole read: this is the attack you must not answer with
        /// your guard.
        /// </remarks>
        public bool Unblockable { get; init; }

        /// <summary>Creates a hit description with sensible defaults for the fields not supplied.</summary>
        /// <param name="amount">Raw damage.</param>
        /// <param name="type">Hit category.</param>
        /// <param name="sourceTeam">Attacking team.</param>
        /// <param name="sourceInstanceId">Attacker instance identifier.</param>
        /// <returns>A populated hit description.</returns>
        public static DamageInfo Create(
            float amount,
            DamageType type,
            CombatantTeam sourceTeam,
            int sourceInstanceId) => new DamageInfo
            {
                Amount = amount,
                Type = type,
                SourceTeam = sourceTeam,
                SourceInstanceId = sourceInstanceId,
                HitDirection = Vector3.forward,
                Stagger = StaggerStrength.Flinch
            };

        /// <summary>Returns a copy of this hit positioned at a specific contact point.</summary>
        /// <param name="hitPoint">World-space contact position.</param>
        /// <param name="hitDirection">Direction of travel, from attacker toward target.</param>
        /// <returns>A copy carrying the supplied contact data.</returns>
        public DamageInfo AtPoint(Vector3 hitPoint, Vector3 hitDirection) =>
            Rebuild(this, Amount, hitPoint, hitDirection);

        /// <summary>Returns a copy of this hit with a different damage amount.</summary>
        /// <remarks>Used by hurtboxes that scale damage, such as a boss weak point.</remarks>
        /// <param name="amount">The replacement damage amount.</param>
        /// <returns>A copy carrying the supplied amount.</returns>
        public DamageInfo WithAmount(float amount) => Rebuild(this, amount, HitPoint, HitDirection);

        /// <summary>
        /// Copies every field, substituting the three that callers are allowed to vary.
        /// </summary>
        /// <remarks>
        /// Init-only properties cannot be reassigned after construction and C# 9 offers no
        /// <c>with</c> expression for structs, so copies must be written out in full. Routing them
        /// through one method means a field added later cannot be forgotten by half the copies.
        /// </remarks>
        private static DamageInfo Rebuild(
            in DamageInfo source,
            float amount,
            Vector3 hitPoint,
            Vector3 hitDirection) => new DamageInfo
            {
                Amount = amount,
                Type = source.Type,
                SourceTeam = source.SourceTeam,
                SourceInstanceId = source.SourceInstanceId,
                HitPoint = hitPoint,
                HitDirection = hitDirection,
                KnockbackSpeed = source.KnockbackSpeed,
                PoiseDamage = source.PoiseDamage,
                Stagger = source.Stagger,
                HitStopSeconds = source.HitStopSeconds,
                IgnoresInvulnerability = source.IgnoresInvulnerability,
                Unblockable = source.Unblockable
            };
    }

    /// <summary>Outcome of offering a <see cref="DamageInfo"/> to a target.</summary>
    public readonly struct DamageResult
    {
        /// <summary>What the target did with the hit.</summary>
        public DamageOutcome Outcome { get; init; }

        /// <summary>Health actually removed, after resistances and clamping.</summary>
        public float AmountApplied { get; init; }

        /// <summary>True when this hit reduced the target to zero health.</summary>
        public bool WasLethal { get; init; }

        /// <summary>True when the target lost health, as opposed to avoiding or absorbing the hit.</summary>
        public bool Connected => Outcome == DamageOutcome.Applied;

        /// <summary>Result for a hit that removed health.</summary>
        /// <param name="amountApplied">Health removed.</param>
        /// <param name="wasLethal">Whether the target died.</param>
        /// <returns>A populated result.</returns>
        public static DamageResult Applied(float amountApplied, bool wasLethal) => new DamageResult
        {
            Outcome = DamageOutcome.Applied,
            AmountApplied = amountApplied,
            WasLethal = wasLethal
        };

        /// <summary>Result for a hit that was avoided or refused without removing health.</summary>
        /// <param name="outcome">The non-damaging outcome.</param>
        /// <returns>A populated result.</returns>
        public static DamageResult NoDamage(DamageOutcome outcome) => new DamageResult
        {
            Outcome = outcome
        };
    }
}
