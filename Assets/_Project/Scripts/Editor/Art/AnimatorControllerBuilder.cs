using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdaptiveBossArena.Combat.Feel;
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

        /// <summary>Whether a table's model is in the project.</summary>
        /// <param name="table">The fighter's table.</param>
        /// <returns>True when its model can be loaded.</returns>
        public static bool IsAvailable(CharacterClipTable table) => LoadRigModel(table) != null;

        /// <summary>Loads the model a fighter is built from.</summary>
        /// <param name="table">The fighter's table.</param>
        /// <returns>The imported model, or null when its art is absent.</returns>
        public static GameObject LoadRigModel(CharacterClipTable table) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(table.RigModel);

        /// <summary>Rebuilds one fighter's controller from its table.</summary>
        /// <param name="table">The fighter's table.</param>
        /// <returns>The controller, or null when the table's clips are not in the project.</returns>
        public static AnimatorController Build(CharacterClipTable table)
        {
            Dictionary<string, AnimationClip> clips = LoadClips(table.ClipSources);

            if (clips.Count == 0)
            {
                Debug.LogWarning("[Adaptive Boss Arena] No clips found for " + table.Name + "; the generated bodies stay.");
                return null;
            }

            AssetAuthoring.EnsureFolderExists(ControllerFolder);

            string path = ControllerFolder + "/" + table.Name + ".controller";
            AnimatorController controller = LoadEmptiedOrCreate(path);
            controller.AddParameter(CharacterAnimatorParameters.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterAnimatorParameters.AttackTime, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterAnimatorParameters.ReactionTime, AnimatorControllerParameterType.Float);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState locomotion = machine.AddState(CharacterAnimatorParameters.LocomotionState);
            locomotion.motion = BuildLocomotion(controller, clips, table.Locomotion);
            machine.defaultState = locomotion;

            foreach (KeyValuePair<string, string> entry in table.States)
            {
                AnimatorState state = AddClipState(machine, entry.Key, clips, entry.Value);

                if (state == null)
                {
                    continue;
                }

                if (entry.Key == CharacterAnimatorParameters.KnockedDownState)
                {
                    state.timeParameterActive = true;
                    state.timeParameter = CharacterAnimatorParameters.ReactionTime;
                }
                else if (CharacterClipTable.AttackStateNames.Contains(entry.Key))
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
        private static AnimatorController LoadEmptiedOrCreate(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

            if (controller == null)
            {
                return AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            controller.parameters = new AnimatorControllerParameter[0];

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            foreach (ChildAnimatorState child in machine.states)
            {
                machine.RemoveState(child.state);
            }

            foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (subAsset is BlendTree)
                {
                    Object.DestroyImmediate(subAsset, true);
                }
            }

            return controller;
        }

        /// <summary>Every clip in the sources, by name; where two share a name, the earlier source wins.</summary>
        private static Dictionary<string, AnimationClip> LoadClips(IEnumerable<string> sources)
        {
            var clips = new Dictionary<string, AnimationClip>();

            foreach (string source in sources)
            {
                IEnumerable<string> files = AssetDatabase.IsValidFolder(source)
                    ? Directory.GetFiles(source, "*.fbx", SearchOption.AllDirectories).Select(f => f.Replace(Path.DirectorySeparatorChar, '/'))
                    : new[] { source };

                foreach (AnimationClip clip in files.SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>())
                {
                    if (!clip.name.StartsWith("__preview__") && !clips.ContainsKey(clip.name))
                    {
                        clips.Add(clip.name, clip);
                    }
                }
            }

            return clips;
        }

        private static Motion BuildLocomotion(
            AnimatorController controller, Dictionary<string, AnimationClip> clips, (float Speed, string Clip)[] steps)
        {
            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = CharacterAnimatorParameters.Speed,
                useAutomaticThresholds = false
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            foreach ((float speed, string clip) in steps)
            {
                AddChild(tree, clips, clip, speed);
            }

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
