using UnityEngine;

namespace AdaptiveBossArena.Player.Controls
{
    /// <summary>Discrete actions the player can trigger.</summary>
    /// <remarks>
    /// Values are contiguous from zero so they can index the buffer's timestamp array directly,
    /// which keeps input buffering allocation-free and constant-time.
    /// </remarks>
    public enum PlayerInputAction
    {
        /// <summary>Evasive dash.</summary>
        Dash = 0,

        /// <summary>Fast primary attack.</summary>
        LightAttack = 1,

        /// <summary>Slow committed attack.</summary>
        HeavyAttack = 2,

        /// <summary>Special ability.</summary>
        Special = 3,

        /// <summary>Restore health.</summary>
        Heal = 4,

        /// <summary>Raise the guard. Pressed at the right instant this becomes a deflect.</summary>
        Guard = 5,

        /// <summary>Cycle to the next weapon.</summary>
        SwapWeapon = 6
    }

    /// <summary>Names the runtime looks up in the generated input actions asset.</summary>
    /// <remarks>
    /// A mistyped action name fails at runtime with a null action rather than at compile time, and
    /// the symptom is a control that silently does nothing. Both the generator and the reader use
    /// these constants, so the two cannot drift apart.
    /// </remarks>
    public static class InputActionNames
    {
        /// <summary>Action map containing all in-combat controls.</summary>
        public const string GameplayMap = "Gameplay";

        /// <summary>Two-dimensional movement axis.</summary>
        public const string Move = "Move";

        /// <summary>Evasive dash.</summary>
        public const string Dash = "Dash";

        /// <summary>Fast primary attack.</summary>
        public const string LightAttack = "LightAttack";

        /// <summary>Slow committed attack.</summary>
        public const string HeavyAttack = "HeavyAttack";

        /// <summary>Special ability.</summary>
        public const string Special = "Special";

        /// <summary>Restore health.</summary>
        public const string Heal = "Heal";

        /// <summary>Raise the guard, and with the right timing, deflect.</summary>
        public const string Guard = "Guard";

        /// <summary>Cycle to the next weapon.</summary>
        public const string SwapWeapon = "SwapWeapon";

        /// <summary>Open the pause menu.</summary>
        public const string Pause = "Pause";

        /// <summary>Resolves the asset action name for a buffered action.</summary>
        /// <param name="action">The action to resolve.</param>
        /// <returns>The name used in the input actions asset.</returns>
        public static string For(PlayerInputAction action)
        {
            switch (action)
            {
                case PlayerInputAction.Dash: return Dash;
                case PlayerInputAction.LightAttack: return LightAttack;
                case PlayerInputAction.HeavyAttack: return HeavyAttack;
                case PlayerInputAction.Special: return Special;
                case PlayerInputAction.Heal: return Heal;
                case PlayerInputAction.Guard: return Guard;
                case PlayerInputAction.SwapWeapon: return SwapWeapon;
                default: return string.Empty;
            }
        }
    }

    /// <summary>
    /// The player character's view of the controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Interposed between the Input System and the character so that gameplay code never talks to
    /// the input package directly. That buys two things: play-mode tests can drive the character
    /// from a scripted fake, and swapping input backends touches one class.
    /// </para>
    /// <para>
    /// This interface lives in the Player assembly and must stay there. Moving it into Core would
    /// place player intent within reach of the boss's assemblies and defeat the project's central
    /// design constraint.
    /// </para>
    /// </remarks>
    public interface IPlayerInput
    {
        /// <summary>Movement axis, normalised to at most unit length.</summary>
        Vector2 MoveDirection { get; }

        /// <summary>True on the single frame an action was triggered.</summary>
        /// <param name="action">The action to test.</param>
        /// <returns>Whether the action began this frame.</returns>
        bool WasPressedThisFrame(PlayerInputAction action);

        /// <summary>True while an action's control is held down.</summary>
        /// <param name="action">The action to test.</param>
        /// <returns>Whether the control is currently held.</returns>
        bool IsHeld(PlayerInputAction action);

        /// <summary>Enables or suspends input, used when pausing or on death.</summary>
        /// <param name="enabled">True to accept input.</param>
        void SetEnabled(bool enabled);
    }
}
