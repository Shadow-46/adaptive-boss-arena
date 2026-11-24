using System;
using System.Text;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Services;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveBossArena.UI
{
    /// <summary>
    /// The title-screen panel where challenge modifiers are chosen for the next run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Writes the choice to <see cref="RunSettings.Current"/> — the static the fight reads once it
    /// loads — and persists it so a preferred challenge is remembered between sessions. It only ever
    /// records intent; the encounter's composition root is what actually applies these numbers to the
    /// player and the boss, keeping the menu ignorant of either.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RunModifierMenu : MonoBehaviour
    {
        [SerializeField]
        private Toggle _fastLearnerToggle;

        [SerializeField]
        private Toggle _noHealingToggle;

        [SerializeField]
        private Toggle _fragileToggle;

        [SerializeField]
        private Toggle _trainingToggle;

        [SerializeField]
        [Tooltip("The difficulty button's label.")]
        private Text _difficultyLabel;

        [SerializeField]
        [Tooltip("One line under the difficulty saying what it changes.")]
        private Text _difficultyBlurb;

        [SerializeField]
        [Tooltip("The armour button's label.")]
        private Text _tintLabel;

        [SerializeField]
        [Tooltip("The Ascended rematch toggle, locked until Igris is beaten on Knight or Nightmare.")]
        private Toggle _ascendedToggle;

        [SerializeField]
        [Tooltip("The Ascended toggle's caption, which says how to unlock it while it is locked.")]
        private Text _ascendedLabel;

        [SerializeField]
        [Tooltip("The records panel's body.")]
        private Text _recordsText;

        [SerializeField]
        [Tooltip("The clear-records button's label, which asks for a second click before erasing.")]
        private Text _clearLabel;

        /// <summary>The clear button's resting caption.</summary>
        public const string ClearCaption = "Clear Records";

        /// <summary>The caption asking for the second, confirming click.</summary>
        public const string ConfirmClearCaption = "Click again to erase";

        /// <summary>Seconds the confirmation stays armed before the button forgets the first click.</summary>
        private const float ClearConfirmSeconds = 4f;

        private float _clearArmedUntil = float.NegativeInfinity;
        private RecordsData _records = new RecordsData();
        private ISaveService _saveService;
        private RunModifiers _modifiers = new RunModifiers();

        // Set while the toggles are being driven from loaded data, so reflecting a value back into a
        // toggle does not immediately re-save it as if the player had clicked it.
        private bool _loading;

        private void Start()
        {
            _saveService = ServiceRegistry.Current != null &&
                           ServiceRegistry.Current.TryGet(out ISaveService service)
                ? service
                : null;

            _modifiers = Load();
            _records = LoadRecords();
            Sanitise();
            RunSettings.Current = _modifiers;
            ReflectToToggles();
        }

        /// <summary>Toggles the Fast Learner modifier. Bound to its toggle.</summary>
        public void SetFastLearner(bool on) => Change(() => _modifiers.FastLearner = on);

        /// <summary>Toggles the No Healing modifier. Bound to its toggle.</summary>
        public void SetNoHealing(bool on) => Change(() => _modifiers.NoHealing = on);

        /// <summary>Toggles the Fragile modifier. Bound to its toggle.</summary>
        public void SetFragile(bool on) => Change(() => _modifiers.FragilePlayer = on);

        /// <summary>Toggles Training mode. Bound to its toggle.</summary>
        public void SetTraining(bool on) => Change(() => _modifiers.TrainingMode = on);

        /// <summary>Applies a change, publishes it to the session, and persists it.</summary>
        /// <summary>Steps the difficulty: Story, Knight, Nightmare, and round again.</summary>
        public void CycleDifficulty() => Change(() => _modifiers.Difficulty = DifficultyProfile.Next(_modifiers.Difficulty));

        /// <summary>Steps to the next armour tint the player has unlocked.</summary>
        public void CycleTint() =>
            Change(() => _modifiers.KnightTintId = TrialBook.NextUnlockedTint(_modifiers.KnightTintId, _records));

        /// <summary>Turns the Ascended rematch on or off. Ignored while it is still locked.</summary>
        /// <param name="on">Whether to fight the rematch.</param>
        public void SetAscended(bool on) => Change(() => _modifiers.Ascended = on && _records.AscendedUnlocked);

        /// <summary>
        /// Erases every record and trial, on the second click within a few seconds of the first.
        /// </summary>
        /// <remarks>
        /// Two clicks because it cannot be undone. Unearned choices fall back with it: the Ascended rematch locks
        /// again and the armour returns to steel.
        /// </remarks>
        public void ClearRecords()
        {
            if (Time.unscaledTime > _clearArmedUntil)
            {
                _clearArmedUntil = Time.unscaledTime + ClearConfirmSeconds;
                SetClearCaption(ConfirmClearCaption);
                return;
            }

            _clearArmedUntil = float.NegativeInfinity;
            _records = new RecordsData();
            _saveService?.Save(SaveKeys.Records, _records);
            SetClearCaption(ClearCaption);
            Change(Sanitise);
        }

        private void Update()
        {
            if (_clearLabel != null && _clearLabel.text == ConfirmClearCaption && Time.unscaledTime > _clearArmedUntil)
            {
                SetClearCaption(ClearCaption);
            }
        }

        private void SetClearCaption(string caption)
        {
            if (_clearLabel != null)
            {
                _clearLabel.text = caption;
            }
        }

        /// <summary>Assigns the clear button's label. Used by the scene generator.</summary>
        /// <param name="label">The label.</param>
        public void BindClear(Text label) => _clearLabel = label;

        /// <summary>
        /// Clears any choice a saved file holds that the player has not earned.
        /// </summary>
        /// <remarks>
        /// The run modifiers and the records are separate files. A records file deleted or reset leaves a
        /// run-modifier file still asking for Ascended or a trial's armour, which would hand both out for free.
        /// </remarks>
        private void Sanitise()
        {
            if (!_records.AscendedUnlocked)
            {
                _modifiers.Ascended = false;
            }

            if (!TrialBook.IsTintUnlocked(_modifiers.KnightTintId, _records))
            {
                _modifiers.KnightTintId = TrialBook.DefaultTintId;
            }
        }

        private void Change(Action apply)
        {
            if (_loading)
            {
                return;
            }

            apply();
            RunSettings.Current = _modifiers;
            _saveService?.Save(SaveKeys.RunModifiers, _modifiers);
            ReflectToToggles();
        }

        /// <summary>Pushes the loaded modifiers back into the toggles without re-saving them.</summary>
        private void ReflectToToggles()
        {
            _loading = true;

            if (_fastLearnerToggle != null)
            {
                _fastLearnerToggle.isOn = _modifiers.FastLearner;
            }

            if (_noHealingToggle != null)
            {
                _noHealingToggle.isOn = _modifiers.NoHealing;
            }

            if (_fragileToggle != null)
            {
                _fragileToggle.isOn = _modifiers.FragilePlayer;
            }

            if (_trainingToggle != null)
            {
                _trainingToggle.isOn = _modifiers.TrainingMode;
            }

            ReflectProgression();
            _loading = false;
        }

        /// <summary>Reads the saved modifiers, defaulting to an unmodified run.</summary>
        /// <summary>Shows the difficulty, the armour, the rematch and the records as they now stand.</summary>
        private void ReflectProgression()
        {
            if (_difficultyLabel != null)
            {
                _difficultyLabel.text = "Difficulty: " + DifficultyProfile.Label(_modifiers.Difficulty);
            }

            if (_difficultyBlurb != null)
            {
                _difficultyBlurb.text = DifficultyProfile.Blurb(_modifiers.Difficulty);
            }

            if (_tintLabel != null)
            {
                _tintLabel.text = "Armour: " + TrialBook.TintById(_modifiers.KnightTintId).Name;
            }

            bool unlocked = _records.AscendedUnlocked;

            if (_ascendedToggle != null)
            {
                _ascendedToggle.interactable = unlocked;
                _ascendedToggle.isOn = _modifiers.Ascended;
            }

            if (_ascendedLabel != null)
            {
                _ascendedLabel.text = unlocked ? "Igris Ascended" : "Ascended (beat Knight)";
            }

            if (_recordsText != null)
            {
                _recordsText.text = DescribeRecords(_records);
            }
        }

        /// <summary>The records panel: one line per difficulty, the rematch if fought, and the trials.</summary>
        /// <remarks>Static and pure so it can be tested without a scene.</remarks>
        /// <param name="records">The player's records.</param>
        /// <returns>The panel's text.</returns>
        public static string DescribeRecords(RecordsData records)
        {
            records ??= new RecordsData();
            var builder = new StringBuilder();

            foreach (Difficulty difficulty in new[] { Difficulty.Story, Difficulty.Knight, Difficulty.Nightmare })
            {
                builder.AppendLine(DescribeMode(DifficultyProfile.Label(difficulty), records.Find(difficulty, false)));
            }

            foreach (Difficulty difficulty in new[] { Difficulty.Knight, Difficulty.Nightmare })
            {
                ModeRecord ascended = records.Find(difficulty, true);

                if (ascended != null)
                {
                    builder.AppendLine(DescribeMode("Ascended " + DifficultyProfile.Label(difficulty), ascended));
                }
            }

            int completed = 0;

            foreach (Trial trial in TrialBook.Trials)
            {
                if (records.HasCompleted(trial.Id))
                {
                    completed++;
                }
            }

            builder.AppendLine();
            builder.Append("Trials ").Append(completed).Append(" / ").Append(TrialBook.Trials.Count);

            foreach (Trial trial in TrialBook.Trials)
            {
                builder.AppendLine();
                builder.Append(records.HasCompleted(trial.Id) ? "<color=#E8B04A>+ " : "<color=#7A6E62>- ")
                    .Append(trial.Title).Append("</color>  <size=16>").Append(trial.Description).Append("</size>");
            }

            return builder.ToString();
        }

        /// <summary>One mode's line on the records panel.</summary>
        private static string DescribeMode(string name, ModeRecord record)
        {
            if (record == null || record.Attempts == 0)
            {
                return "<b>" + name + "</b>   not yet fought";
            }

            if (record.Wins == 0)
            {
                return $"<b>{name}</b>   unbeaten   fallen {record.Deaths}";
            }

            string flawless = record.FlawlessWin ? "   <color=#E8B04A>FLAWLESS</color>" : string.Empty;
            return $"<b>{name}</b>   wins {record.Wins}   fallen {record.Deaths}   best {FormatTime(record.FastestVictorySeconds)}{flawless}";
        }

        /// <summary>Minutes and seconds, as the end screen shows them.</summary>
        /// <param name="seconds">A duration.</param>
        /// <returns>For example "3:07".</returns>
        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        private RecordsData LoadRecords()
        {
            if (_saveService != null &&
                _saveService.TryLoad(SaveKeys.Records, out RecordsData loaded) &&
                loaded != null)
            {
                loaded.Migrate();
                return loaded;
            }

            return new RecordsData();
        }

        private RunModifiers Load()
        {
            if (_saveService != null &&
                _saveService.TryLoad(SaveKeys.RunModifiers, out RunModifiers loaded) &&
                loaded != null)
            {
                return loaded;
            }

            return new RunModifiers();
        }

        /// <summary>Assigns the progression widgets. Used by the scene generator.</summary>
        /// <param name="difficultyLabel">The difficulty button's label.</param>
        /// <param name="difficultyBlurb">The line under it.</param>
        /// <param name="tintLabel">The armour button's label.</param>
        /// <param name="ascended">The Ascended toggle.</param>
        /// <param name="ascendedLabel">The Ascended caption.</param>
        /// <param name="records">The records panel's body.</param>
        public void BindProgression(
            Text difficultyLabel, Text difficultyBlurb, Text tintLabel, Toggle ascended, Text ascendedLabel, Text records)
        {
            _difficultyLabel = difficultyLabel;
            _difficultyBlurb = difficultyBlurb;
            _tintLabel = tintLabel;
            _ascendedToggle = ascended;
            _ascendedLabel = ascendedLabel;
            _recordsText = records;
        }

        /// <summary>Assigns the toggles. Used by the scene generator.</summary>
        /// <param name="fastLearner">Fast Learner toggle.</param>
        /// <param name="noHealing">No Healing toggle.</param>
        /// <param name="fragile">Fragile toggle.</param>
        /// <param name="training">Training toggle.</param>
        public void Bind(Toggle fastLearner, Toggle noHealing, Toggle fragile, Toggle training)
        {
            _fastLearnerToggle = fastLearner;
            _noHealingToggle = noHealing;
            _fragileToggle = fragile;
            _trainingToggle = training;
        }
    }
}
