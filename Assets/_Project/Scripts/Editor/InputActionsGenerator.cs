using System.IO;
using AdaptiveBossArena.Player.Controls;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Generates the project's input actions asset from code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The asset is built in memory through the Input System's own authoring API and then written
    /// out with <see cref="InputActionAsset.ToJson"/>. Hand-writing the JSON would mean inventing
    /// binding identifiers by hand and would fail at import if any of them were malformed; letting
    /// the package serialise its own format makes that class of error impossible.
    /// </para>
    /// <para>
    /// Every binding is authored against the constants in <see cref="InputActionNames"/>, so the
    /// generator and the runtime reader cannot disagree about what an action is called.
    /// </para>
    /// </remarks>
    public static class InputActionsGenerator
    {
        /// <summary>Where the generated asset is written.</summary>
        public const string AssetPath = "Assets/_Project/Settings/PlayerControls.inputactions";

        private const string KeyboardMouseScheme = "Keyboard&Mouse";
        private const string GamepadScheme = "Gamepad";

        /// <summary>Creates or overwrites the input actions asset.</summary>
        [MenuItem(EditorMenus.Setup + "Generate Input Actions", priority = EditorMenus.SetupPriorityGenerateAssets + 1)]
        public static void GenerateInputActions()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();

            try
            {
                asset.name = Path.GetFileNameWithoutExtension(AssetPath);

                BuildControlSchemes(asset);
                BuildGameplayMap(asset);

                AssetAuthoring.EnsureFolderExists(Path.GetDirectoryName(AssetPath));
                File.WriteAllText(AssetPath, asset.ToJson());
            }
            finally
            {
                // The in-memory asset was only ever a serialisation vehicle; the file on disk is the
                // real product, and Unity's importer will construct its own instance from it.
                Object.DestroyImmediate(asset);
            }

            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[Adaptive Boss Arena] Input actions generated at '{AssetPath}'.");
        }

        /// <summary>Declares the device groups so bindings can be filtered per scheme later.</summary>
        private static void BuildControlSchemes(InputActionAsset asset)
        {
            asset.AddControlScheme(KeyboardMouseScheme)
                .WithRequiredDevice<Keyboard>()
                .WithOptionalDevice<Mouse>();

            asset.AddControlScheme(GamepadScheme)
                .WithRequiredDevice<Gamepad>();
        }

        /// <summary>Builds the in-combat action map and all of its bindings.</summary>
        private static void BuildGameplayMap(InputActionAsset asset)
        {
            InputActionMap map = asset.AddActionMap(InputActionNames.GameplayMap);

            BuildMoveAction(map);
            BuildLookAction(map);

            // Dash sits on a key the thumb can reach without leaving the movement keys, because it
            // is pressed under pressure and mid-movement more than any other control.
            BuildButton(map, InputActionNames.Dash, "<Keyboard>/space", "<Gamepad>/buttonEast");

            // Guard is held on the right mouse button, the hand that is already on the mouse: the player asked
            // for block and deflect under the mouse, and Shift is now the heavy-attack modifier.
            BuildButton(map, InputActionNames.Guard, "<Mouse>/rightButton", "<Gamepad>/leftShoulder");

            BuildButton(map, InputActionNames.LightAttack, "<Mouse>/leftButton", "<Gamepad>/buttonWest");
            BuildHeavyAttack(map);

            // E, not Q: Q is reserved for the dedicated parry.
            BuildButton(map, InputActionNames.Special, "<Keyboard>/e", "<Gamepad>/buttonNorth");
            BuildButton(map, InputActionNames.Heal, "<Keyboard>/r", "<Gamepad>/dpad/up");

            // Deliberately not Tab, which locks on. Two actions on one key meant swapping weapon and
            // breaking lock at the same moment, which reads as the game being broken.
            BuildButton(map, InputActionNames.SwapWeapon, "<Keyboard>/v", "<Gamepad>/dpad/right");
            BuildButton(map, InputActionNames.Pause, "<Keyboard>/escape", "<Gamepad>/start");

            // Camera controls live in the asset rather than as keys hard-coded in the camera, so they have a
            // gamepad binding and can be rebound like everything else.
            InputAction lockOn = map.AddAction(InputActionNames.LockOn, InputActionType.Button);
            lockOn.AddBinding("<Keyboard>/tab", groups: KeyboardMouseScheme);
            lockOn.AddBinding("<Mouse>/middleButton", groups: KeyboardMouseScheme);
            lockOn.AddBinding("<Gamepad>/rightStickPress", groups: GamepadScheme);

            BuildButton(map, InputActionNames.CycleCamera, "<Keyboard>/c", "<Gamepad>/dpad/down");
        }

        /// <summary>
        /// Heavy attack: Shift held with the left mouse button, or the right trigger.
        /// </summary>
        /// <remarks>
        /// A modifier composite, so the same button reads as light or heavy by whether Shift is held. The
        /// reader drops the light press when the heavy one fires on the same frame, since both see the click.
        /// </remarks>
        private static void BuildHeavyAttack(InputActionMap map)
        {
            InputAction heavy = map.AddAction(InputActionNames.HeavyAttack, InputActionType.Button);

            heavy.AddCompositeBinding("OneModifier")
                .With("Modifier", "<Keyboard>/leftShift", KeyboardMouseScheme)
                .With("Binding", "<Mouse>/leftButton", KeyboardMouseScheme);

            heavy.AddBinding("<Gamepad>/rightTrigger", groups: GamepadScheme);
        }

        /// <summary>Camera orbit: raw mouse movement in pixels, or the right stick as a rate.</summary>
        private static void BuildLookAction(InputActionMap map)
        {
            InputAction look = map.AddAction(
                InputActionNames.Look,
                InputActionType.Value,
                expectedControlLayout: nameof(Vector2));

            look.AddBinding("<Mouse>/delta", groups: KeyboardMouseScheme);
            look.AddBinding("<Gamepad>/rightStick", groups: GamepadScheme).WithProcessor("stickDeadzone");
        }

        /// <summary>Builds the movement axis with keyboard composites and a gamepad stick.</summary>
        private static void BuildMoveAction(InputActionMap map)
        {
            // Note the parameter name: the authoring API calls this the expected control *layout*,
            // even though the field it serialises to in the .inputactions file is named
            // expectedControlType. Mixing the two up is a compile error, not a runtime surprise.
            InputAction move = map.AddAction(
                InputActionNames.Move,
                InputActionType.Value,
                expectedControlLayout: nameof(Vector2));

            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", KeyboardMouseScheme)
                .With("Down", "<Keyboard>/s", KeyboardMouseScheme)
                .With("Left", "<Keyboard>/a", KeyboardMouseScheme)
                .With("Right", "<Keyboard>/d", KeyboardMouseScheme);

            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow", KeyboardMouseScheme)
                .With("Down", "<Keyboard>/downArrow", KeyboardMouseScheme)
                .With("Left", "<Keyboard>/leftArrow", KeyboardMouseScheme)
                .With("Right", "<Keyboard>/rightArrow", KeyboardMouseScheme);

            // A stick deflected slightly by a worn controller must not creep the character.
            move.AddBinding("<Gamepad>/leftStick", groups: GamepadScheme)
                .WithProcessor("stickDeadzone");
        }

        /// <summary>Builds a button action bound to one keyboard or mouse control and one gamepad control.</summary>
        private static void BuildButton(
            InputActionMap map,
            string actionName,
            string keyboardPath,
            string gamepadPath)
        {
            InputAction action = map.AddAction(actionName, InputActionType.Button);

            action.AddBinding(keyboardPath, groups: KeyboardMouseScheme);
            action.AddBinding(gamepadPath, groups: GamepadScheme);
        }
    }
}
