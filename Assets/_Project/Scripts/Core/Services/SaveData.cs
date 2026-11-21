using System;
using System.Collections.Generic;

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

        /// <summary>
        /// Starting levels, shared with the audio service.
        /// </summary>
        /// <remarks>
        /// Named constants rather than literals in two places. The service used to hard-code its own
        /// defaults, and one of them disagreed with the value here, so music played at the wrong
        /// level until the settings menu was opened for the first time and pushed the saved value
        /// down.
        /// </remarks>
        public const float DefaultMasterVolume = 0.8f;

        /// <summary>Starting music level.</summary>
        public const float DefaultMusicVolume = 0.7f;

        /// <summary>Starting sound-effects level.</summary>
        public const float DefaultEffectsVolume = 0.9f;

        /// <summary>Overall output level, zero to one.</summary>
        public float MasterVolume = DefaultMasterVolume;

        /// <summary>Music level, zero to one.</summary>
        public float MusicVolume = DefaultMusicVolume;

        /// <summary>Sound effects level, zero to one.</summary>
        public float EffectsVolume = DefaultEffectsVolume;

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

        /// <summary>Multiplier on how fast the camera turns with the mouse or stick; one is the default.</summary>
        public float MouseSensitivity = LookSettings.DefaultSensitivity;

        /// <summary>When true, moving the mouse up looks down.</summary>
        public bool InvertLook;

        /// <summary>The graphics quality the player chose, zero to two, or minus one to let the game choose.</summary>
        public int GraphicsQuality = -1;

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
        public const int CurrentSchemaVersion = 2;

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

        /// <summary>The bests kept separately for each difficulty, and for the Ascended rematch of each.</summary>
        /// <remarks>
        /// A list rather than a dictionary because <c>JsonUtility</c> cannot write dictionaries. A Story win
        /// and a Nightmare win are different achievements, so one fastest time across both would let the
        /// easy mode overwrite the hard one. The fields above stay as the across-all-modes summary.
        /// </remarks>
        public List<ModeRecord> Modes = new List<ModeRecord>();

        /// <summary>Ids of the trials completed, in the order they were first completed.</summary>
        public List<string> CompletedTrials = new List<string>();

        /// <summary>The record for one mode, created empty the first time the mode is asked for.</summary>
        /// <param name="difficulty">The difficulty.</param>
        /// <param name="ascended">Whether it is the Ascended rematch.</param>
        /// <returns>The record. Never null.</returns>
        public ModeRecord For(Difficulty difficulty, bool ascended)
        {
            ModeRecord found = Find(difficulty, ascended);

            if (found != null)
            {
                return found;
            }

            var created = new ModeRecord { ModeKey = ModeRecord.KeyFor(difficulty, ascended) };
            Modes ??= new List<ModeRecord>();
            Modes.Add(created);
            return created;
        }

        /// <summary>The record for one mode if it has ever been played, without creating it.</summary>
        /// <param name="difficulty">The difficulty.</param>
        /// <param name="ascended">Whether it is the Ascended rematch.</param>
        /// <returns>The record, or null when the mode has never been played.</returns>
        public ModeRecord Find(Difficulty difficulty, bool ascended)
        {
            if (Modes == null)
            {
                return null;
            }

            int key = ModeRecord.KeyFor(difficulty, ascended);

            foreach (ModeRecord mode in Modes)
            {
                if (mode != null && mode.ModeKey == key)
                {
                    return mode;
                }
            }

            return null;
        }

        /// <summary>Whether the Ascended rematch is open: Igris beaten on Knight or Nightmare.</summary>
        /// <remarks>
        /// Not on Story. The rematch opens in his second phase on top of whatever difficulty is chosen, and it
        /// is meant as a reward for beating the fight as it was balanced.
        /// </remarks>
        public bool AscendedUnlocked =>
            (Find(Difficulty.Knight, false)?.Wins ?? 0) > 0 || (Find(Difficulty.Nightmare, false)?.Wins ?? 0) > 0;

        /// <summary>Whether a trial has ever been completed.</summary>
        /// <param name="trialId">The trial's id.</param>
        /// <returns>True when it has.</returns>
        public bool HasCompleted(string trialId) => CompletedTrials != null && CompletedTrials.Contains(trialId);

        /// <summary>Marks a trial completed. Returns true only the first time.</summary>
        /// <param name="trialId">The trial's id.</param>
        /// <returns>True when this completes it for the first time.</returns>
        public bool CompleteTrial(string trialId)
        {
            CompletedTrials ??= new List<string>();

            if (string.IsNullOrEmpty(trialId) || CompletedTrials.Contains(trialId))
            {
                return false;
            }

            CompletedTrials.Add(trialId);
            return true;
        }

        /// <summary>
        /// Brings a record written by an older version up to date.
        /// </summary>
        /// <remarks>
        /// Version one kept one set of bests. Every one of those fights was the Knight fight, the only one there
        /// was, so the bests move into the Knight record rather than being lost or credited to Story.
        /// </remarks>
        public void Migrate()
        {
            Modes ??= new List<ModeRecord>();
            CompletedTrials ??= new List<string>();

            if (SchemaVersion >= 2)
            {
                return;
            }

            if (TotalAttempts > 0 || HasWon)
            {
                ModeRecord knight = For(Difficulty.Knight, false);
                knight.Attempts = TotalAttempts;
                knight.Wins = HasWon ? 1 : 0;
                knight.FastestVictorySeconds = FastestVictorySeconds;
                knight.BestRemainingHealth = BestRemainingHealth;
                knight.FewestAdaptationsAllowed = FewestAdaptationsAllowed;
            }

            SchemaVersion = CurrentSchemaVersion;
        }

        /// <summary>
        /// Records a completed attempt in both the overall bests and its own mode's.
        /// </summary>
        /// <param name="summary">How the attempt went.</param>
        /// <returns>True when a win set a new best in its mode.</returns>
        public bool RecordAttempt(in AttemptSummary summary)
        {
            RecordAttempt(summary.Won, summary.DurationSeconds, summary.RemainingHealth, summary.AdaptationsAllowed);
            return For(summary.Difficulty, summary.Ascended).Record(summary);
        }

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

    /// <summary>Bests for one difficulty, or for the Ascended rematch of one.</summary>
    [Serializable]
    public sealed class ModeRecord
    {
        /// <summary>Added to the difficulty's value to key its Ascended record apart from the ordinary one.</summary>
        public const int AscendedKeyOffset = 100;

        /// <summary>Which mode this is: the difficulty's value, plus <see cref="AscendedKeyOffset"/> for Ascended.</summary>
        public int ModeKey;

        /// <summary>Attempts made in this mode.</summary>
        public int Attempts;

        /// <summary>Times Igris was beaten in this mode.</summary>
        public int Wins;

        /// <summary>Fastest win in seconds; zero before the first.</summary>
        public float FastestVictorySeconds;

        /// <summary>Highest health fraction a win ended with.</summary>
        public float BestRemainingHealth;

        /// <summary>Fewest counter-strategies Igris developed in a win.</summary>
        public int FewestAdaptationsAllowed = int.MaxValue;

        /// <summary>Whether Igris has been beaten here without the knight taking a single hit.</summary>
        public bool FlawlessWin;

        /// <summary>The key for a mode.</summary>
        /// <param name="difficulty">The difficulty.</param>
        /// <param name="ascended">Whether it is the Ascended rematch.</param>
        /// <returns>The key.</returns>
        public static int KeyFor(Difficulty difficulty, bool ascended) =>
            (int)difficulty + (ascended ? AscendedKeyOffset : 0);

        /// <summary>Deaths in this mode: every attempt that was not a win.</summary>
        public int Deaths => Math.Max(0, Attempts - Wins);

        /// <summary>Folds an attempt in, keeping whichever results are better.</summary>
        /// <param name="summary">How the attempt went.</param>
        /// <returns>True when a win improved any best.</returns>
        public bool Record(in AttemptSummary summary)
        {
            Attempts++;

            // Only wins touch the bests, so a deliberate loss can never poison a record.
            if (!summary.Won)
            {
                return false;
            }

            bool improved = Wins == 0;

            if (Wins == 0 || summary.DurationSeconds < FastestVictorySeconds)
            {
                FastestVictorySeconds = summary.DurationSeconds;
                improved = true;
            }

            if (summary.RemainingHealth > BestRemainingHealth)
            {
                BestRemainingHealth = summary.RemainingHealth;
            }

            if (summary.AdaptationsAllowed < FewestAdaptationsAllowed)
            {
                FewestAdaptationsAllowed = summary.AdaptationsAllowed;
                improved = true;
            }

            if (summary.HitsTaken == 0 && !FlawlessWin)
            {
                FlawlessWin = true;
                improved = true;
            }

            Wins++;
            return improved;
        }
    }
}
