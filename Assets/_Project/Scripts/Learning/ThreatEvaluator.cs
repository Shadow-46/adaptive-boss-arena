using UnityEngine;

namespace AdaptiveBossArena.Learning
{
    /// <summary>
    /// Reads the situation right now, as opposed to the player's habits over time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately separate from <see cref="BehaviorProfile"/>, because the two answer different
    /// questions. The profile says what this player tends to do; this says how the fight is going at
    /// this moment. A boss that only had the profile would press a losing exchange because the
    /// player is usually passive, and a boss that only had the situation would never learn anything.
    /// </para>
    /// <para>
    /// Everything it reads is visible: the two health bars, the distance between the combatants, and
    /// what the player appears to be doing. No hidden state is consulted.
    /// </para>
    /// </remarks>
    public sealed class ThreatEvaluator
    {
        /// <summary>Health fraction below which the boss treats its own position as precarious.</summary>
        private const float DesperationThreshold = 0.3f;

        /// <summary>Distance treated as the far end of the arena when normalising.</summary>
        private const float MaxMeaningfulDistance = 14f;

        /// <summary>
        /// Scores how favourable the current exchange is for the boss, from zero to one.
        /// </summary>
        /// <param name="bossHealthNormalized">Boss health as a fraction of maximum.</param>
        /// <param name="playerHealthNormalized">Player health as a fraction of maximum, read from their bar.</param>
        /// <returns>Zero when the boss is losing badly, one when it is comfortably ahead.</returns>
        public float EvaluateAdvantage(float bossHealthNormalized, float playerHealthNormalized) =>
            Mathf.Clamp01((bossHealthNormalized - playerHealthNormalized) * 0.5f + 0.5f);

        /// <summary>
        /// Scores how much the boss should want to close the gap, from zero to one.
        /// </summary>
        /// <remarks>
        /// Combines raw distance with learned aggression, so a boss that has worked out the player
        /// kites will read the same gap as more urgent than one that has not.
        /// </remarks>
        /// <param name="distance">Current separation.</param>
        /// <param name="learnedAggression">Aggression tuning value.</param>
        /// <returns>Urgency of closing, from zero to one.</returns>
        public float EvaluateClosingUrgency(float distance, float learnedAggression)
        {
            float distanceFactor = Mathf.Clamp01(distance / MaxMeaningfulDistance);
            return Mathf.Clamp01(distanceFactor * Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(learnedAggression)));
        }

        /// <summary>
        /// True when the boss is low enough that it should fight as though it has little to lose.
        /// </summary>
        /// <param name="bossHealthNormalized">Boss health as a fraction of maximum.</param>
        /// <returns>Whether the boss is in a desperate position.</returns>
        public bool IsDesperate(float bossHealthNormalized) =>
            bossHealthNormalized <= DesperationThreshold;

        /// <summary>
        /// True when the player looks committed to something and cannot easily answer a punish.
        /// </summary>
        /// <remarks>
        /// The only situational read the boss acts on directly, and it is drawn from the delayed
        /// observation like everything else. It notices that a heavy swing or a heal has been running
        /// for a while, which is exactly what an attentive opponent would notice.
        /// </remarks>
        /// <param name="observation">The delayed player snapshot.</param>
        /// <param name="minimumCommitmentSeconds">How long the action must have been running.</param>
        /// <returns>Whether an opening appears to be available.</returns>
        public bool LooksCommitted(
            Core.Perception.PlayerObservation observation,
            float minimumCommitmentSeconds = 0.15f)
        {
            if (!observation.IsValid)
            {
                return false;
            }

            bool isCommittedAction =
                observation.ActionState == Core.Perception.ObservableActionState.HeavyAttacking ||
                observation.ActionState == Core.Perception.ObservableActionState.Healing ||
                observation.ActionState == Core.Perception.ObservableActionState.Staggered;

            return isCommittedAction && observation.TimeInActionState >= minimumCommitmentSeconds;
        }
    }
}
