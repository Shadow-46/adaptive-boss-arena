using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.Player;
using AdaptiveBossArena.Player.Controls;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Generates the player prefab from primitives and wires its references.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Prefabs are serialised with GUID references that make them unreviewable in a diff and prone
    /// to silent breakage when an asset moves. Generating the prefab means its structure is
    /// expressed as code that can be read, reviewed and re-run, and that a broken reference becomes
    /// a compile error rather than a missing script at runtime.
    /// </para>
    /// <para>
    /// The character is deliberately built from a capsule and nothing else. Placeholder art that
    /// looks unfinished keeps attention on how the character handles, which is the only thing worth
    /// judging at this stage.
    /// </para>
    /// </remarks>
    public static class PlayerPrefabBuilder
    {
        /// <summary>Where the generated prefab is written.</summary>
        public const string PrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        private const float CapsuleHeight = 1.8f;
        private const float CapsuleRadius = 0.4f;

        /// <summary>Slightly under the body radius so attacks must overlap the character properly.</summary>
        private const float HurtboxRadius = 0.38f;

        private const float StepOffset = 0.3f;
        private const float SkinWidth = 0.03f;

        private static readonly Color PlayerColor = new Color(0.35f, 0.75f, 0.95f);

        /// <summary>Creates or replaces the player prefab.</summary>
        [MenuItem(EditorMenus.Setup + "Generate Player Prefab", priority = EditorMenus.SetupPriorityBuildScene - 1)]
        public static void GeneratePlayerPrefab()
        {
            GameObject root = BuildPlayerObject();

            try
            {
                AssetAuthoring.EnsureFolderExists("Assets/_Project/Prefabs");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Adaptive Boss Arena] Player prefab generated at '{PrefabPath}'.");
        }

        /// <summary>Builds the player hierarchy in the current scene, ready to be saved as a prefab.</summary>
        private static GameObject BuildPlayerObject()
        {
            var root = new GameObject("Player")
            {
                layer = Layers.Player,
                tag = TagNames.Player
            };

            ConfigureCharacterController(root);
            BuildVisual(root.transform);
            BuildHurtbox(root.transform);
            AttackOverlayBuilder.Attach(root.transform);
            WireComponents(root);

            return root;
        }

        /// <summary>Adds and tunes the character controller that performs movement and collision.</summary>
        private static void ConfigureCharacterController(GameObject root)
        {
            CharacterController controller = root.AddComponent<CharacterController>();

            controller.height = CapsuleHeight;
            controller.radius = CapsuleRadius;
            controller.center = new Vector3(0f, CapsuleHeight * 0.5f, 0f);
            controller.stepOffset = StepOffset;
            controller.skinWidth = SkinWidth;

            // Zero tolerance would let the controller jitter against the arena wall segments where
            // they meet.
            controller.minMoveDistance = 0f;
        }

        /// <summary>Adds the placeholder body and a nose marker showing which way the character faces.</summary>
        private static void BuildVisual(Transform parent)
        {
            var visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(parent, false);

            // A helmed, upright silhouette rather than a capsule. Every part is collider-free: the
            // character controller on the root above owns collision and the hurtbox owns damage, so
            // the body is purely something to look at and combat timing is unaffected. The lit visor
            // replaces the white cube that used to show which way the character faces.
            SilhouetteBuilder.BuildKnight(visualRoot.transform, CapsuleHeight, CapsuleRadius);

            // Attached to the visual root so it tints every renderer beneath it, including any art
            // that replaces these primitives later.
            visualRoot.AddComponent<HitFlash>();

            // The procedural animator moves this whole visual root — body, marker and trail — while
            // the collider and hurtbox stay on the character root above it, so the swing is cosmetic
            // and the hitbox never moves with it.
            visualRoot.AddComponent<CharacterAnimator>();

            // Dormant character-art seam: drives a skeletal Animator from the same state stream once a
            // rigged model is a child of this root. A no-op while the player is a primitive.
            visualRoot.AddComponent<CharacterAnimationBridge>();

            // Placed out at arm's length and slightly ahead, so the ribbon traces the arc a blade
            // would sweep rather than a circle around the character's centre.
            WeaponTrailBuilder.Attach(
                visualRoot.transform,
                new Vector3(CapsuleRadius * 1.4f, CapsuleHeight * 0.55f, CapsuleRadius * 1.2f),
                new Color(0.6f, 0.85f, 1f),
                width: 0.3f);

            BuildWeaponSocket(visualRoot.transform);
        }

        /// <summary>Adds the empty mount a weapon model attaches to, at a placeholder hand position.</summary>
        /// <remarks>
        /// Empty and invisible today; the weapon-art seam. Positioned roughly where a right hand would
        /// hold a hilt so a dropped-in blade sits believably even before a rig exists to parent it to a
        /// real bone. See <see cref="Combat.Feel.WeaponSocket"/>.
        /// </remarks>
        private static void BuildWeaponSocket(Transform visualRoot)
        {
            var socket = new GameObject("WeaponSocket");
            socket.transform.SetParent(visualRoot, false);
            socket.transform.localPosition =
                new Vector3(CapsuleRadius * 1.2f, CapsuleHeight * 0.55f, CapsuleRadius * 1.1f);
            socket.AddComponent<WeaponSocket>();
        }

        /// <summary>Adds the trigger volume that receives boss attacks.</summary>
        private static void BuildHurtbox(Transform parent)
        {
            var hurtboxObject = new GameObject("Hurtbox")
            {
                layer = Layers.PlayerHurtbox
            };

            hurtboxObject.transform.SetParent(parent, false);

            CapsuleCollider collider = hurtboxObject.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.height = CapsuleHeight;
            collider.radius = HurtboxRadius;
            collider.center = new Vector3(0f, CapsuleHeight * 0.5f, 0f);

            hurtboxObject.AddComponent<Hurtbox>();
        }

        /// <summary>Adds the gameplay components and resolves their asset references.</summary>
        private static void WireComponents(GameObject root)
        {
            PlayerInputReader inputReader = root.AddComponent<PlayerInputReader>();
            PlayerController controller = root.AddComponent<PlayerController>();

            // Footsteps in cadence with actual travel. The player's are a light, quick tap.
            root.AddComponent<Combat.Feel.FootstepEmitter>()
                .Configure(Game.AudioService.Cues.FootstepPlayer, strideLength: 1.5f);

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsGenerator.AssetPath);
            if (actions != null)
            {
                inputReader.SetActionsAsset(actions);
            }
            else
            {
                Debug.LogWarning(
                    "[Adaptive Boss Arena] Input actions asset not found. Run " +
                    "'Generate Input Actions' and regenerate the prefab.");
            }

            PlayerConfig config = GeneratedAssets.Config<PlayerConfig>("DefaultPlayerConfig");

            if (config != null)
            {
                controller.SetConfig(config);
            }

            var animator = root.GetComponentInChildren<CharacterAnimator>();
            var animationConfig =
                GeneratedAssets.Config<CharacterAnimationConfig>("DefaultPlayerAnimation");

            if (animator != null && animationConfig != null)
            {
                animator.SetConfig(animationConfig);
            }

            controller.SetChannels(
                GeneratedAssets.EventChannel<FloatEventChannel>("OnPlayerHealthChanged"),
                GeneratedAssets.EventChannel<FloatEventChannel>("OnPlayerStaminaChanged"),
                GeneratedAssets.EventChannel<VoidEventChannel>("OnPlayerDied"),
                GeneratedAssets.EventChannel<VoidEventChannel>("OnPerfectDodge"),
                GeneratedAssets.EventChannel<VoidEventChannel>("OnDeflect"),
                GeneratedAssets.EventChannel<FloatEventChannel>("OnPlayerPostureChanged"),
                GeneratedAssets.EventChannel<StringEventChannel>("OnWeaponDrawn"),
                GeneratedAssets.EventChannel<FloatEventChannel>("OnPlayerFocusChanged"),
                GeneratedAssets.EventChannel<StringEventChannel>("OnWeaponSwingCue"));
        }

        /// <summary>Applies a colour to a primitive using a shared generated material.</summary>
        private static void ApplyColor(GameObject target, Color color) =>
            MaterialLibrary.Apply(target, $"Player_{ColorUtility.ToHtmlStringRGB(color)}", color);

    }
}
