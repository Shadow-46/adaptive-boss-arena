using AdaptiveBossArena.Combat;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Mounts the parts of a rigged body its blows land with.
    /// </summary>
    /// <remarks>
    /// Only on a rig: a primitive body has no limbs that move with a swing, so its attacks stay on their authored
    /// volumes, which is also what every test that builds a bare combatant expects.
    /// </remarks>
    public static class StrikeVolumeBuilder
    {
        /// <summary>Mounts one striking part on a transform, along its forward.</summary>
        /// <param name="mount">The bone or weapon it rides on.</param>
        /// <param name="part">Which part it is.</param>
        /// <param name="baseDistance">Where it begins along the mount's forward.</param>
        /// <param name="tipDistance">Where it ends.</param>
        /// <param name="radius">Its thickness.</param>
        /// <returns>The mounted part.</returns>
        public static StrikeVolume Attach(
            Transform mount, StrikerParts part, float baseDistance, float tipDistance, float radius)
        {
            var strike = new GameObject("Strike" + part);
            strike.transform.SetParent(mount, false);

            var volume = strike.AddComponent<StrikeVolume>();
            volume.Configure(part, baseDistance, tipDistance, radius);

            return volume;
        }

        /// <summary>
        /// Mounts the fists, the right foot and the body on a rig.
        /// </summary>
        /// <remarks>
        /// Short capsules standing off each bone: a bone's own axes point in no useful direction, so the span is
        /// kept small enough that it reads as the fist or foot whichever way it faces, and the radius does the work.
        /// </remarks>
        /// <param name="rig">The rigged body.</param>
        /// <param name="fistRadius">Thickness of a fist.</param>
        /// <param name="footRadius">Thickness of the foot.</param>
        /// <param name="bodyRadius">Thickness of the body, for a leap or charge landing with its weight.</param>
        public static void AttachLimbs(Animator rig, float fistRadius, float footRadius, float bodyRadius)
        {
            if (rig == null || !rig.isHuman)
            {
                return;
            }

            AttachToBone(rig, HumanBodyBones.RightHand, StrikerParts.RightHand, fistRadius);
            AttachToBone(rig, HumanBodyBones.LeftHand, StrikerParts.LeftHand, fistRadius);
            AttachToBone(rig, HumanBodyBones.RightFoot, StrikerParts.RightFoot, footRadius);
            AttachToBone(rig, HumanBodyBones.Hips, StrikerParts.Body, bodyRadius);
        }

        private static void AttachToBone(Animator rig, HumanBodyBones bone, StrikerParts part, float radius)
        {
            Transform mount = rig.GetBoneTransform(bone);

            if (mount != null)
            {
                Attach(mount, part, 0f, 0.1f, radius);
            }
        }
    }
}
