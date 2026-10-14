using System.Linq;
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
        /// <summary>Radius of the brute's hurtbox: how close a knight can stand, and how far past a blade's tip it can be struck.</summary>
        public const float HurtboxRadius = 0.85f;

        /// <summary>
        /// The knight's blade glow at rest: dark enough to read as cold iron, but above zero, because a material
        /// generated with no emission at all is built without the emission keyword and could never burn.
        /// </summary>
        private static readonly Color DormantBladeGlow = new Color(0.004f, 0.001f, 0f);

        /// <summary>The weak-point core's size against the body radius, on the brute and on the corrupted knight.</summary>
        private const float BruteCoreFraction = 0.34f;
        private const float KnightCoreFraction = 0.17f;

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

        /// <summary>How far the knight's own sword reaches from his grip, in metres.</summary>
        /// <remarks>
        /// The model's blade is drawn for a giant: at his size it would reach two and a half metres, past anything the
        /// fight's reach was tuned around. A third longer than the generated great sword reads as his and still
        /// leaves the player a ring to stand in.
        /// </remarks>
        private const float KnightSwordTip = 1.9f;

        /// <summary>Where the knight's blade begins past the guard, as a fraction of its reach.</summary>
        private const float KnightSwordGuardFraction = 0.17f;

        /// <summary>The generated great sword's blade, from the hand.</summary>
        private const float GreatSwordBase = 0.45f;
        private const float GreatSwordTip = 1.6f;

        /// <summary>Gives the corrupted knight the fire that breaks out of it phase by phase.</summary>
        /// <param name="visualRoot">The boss's visual root, which carries the display.</param>
        /// <param name="rig">The knight's rig.</param>
        /// <param name="blades">The sword's renderers, which burn.</param>
        private static void AttachCorruption(GameObject visualRoot, Animator rig, Renderer[] blades)
        {
            SkinnedMeshRenderer body = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(renderer => renderer.enabled && Art.SpareMeshes.IsBody(renderer.name));

            visualRoot.AddComponent<CorruptionDisplay>().Bind(
                body, blades, Art.CorruptedKnightMaterials.LoadCrackLevels(), Art.CorruptedKnightMaterials.EmberGlow,
                MaterialLibrary.GetOrCreateImpactSparks(), MaterialLibrary.GetOrCreateHeatHaze());
        }

        /// <summary>
        /// Puts the knight's own sword in his hand, drawn in the blade's material.
        /// </summary>
        /// <param name="rig">The knight's rig.</param>
        /// <param name="blade">The blade's material.</param>
        /// <returns>The sword's renderers, empty when the model carries no sword of its own.</returns>
        private static Renderer[] HoldOwnSword(Animator rig, Material blade)
        {
            SkinnedMeshRenderer sword = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(renderer => renderer.enabled && Art.SpareMeshes.IsWeapon(renderer.name));

            if (sword == null || Art.ArmsBuilder.HoldPropSword(rig, sword, KnightSwordTip) == null)
            {
                return new Renderer[0];
            }

            sword.sharedMaterials = Enumerable.Repeat(blade, Mathf.Max(1, sword.sharedMaterials.Length)).ToArray();

            // Swung through arcs the model never posed in: the bounds of its rest pose would cull the blade mid-swing.
            sword.updateWhenOffscreen = true;

            return new Renderer[] { sword };
        }

        /// <summary>The generated great sword's blade renderers, for a knight model that carries no sword of its own.</summary>
        /// <param name="rig">The knight's rig.</param>
        /// <param name="blade">The blade's material, which marks which of the sword's parts burn.</param>
        /// <returns>The blade's renderers.</returns>
        private static Renderer[] MountedBlades(Animator rig, Material blade)
        {
            Transform hand = rig.GetBoneTransform(HumanBodyBones.RightHand);
            Transform cleaver = hand != null ? hand.Find("Cleaver") : null;

            return cleaver != null
                ? cleaver.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.sharedMaterial == blade).ToArray()
                : new Renderer[0];
        }

        /// <summary>Creates or replaces the boss prefab.</summary>
        [MenuItem(EditorMenus.Setup + "Generate Boss Prefab", priority = EditorMenus.SetupPriorityBuildScene - 1)]
        public static void GenerateBossPrefab()
        {
            GameObject root = BuildBossObject();
            CharacterRendering.Mark(root);

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

            bool ownSword = false;

            if (SilhouetteBuilder.TryBuildRig(
                    visualRoot.transform, animationConfig, SilhouetteBuilder.BruteRigMaterial(), out Animator rig))
            {
                // The corrupted knight is slim where the brute was barrel-chested: a brute-sized core stood off its
                // breastplate as a ball. Half the size, it sits in the plate as an ember heart and still marks the
                // weak point.
                bool knight = Art.LicensedArtPostprocessor.BossBody == Art.LicensedArtPostprocessor.CorruptedKnightBody;
                SilhouetteBuilder.AttachBruteCore(rig, BodyRadius * (knight ? KnightCoreFraction : BruteCoreFraction));

                // The brute's clips are a two-handed great sword set, swung by nothing until now. The knight's blade
                // is dark iron that can burn: its emission is kept, faintly, so the corruption can raise it per phase.
                Material blade = knight
                    ? MaterialLibrary.GetOrCreateSurface("CorruptedBlade", new Color(0.12f, 0.1f, 0.1f), metallic: 0.9f, smoothness: 0.5f, emission: DormantBladeGlow)
                    : MaterialLibrary.GetOrCreateSurface("BruteCleaver", new Color(0.3f, 0.28f, 0.27f), metallic: 0.9f, smoothness: 0.45f);

                // The knight swings his own jagged sword; the brute, or a knight model without one, the generated blade.
                Renderer[] blades = knight ? HoldOwnSword(rig, blade) : new Renderer[0];

                if (blades.Length == 0)
                {
                    Art.ArmsBuilder.MountCleaver(
                        rig,
                        blade,
                        MaterialLibrary.GetOrCreateSurface("BruteGrip", new Color(0.12f, 0.08f, 0.06f), metallic: 0.1f, smoothness: 0.25f));
                }

                if (knight)
                {
                    AttachCorruption(visualRoot, rig, blades.Length > 0 ? blades : MountedBlades(rig, blade));
                }

                ownSword = blades.Length > 0;
            }
            else
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
            visualRoot.AddComponent<PhaseAura>().SetEmberMaterial(MaterialLibrary.GetOrCreateImpactSparks());

            // Along the cleaver's broad blade when there is one, and out from the body where the primitive's arm
            // would be when there is not. Longer and faintly warm: the boss's swings cover more ground, and
            // the smear is a large part of how the player judges whether one will reach them.
            Transform hand = rig != null && rig.isHuman ? rig.GetBoneTransform(HumanBodyBones.RightHand) : null;
            Transform cleaver = hand != null ? hand.Find("Cleaver") : null;
            var warmSteel = new Color(0.5f, 0.36f, 0.3f, 1f);

            if (cleaver != null)
            {
                float tip = ownSword ? KnightSwordTip : GreatSwordTip;
                float bladeBase = ownSword ? KnightSwordTip * KnightSwordGuardFraction : GreatSwordBase;
                WeaponTrailBuilder.Attach(cleaver, Vector3.zero, baseDistance: bladeBase, tipDistance: tip, warmSteel);

                // The sword the cuts land with, over the same span the trail smears.
                StrikeVolumeBuilder.Attach(cleaver, Combat.StrikerParts.Weapon, bladeBase, tip, 0.14f);
            }
            else
            {
                WeaponTrailBuilder.Attach(
                    visualRoot.transform, new Vector3(BodyRadius * 0.8f, BodyHeight * 0.6f, 0f), 0.3f, 1.4f, warmSteel);
            }

            // Fists, a foot and the body, for the kick, the grab, the leap and the frenzy's bare hands. A fist is
            // wide on this body; the body sphere is its torso, for landing on the knight. A no-op without a rig.
            StrikeVolumeBuilder.AttachLimbs(rig, fistRadius: 0.2f, footRadius: 0.2f, bodyRadius: 0.8f);
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
            // moving around the arena even as a primitive, and each one drives up more dust.
            root.AddComponent<Combat.Feel.FootstepEmitter>()
                .Configure(
                    Game.AudioService.Cues.FootstepBoss, strideLength: 2.4f,
                    MaterialLibrary.GetOrCreateImpactMatter(), dustSize: 0.5f, tremor: 0.07f);

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
                GeneratedAssets.EventChannel<VoidEventChannel>("OnBossOverbalanced"),
                GeneratedAssets.EventChannel<VoidEventChannel>("OnBossParried"));
        }
    }
}
