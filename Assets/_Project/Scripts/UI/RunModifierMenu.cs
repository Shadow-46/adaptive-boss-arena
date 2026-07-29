using System;
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
        private void Change(Action apply)
        {
            if (_loading)
            {
                return;
            }

            apply();
            RunSettings.Current = _modifiers;
            _saveService?.Save(SaveKeys.RunModifiers, _modifiers);
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

            _loading = false;
        }

        /// <summary>Reads the saved modifiers, defaulting to an unmodified run.</summary>
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
