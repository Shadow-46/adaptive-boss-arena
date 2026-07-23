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

            // Dash sits on a key the thumb can reach without leaving the movement keys, because it
            // is pressed under pressure and mid-movement more than any other control.
            BuildButton(map, InputActionNames.Dash, "<Keyboard>/space", "<Gamepad>/buttonEast");

            // Guard sits under the off-hand finger and is held, not tapped, so it needs a control
            // that can be comfortably kept down while still moving and attacking.
            BuildButton(map, InputActionNames.Guard, "<Keyboard>/leftShift", "<Gamepad>/leftShoulder");

            BuildButton(map, InputActionNames.LightAttack, "<Mouse>/leftButton", "<Gamepad>/buttonWest");
            BuildButton(map, InputActionNames.HeavyAttack, "<Mouse>/rightButton", "<Gamepad>/buttonNorth");
            BuildButton(map, InputActionNames.Special, "<Keyboard>/q", "<Gamepad>/rightShoulder");
            BuildButton(map, InputActionNames.Heal, "<Keyboard>/r", "<Gamepad>/dpad/up");
            // Deliberately not Tab, which the camera uses for lock-on. Two actions on one key meant
            // swapping weapon and breaking lock at the same moment, which is exactly the kind of
            // thing that reads as the game being broken rather than as a binding clash.
            BuildButton(map, InputActionNames.SwapWeapon, "<Keyboard>/v", "<Gamepad>/dpad/right");
            BuildButton(map, InputActionNames.Pause, "<Keyboard>/escape", "<Gamepad>/start");
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
