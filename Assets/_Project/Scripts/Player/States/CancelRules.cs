using AdaptiveBossArena.Combat;

namespace AdaptiveBossArena.Player.States
{
    /// <summary>
    /// When the knight may break out of what he is doing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The player's verdict was "no proper control over the character": a guard held for a fixed minimum, a roll
    /// that could only end in an attack, a heal that could not be abandoned, an attack that could never be
    /// guarded out of. Commitment still matters - an attack's startup and live frames can never be escaped, and a
    /// heal abandoned is a heal lost - but every action now has a way out once its commitment is paid.
    /// </para>
    /// <para>
    /// Pure, so the table can be tested without a scene, and in one place, so the windows are read together.
    /// </para>
    /// </remarks>
    public static class CancelRules
    {
        /// <summary>How far through an attack's recovery the guard may be raised.</summary>
        public const float AttackGuardRecoveryFraction = 0.5f;

        /// <summary>How far into a heal a dodge may abandon it.</summary>
        public const float HealDodgeFraction = 0.4f;

        /// <summary>Whether an attack may be dodged out of.</summary>
        /// <remarks>From the moment the blow is over: its whole recovery.</remarks>
        /// <param name="phase">The attack's current phase.</param>
        /// <returns>True when a dodge may interrupt it.</returns>
        public static bool AttackCanDodge(AttackPhase phase) => phase == AttackPhase.Recovery;

        /// <summary>Whether an attack may be guarded out of.</summary>
        /// <remarks>Later than a dodge: a guard is safer than a roll, so it costs more of the recovery.</remarks>
        /// <param name="phase">The attack's current phase.</param>
        /// <param name="elapsedSeconds">Time since the attack began.</param>
        /// <param name="attack">The attack in progress.</param>
        /// <returns>True when the guard may interrupt it.</returns>
        public static bool AttackCanGuard(AttackPhase phase, float elapsedSeconds, AttackDefinition attack)
        {
            if (phase != AttackPhase.Recovery || attack == null)
            {
                return false;
            }

            float intoRecovery = elapsedSeconds - attack.StartupSeconds - attack.ActiveSeconds;
            return intoRecovery >= attack.RecoverySeconds * AttackGuardRecoveryFraction;
        }

        /// <summary>Whether a roll has slowed enough to be left for another action.</summary>
        /// <param name="timeInRollSeconds">Time since the roll began.</param>
        /// <param name="rollSeconds">The roll's full duration.</param>
        /// <param name="cancelFraction">How far through the roll it may be left.</param>
        /// <returns>True when attack, guard or another roll may interrupt it.</returns>
        public static bool RollCanBeLeft(float timeInRollSeconds, float rollSeconds, float cancelFraction) =>
            timeInRollSeconds >= rollSeconds * cancelFraction;

        /// <summary>Whether a heal may still be abandoned for a dodge.</summary>
        /// <param name="timeInHealSeconds">Time since the heal began.</param>
        /// <param name="healSeconds">The heal's full channel.</param>
        /// <returns>True early in the channel, before the potion is committed.</returns>
        public static bool HealCanDodge(float timeInHealSeconds, float healSeconds) =>
            timeInHealSeconds <= healSeconds * HealDodgeFraction;
    }
}
