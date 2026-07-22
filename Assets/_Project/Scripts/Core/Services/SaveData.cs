using System;

namespace AdaptiveBossArena.Core.Services
{
    /// <summary>
    /// Player-adjustable settings, persisted between sessions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain serialisable class rather than a ScriptableObject, because this is player data that
    /// belongs in the save folder rather than project data that belongs in the build.
    /// </para>
    /// <para>
    /// Every field carries a sensible default, so a save file written by an older version that
    /// lacks a field still loads correctly and simply picks up the default for whatever is new.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class SettingsData
    {
        /// <summary>Schema version this record was written with.</summary>
        public int SchemaVersion = CurrentSchemaVersion;

        /// <summary>Version the current code writes and understands.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Overall output level, zero to one.</summary>
        public float MasterVolume = 0.8f;

        /// <summary>Music level, zero to one.</summary>
        public float MusicVolume = 0.7f;

        /// <summary>Sound effects level, zero to one.</summary>
        public float EffectsVolume = 0.9f;

        /// <summary>
        /// Camera shake intensity, zero to one.
        /// </summary>
        /// <remarks>
        /// Exposed as a full range rather than an on/off toggle. Screen shake is a genuine
        /// accessibility problem for some players, and "less" is a more useful answer than "none"
        /// for most of them.
        /// </remarks>
        public float ScreenShakeIntensity = 1f;

        /// <summary>
        /// When true, hit flashes are muted.
        /// </summary>
        /// <remarks>Rapid full-screen flashing is a photosensitivity risk and must be defeatable.</remarks>
        public bool ReducedFlashing;

        /// <summary>When true, the boss's adaptation tells stay on screen longer.</summary>
        public bool ExtendedTellDuration;

        /// <summary>Serialised input rebindings, in the Input System's own JSON format.</summary>
        public string InputRebinds = string.Empty;
    }

    /// <summary>Best results the player has achieved against the boss.</summary>
    /// <remarks>
    /// Deliberately a separate record from settings. A player clearing their settings should not
    /// lose their records, and a corrupt settings file should not cost them either.
    /// </remarks>
    [Serializable]
    public sealed class RecordsData
    {
        /// <summary>Schema version this record was written with.</summary>
        public int SchemaVersion = CurrentSchemaVersion;

        /// <summary>Version the current code writes and understands.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Whether the boss has ever been defeated.</summary>
        public bool HasWon;

        /// <summary>Fastest victory in seconds. Zero when the boss has never been beaten.</summary>
        public float FastestVictorySeconds;

        /// <summary>Highest health fraction the player finished a winning fight with.</summary>
        public float BestRemainingHealth;

        /// <summary>Total attempts made.</summary>
        public int TotalAttempts;

        /// <summary>Fewest counter-strategies the boss managed to develop in a winning fight.</summary>
        /// <remarks>
        /// The most interesting score this game can offer. Winning quickly is one kind of skill;
        /// winning while giving the boss nothing to learn from is a much harder one, because it
        /// means varying your play rather than finding one strong tactic and repeating it.
        /// </remarks>
        public int FewestAdaptationsAllowed = int.MaxValue;

        /// <summary>Records a completed attempt, keeping whichever results are better.</summary>
        /// <param name="won">Whether the boss was defeated.</param>
        /// <param name="durationSeconds">How long the attempt lasted.</param>
        /// <param name="remainingHealth">Player health fraction at the end.</param>
        /// <param name="adaptationsAllowed">Counter-strategies the boss developed.</param>
        public void RecordAttempt(
            bool won,
            float durationSeconds,
            float remainingHealth,
            int adaptationsAllowed)
        {
            TotalAttempts++;

            if (!won)
            {
                return;
            }

            // Only winning attempts update the bests, so a deliberate loss cannot poison a record.
            if (!HasWon || durationSeconds < FastestVictorySeconds)
            {
                FastestVictorySeconds = durationSeconds;
            }

            if (remainingHealth > BestRemainingHealth)
            {
                BestRemainingHealth = remainingHealth;
            }

            if (adaptationsAllowed < FewestAdaptationsAllowed)
            {
                FewestAdaptationsAllowed = adaptationsAllowed;
            }

            HasWon = true;
        }
    }
}
