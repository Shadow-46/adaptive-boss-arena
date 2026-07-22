using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AdaptiveBossArena.Player.Controls
{
    /// <summary>
    /// Reads the controls through the Input System package.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The only type in the project that touches the input package at runtime, which is what allows
    /// play-mode tests to substitute a scripted fake and drive the character without a device.
    /// </para>
    /// <para>
    /// Actions are resolved once at initialisation and cached in an array indexed by
    /// <see cref="PlayerInputAction"/>. Looking actions up by string every frame is both wasteful
    /// and forgiving of typos in a way that hides broken controls.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInput
    {
        [SerializeField]
        [Tooltip("Generated input actions asset. Produced by " +
                 "'Adaptive Boss Arena/Setup/Generate Input Actions'.")]
        private InputActionAsset _actions;

        private InputActionMap _gameplayMap;
        private InputAction _moveAction;
        private InputAction[] _actionLookup;
        private bool _isEnabled = true;

        /// <inheritdoc />
        public Vector2 MoveDirection
        {
            get
            {
                if (!_isEnabled || _moveAction == null)
                {
                    return Vector2.zero;
                }

                Vector2 raw = _moveAction.ReadValue<Vector2>();

                // Composite keyboard bindings can exceed unit length on a diagonal, which would let
                // a diagonal run be measurably faster than a straight one.
                return raw.sqrMagnitude > 1f ? raw.normalized : raw;
            }
        }

        /// <inheritdoc />
        public bool WasPressedThisFrame(PlayerInputAction action)
        {
            if (!_isEnabled)
            {
                return false;
            }

            InputAction resolved = _actionLookup[(int)action];
            return resolved != null && resolved.WasPressedThisFrame();
        }

        /// <inheritdoc />
        public bool IsHeld(PlayerInputAction action)
        {
            if (!_isEnabled)
            {
                return false;
            }

            InputAction resolved = _actionLookup[(int)action];
            return resolved != null && resolved.IsPressed();
        }

        /// <inheritdoc />
        public void SetEnabled(bool enabled) => _isEnabled = enabled;

        private void Awake() => ResolveActions();

        private void OnEnable() => _gameplayMap?.Enable();

        private void OnDisable() => _gameplayMap?.Disable();

        /// <summary>Caches every action referenced by the character, reporting any that are missing.</summary>
        private void ResolveActions()
        {
            int actionCount = Enum.GetValues(typeof(PlayerInputAction)).Length;
            _actionLookup = new InputAction[actionCount];

            if (_actions == null)
            {
                Debug.LogError(
                    "[Adaptive Boss Arena] No input actions asset assigned. The player will not respond " +
                    "to input. Run 'Adaptive Boss Arena/Setup/Generate Input Actions' and assign the result.",
                    this);
                return;
            }

            _gameplayMap = _actions.FindActionMap(InputActionNames.GameplayMap, throwIfNotFound: false);

            if (_gameplayMap == null)
            {
                Debug.LogError(
                    $"[Adaptive Boss Arena] Action map '{InputActionNames.GameplayMap}' was not found in " +
                    $"'{_actions.name}'. The asset may predate a rename.",
                    this);
                return;
            }

            _moveAction = FindOrWarn(InputActionNames.Move);

            for (int i = 0; i < actionCount; i++)
            {
                _actionLookup[i] = FindOrWarn(InputActionNames.For((PlayerInputAction)i));
            }
        }

        /// <summary>Resolves one action, warning rather than throwing so the game still runs.</summary>
        private InputAction FindOrWarn(string actionName)
        {
            InputAction action = _gameplayMap.FindAction(actionName, throwIfNotFound: false);

            if (action == null)
            {
                Debug.LogWarning(
                    $"[Adaptive Boss Arena] Input action '{actionName}' was not found. " +
                    "The corresponding control will do nothing.",
                    this);
            }

            return action;
        }

        /// <summary>Assigns the actions asset. Used by the prefab generator.</summary>
        /// <param name="actions">The actions asset to read from.</param>
        public void SetActionsAsset(InputActionAsset actions) => _actions = actions;
    }
}
