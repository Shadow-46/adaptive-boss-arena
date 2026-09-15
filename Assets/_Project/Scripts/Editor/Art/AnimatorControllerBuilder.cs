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
            controller.AddParameter(CharacterAnimatorParameters.MoveX, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterAnimatorParameters.MoveZ, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterAnimatorParameters.AttackTime, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterAnimatorParameters.ReactionTime, AnimatorControllerParameterType.Float);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState locomotion = machine.AddState(CharacterAnimatorParameters.LocomotionState);
            locomotion.motion = table.Directional.Length > 0
                ? BuildDirectionalLocomotion(controller, clips, table)
                : BuildLocomotion(controller, clips, table.Locomotion);
            machine.defaultState = locomotion;

            foreach (KeyValuePair<string, string> entry in table.States)
            {
                // The flinch lives on its own layer, below.
                if (entry.Key == CharacterAnimatorParameters.HitState)
                {
                    continue;
                }

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

            AddHitLayer(controller, clips, table);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return controller;
        }

        /// <summary>
        /// Adds the layer a landed blow plays on: the fighter's own impact clip, masked to the upper body.
        /// </summary>
        /// <remarks>
        /// Weight zero at rest; the bridge raises it for the length of the flinch. Override blending with an upper-
        /// body mask means the torso and arms take the blow while the legs carry on with the step or stance below.
        /// </remarks>
        private static void AddHitLayer(AnimatorController controller, Dictionary<string, AnimationClip> clips, CharacterClipTable table)
        {
            if (!table.States.TryGetValue(CharacterAnimatorParameters.HitState, out string clipName) ||
                !clips.TryGetValue(clipName, out AnimationClip clip))
            {
                return;
            }

            var hits = new AnimatorStateMachine { name = CharacterAnimatorParameters.HitLayer, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(hits, controller);

            AnimatorState rest = hits.AddState(CharacterAnimatorParameters.HitRestState);
            hits.defaultState = rest;
            hits.AddState(CharacterAnimatorParameters.HitState).motion = clip;

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = CharacterAnimatorParameters.HitLayer,
                stateMachine = hits,
                defaultWeight = 0f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = UpperBodyMask()
            });
        }

        /// <summary>Loads or creates the mask covering the spine, head and arms, but not the legs or root.</summary>
        private static AvatarMask UpperBodyMask()
        {
            string path = ControllerFolder + "/UpperBody.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);

            if (mask == null)
            {
                mask = new AvatarMask { name = "UpperBody" };
                AssetDatabase.CreateAsset(mask, path);
            }

            for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++)
            {
                var bodyPart = (AvatarMaskBodyPart)part;
                bool upper = bodyPart == AvatarMaskBodyPart.Body || bodyPart == AvatarMaskBodyPart.Head ||
                             bodyPart == AvatarMaskBodyPart.LeftArm || bodyPart == AvatarMaskBodyPart.RightArm ||
                             bodyPart == AvatarMaskBodyPart.LeftFingers || bodyPart == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(bodyPart, upper);
            }

            EditorUtility.SetDirty(mask);
            return mask;
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

            // Every layer above the base is rebuilt too; keeping them would stack a new hit layer on each run.
            while (controller.layers.Length > 1)
            {
                AnimatorStateMachine extra = controller.layers[controller.layers.Length - 1].stateMachine;
                controller.RemoveLayer(controller.layers.Length - 1);

                if (extra != null)
                {
                    Object.DestroyImmediate(extra, true);
                }
            }

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

        /// <summary>
        /// A two-dimensional blend of idle, walks, runs, back-steps and strafes, each placed where it travels.
        /// </summary>
        /// <remarks>
        /// Each clip's position is its measured travel over the fastest clip's, so the run sits at one along the
        /// facing, a walk about a third of the way, and a strafe out to its side at the speed it actually steps.
        /// Driven by movement relative to the facing, so sideways and backwards motion gets legs that match.
        /// </remarks>
        private static Motion BuildDirectionalLocomotion(
            AnimatorController controller, Dictionary<string, AnimationClip> clips, CharacterClipTable table)
        {
            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = CharacterAnimatorParameters.MoveX,
                blendParameterY = CharacterAnimatorParameters.MoveZ,
                useAutomaticThresholds = false
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            if (table.Locomotion.Length > 0 && clips.TryGetValue(table.Locomotion[0].Clip, out AnimationClip idle))
            {
                tree.AddChild(idle, Vector2.zero);
            }

            var travels = table.Directional
                .Where(clips.ContainsKey)
                .Select(name => (Name: name, Travel: ClipTravelMeasure.TravelOf(table, name)))
                .Where(entry => entry.Travel.magnitude > MinimumTravel)
                .ToList();

            float fastest = travels.Count > 0 ? travels.Max(entry => entry.Travel.magnitude) : 1f;

            foreach ((string name, Vector2 travel) in travels)
            {
                tree.AddChild(clips[name], travel / fastest);
            }

            return tree;
        }

        /// <summary>Travel below which a clip is treated as standing, in metres per second.</summary>
        private const float MinimumTravel = 0.2f;

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
