using AdaptiveBossArena.AI;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Events;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Generates the boss prefab from primitives and wires its references.
    /// </summary>
    /// <remarks>
    /// The boss is built noticeably larger than the player, which is doing real work rather than
    /// decoration: silhouette is how the player judges range at a glance, and its attacks are tuned
    /// against a body of this size.
    /// </remarks>
    public static class BossPrefabBuilder
    {
        /// <summary>Where the generated prefab is written.</summary>
        public const string PrefabPath = "Assets/_Project/Prefabs/Boss.prefab";

        private const float BodyHeight = 2.8f;
        private const float BodyRadius = 0.9f;
        private const float HurtboxRadius = 0.85f;

        // The weak point sits over the glowing core the brute silhouette already carries, and is
        // deliberately wider than the art it covers: a 0.3 m sphere would be a pixel-hunt at combat
        // speed, and the decision worth asking for is where the player stands, not how precisely
        // they aim. Both numbers are read together with SilhouetteBuilder's Core part, which is
        // placed at the same fractions of height and radius.
        private const float WeakPointRadius = 0.45f;
        private const float WeakPointHeightFraction = 0.63f;
        private const float WeakPointForwardFraction = 0.56f;

        /// <summary>Damage scaling for hits landing on the core.</summary>
        /// <remarks>
        /// Chosen so a full light chain to the core is clearly better than one to the flank without
        /// making anything else pointless. The trade the player is being offered is that the core
        /// faces them only while the boss does too, so the extra damage is paid for in risk.
        /// </remarks>
        private const float WeakPointMultiplier = 1.6f;

        private static readonly Color BossColor = new Color(0.85f, 0.25f, 0.3f);
        private static readonly Color WeakPointColor = new Color(1f, 0.85f, 0.3f);

        /// <summary>Creates or replaces the boss prefab.</summary>
        [MenuItem(EditorMenus.Setup + "Generate Boss Prefab", priority = EditorMenus.SetupPriorityBuildScene - 1)]
        public static void GenerateBossPrefab()
        {
            GameObject root = BuildBossObject();

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
            Debug.Log($"[Adaptive Boss Arena] Boss prefab generated at '{PrefabPath}'.");
        }

        private static GameObject BuildBossObject()
        {
            var root = new GameObject("Boss")
            {
                layer = Layers.Boss,
                tag = TagNames.Boss
            };

            ConfigureCharacterController(root);
            BuildVisual(root.transform);
            BuildHurtboxes(root.transform);
            AttackOverlayBuilder.Attach(root.transform);
            WireComponents(root);

            return root;
        }

        private static void ConfigureCharacterController(GameObject root)
        {
            CharacterController controller = root.AddComponent<CharacterController>();

            controller.height = BodyHeight;
            controller.radius = BodyRadius;
            controller.center = new Vector3(0f, BodyHeight * 0.5f, 0f);
            controller.stepOffset = 0.4f;
            controller.skinWidth = 0.04f;
            controller.minMoveDistance = 0f;
        }

        private static void BuildVisual(Transform parent)
        {
            var visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(parent, false);

            // A rigged model wins when one has been supplied; otherwise a wide, low, horned
            // silhouette. Facing has to be readable from across the arena, because reading the
            // boss's facing is how the player judges whether an attack is aimed at them — the
            // glowing forward core does that job, and doubles as a visible source for the phase
            // aura that previously seemed to come from nowhere.
            var animationConfig =
                GeneratedAssets.Config<CharacterAnimationConfig>("DefaultBossAnimation");

            if (!SilhouetteBuilder.TryBuildRig(
                    visualRoot.transform, animationConfig != null ? animationConfig.RigPrefab : null))
            {
                SilhouetteBuilder.BuildBrute(visualRoot.transform, BodyHeight, BodyRadius);
            }

            visualRoot.AddComponent<HitFlash>();

            // Same rig as the player, tuned heavier through the boss animation config so its swings
            // feel like they carry more mass. Moves the visual only; the hurtbox stays on the root.
            visualRoot.AddComponent<CharacterAnimator>();

            // Dormant character-art seam: drives a skeletal Animator from the same state stream once a
            // rigged model is a child of this root. A no-op while the boss is a primitive.
            visualRoot.AddComponent<CharacterAnimationBridge>();

            // Lights the boss from within, dark at the start and hotter each phase, so escalation is
            // visible on the character rather than only felt in the damage.
            visualRoot.AddComponent<PhaseAura>();

            // Wider and hotter than the player's. The boss's swings cover more ground, and the
            // ribbon is a large part of how the player judges whether one will reach them.
            WeaponTrailBuilder.Attach(
                visualRoot.transform,
                new Vector3(BodyRadius * 1.5f, BodyHeight * 0.6f, BodyRadius * 1.3f),
                new Color(1f, 0.45f, 0.35f),
                width: 0.55f);
        }

        private static void BuildHurtboxes(Transform parent)
        {
            var hurtboxObject = new GameObject("Hurtbox")
            {
                layer = Layers.BossHurtbox
            };

            hurtboxObject.transform.SetParent(parent, false);

            CapsuleCollider collider = hurtboxObject.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.height = BodyHeight;
            collider.radius = HurtboxRadius;
            collider.center = new Vector3(0f, BodyHeight * 0.5f, 0f);

            hurtboxObject.AddComponent<Hurtbox>();

            BuildWeakPoint(parent);
        }

        /// <summary>
        /// Adds the second, higher-multiplier hurtbox over the boss's forward core.
        /// </summary>
        /// <remarks>
        /// Parented to the root rather than to the visual, for the same reason the body hurtbox is:
        /// the visual leans and bobs under the animator, and a hitbox that moved with an idle
        /// animation would make the same swing land differently from one moment to the next.
        /// <para>
        /// It sits entirely inside the body capsule, which is intended. Both are found by a swing
        /// from the front, and <see cref="AttackHitDetector"/> resolves that overlap in favour of
        /// the higher multiplier — so the core rewards fighting the boss face-on, where its own
        /// attacks are pointed.
        /// </para>
        /// </remarks>
        private static void BuildWeakPoint(Transform parent)
        {
            var weakPointObject = new GameObject("WeakPoint")
            {
                layer = Layers.BossHurtbox
            };

            weakPointObject.transform.SetParent(parent, false);

            SphereCollider collider = weakPointObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = WeakPointRadius;
            collider.center = new Vector3(
                0f, BodyHeight * WeakPointHeightFraction, BodyRadius * WeakPointForwardFraction);

            var hurtbox = weakPointObject.AddComponent<Hurtbox>();

            using (AssetAuthoring.AssetWriter writer = AssetAuthoring.Edit(hurtbox))
            {
                writer.Float("_damageMultiplier", WeakPointMultiplier);
            }
        }

        private static void WireComponents(GameObject root)
        {
            BossController controller = root.AddComponent<BossController>();

            // Heavier, slower-cadenced footfalls than the player, so the boss reads as a large mass
            // moving around the arena even as a primitive.
            root.AddComponent<Combat.Feel.FootstepEmitter>()
                .Configure(Game.AudioService.Cues.FootstepBoss, strideLength: 2.4f);

            BossConfig config = GeneratedAssets.Config<BossConfig>("DefaultBossConfig");

            if (config != null)
            {
                controller.SetConfig(config);
            }

            var animator = root.GetComponentInChildren<CharacterAnimator>();
            var animationConfig =
                GeneratedAssets.Config<CharacterAnimationConfig>("DefaultBossAnimation");

            if (animator != null && animationConfig != null)
            {
                animator.SetConfig(animationConfig);
            }

            controller.SetChannels(
                GeneratedAssets.EventChannel<FloatEventChannel>("OnBossHealthChanged"),
                GeneratedAssets.EventChannel<IntEventChannel>("OnBossPhaseChanged"),
                GeneratedAssets.EventChannel<VoidEventChannel>("OnBossDefeated"),
                GeneratedAssets.EventChannel<StringEventChannel>("OnAdaptationAdopted"),
                GeneratedAssets.EventChannel<FloatEventChannel>("OnBossPostureChanged"),
                GeneratedAssets.EventChannel<VoidEventChannel>("OnBossOverbalanced"));
        }
    }
}
