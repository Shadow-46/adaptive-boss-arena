using AdaptiveBossArena.AI;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Game;
using AdaptiveBossArena.Learning;
using AdaptiveBossArena.Player;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Generates the project's default configuration assets and wires them together.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Most field values come from the initialisers on the configuration types themselves, so this
    /// generator concerns itself with what those cannot express: the per-asset differences between
    /// attacks, and the references that connect assets to one another.
    /// </para>
    /// <para>
    /// Existing assets are loaded rather than replaced, so running this after tuning values by hand
    /// will not discard that work. Delete an asset to have it regenerated from defaults.
    /// </para>
    /// <para>
    /// That safety has a cost: changing a number in this file has no effect on an asset that already
    /// exists, so a tuning pass written here would silently do nothing. "Retune Generated Assets"
    /// exists for exactly that case. It rewrites tuning values into the assets already on disk
    /// rather than recreating them, which matters because recreating an asset gives it a new GUID
    /// and leaves every reference to it dangling - a failure that has already cost this project a
    /// debugging session in which the boss silently stopped attacking.
    /// </para>
    /// </remarks>
    public static class DefaultAssetGenerator
    {
        private const string ConfigFolder = EditorMenus.GeneratedAssetFolder + "/Config";
        private const string AttackFolder = EditorMenus.GeneratedAssetFolder + "/Attacks";
        private const string StrategyFolder = EditorMenus.GeneratedAssetFolder + "/Strategies";
        private const string EventFolder = EditorMenus.GeneratedAssetFolder + "/Events";
        private const string WeaponFolder = EditorMenus.GeneratedAssetFolder + "/Weapons";

        /// <summary>
        /// When true, tuning values are written even to assets that already exist.
        /// </summary>
        /// <remarks>
        /// A field rather than a parameter because it has to reach a dozen private helpers that
        /// otherwise share no call path. It is set and cleared within a single synchronous call, so
        /// it is never observable in an inconsistent state.
        /// </remarks>
        private static bool _rewriteTuning;

        /// <summary>Whether tuning values should be written for an asset with this creation state.</summary>
        private static bool ShouldWriteTuning(bool created) => created || _rewriteTuning;

        /// <summary>
        /// Rewrites the tuning values of every generated asset from the numbers in this file.
        /// </summary>
        /// <remarks>
        /// Deliberately a separate, explicitly chosen action rather than part of the normal run.
        /// Anything adjusted in the inspector is discarded by this, so it must never happen as a
        /// side effect of regenerating the project.
        /// </remarks>
        [MenuItem(EditorMenus.Setup + "Retune Generated Assets", priority = EditorMenus.SetupPriorityGenerateAssets + 2)]
        public static void RetuneGeneratedAssets()
        {
            _rewriteTuning = true;

            try
            {
                GenerateDefaultAssets();
            }
            finally
            {
                _rewriteTuning = false;
            }

            Debug.Log("[Adaptive Boss Arena] Generated assets retuned from code defaults.");
        }

        /// <summary>Creates every default configuration asset that does not already exist.</summary>
        [MenuItem(EditorMenus.Setup + "2. Generate Default Assets", priority = EditorMenus.SetupPriorityGenerateAssets)]
        public static void GenerateDefaultAssets()
        {
            // Deliberately not wrapped in StartAssetEditing/StopAssetEditing. Batching imports is a
            // worthwhile optimisation for hundreds of assets, but it also suspends importing, so
            // assets created inside the block cannot be loaded back until it closes. This generator
            // creates a few dozen assets that reference one another, so correctness matters far more
            // than the milliseconds batching would save.
            try
            {
                AssetAuthoring.EnsureFolderExists(ConfigFolder);
                AssetAuthoring.EnsureFolderExists(AttackFolder);
                AssetAuthoring.EnsureFolderExists(StrategyFolder);
                AssetAuthoring.EnsureFolderExists(EventFolder);
                AssetAuthoring.EnsureFolderExists(WeaponFolder);

                ReactionProfile reactionProfile = CreateReactionProfile();
                PlayerAttacks playerAttacks = CreatePlayerAttacks();
                BossAttacks bossAttacks = CreateBossAttacks();
                CounterStrategy[] strategies = CreateCounterStrategies();

                WeaponDefinition[] weapons = CreateWeapons(playerAttacks);

                CreatePlayerConfig(playerAttacks, weapons);
                CreateBossConfig(reactionProfile, bossAttacks, strategies);
                CreateAnimationConfigs();
                CreateArenaConfig();
                CreateEventChannels();
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[Adaptive Boss Arena] Default assets generated under '{EditorMenus.GeneratedAssetFolder}'.");
        }

        /// <summary>The player's attack set, grouped for wiring into the player configuration.</summary>
        private readonly struct PlayerAttacks
        {
            public PlayerAttacks(AttackDefinition[] lightChain, AttackDefinition heavy, AttackDefinition special)
            {
                LightChain = lightChain;
                Heavy = heavy;
                Special = special;
            }

            public AttackDefinition[] LightChain { get; }

            public AttackDefinition Heavy { get; }

            public AttackDefinition Special { get; }
        }

        /// <summary>The boss's attack set, grouped by the phase each becomes available in.</summary>
        private readonly struct BossAttacks
        {
            public BossAttacks(
                AttackDefinition sweep,
                AttackDefinition slam,
                AttackDefinition charge,
                AttackDefinition shockwave,
                AttackDefinition jab,
                AttackDefinition perilous,
                AttackDefinition phaseShockwave)
            {
                Sweep = sweep;
                Slam = slam;
                Charge = charge;
                Shockwave = shockwave;
                Jab = jab;
                Perilous = perilous;
                PhaseShockwave = phaseShockwave;
            }

            public AttackDefinition Sweep { get; }

            public AttackDefinition Slam { get; }

            public AttackDefinition Charge { get; }

            public AttackDefinition Shockwave { get; }

            public AttackDefinition Jab { get; }

            /// <summary>The unblockable "perilous" overhead — must be dodged, never blocked.</summary>
            public AttackDefinition Perilous { get; }

            /// <summary>The arena-wide unblockable shockwave for phase transitions and the gambit.</summary>
            public AttackDefinition PhaseShockwave { get; }
        }

        private static ReactionProfile CreateReactionProfile()
        {
            var profile = AssetAuthoring.CreateOrLoad<ReactionProfile>(
                $"{ConfigFolder}/DefaultReactionProfile.asset", out bool created);

            if (created)
            {
                using (AssetAuthoring.AssetWriter writer = AssetAuthoring.Edit(profile))
                {
                    writer.String("_id", "reaction.default")
                        .String("_designerNotes",
                            "Baseline human-plausible reaction limits. Later phases tighten these " +
                            "toward the fast end but must never go below it.");
                }
            }

            return profile;
        }

        /// <summary>
        /// Creates the two procedural-animation configs, one per combatant.
        /// </summary>
        /// <remarks>
        /// Two assets rather than one shared asset because the whole point is that they feel
        /// different: the player is snappy and light, the boss heavy and deliberate. The player's
        /// asset rides entirely on the type's own field defaults; the boss's asset overrides a
        /// handful of magnitudes to add mass. Both are magnitudes only — neither can change when a
        /// hitbox opens.
        /// </remarks>
        private static void CreateAnimationConfigs()
        {
            CreateAnimationConfig(
                "DefaultPlayerAnimation", "animation.player", isBoss: false);
            CreateAnimationConfig(
                "DefaultBossAnimation", "animation.boss", isBoss: true);
        }

        /// <summary>Creates or retunes a single animation config.</summary>
        private static void CreateAnimationConfig(string assetName, string id, bool isBoss)
        {
            var config = AssetAuthoring.CreateOrLoad<CharacterAnimationConfig>(
                $"{ConfigFolder}/{assetName}.asset", out bool created);

            // The player takes the type's defaults wholesale. The boss overrides only the values
            // that make it read as heavier: slower easing, a deeper wind-up, a longer lunge and a
            // more emphatic heavy multiplier, while being thrown less by a hit.
            bool writeBossTuning = isBoss && ShouldWriteTuning(created);

            if (!created && !writeBossTuning)
            {
                return;
            }

            using (AssetAuthoring.AssetWriter writer = AssetAuthoring.Edit(config))
            {
                if (created)
                {
                    writer.String("_id", id);
                }

                if (writeBossTuning)
                {
                    writer.Float("_positionHalfLife", 0.09f)
                        .Float("_rotationHalfLife", 0.08f)
                        .Float("_scaleHalfLife", 0.1f)
                        .Float("_anticipationCrouch", 0.2f)
                        .Float("_anticipationLeanDegrees", 18f)
                        .Float("_strikeLunge", 0.55f)
                        .Float("_strikeLeanDegrees", 22f)
                        .Float("_heavyMultiplier", 1.7f)
                        .Float("_moveLeanDegrees", 8f)
                        .Float("_idleBobFrequency", 1f)
                        .Float("_recoilDistance", 0.22f);
                }
            }
        }

        /// <summary>Cool tint for the player's swings, so both sides read as distinct at a glance.</summary>
        private static readonly Color PlayerSwingColor = new Color(0.55f, 0.85f, 1f, 0.6f);

        private static PlayerAttacks CreatePlayerAttacks()
        {
            // A rising combo: each step commits further and rewards more, so continuing the chain is
            // a real decision rather than a free follow-up.
            AttackDefinition light1 = CreateAttack(
                "PlayerLight1", "Slash", DamageType.Light,
                damage: 12f, startup: 0.09f, active: 0.07f, recovery: 0.20f,
                range: 2.1f, arc: 110f, poise: 12f, knockback: 2.5f, hitStop: 0.045f, trauma: 0.12f, overlayColor: PlayerSwingColor);

            AttackDefinition light2 = CreateAttack(
                "PlayerLight2", "Backslash", DamageType.Light,
                damage: 15f, startup: 0.08f, active: 0.07f, recovery: 0.22f,
                range: 2.2f, arc: 120f, poise: 14f, knockback: 2.8f, hitStop: 0.05f, trauma: 0.14f, overlayColor: PlayerSwingColor);

            AttackDefinition light3 = CreateAttack(
                "PlayerLight3", "Rising Cut", DamageType.Light,
                damage: 22f, startup: 0.12f, active: 0.09f, recovery: 0.34f,
                range: 2.4f, arc: 130f, poise: 22f, knockback: 4.5f, hitStop: 0.075f, trauma: 0.22f, overlayColor: PlayerSwingColor);

            // Long recovery is the point: a whiffed heavy must be punishable, which is what gives the
            // boss's parry adaptation something meaningful to counter.
            AttackDefinition heavy = CreateAttack(
                "PlayerHeavy", "Overhead", DamageType.Heavy,
                damage: 42f, startup: 0.34f, active: 0.10f, recovery: 0.52f,
                range: 2.8f, arc: 90f, poise: 45f, knockback: 6f, hitStop: 0.11f, trauma: 0.35f,
                staminaCost: 26f, lungeSpeed: 3.5f, stagger: StaggerStrength.Interrupt, overlayColor: PlayerSwingColor);

            AttackDefinition special = CreateAttack(
                "PlayerSpecial", "Whirlwind", DamageType.Special,
                damage: 30f, startup: 0.20f, active: 0.30f, recovery: 0.40f,
                range: 3.4f, arc: 360f, poise: 30f, knockback: 5f, hitStop: 0.08f, trauma: 0.3f,
                staminaCost: 30f, shape: AttackShape.Sphere, overlayColor: PlayerSwingColor);

            return new PlayerAttacks(new[] { light1, light2, light3 }, heavy, special);
        }

        private static BossAttacks CreateBossAttacks()
        {
            AttackDefinition sweep = CreateAttack(
                "BossSweep", "Wide Sweep", DamageType.BossMelee,
                damage: 12f, startup: 0.45f, active: 0.12f, recovery: 0.45f,
                range: 4f, arc: 160f, poise: 20f, knockback: 5f, hitStop: 0.06f, trauma: 0.25f,
                telegraph: true);

            AttackDefinition slam = CreateAttack(
                "BossSlam", "Ground Slam", DamageType.BossMelee,
                damage: 18f, startup: 0.62f, active: 0.14f, recovery: 0.70f,
                range: 3.6f, arc: 360f, poise: 40f, knockback: 8f, hitStop: 0.10f, trauma: 0.45f,
                shape: AttackShape.Sphere, stagger: StaggerStrength.Interrupt, telegraph: true,
                leavesHazard: true, hazardRadius: 3f, hazardDamagePerTick: 3f, hazardDuration: 3.5f);

            // The gap-closer. Adopted in force against players the boss has learned keep their
            // distance, which is the design's stated answer to a kiting player.
            AttackDefinition charge = CreateAttack(
                "BossCharge", "Charge", DamageType.BossMelee,
                damage: 15f, startup: 0.55f, active: 0.30f, recovery: 0.55f,
                range: 2.6f, arc: 100f, poise: 35f, knockback: 9f, hitStop: 0.09f, trauma: 0.4f,
                lungeSpeed: 16f, stagger: StaggerStrength.Interrupt, telegraph: true);

            AttackDefinition shockwave = CreateAttack(
                "BossShockwave", "Shockwave", DamageType.BossProjectile,
                damage: 14f, startup: 0.70f, active: 0.45f, recovery: 0.60f,
                range: 7f, arc: 360f, poise: 25f, knockback: 6f, hitStop: 0.07f, trauma: 0.35f,
                shape: AttackShape.Sphere, telegraph: true,
                leavesHazard: true, hazardRadius: 3.5f, hazardDamagePerTick: 3f, hazardDuration: 4f);

            // Fast and cheap, with a short wind-up. On its own it is barely a threat; its purpose is
            // to be the quick beat a combo string opens or closes on, so a chain has a rhythm rather
            // than being two heavy swings back to back.
            AttackDefinition jab = CreateAttack(
                "BossJab", "Jab", DamageType.BossMelee,
                damage: 8f, startup: 0.28f, active: 0.08f, recovery: 0.32f,
                range: 3f, arc: 70f, poise: 12f, knockback: 3f, hitStop: 0.05f, trauma: 0.18f,
                telegraph: true);

            // The perilous overhead: a long, deliberately-telegraphed unblockable that punishes the
            // reflex to hold guard. Its slow wind-up is fair — there is time to read the red pulse and
            // roll — and its heavy poise damage makes eating it while blocking a serious mistake.
            AttackDefinition perilous = CreateAttack(
                "BossPerilousOverhead", "Perilous Overhead", DamageType.BossMelee,
                damage: 26f, startup: 0.85f, active: 0.12f, recovery: 0.72f,
                range: 3.4f, arc: 120f, poise: 45f, knockback: 7f, hitStop: 0.12f, trauma: 0.5f,
                stagger: StaggerStrength.Interrupt, telegraph: true, unblockable: true);

            // The set-piece: an arena-wide, unblockable shockwave with a long, honest wind-up, thrown
            // on a phase transition and as the Resolve gambit. It scars the ground it lands on, so the
            // fight's biggest beats compose the mix-up read and the spatial pressure into one moment.
            AttackDefinition phaseShockwave = CreateAttack(
                "BossPhaseShockwave", "Cataclysmic Wave", DamageType.BossProjectile,
                damage: 22f, startup: 0.95f, active: 0.40f, recovery: 0.70f,
                range: 8.5f, arc: 360f, poise: 30f, knockback: 9f, hitStop: 0.12f, trauma: 0.6f,
                shape: AttackShape.Sphere, telegraph: true, unblockable: true,
                leavesHazard: true, hazardRadius: 4f, hazardDamagePerTick: 3f, hazardDuration: 4.5f);

            return new BossAttacks(sweep, slam, charge, shockwave, jab, perilous, phaseShockwave);
        }

        private static AttackDefinition CreateAttack(
            string assetName,
            string displayName,
            DamageType damageType,
            float damage,
            float startup,
            float active,
            float recovery,
            float range,
            float arc,
            float poise,
            float knockback,
            float hitStop,
            float trauma,
            float staminaCost = 0f,
            float lungeSpeed = 0f,
            AttackShape shape = AttackShape.Arc,
            StaggerStrength stagger = StaggerStrength.Flinch,
            bool telegraph = true,
            Color? overlayColor = null,
            bool unblockable = false,
            bool leavesHazard = false,
            float hazardRadius = 2.5f,
            float hazardDamagePerTick = 4f,
            float hazardDuration = 3f)
        {
            var attack = AssetAuthoring.CreateOrLoad<AttackDefinition>(
                $"{AttackFolder}/{assetName}.asset", out bool created);

            if (!ShouldWriteTuning(created))
            {
                return attack;
            }

            using (AssetAuthoring.AssetWriter writer = AssetAuthoring.Edit(attack))
            {
                writer.String("_id", $"attack.{assetName.ToLowerInvariant()}")
                    .String("_displayName", displayName)
                    .Enum("_damageType", damageType)
                    .Float("_damage", damage)
                    .Float("_poiseDamage", poise)
                    .Enum("_stagger", stagger)
                    .Float("_knockbackSpeed", knockback)
                    .Float("_startupSeconds", startup)
                    .Float("_activeSeconds", active)
                    .Float("_recoverySeconds", recovery)
                    .Enum("_shape", shape)
                    .Float("_range", range)
                    .Float("_arcDegrees", arc)
                    .Float("_staminaCost", staminaCost)
                    .Float("_lungeSpeed", lungeSpeed)
                    .Float("_hitStopSeconds", hitStop)
                    .Float("_cameraTrauma", trauma)
                    .Bool("_showTelegraph", telegraph)
                    .Bool("_unblockable", unblockable)
                    .Bool("_leavesHazard", leavesHazard)
                    .Float("_hazardRadius", hazardRadius)
                    .Float("_hazardDamagePerTick", hazardDamagePerTick)
                    .Float("_hazardDurationSeconds", hazardDuration);

                if (overlayColor.HasValue)
                {
                    writer.Color("_telegraphColor", overlayColor.Value);
                }
            }

            return attack;
        }

        private static CounterStrategy[] CreateCounterStrategies()
        {
            // Each of these answers a habit named in the design brief. Thresholds are starting points
            // for tuning, not tested values.
            CounterStrategy delayAttacks = CreateStrategy(
                "CounterPostDodgeAggression",
                BehaviorFeature.PostDodgeAggression, FeatureComparison.AtOrAbove, threshold: 0.6f,
                phase: 1, weight: 1.4f,
                tell: "It waits for your opening now.",
                (BossTuningParameter.AttackDelay, 0.35f),
                (BossTuningParameter.FeintChance, 0.3f));

            CounterStrategy closeDistance = CreateStrategy(
                "CounterKiting",
                BehaviorFeature.PreferredDistance, FeatureComparison.AtOrAbove, threshold: 0.65f,
                phase: 1, weight: 1.2f,
                tell: "It refuses to let you breathe.",
                (BossTuningParameter.GapCloserWeight, 0.8f),
                (BossTuningParameter.PreferredRange, 1.8f),
                (BossTuningParameter.Aggression, 0.75f));

            CounterStrategy parryHeavy = CreateStrategy(
                "CounterHeavySpam",
                BehaviorFeature.HeavyAttackRatio, FeatureComparison.AtOrAbove, threshold: 0.55f,
                phase: 2, weight: 1.5f,
                tell: "It has started reading your heavy swing.",
                (BossTuningParameter.ParryChance, 0.45f));

            CounterStrategy predictDodge = CreateStrategy(
                "CounterPredictableDodge",
                BehaviorFeature.DashDirectionBias, FeatureComparison.AtOrAbove, threshold: 0.55f,
                phase: 2, weight: 1.3f,
                tell: "It knows which way you will roll.",
                (BossTuningParameter.DodgePredictionStrength, 0.7f));

            CounterStrategy areaDenial = CreateStrategy(
                "CounterEvasive",
                BehaviorFeature.EvasionSuccess, FeatureComparison.AtOrAbove, threshold: 0.7f,
                phase: 3, weight: 1.6f,
                tell: "It stops aiming and starts covering ground.",
                (BossTuningParameter.AreaDenialWeight, 0.85f),
                (BossTuningParameter.ComboExtensionChance, 0.5f));

            CounterStrategy punishPassivity = CreateStrategy(
                "CounterPassivity",
                BehaviorFeature.AttackFrequency, FeatureComparison.AtOrBelow, threshold: 0.25f,
                phase: 1, weight: 1f,
                tell: "It presses the advantage.",
                (BossTuningParameter.Aggression, 0.85f),
                (BossTuningParameter.ComboExtensionChance, 0.6f));

            // A player who heals often is telling the boss when they are briefly helpless. The answer
            // is not a new move but the willingness to spend one closing the gap and pressing, so the
            // heal has to be earned rather than taken for free.
            CounterStrategy punishHealing = CreateStrategy(
                "CounterHealing",
                BehaviorFeature.HealFrequency, FeatureComparison.AtOrAbove, threshold: 0.5f,
                phase: 1, weight: 1.1f,
                tell: "It lunges the instant you falter.",
                (BossTuningParameter.Aggression, 0.8f),
                (BossTuningParameter.GapCloserWeight, 0.6f));

            // The dangerous player: one whose perfect dodges land on the exact frame, fight after
            // fight. Feinting is the only honest answer — a telegraph the boss shows and cancels turns
            // that flawless timing against itself, because a dodge spent on a blow that never comes
            // leaves nothing to answer the one that does.
            CounterStrategy baitRhythmicDodge = CreateStrategy(
                "CounterRhythmicDodge",
                BehaviorFeature.DodgeTimingConsistency, FeatureComparison.AtOrAbove, threshold: 0.5f,
                phase: 2, weight: 1.35f,
                tell: "It fakes the blow you time so well.",
                (BossTuningParameter.FeintChance, 0.6f),
                (BossTuningParameter.AttackDelay, 0.2f));

            // A player who whiffs often is panicking, and every wasted swing is a recovery the boss can
            // step into. The answer is pure commitment — press, close the gap, and string the punish —
            // which also raises the boss's own commitment, so a swing that then misses because the
            // player has stopped flailing overbalances it in turn. That reversal is the whole point.
            CounterStrategy punishWhiffing = CreateStrategy(
                "CounterWhiffPunish",
                BehaviorFeature.WhiffRatio, FeatureComparison.AtOrAbove, threshold: 0.5f,
                phase: 1, weight: 1.2f,
                tell: "It punishes your wasted swings.",
                (BossTuningParameter.Aggression, 0.8f),
                (BossTuningParameter.GapCloserWeight, 0.7f),
                (BossTuningParameter.ComboExtensionChance, 0.5f));

            // A player who answers everything with the guard has told the boss something it can act
            // on honestly: the guard only works on attacks that respect it. Reaching more often for
            // the perilous attacks it already owns turns that habit into a liability, and because a
            // perilous wind-up is the most heavily telegraphed thing in the fight, the answer stays
            // fair — the player is being asked to dodge, not being denied a defence.
            CounterStrategy punishTurtling = CreateStrategy(
                "CounterGuardReliance",
                BehaviorFeature.GuardReliance, FeatureComparison.AtOrAbove, threshold: 0.55f,
                phase: 1, weight: 1.3f,
                tell: "It stops respecting your guard.",
                (BossTuningParameter.UnblockableWeight, 0.9f));

            // The parry master is a different problem from the turtle. Their timing is the thing to
            // attack, so the boss shows blows it never throws and mixes in attacks the guard cannot
            // answer at all — punishing the precision itself rather than the reliance.
            CounterStrategy punishDeflectMastery = CreateStrategy(
                "CounterDeflectMastery",
                BehaviorFeature.DeflectSkill, FeatureComparison.AtOrAbove, threshold: 0.6f,
                phase: 2, weight: 1.45f,
                tell: "It will not give you a clean beat.",
                (BossTuningParameter.FeintChance, 0.55f),
                (BossTuningParameter.UnblockableWeight, 0.6f));

            return new[]
            {
                delayAttacks, closeDistance, parryHeavy, predictDodge, areaDenial, punishPassivity,
                punishHealing, baitRhythmicDodge, punishWhiffing, punishTurtling, punishDeflectMastery
            };
        }

        private static CounterStrategy CreateStrategy(
            string assetName,
            BehaviorFeature feature,
            FeatureComparison comparison,
            float threshold,
            int phase,
            float weight,
            string tell,
            params (BossTuningParameter Parameter, float Target)[] adjustments)
        {
            var strategy = AssetAuthoring.CreateOrLoad<CounterStrategy>(
                $"{StrategyFolder}/{assetName}.asset", out bool created);

            if (!ShouldWriteTuning(created))
            {
                return strategy;
            }

            using (AssetAuthoring.AssetWriter writer = AssetAuthoring.Edit(strategy))
            {
                writer.String("_id", $"strategy.{assetName.ToLowerInvariant()}")
                    .Enum("_triggerFeature", feature)
                    .Enum("_comparison", comparison)
                    .Float("_threshold", threshold)
                    .Int("_minimumPhase", phase)
                    .Float("_selectionWeight", weight)
                    .String("_tellMessage", tell);

                SerializedProperty array = writer.Array("_adjustments", adjustments.Length);
                for (int i = 0; i < adjustments.Length; i++)
                {
                    SerializedProperty element = array.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("_parameter").enumValueIndex = (int)adjustments[i].Parameter;
                    element.FindPropertyRelative("_targetValue").floatValue = adjustments[i].Target;
                }
            }

            return strategy;
        }

        /// <summary>
        /// Creates or repairs the player configuration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// References are rewritten on every run; tuning values are written only on creation. The
        /// distinction matters and was learned the hard way. References are derived data — they must
        /// heal themselves whenever an asset is deleted, moved or regenerated, because a dangling
        /// reference here means the player's attacks resolve to null and the attack button silently
        /// does nothing. Tuning values are authored by hand and must survive a re-run.
        /// </para>
        /// <para>
        /// The previous behaviour skipped existing configs entirely, so regenerating the attack
        /// assets left both combatants permanently unable to attack, with no error anywhere.
        /// </para>
        /// </remarks>
        private static void CreatePlayerConfig(PlayerAttacks attacks, WeaponDefinition[] weapons)
        {
            var config = AssetAuthoring.CreateOrLoad<PlayerConfig>(
                $"{ConfigFolder}/DefaultPlayerConfig.asset", out bool created);

            // The execution: a devastating answer to a broken guard, thrown by the riposte state. Its
            // long active window and deep hit-stop read as a finishing strike, and the encounter
            // director turns a lethal one into a cinematic beat.
            AttackDefinition execution = CreateAttack(
                "PlayerExecution", "Execution", DamageType.Heavy,
                damage: 72f, startup: 0.10f, active: 0.34f, recovery: 0.46f,
                range: 2.9f, arc: 120f, poise: 60f, knockback: 8f, hitStop: 0.18f, trauma: 0.6f,
                stagger: StaggerStrength.Break, overlayColor: PlayerSwingColor);

            using (AssetAuthoring.AssetWriter writer = AssetAuthoring.Edit(config))
            {
                if (created)
                {
                    writer.String("_id", "player.default");
                }

                if (ShouldWriteTuning(created))
                {
                    // Blocking has to lose ground faster than deflecting gains it, or turtling
                    // becomes the correct answer and the deflect window stops mattering.
                    writer.Float("_blockPostureCost", 22f)
                        .Float("_deflectPostureDamage", 28f);
                }

                writer.ReferenceArray("_weapons", weapons)
                    .ReferenceArray("_lightComboChain", attacks.LightChain)
                    .Reference("_heavyAttack", attacks.Heavy)
                    .Reference("_specialAttack", attacks.Special)
                    .Reference("_executionAttack", execution);
            }
        }

        /// <summary>
        /// Creates the three weapon archetypes.
        /// </summary>
        /// <remarks>
        /// They share the same underlying attack assets for now, differing in defence, handling and
        /// tuning. That keeps the count of assets manageable while still making the choice matter:
        /// the greatsword cannot parry at all, the energy blade guards cheaply but for less, and the
        /// blade sits between them with the tightest, most rewarding deflect.
        /// </remarks>
        private static WeaponDefinition[] CreateWeapons(PlayerAttacks bladeAttacks)
        {
            // Each weapon now swings its own moveset, not a shared one. Handling (speed, stamina,
            // defence) already differed; giving them distinct attacks is what makes the choice change
            // how the fight is played rather than only how it is defended, and it gives the boss's
            // weapon-preference learning something real to read.
            PlayerAttacks greatswordAttacks = CreateGreatswordAttacks();
            PlayerAttacks energyAttacks = CreateEnergyBladeAttacks();

            WeaponDefinition blade = CreateWeapon(
                "WeaponBlade", "Blade", new Color(0.55f, 0.85f, 1f, 0.6f),
                bladeAttacks, CreateBladeEmpoweredSpecial(), DefenceStyle.Deflect,
                deflectWindow: 0.2f, deflectPosture: 28f,
                moveMultiplier: 1f, staminaMultiplier: 1f,
                swingCueId: "swing.blade");

            // No parry at all. It wins the exchanges it commits to and loses badly when the
            // commitment was wrong, which is a different kind of decision rather than a worse one.
            WeaponDefinition greatsword = CreateWeapon(
                "WeaponGreatsword", "Greatsword", new Color(1f, 0.75f, 0.35f, 0.6f),
                greatswordAttacks, CreateGreatswordEmpoweredSpecial(), DefenceStyle.HyperArmour,
                deflectWindow: 0.2f, deflectPosture: 0f,
                moveMultiplier: 0.82f, staminaMultiplier: 1.35f,
                swingCueId: "swing.greatsword");

            WeaponDefinition energyBlade = CreateWeapon(
                "WeaponEnergyBlade", "Energy Blade", new Color(0.7f, 1f, 0.8f, 0.6f),
                energyAttacks, CreateEnergyEmpoweredSpecial(), DefenceStyle.SustainedGuard,
                deflectWindow: 0.28f, deflectPosture: 16f,
                moveMultiplier: 1.12f, staminaMultiplier: 0.85f,
                swingCueId: "swing.energy");

            return new[] { blade, greatsword, energyBlade };
        }

        /// <summary>Violet tint marking a focus-empowered special, matching the focus bar.</summary>
        private static readonly Color EmpoweredSwingColor = new Color(1f, 0.55f, 1f, 0.7f);

        /// <summary>The blade's empowered special: a wider, harder-hitting whirlwind, focus-free.</summary>
        /// <remarks>
        /// Empowered specials cost no stamina — the focus meter is their price — and hit markedly
        /// harder with more poise, so a full meter spent well is a real swing in the exchange and not
        /// just a recolour. Their violet flash matches the focus bar, so the payoff reads on screen.
        /// </remarks>
        private static AttackDefinition CreateBladeEmpoweredSpecial() => CreateAttack(
            "BladeSpecialEmpowered", "Tempest", DamageType.Special,
            damage: 60f, startup: 0.16f, active: 0.34f, recovery: 0.42f,
            range: 3.3f, arc: 360f, poise: 52f, knockback: 7f, hitStop: 0.12f, trauma: 0.5f,
            staminaCost: 0f, shape: AttackShape.Sphere, overlayColor: EmpoweredSwingColor);

        /// <summary>The greatsword's empowered special: a devastating, arena-clearing cataclysm.</summary>
        private static AttackDefinition CreateGreatswordEmpoweredSpecial() => CreateAttack(
            "GreatswordSpecialEmpowered", "Cataclysm", DamageType.Special,
            damage: 78f, startup: 0.30f, active: 0.28f, recovery: 0.5f,
            range: 4.3f, arc: 360f, poise: 80f, knockback: 11f, hitStop: 0.16f, trauma: 0.6f,
            staminaCost: 0f, shape: AttackShape.Sphere, stagger: StaggerStrength.Interrupt,
            overlayColor: EmpoweredSwingColor);

        /// <summary>The energy blade's empowered special: a longer, faster storm of blades.</summary>
        private static AttackDefinition CreateEnergyEmpoweredSpecial() => CreateAttack(
            "EnergySpecialEmpowered", "Storm of Blades", DamageType.Special,
            damage: 48f, startup: 0.14f, active: 0.40f, recovery: 0.34f,
            range: 3.1f, arc: 360f, poise: 46f, knockback: 5f, hitStop: 0.09f, trauma: 0.42f,
            staminaCost: 0f, shape: AttackShape.Sphere, overlayColor: EmpoweredSwingColor);

        /// <summary>Amber tint for the greatsword's swings.</summary>
        private static readonly Color GreatswordSwingColor = new Color(1f, 0.7f, 0.35f, 0.6f);

        /// <summary>Cyan tint for the energy blade's swings.</summary>
        private static readonly Color EnergySwingColor = new Color(0.6f, 1f, 0.85f, 0.6f);

        /// <summary>
        /// The greatsword's moveset: a slow, heavy two-hit chain that trades speed for reach, poise
        /// damage and a genuinely frightening committed swing.
        /// </summary>
        private static PlayerAttacks CreateGreatswordAttacks()
        {
            AttackDefinition cleave = CreateAttack(
                "GreatswordLight1", "Cleave", DamageType.Light,
                damage: 20f, startup: 0.20f, active: 0.10f, recovery: 0.34f,
                range: 2.7f, arc: 140f, poise: 30f, knockback: 5f, hitStop: 0.08f, trauma: 0.24f,
                overlayColor: GreatswordSwingColor);

            AttackDefinition rend = CreateAttack(
                "GreatswordLight2", "Rend", DamageType.Light,
                damage: 30f, startup: 0.26f, active: 0.10f, recovery: 0.44f,
                range: 2.9f, arc: 150f, poise: 42f, knockback: 7f, hitStop: 0.10f, trauma: 0.32f,
                stagger: StaggerStrength.Interrupt, overlayColor: GreatswordSwingColor);

            AttackDefinition earthbreaker = CreateAttack(
                "GreatswordHeavy", "Earthbreaker", DamageType.Heavy,
                damage: 56f, startup: 0.44f, active: 0.12f, recovery: 0.62f,
                range: 3.0f, arc: 110f, poise: 60f, knockback: 9f, hitStop: 0.14f, trauma: 0.5f,
                staminaCost: 34f, lungeSpeed: 3f, stagger: StaggerStrength.Interrupt,
                overlayColor: GreatswordSwingColor);

            AttackDefinition seismic = CreateAttack(
                "GreatswordSpecial", "Seismic Slam", DamageType.Special,
                damage: 40f, startup: 0.30f, active: 0.26f, recovery: 0.5f,
                range: 3.8f, arc: 360f, poise: 45f, knockback: 8f, hitStop: 0.10f, trauma: 0.4f,
                staminaCost: 38f, shape: AttackShape.Sphere, overlayColor: GreatswordSwingColor);

            return new PlayerAttacks(new[] { cleave, rend }, earthbreaker, seismic);
        }

        /// <summary>
        /// The energy blade's moveset: a rapid four-hit flurry, short-reach and low-damage per hit,
        /// that rewards staying on top of the boss and rarely staggers anything.
        /// </summary>
        private static PlayerAttacks CreateEnergyBladeAttacks()
        {
            AttackDefinition slice = CreateAttack(
                "EnergyLight1", "Slice", DamageType.Light,
                damage: 8f, startup: 0.06f, active: 0.05f, recovery: 0.14f,
                range: 1.9f, arc: 90f, poise: 8f, knockback: 1.5f, hitStop: 0.03f, trauma: 0.08f,
                overlayColor: EnergySwingColor);

            AttackDefinition sliceBack = CreateAttack(
                "EnergyLight2", "Backslice", DamageType.Light,
                damage: 8f, startup: 0.05f, active: 0.05f, recovery: 0.14f,
                range: 1.9f, arc: 95f, poise: 9f, knockback: 1.8f, hitStop: 0.035f, trauma: 0.09f,
                overlayColor: EnergySwingColor);

            AttackDefinition cross = CreateAttack(
                "EnergyLight3", "Cross", DamageType.Light,
                damage: 10f, startup: 0.05f, active: 0.05f, recovery: 0.16f,
                range: 2.0f, arc: 100f, poise: 12f, knockback: 2.2f, hitStop: 0.04f, trauma: 0.11f,
                overlayColor: EnergySwingColor);

            AttackDefinition flourish = CreateAttack(
                "EnergyLight4", "Flourish", DamageType.Light,
                damage: 14f, startup: 0.08f, active: 0.06f, recovery: 0.26f,
                range: 2.1f, arc: 120f, poise: 18f, knockback: 3.5f, hitStop: 0.06f, trauma: 0.18f,
                overlayColor: EnergySwingColor);

            AttackDefinition thrust = CreateAttack(
                "EnergyHeavy", "Lunge Thrust", DamageType.Heavy,
                damage: 30f, startup: 0.22f, active: 0.08f, recovery: 0.4f,
                range: 3.4f, arc: 40f, poise: 30f, knockback: 5f, hitStop: 0.10f, trauma: 0.3f,
                staminaCost: 22f, lungeSpeed: 8f, stagger: StaggerStrength.Interrupt,
                overlayColor: EnergySwingColor);

            AttackDefinition dance = CreateAttack(
                "EnergySpecial", "Blade Dance", DamageType.Special,
                damage: 26f, startup: 0.15f, active: 0.30f, recovery: 0.36f,
                range: 2.8f, arc: 360f, poise: 28f, knockback: 4f, hitStop: 0.07f, trauma: 0.28f,
                staminaCost: 28f, shape: AttackShape.Sphere, overlayColor: EnergySwingColor);

            return new PlayerAttacks(new[] { slice, sliceBack, cross, flourish }, thrust, dance);
        }

        private static WeaponDefinition CreateWeapon(
            string assetName,
            string displayName,
            Color signature,
            PlayerAttacks attacks,
            AttackDefinition empoweredSpecial,
            DefenceStyle defence,
            float deflectWindow,
            float deflectPosture,
            float moveMultiplier,
            float staminaMultiplier,
            string swingCueId)
        {
            var weapon = AssetAuthoring.CreateOrLoad<WeaponDefinition>(
                $"{WeaponFolder}/{assetName}.asset", out bool created);

            using (AssetAuthoring.AssetWriter writer = AssetAuthoring.Edit(weapon))
            {
                // References always repaired; tuning only written on creation or a retune.
                writer.ReferenceArray("_lightComboChain", attacks.LightChain)
                    .Reference("_heavyAttack", attacks.Heavy)
                    .Reference("_specialAttack", attacks.Special)
                    .Reference("_empoweredSpecialAttack", empoweredSpecial)
                    .Reference("_riposteAttack", attacks.Heavy);

                if (ShouldWriteTuning(created))
                {
                    writer.String("_id", $"weapon.{assetName.ToLowerInvariant()}")
                        .String("_displayName", displayName)
                        .Color("_signatureColor", signature)
                        .String("_swingCueId", swingCueId)
                        .Enum("_defence", defence)
                        .Float("_deflectWindowSeconds", deflectWindow)
                        .Float("_deflectPostureDamage", deflectPosture)
                        .Float("_moveSpeedMultiplier", moveMultiplier)
                        .Float("_staminaCostMultiplier", staminaMultiplier);
                }
            }

            return weapon;
        }

        private static void CreateBossConfig(
            ReactionProfile reactionProfile,
            BossAttacks attacks,
            CounterStrategy[] strategies)
        {
            var config = AssetAuthoring.CreateOrLoad<BossConfig>(
                $"{ConfigFolder}/DefaultBossConfig.asset", out bool created);

            using (AssetAuthoring.AssetWriter writer = AssetAuthoring.Edit(config))
            {
                // Always repaired: a dangling attack reference leaves the boss with an empty
                // catalogue, which makes it circle the arena forever without ever attacking.
                writer.Reference("_reactionProfile", reactionProfile)
                    .Reference("_phaseShockwave", attacks.PhaseShockwave)
                    .ReferenceArray("_counterStrategies", strategies);

                if (created)
                {
                    writer.String("_id", "boss.default");
                }

                if (ShouldWriteTuning(created))
                {
                    // Written explicitly rather than left to the C# field defaults, which only apply
                    // at the moment an asset is first created and are unreachable afterwards.
                    // 650 against a player dealing roughly 25 a second is about half a minute of
                    // sustained pressure, which is where a single-encounter slice wants to sit. The
                    // 1000 it started at read as a sponge.
                    writer.Float("_maxHealth", 650f)
                        .Float("_maxPoise", 100f)
                        .Float("_poiseRegenPerSecond", 12f)
                        .Float("_resolveThreshold", 100f)
                        .Float("_resolveBuildPerSecond", 3.2f)
                        .Float("_resolveBuildPerWhiff", 14f);
                }

                SerializedProperty phases = writer.Array("_phases", 4);

                WritePhase(
                    phases.GetArrayElementAtIndex(0), ShouldWriteTuning(created), "Measured", threshold: 1f,
                    moveMultiplier: 1f, cooldown: 1.15f, adaptationRate: 1f,
                    signature: attacks.Slam,
                    attacks.Sweep, attacks.Slam);

                WritePhase(
                    phases.GetArrayElementAtIndex(1), ShouldWriteTuning(created), "Pressing", threshold: 0.66f,
                    moveMultiplier: 1.15f, cooldown: 0.85f, adaptationRate: 1.4f,
                    signature: attacks.Charge,
                    attacks.Sweep, attacks.Slam, attacks.Charge, attacks.Jab, attacks.Perilous);

                WritePhase(
                    phases.GetArrayElementAtIndex(2), ShouldWriteTuning(created), "Relentless", threshold: 0.33f,
                    moveMultiplier: 1.3f, cooldown: 0.6f, adaptationRate: 2f,
                    signature: attacks.Shockwave,
                    attacks.Sweep, attacks.Slam, attacks.Charge, attacks.Shockwave, attacks.Jab, attacks.Perilous);

                // Last Stand: the desperation phase. It flows through the same escalation beat as the
                // others — roar, shockwave, invulnerable rear-up — but arrives with the fullest
                // moveset, the shortest recovery and the fastest adaptation the boss can reach, so the
                // final stretch of the fight is its most dangerous and its most legible at once.
                WritePhase(
                    phases.GetArrayElementAtIndex(3), ShouldWriteTuning(created), "Last Stand", threshold: 0.15f,
                    moveMultiplier: 1.4f, cooldown: 0.45f, adaptationRate: 2.5f,
                    signature: attacks.Shockwave,
                    attacks.Sweep, attacks.Slam, attacks.Charge, attacks.Shockwave, attacks.Jab, attacks.Perilous);
            }
        }

        private static void WritePhase(
            SerializedProperty phase,
            bool writeTuningValues,
            string name,
            float threshold,
            float moveMultiplier,
            float cooldown,
            float adaptationRate,
            AttackDefinition signature,
            params AttackDefinition[] attacks)
        {
            // Tuning values are only written on creation so a designer's adjustments survive a
            // re-run. The attack list and signature below are always rewritten, because they are
            // derived data — a dangling attack reference would leave the phase silent.
            if (writeTuningValues)
            {
                phase.FindPropertyRelative("_name").stringValue = name;
                phase.FindPropertyRelative("_healthThreshold").floatValue = threshold;
                phase.FindPropertyRelative("_moveSpeedMultiplier").floatValue = moveMultiplier;
                phase.FindPropertyRelative("_attackCooldownSeconds").floatValue = cooldown;
                phase.FindPropertyRelative("_adaptationRateMultiplier").floatValue = adaptationRate;
            }

            phase.FindPropertyRelative("_signatureAttack").objectReferenceValue = signature;

            SerializedProperty attackArray = phase.FindPropertyRelative("_attacks");
            attackArray.arraySize = attacks.Length;

            for (int i = 0; i < attacks.Length; i++)
            {
                attackArray.GetArrayElementAtIndex(i).objectReferenceValue = attacks[i];
            }
        }

        private static void CreateArenaConfig()
        {
            var config = AssetAuthoring.CreateOrLoad<ArenaConfig>(
                $"{ConfigFolder}/DefaultArenaConfig.asset", out bool created);

            if (created)
            {
                using (AssetAuthoring.AssetWriter writer = AssetAuthoring.Edit(config))
                {
                    writer.String("_id", "arena.default");
                }
            }
        }

        private static void CreateEventChannels()
        {
            CreateChannel<VoidEventChannel>("OnPlayerDied", "Raised once when the player is defeated.");
            CreateChannel<VoidEventChannel>(
                "OnPerfectDodge",
                "Raised when a dodge negates an attack within the perfect-dodge window. Drives the " +
                "flourish that tells the player their timing was read correctly.");
            CreateChannel<VoidEventChannel>("OnBossDefeated", "Raised once when the boss is defeated.");
            CreateChannel<IntEventChannel>("OnBossPhaseChanged", "Carries the new zero-based phase index.");
            CreateChannel<FloatEventChannel>("OnBossHealthChanged", "Carries normalised boss health for the health bar.");
            CreateChannel<FloatEventChannel>("OnPlayerHealthChanged", "Carries normalised player health.");
            CreateChannel<FloatEventChannel>("OnPlayerStaminaChanged", "Carries normalised player stamina.");
            CreateChannel<FloatEventChannel>(
                "OnPlayerPostureChanged",
                "Carries normalised player posture. Draining it means the guard is about to break.");
            CreateChannel<FloatEventChannel>(
                "OnPlayerFocusChanged",
                "Carries normalised player focus. Filling it empowers the next special.");
            CreateChannel<FloatEventChannel>(
                "OnBossPostureChanged",
                "Carries normalised boss posture. Emptying it opens the riposte.");
            CreateChannel<StringEventChannel>(
                "OnWeaponDrawn",
                "Carries the name of a newly drawn weapon.");
            CreateChannel<StringEventChannel>(
                "OnWeaponSwingCue",
                "Carries the audio cue the drawn weapon swings with, so three weapons sharing one " +
                "moveset can still be told apart by ear. Separate from the display name because " +
                "matching on a name shown to the player would break the moment it were reworded.");
            CreateChannel<VoidEventChannel>(
                "OnDeflect",
                "Raised on a clean deflect. Drives the metallic ring, spark and freeze that make the " +
                "timing worth learning.");
            CreateChannel<StringEventChannel>(
                "OnAdaptationAdopted",
                "Carries the tell message for a newly adopted counter-strategy. Listened to by the " +
                "heads-up display so the player can perceive that the boss has changed.");
            CreateChannel<BoolEventChannel>("OnPauseStateChanged", "Carries whether the game is now paused.");
            CreateChannel<VoidEventChannel>(
                "OnBossOverbalanced",
                "Raised when a committed boss swing whiffs hard enough to overbalance it. Drives the " +
                "stumble, dust and stagger sound that show the opening the player's read just bought.");
        }

        private static void CreateChannel<TChannel>(string assetName, string description)
            where TChannel : EventChannelBase
        {
            var channel = AssetAuthoring.CreateOrLoad<TChannel>(
                $"{EventFolder}/{assetName}.asset", out bool created);

            if (created)
            {
                using (AssetAuthoring.AssetWriter writer = AssetAuthoring.Edit(channel))
                {
                    writer.String("_description", description);
                }
            }
        }
    }
}
