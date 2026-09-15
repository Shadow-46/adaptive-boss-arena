using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdaptiveBossArena.Combat.Feel;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Measures when each attack clip strikes, by sampling its fighter's right hand through the clip.
    /// </summary>
    /// <remarks>
    /// Runs headless: posing a model with a clip needs no GPU. The measurement itself is the pure rule in
    /// <see cref="ClipContact"/>; this only gathers the hand speeds it reads.
    /// </remarks>
    public static class ClipContactMeasure
    {
        private const int Samples = 48;

        /// <summary>Contact fraction for the clip a table plays in a state.</summary>
        /// <param name="table">The fighter's clip table.</param>
        /// <param name="state">The attack state.</param>
        /// <returns>The measured fraction, or zero when the clip or rig is absent.</returns>
        public static float ContactFor(CharacterClipTable table, string state)
        {
            if (!table.States.TryGetValue(state, out string clipName))
            {
                return 0f;
            }

            AnimationClip clip = FindClip(table.ClipSources, clipName);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(table.RigModel);

            if (clip == null || model == null)
            {
                return 0f;
            }

            GameObject body = Object.Instantiate(model);

            try
            {
                Animator animator = body.GetComponentInChildren<Animator>();
                Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
                Transform hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;

                if (hand == null || hips == null)
                {
                    return 0f;
                }

                var speeds = new float[Samples];
                Vector3 previous = Vector3.zero;
                float step = clip.length / (Samples - 1);

                for (int i = 0; i < Samples; i++)
                {
                    clip.SampleAnimation(body, i * step);

                    // Relative to the hips, so a leap or lunge carrying the whole body is not read as the hand swinging.
                    Vector3 local = hand.position - hips.position;
                    speeds[i] = i == 0 ? 0f : (local - previous).magnitude / Mathf.Max(0.0001f, step);
                    previous = local;
                }

                return ClipContact.PeakFraction(speeds);
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        private static AnimationClip FindClip(IEnumerable<string> sources, string name)
        {
            foreach (string source in sources)
            {
                IEnumerable<string> files = AssetDatabase.IsValidFolder(source)
                    ? Directory.GetFiles(source, "*.fbx", SearchOption.AllDirectories).Select(f => f.Replace(Path.DirectorySeparatorChar, '/'))
                    : new[] { source };

                AnimationClip found = files.SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => c.name == name);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
