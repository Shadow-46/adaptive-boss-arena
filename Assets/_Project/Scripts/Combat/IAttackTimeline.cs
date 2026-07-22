using System;

namespace AdaptiveBossArena.Combat
{
    /// <summary>Stage an in-progress attack has reached.</summary>
    public enum AttackPhase
    {
        /// <summary>No attack is running.</summary>
        Inactive = 0,

        /// <summary>Winding up. Telegraph is visible, hitbox is closed.</summary>
        Startup = 1,

        /// <summary>Hitbox is live and testing for contact.</summary>
        Active = 2,

        /// <summary>Hitbox has closed and the attacker is locked out.</summary>
        Recovery = 3
    }

    /// <summary>
    /// Drives an attack through its phases and announces each transition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the seam that stands in for animation events. Combat code subscribes to
    /// <see cref="PhaseChanged"/> and never asks how the timing was produced, so the current
    /// stopwatch-driven implementation and a future animation-driven one are interchangeable.
    /// </para>
    /// <para>
    /// The abstraction is worth its keep beyond placeholder art. Animation events are notoriously
    /// awkward to test, fire inconsistently at low frame rates, and are silently lost when a clip is
    /// re-imported. Keeping combat logic behind this interface means the frame data stays verifiable
    /// in edit-mode tests whichever driver is in use.
    /// </para>
    /// </remarks>
    public interface IAttackTimeline
    {
        /// <summary>Phase the current attack has reached.</summary>
        AttackPhase Phase { get; }

        /// <summary>True while an attack is running.</summary>
        bool IsRunning { get; }

        /// <summary>Seconds elapsed since the attack began.</summary>
        float ElapsedSeconds { get; }

        /// <summary>Progress through the whole attack, from zero to one.</summary>
        float NormalizedTime { get; }

        /// <summary>The attack currently being driven, or null when inactive.</summary>
        AttackDefinition CurrentAttack { get; }

        /// <summary>True while the attack is inside its combo acceptance window.</summary>
        bool IsInComboWindow { get; }

        /// <summary>Raised whenever the attack moves into a new phase.</summary>
        event Action<AttackPhase> PhaseChanged;

        /// <summary>Raised once when the attack finishes and the attacker becomes actionable.</summary>
        event Action Completed;

        /// <summary>Starts driving an attack, replacing any already in progress.</summary>
        /// <param name="attack">The attack to run.</param>
        void Begin(AttackDefinition attack);

        /// <summary>Advances the timeline.</summary>
        /// <param name="deltaTime">Elapsed scaled time, so hit-stop correctly freezes attack progress.</param>
        void Tick(float deltaTime);

        /// <summary>
        /// Stops the attack immediately without completing it.
        /// </summary>
        /// <remarks>Used when a stagger interrupts the attacker mid-swing.</remarks>
        void Cancel();
    }
}
