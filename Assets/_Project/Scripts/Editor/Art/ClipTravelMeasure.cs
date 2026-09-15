using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Which way, and how fast, a locomotion clip walks the body, read from its planted foot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Mixamo clips are in place: the hips never travel, so the importer reports no motion. The feet still
    /// tell: whichever foot is lower is planted, and a planted foot slides backwards under a body walking
    /// forwards. Averaged over the cycle, the planted foot's slide is the clip's travel, reversed.
    /// </para>
    /// <para>
    /// Measured rather than taken from file names, because the names lie. "great sword run" steps backwards;
    /// it was the brute's run, and the brute ran forwards on legs running away.
    /// </para>
    /// </remarks>
    public static class ClipTravelMeasure
    {
        private const int Samples = 60;

        /// <summary>A clip's travel in the model's own space: x across, y along its forward, in metres per second.</summary>
        /// <param name="table">The fighter whose model poses the clip.</param>
        /// <param name="clipName">The clip.</param>
        /// <returns>The travel, or zero when the clip or model is absent.</returns>
        public static Vector2 TravelOf(CharacterClipTable table, string clipName)
        {
            AnimationClip clip = FindClip(table.ClipSources, clipName);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(table.RigModel);

            if (clip == null || model == null)
            {
                return Vector2.zero;
            }

            GameObject body = Object.Instantiate(model);

            try
            {
                Animator animator = body.GetComponentInChildren<Animator>();

                if (animator == null || !animator.isHuman)
                {
                    return Vector2.zero;
                }

                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);

                float step = clip.length / Samples;
                Vector3 slide = Vector3.zero, previousLeft = Vector3.zero, previousRight = Vector3.zero;

                for (int i = 0; i <= Samples; i++)
                {
                    clip.SampleAnimation(body, i * step);

                    Vector3 leftFoot = body.transform.InverseTransformVector(left.position - hips.position);
                    Vector3 rightFoot = body.transform.InverseTransformVector(right.position - hips.position);

                    if (i > 0 && step > 0f)
                    {
                        Vector3 moved = left.position.y < right.position.y ? leftFoot - previousLeft : rightFoot - previousRight;
                        slide += new Vector3(moved.x, 0f, moved.z) / step;
                    }

                    previousLeft = leftFoot;
                    previousRight = rightFoot;
                }

                Vector3 travel = -slide / Samples;
                return new Vector2(travel.x, travel.z);
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
