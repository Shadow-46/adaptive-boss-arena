using System;
using UnityEngine;

namespace AdaptiveBossArena.Core.Combat
{
    /// <summary>A moment in the fight that the learning system may reason about.</summary>
    /// <remarks>
    /// Every value here corresponds to something an onlooker could witness: a swing thrown, a swing
    /// that hit nothing, a dodge, a heal begun. Nothing describes intent or internal state.
    /// </remarks>
    public enum CombatEventKind
    {
        /// <summary>A combatant began an attack. Visible as a wind-up.</summary>
        AttackStarted = 0,

        /// <summary>An attack connected and dealt damage.</summary>
        AttackLanded = 1,

        /// <summary>An attack completed without touching anything.</summary>
        AttackWhiffed = 2,

        /// <summary>An attack was avoided by an invulnerable target.</summary>
        AttackEvaded = 3,

        /// <summary>A dodge was performed. The direction is visible.</summary>
        DodgePerformed = 4,

        /// <summary>A dodge was timed precisely enough to negate an attack outright.</summary>
        PerfectDodge = 5,

        /// <summary>A combatant's poise broke.</summary>
        PoiseBroken = 6,

        /// <summary>A heal was begun. Deliberately conspicuous so it can be punished.</summary>
        HealStarted = 7,

        /// <summary>A heal completed without being interrupted.</summary>
        HealCompleted = 8,

        /// <summary>A combatant was defeated.</summary>
        Defeated = 9,

        /// <summary>A guard was raised.</summary>
        GuardRaised = 10,

        /// <summary>An attack was deflected on the exact beat, costing the attacker posture.</summary>
        Deflected = 11,

        /// <summary>An attack was blocked late, costing the defender posture and chip damage.</summary>
        Blocked = 12,

        /// <summary>A broken guard was punished with a riposte.</summary>
        Riposte = 13,

        /// <summary>
        /// A weapon was drawn.
        /// </summary>
        /// <remarks>
        /// Legitimately observable — the weapon is visibly in the player's hands — so the boss may
        /// learn which one they favour and answer it.
        /// </remarks>
        WeaponDrawn = 14,

        /// <summary>
        /// A swing was met on the beat and refused.
        /// </summary>
        /// <remarks>
        /// <b>Actor is the defender</b> — the one who parried, not the one who was parried. The
        /// convention matters because <c>CombatMemory</c> routes every event by its actor, so
        /// publishing this with the attacker would credit a parried player with a deflect and the
        /// boss would then adapt to a statistic that never happened.
        /// </remarks>
        Parried = 15
    }

    /// <summary>
    /// A single witnessed combat occurrence.
    /// </summary>
    /// <remarks>
    /// Passed by <c>in</c> and kept to blittable fields so a long fight's worth of events can be
    /// retained in a ring buffer without allocating.
    /// </remarks>
    public readonly struct CombatEvent
    {
        /// <summary>What happened.</summary>
        public CombatEventKind Kind { get; init; }

        /// <summary>Which side caused it.</summary>
        public CombatantTeam Actor { get; init; }

        /// <summary>Combat-clock time at which it happened.</summary>
        public float Timestamp { get; init; }

        /// <summary>Category of the attack involved, where one applies.</summary>
        public DamageType DamageType { get; init; }

        /// <summary>
        /// True when the attack that began is a perilous, unblockable one.
        /// </summary>
        /// <remarks>
        /// Carried on the wind-up so presentation can sound and show the "do not block this" warning.
        /// The learning system ignores it — it is a fact about the boss's own attack, not the player.
        /// </remarks>
        public bool Unblockable { get; init; }

        /// <summary>Where it happened, for events that have a position.</summary>
        public Vector3 Position { get; init; }

        /// <summary>
        /// Direction associated with the event, such as the way a dodge travelled.
        /// </summary>
        public Vector3 Direction { get; init; }

        /// <summary>Distance between the two combatants when it happened.</summary>
        public float SeparationDistance { get; init; }

        /// <summary>Damage dealt, for events that involve damage.</summary>
        public float Magnitude { get; init; }
    }

    /// <summary>
    /// Broadcasts witnessed combat occurrences to whoever is keeping score.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is how the boss's learning system finds out what happened without holding a reference to
    /// the player. It lives in Core so that Combat can raise events and Learning can consume them
    /// while remaining unaware of each other.
    /// </para>
    /// <para>
    /// A caution that matters more than it looks: this bus delivers immediately, so it must never be
    /// used to drive a reaction. It exists to feed statistics that are recomputed every few seconds.
    /// Anything the boss does <em>in response</em> to the player must come through
    /// <see cref="AdaptiveBossArena.Core.Perception.IPerceptionSource"/>, which enforces the
    /// perception delay. Wiring a boss behaviour directly to an event here would reintroduce exactly
    /// the instant reaction the whole design is built to prevent.
    /// </para>
    /// </remarks>
    public interface ICombatEventBus
    {
        /// <summary>Raised for every recorded occurrence.</summary>
        event Action<CombatEvent> EventRecorded;

        /// <summary>Records an occurrence and notifies subscribers.</summary>
        /// <param name="combatEvent">The occurrence to record.</param>
        void Publish(in CombatEvent combatEvent);

        /// <summary>Removes all subscribers, for use when a fight ends.</summary>
        void Clear();
    }
}
