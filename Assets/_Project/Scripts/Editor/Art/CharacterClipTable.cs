using System.Collections.Generic;
using AdaptiveBossArena.Combat.Feel;

namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Which model a fighter is, and which clip plays for each thing it does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One table per body. The Animator controller, the rig and its scale are all built from it, so giving a
    /// fighter different art is a change to one table rather than to the builder.
    /// </para>
    /// <para>
    /// Clips were chosen by viewing a contact sheet of every clip's poses, not by file name - the reasons are
    /// beside each entry. Humanoid clips retarget across skeletons, which is how the knight rolls with the
    /// CC0 library's roll: the Mixamo sword and shield pack has none.
    /// </para>
    /// </remarks>
    public sealed class CharacterClipTable
    {
        /// <summary>The states whose clip is scrubbed by the attack's own timeline rather than played by the clock.</summary>
        public static readonly IReadOnlyCollection<string> AttackStateNames = new[]
        {
            "Light1", "Light2", "Light3", "Heavy", "Special", "Overhead", "Hook", "Dash", "Cast"
        };

        private const string Mixamo = LicensedArtPostprocessor.MixamoFolder;
        private const string QuaterniusLibrary1 = "Assets/_Project/Art/ThirdParty/Quaternius/UniversalAnimationLibrary/UAL1_Standard.fbx";
        private const string QuaterniusLibrary2 = "Assets/_Project/Art/ThirdParty/Quaternius/UniversalAnimationLibrary2/UAL2_Standard.fbx";

        /// <summary>Creates a table.</summary>
        /// <param name="name">Name of the generated controller.</param>
        /// <param name="rigModel">Asset path of the model the fighter is.</param>
        /// <param name="rigScale">Uniform scale that brings the model to the fighter's intended height.</param>
        /// <param name="clipSources">Asset paths and folders the clips are looked up in, first match wins.</param>
        /// <param name="locomotion">Speed thresholds, from zero to one, and the clip at each.</param>
        /// <param name="states">Every other state and its clip.</param>
        /// <param name="directional">
        /// Moving clips for the directional blend - forward, back and both sides - placed by their measured travel.
        /// Empty keeps the single-speed blend of <paramref name="locomotion"/>.
        /// </param>
        public CharacterClipTable(
            string name,
            string rigModel,
            float rigScale,
            string[] clipSources,
            (float Speed, string Clip)[] locomotion,
            IReadOnlyDictionary<string, string> states,
            string[] directional = null)
        {
            Name = name;
            RigModel = rigModel;
            RigScale = rigScale;
            ClipSources = clipSources;
            Locomotion = locomotion;
            States = states;
            Directional = directional ?? new string[0];
        }

        /// <summary>The knight: Mixamo's Paladin with the sword and shield set.</summary>
        public static CharacterClipTable Knight => new CharacterClipTable(
            "KnightController",
            LicensedArtPostprocessor.KnightCharacter,
            1f,
            new[] { Mixamo + "Animations/Knight", Mixamo + "Animations/Shared", QuaterniusLibrary1 },
            new[]
            {
                // idle (4): the steady guard. idle (2) and (3) are eight-second fidgets that wander off the stance.
                (0f, "sword and shield idle (4)"),
                (0.3f, "sword and shield walk"),
                // run, not run (2): (2) is a crouched sprint that reads as fleeing.
                (1f, "sword and shield run")
            },
            new Dictionary<string, string>
            {
                [CharacterAnimatorParameters.RollState] = "Roll",
                [CharacterAnimatorParameters.GuardState] = "sword and shield block idle",
                [CharacterAnimatorParameters.StaggerState] = "sword and shield impact (2)",
                [CharacterAnimatorParameters.DeathState] = "sword and shield death",
                // impact: a short recoil of the shoulders behind the shield, not a stumble.
                [CharacterAnimatorParameters.HitState] = "sword and shield impact",
                // Thrown backwards off the feet; the get-up starts from lying on the back to match.
                [CharacterAnimatorParameters.AirborneState] = "Flying Back Death",
                [CharacterAnimatorParameters.KnockedDownState] = "Getting Up",

                // A three-hit string: a forward cut, a return cut, a low sweep to finish.
                ["Light1"] = "sword and shield slash",
                ["Light2"] = "sword and shield slash (3)",
                ["Light3"] = "sword and shield slash (5)",
                // The widest committed swing in the set, with the body turned through it.
                ["Heavy"] = "sword and shield slash (4)",
                ["Special"] = "sword and shield attack (2)",
                ["Overhead"] = "sword and shield attack",
                ["Hook"] = "sword and shield kick",
                ["Dash"] = "sword and shield attack (3)",
                ["Cast"] = "sword and shield casting (2)"
            },
            // walk (2) steps back; strafe and strafe (4) step right, (2) and (3) left - measured, not assumed.
            new[]
            {
                "sword and shield walk", "sword and shield run", "sword and shield walk (2)",
                "sword and shield strafe", "sword and shield strafe (2)", "sword and shield strafe (3)", "sword and shield strafe (4)"
            });

        /// <summary>The boss: Mixamo's Warrok, a hunched brute, with the great sword set.</summary>
        public static CharacterClipTable Brute => new CharacterClipTable(
            "BruteController",
            LicensedArtPostprocessor.BossCharacter,
            1.05f,
            new[] { Mixamo + "Animations/Boss", Mixamo + "Animations/Shared", QuaterniusLibrary1 },
            new[]
            {
                (0f, "great sword idle"),
                (0.3f, "great sword walk"),
                // run (2), not run: "great sword run" steps backwards, and the brute ran forwards on it.
                (1f, "great sword run (2)")
            },
            new Dictionary<string, string>
            {
                [CharacterAnimatorParameters.RollState] = "Roll",
                [CharacterAnimatorParameters.GuardState] = "great sword blocking (2)",
                // impact (3): the longest recoil, staggering back a step - a poise break, not a flinch.
                [CharacterAnimatorParameters.StaggerState] = "great sword impact (3)",
                [CharacterAnimatorParameters.DeathState] = "two handed sword death",
                [CharacterAnimatorParameters.HitState] = "great sword impact",
                [CharacterAnimatorParameters.AirborneState] = "great sword impact (2)",
                [CharacterAnimatorParameters.KnockedDownState] = "great sword impact (2)",

                ["Light1"] = "great sword slash (5)",
                // The Wide Sweep: slash (4) carries the blade across the body at waist height.
                ["Light2"] = "great sword slash (4)",
                ["Light3"] = "great sword high spin attack",
                // The Perilous Overhead: slash (3) raises the blade high before bringing it down.
                ["Heavy"] = "great sword slash (3)",
                ["Special"] = "great sword attack",
                // The Ground Slam: a leap that brings the whole body down with the blade.
                ["Overhead"] = "great sword jump attack",
                // The Jab: the quickest cut in the set.
                ["Hook"] = "great sword slash",
                // The Charge: a driving slide into a cut.
                ["Dash"] = "great sword slide attack",
                // Shockwaves: a gathering cast that releases outward.
                ["Cast"] = "spell cast"
            },
            // walk (2) steps back; strafe (2) and (4) step right, strafe and (3) left.
            new[]
            {
                "great sword walk", "great sword run (2)", "great sword walk (2)",
                "great sword strafe", "great sword strafe (2)", "great sword strafe (3)", "great sword strafe (4)"
            });

        /// <summary>Name of the generated controller.</summary>
        public string Name { get; }

        /// <summary>Asset path of the model the fighter is.</summary>
        public string RigModel { get; }

        /// <summary>Uniform scale that brings the model to the fighter's intended height.</summary>
        public float RigScale { get; }

        /// <summary>Asset paths and folders the clips are looked up in, first match wins.</summary>
        public string[] ClipSources { get; }

        /// <summary>Speed thresholds and their clips, blended by the Speed parameter.</summary>
        public (float Speed, string Clip)[] Locomotion { get; }

        /// <summary>Every other state and its clip.</summary>
        public IReadOnlyDictionary<string, string> States { get; }

        /// <summary>Moving clips for the directional blend, or empty for the single-speed blend.</summary>
        public string[] Directional { get; }

        /// <summary>The CC0 mannequin both fighters use when the licensed art is absent.</summary>
        /// <param name="name">Name of the generated controller.</param>
        /// <param name="rigScale">Scale for this fighter.</param>
        /// <returns>The table.</returns>
        public static CharacterClipTable Fallback(string name, float rigScale) => new CharacterClipTable(
            name,
            QuaterniusLibrary1,
            rigScale,
            new[] { QuaterniusLibrary1, QuaterniusLibrary2 },
            new[] { (0f, "Sword_Idle"), (0.3f, "Walk_Loop"), (0.7f, "Jog_Fwd_Loop"), (1f, "Sprint_Loop") },
            new Dictionary<string, string>
            {
                [CharacterAnimatorParameters.RollState] = "Roll",
                [CharacterAnimatorParameters.GuardState] = "Sword_Block",
                [CharacterAnimatorParameters.StaggerState] = "Idle_Shield_Break",
                [CharacterAnimatorParameters.DeathState] = "Death01",
                [CharacterAnimatorParameters.HitState] = "Hit_Chest",
                [CharacterAnimatorParameters.AirborneState] = "Hit_Knockback",
                [CharacterAnimatorParameters.KnockedDownState] = "LayToIdle",
                ["Light1"] = "Sword_Regular_A",
                ["Light2"] = "Sword_Regular_B",
                ["Light3"] = "Sword_Regular_C",
                ["Heavy"] = "Sword_Attack",
                ["Special"] = "Sword_Regular_Combo",
                ["Overhead"] = "OverhandThrow",
                ["Hook"] = "Melee_Hook",
                ["Dash"] = "Sword_Dash_RM",
                ["Cast"] = "OverhandThrow"
            });
    }
}
