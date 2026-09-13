using System.Collections.Generic;
using AdaptiveBossArena.Combat.Feel;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Builds the Animator controller the rigged characters play through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rebuilt from code on every run rather than loaded if it exists, because it is structure, not
    /// tuning: nothing in it is meant to be adjusted by hand, and a stale controller missing a state the
    /// code crossfades to would fail silently - the character would simply keep its last pose.
    /// </para>
    /// <para>
    /// It has no transitions at all. The bridge decides which state a character should be in from the
    /// same observable state everything else reads, and crossfades to it. Encoding those decisions a
    /// second time as Animator transitions would give two sources of truth for what a character is doing.
    /// Attack states play by an <c>AttackTime</c> parameter rather than by the clock, so the attack's own
    /// timeline - and its hit-stop - drives the pose.
    /// </para>
    /// </remarks>
    public static class AnimatorControllerBuilder
    {
        /// <summary>Folder the generated controller lives in.</summary>
        public const string ControllerFolder = EditorMenus.GeneratedAssetFolder + "/Animation";

        /// <summary>Where the generated controller is written.</summary>
        public const string ControllerPath = ControllerFolder + "/CharacterController.controller";

        private const string Library1 =
            "Assets/_Project/Art/ThirdParty/Quaternius/UniversalAnimationLibrary/UAL1_Standard.fbx";

        private const string Library2 =
            "Assets/_Project/Art/ThirdParty/Quaternius/UniversalAnimationLibrary2/UAL2_Standard.fbx";

        /// <summary>State name to the clip it plays.</summary>
        public static readonly IReadOnlyDictionary<string, string> AttackStates = new Dictionary<string, string>
        {
            { "Light1", "Sword_Regular_A" },
            { "Light2", "Sword_Regular_B" },
            { "Light3", "Sword_Regular_C" },
            { "Heavy", "Sword_Attack" },
            { "Special", "Sword_Regular_Combo" },
            { "Overhead", "OverhandThrow" },
            { "Hook", "Melee_Hook" },
            { "Dash", "Sword_Dash_RM" }
        };

        /// <summary>Loads the model the rigged characters are built from.</summary>
        /// <returns>The imported mannequin model, or null when the art is absent.</returns>
        public static GameObject LoadRigModel() => AssetDatabase.LoadAssetAtPath<GameObject>(Library1);

        /// <summary>Rebuilds the controller from the imported clips.</summary>
        /// <returns>The controller, or null when the clips are not in the project.</returns>
        public static AnimatorController Build()
        {
            Dictionary<string, AnimationClip> clips = LoadClips();

            if (clips.Count == 0)
            {
                Debug.LogWarning("[Adaptive Boss Arena] No character animation clips found; the generated bodies stay.");
                return null;
            }

            AssetAuthoring.EnsureFolderExists(ControllerFolder);

            AnimatorController controller = LoadEmptiedOrCreate();
            controller.AddParameter(CharacterAnimatorParameters.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterAnimatorParameters.AttackTime, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterAnimatorParameters.ReactionTime, AnimatorControllerParameterType.Float);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState locomotion = machine.AddState(CharacterAnimatorParameters.LocomotionState);
            locomotion.motion = BuildLocomotion(controller, clips);
            machine.defaultState = locomotion;

            AddClipState(machine, CharacterAnimatorParameters.RollState, clips, "Roll");
            AddClipState(machine, CharacterAnimatorParameters.GuardState, clips, "Sword_Block");
            AddClipState(machine, CharacterAnimatorParameters.StaggerState, clips, "Idle_Shield_Break");
            AddClipState(machine, CharacterAnimatorParameters.DeathState, clips, "Death01");
            AddClipState(machine, CharacterAnimatorParameters.AirborneState, clips, "Hit_Knockback");

            AnimatorState floored = AddClipState(machine, CharacterAnimatorParameters.KnockedDownState, clips, "LayToIdle");

            if (floored != null)
            {
                floored.timeParameterActive = true;
                floored.timeParameter = CharacterAnimatorParameters.ReactionTime;
            }

            foreach (KeyValuePair<string, string> attack in AttackStates)
            {
                AnimatorState state = AddClipState(machine, attack.Key, clips, attack.Value);

                if (state != null)
                {
                    state.timeParameterActive = true;
                    state.timeParameter = CharacterAnimatorParameters.AttackTime;
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return controller;
        }

        /// <summary>
        /// Loads the existing controller emptied of its contents, or creates it.
        /// </summary>
        /// <remarks>
        /// Emptied in place rather than deleted and recreated: recreating an asset gives it a new GUID,
        /// and every prefab and config pointing at the controller would then point at nothing - the
        /// failure that once left the boss unable to attack. Its contents are structure and are rebuilt
        /// in full each run; only its identity is kept.
        /// </remarks>
        private static AnimatorController LoadEmptiedOrCreate()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null)
            {
                return AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            controller.parameters = new AnimatorControllerParameter[0];

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            foreach (ChildAnimatorState child in machine.states)
            {
                machine.RemoveState(child.state);
            }

            foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (subAsset is BlendTree)
                {
                    Object.DestroyImmediate(subAsset, true);
                }
            }

            return controller;
        }

        private static Dictionary<string, AnimationClip> LoadClips() =>
            AssetDatabase.LoadAllAssetsAtPath(Library1)
                .Concat(AssetDatabase.LoadAllAssetsAtPath(Library2))
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__"))
                .GroupBy(clip => clip.name)
                .ToDictionary(group => group.Key, group => group.First());

        private static Motion BuildLocomotion(AnimatorController controller, Dictionary<string, AnimationClip> clips)
        {
            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = CharacterAnimatorParameters.Speed,
                useAutomaticThresholds = false
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            AddChild(tree, clips, "Sword_Idle", 0f);
            AddChild(tree, clips, "Walk_Loop", 0.3f);
            AddChild(tree, clips, "Jog_Fwd_Loop", 0.7f);
            AddChild(tree, clips, "Sprint_Loop", 1f);

            return tree;
        }

        private static void AddChild(BlendTree tree, Dictionary<string, AnimationClip> clips, string clip, float threshold)
        {
            if (clips.TryGetValue(clip, out AnimationClip found))
            {
                tree.AddChild(found, threshold);
            }
        }

        private static AnimatorState AddClipState(
            AnimatorStateMachine machine, string stateName, Dictionary<string, AnimationClip> clips, string clip)
        {
            if (!clips.TryGetValue(clip, out AnimationClip found))
            {
                Debug.LogWarning("[Adaptive Boss Arena] Clip " + clip + " is missing; state " + stateName + " has no motion.");
                return null;
            }

            AnimatorState state = machine.AddState(stateName);
            state.motion = found;

            return state;
        }
    }
}
