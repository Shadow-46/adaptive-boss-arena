using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AdaptiveBossArena.UI
{
    /// <summary>
    /// The settings panel: audio, accessibility and control rebinding, applied live and persisted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reachable from both the title screen and the pause menu, and present in both scenes, because
    /// it is also the thing that <em>applies</em> the saved settings when a scene loads — not only
    /// the thing that edits them. A player who set their preferences once expects them honoured every
    /// time the game starts, whether or not they open this panel again.
    /// </para>
    /// <para>
    /// It reaches the systems it controls through the narrowest seam available for each: audio and
    /// screen-shake through their service interfaces, and the two accessibility flags through the
    /// global <see cref="FeedbackSettings"/> that the combat feedback reads. That keeps this menu
    /// from having to hold a reference to every hit flash and camera in the scene.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SettingsMenu : MonoBehaviour
    {
        /// <summary>Hold multiplier applied when the extended-tell accessibility option is on.</summary>
        private const float ExtendedTellMultiplier = 2f;

        /// <summary>Name of the gameplay action map, where the rebindable controls live.</summary>
        private const string GameplayMapName = "Gameplay";

        [Header("Panel")]
        [SerializeField]
        [Tooltip("Root object shown while the settings are open.")]
        private GameObject _panel;

        [Header("Audio")]
        [SerializeField]
        private Slider _masterSlider;

        [SerializeField]
        private Slider _musicSlider;

        [SerializeField]
        private Slider _effectsSlider;

        [Header("Accessibility")]
        [SerializeField]
        private Slider _shakeSlider;

        [SerializeField]
        private Toggle _reducedFlashingToggle;

        [SerializeField]
        private Toggle _extendedTellToggle;

        [Header("Input")]
        [SerializeField]
        [Tooltip("The shared input actions asset, so rebindings reach the controls the game reads.")]
        private InputActionAsset _actions;

        private ISaveService _saveService;
        private SettingsData _settings;

        private InputActionMap _gameplayMap;
        private RebindButton[] _rebindRows;
        private InputActionRebindingExtensions.RebindingOperation _activeRebind;

        // Set while control values are being written from loaded data, so the resulting change
        // callbacks do not treat a programmatic sync as a player edit and save over themselves.
        private bool _isSyncing;

        /// <summary>True while the settings panel is open.</summary>
        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Start()
        {
            ServiceRegistry.Current.TryGet(out _saveService);

            _settings = Load();

            ApplyInputOverrides();
            ApplyAll();
            SyncControls();

            _gameplayMap = _actions != null ? _actions.FindActionMap(GameplayMapName, throwIfNotFound: false) : null;
            _rebindRows = GetComponentsInChildren<RebindButton>(includeInactive: true);
            RefreshRebindLabels();

            Close();
        }

        private void OnDestroy()
        {
            // An operation left running past this component's life keeps a reference to a destroyed
            // callback target, so it must be disposed rather than abandoned.
            _activeRebind?.Dispose();
            _activeRebind = null;
        }

        /// <summary>
        /// Shows the settings panel, on top of whatever it was opened from.
        /// </summary>
        /// <remarks>
        /// The panel is raised to the last sibling every time rather than relying on where it happened
        /// to be built. Unity draws later siblings over earlier ones, and this panel is opened from
        /// screens created after it — the title's buttons, the pause menu — which would otherwise draw
        /// straight through it no matter how opaque its background is.
        /// </remarks>
        public void Open()
        {
            RaiseToTop();

            if (_panel != null)
            {
                _panel.SetActive(true);
            }
        }

        /// <summary>
        /// Moves the panel above its siblings so nothing draws through it.
        /// </summary>
        /// <remarks>
        /// Called both at build time, so the order is already correct in the saved scene, and every
        /// time the panel opens, so it stays correct however the surrounding screens were assembled.
        /// </remarks>
        public void RaiseToTop()
        {
            if (_panel != null)
            {
                _panel.transform.SetAsLastSibling();
            }
        }

        /// <summary>Hides the settings panel.</summary>
        public void Close()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        // --- Control callbacks, wired to the sliders and toggles by the interface generator. ---

        /// <summary>Sets the master volume from its slider.</summary>
        /// <param name="value">Linear volume, zero to one.</param>
        public void SetMasterVolume(float value)
        {
            if (_isSyncing) return;
            _settings.MasterVolume = value;
            ApplyVolumes();
            Save();
        }

        /// <summary>Sets the music volume from its slider.</summary>
        /// <param name="value">Linear volume, zero to one.</param>
        public void SetMusicVolume(float value)
        {
            if (_isSyncing) return;
            _settings.MusicVolume = value;
            ApplyVolumes();
            Save();
        }

        /// <summary>Sets the effects volume from its slider.</summary>
        /// <param name="value">Linear volume, zero to one.</param>
        public void SetEffectsVolume(float value)
        {
            if (_isSyncing) return;
            _settings.EffectsVolume = value;
            ApplyVolumes();
            Save();
        }

        /// <summary>Sets the screen-shake intensity from its slider.</summary>
        /// <param name="value">Intensity, zero (off) to one.</param>
        public void SetShakeIntensity(float value)
        {
            if (_isSyncing) return;
            _settings.ScreenShakeIntensity = value;
            ApplyShake();
            Save();
        }

        /// <summary>Toggles reduced flashing.</summary>
        /// <param name="on">Whether flashes are suppressed.</param>
        public void SetReducedFlashing(bool on)
        {
            if (_isSyncing) return;
            _settings.ReducedFlashing = on;
            ApplyFlashing();
            Save();
        }

        /// <summary>Toggles extended adaptation-tell duration.</summary>
        /// <param name="on">Whether tells linger longer.</param>
        public void SetExtendedTell(bool on)
        {
            if (_isSyncing) return;
            _settings.ExtendedTellDuration = on;
            ApplyTell();
            Save();
        }

        // --- Control rebinding. ---

        /// <summary>
        /// Starts listening for a new key to bind to a control row.
        /// </summary>
        /// <remarks>
        /// The action is disabled for the duration so the press being captured does not also fire the
        /// control it is rebinding. Escape cancels, and the mouse is excluded so moving it cannot be
        /// mistaken for the new binding. On completion the override is serialised straight into the
        /// settings, so it survives the session exactly as the sliders do.
        /// </remarks>
        /// <param name="row">The row whose control is being rebound.</param>
        public void BeginRebind(RebindButton row)
        {
            if (row == null || _gameplayMap == null || _activeRebind != null)
            {
                return;
            }

            InputAction action = _gameplayMap.FindAction(row.ActionName, throwIfNotFound: false);

            if (action == null)
            {
                return;
            }

            row.SetLabel("Press a key…");

            bool wasEnabled = action.enabled;
            action.Disable();

            _activeRebind = action.PerformInteractiveRebinding(row.BindingIndex)
                .WithControlsExcluding("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op => CompleteRebind(op, action, row, wasEnabled, save: true))
                .OnCancel(op => CompleteRebind(op, action, row, wasEnabled, save: false))
                .Start();
        }

        /// <summary>Clears every rebinding back to the shipped defaults.</summary>
        public void ResetControls()
        {
            if (_actions == null)
            {
                return;
            }

            _actions.RemoveAllBindingOverrides();
            _settings.InputRebinds = string.Empty;
            Save();
            RefreshRebindLabels();
        }

        /// <summary>Finishes a rebind, re-enabling the action and recording the result.</summary>
        private void CompleteRebind(
            InputActionRebindingExtensions.RebindingOperation operation,
            InputAction action,
            RebindButton row,
            bool wasEnabled,
            bool save)
        {
            operation.Dispose();
            _activeRebind = null;

            if (wasEnabled)
            {
                action.Enable();
            }

            RefreshLabel(row, action);

            if (save)
            {
                _settings.InputRebinds = _actions.SaveBindingOverridesAsJson();
                Save();
            }
        }

        /// <summary>Rewrites every rebind row's label from its action's current binding.</summary>
        private void RefreshRebindLabels()
        {
            if (_rebindRows == null || _gameplayMap == null)
            {
                return;
            }

            foreach (RebindButton row in _rebindRows)
            {
                InputAction action = _gameplayMap.FindAction(row.ActionName, throwIfNotFound: false);

                if (action != null)
                {
                    RefreshLabel(row, action);
                }
            }
        }

        /// <summary>Writes one row's label from its binding, stripped of the device prefix.</summary>
        private static void RefreshLabel(RebindButton row, InputAction action)
        {
            if (row.BindingIndex < 0 || row.BindingIndex >= action.bindings.Count)
            {
                return;
            }

            row.SetLabel(InputControlPath.ToHumanReadableString(
                action.bindings[row.BindingIndex].effectivePath,
                InputControlPath.HumanReadableStringOptions.OmitDevice));
        }

        /// <summary>Assigns the references. Used by the interface generator.</summary>
        /// <param name="panel">Root object of the panel.</param>
        /// <param name="master">Master volume slider.</param>
        /// <param name="music">Music volume slider.</param>
        /// <param name="effects">Effects volume slider.</param>
        /// <param name="shake">Screen-shake slider.</param>
        /// <param name="reducedFlashing">Reduced-flashing toggle.</param>
        /// <param name="extendedTell">Extended-tell toggle.</param>
        /// <param name="actions">Shared input actions asset.</param>
        public void Bind(
            GameObject panel,
            Slider master,
            Slider music,
            Slider effects,
            Slider shake,
            Toggle reducedFlashing,
            Toggle extendedTell,
            InputActionAsset actions)
        {
            _panel = panel;
            _masterSlider = master;
            _musicSlider = music;
            _effectsSlider = effects;
            _shakeSlider = shake;
            _reducedFlashingToggle = reducedFlashing;
            _extendedTellToggle = extendedTell;
            _actions = actions;
        }

        private SettingsData Load() =>
            _saveService != null && _saveService.TryLoad(SaveKeys.Settings, out SettingsData loaded)
                ? loaded
                : new SettingsData();

        private void Save() => _saveService?.Save(SaveKeys.Settings, _settings);

        /// <summary>Applies every setting to the systems that honour it.</summary>
        private void ApplyAll()
        {
            ApplyVolumes();
            ApplyShake();
            ApplyFlashing();
            ApplyTell();
        }

        private void ApplyVolumes()
        {
            if (ServiceRegistry.Current.TryGet(out IAudioService audio))
            {
                audio.SetBusVolume(AudioBus.Master, _settings.MasterVolume);
                audio.SetBusVolume(AudioBus.Music, _settings.MusicVolume);
                audio.SetBusVolume(AudioBus.Effects, _settings.EffectsVolume);
            }
        }

        private void ApplyShake()
        {
            if (ServiceRegistry.Current.TryGet(out IScreenShake shake))
            {
                shake.SetUserIntensity(_settings.ScreenShakeIntensity);
            }
        }

        private void ApplyFlashing() => FeedbackSettings.ReducedFlashing = _settings.ReducedFlashing;

        private void ApplyTell() =>
            FeedbackSettings.TellHoldMultiplier = _settings.ExtendedTellDuration ? ExtendedTellMultiplier : 1f;

        /// <summary>Loads any saved control rebindings onto the shared input asset.</summary>
        private void ApplyInputOverrides()
        {
            if (_actions != null && !string.IsNullOrEmpty(_settings.InputRebinds))
            {
                _actions.LoadBindingOverridesFromJson(_settings.InputRebinds);
            }
        }

        /// <summary>Writes the loaded values into the controls without treating it as an edit.</summary>
        private void SyncControls()
        {
            _isSyncing = true;

            if (_masterSlider != null) _masterSlider.value = _settings.MasterVolume;
            if (_musicSlider != null) _musicSlider.value = _settings.MusicVolume;
            if (_effectsSlider != null) _effectsSlider.value = _settings.EffectsVolume;
            if (_shakeSlider != null) _shakeSlider.value = _settings.ScreenShakeIntensity;
            if (_reducedFlashingToggle != null) _reducedFlashingToggle.isOn = _settings.ReducedFlashing;
            if (_extendedTellToggle != null) _extendedTellToggle.isOn = _settings.ExtendedTellDuration;

            _isSyncing = false;
        }
    }
}
