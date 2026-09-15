using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Asserts that each rigged character's body faces the way its root does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The motors turn the root to face the opponent and every hitbox is authored along the root's
    /// forward, so the body has to agree or the character visibly fights backwards: swings arc behind it
    /// while the hit lands in front. The imported mannequin is authored facing -Z, and attaching it with an
    /// identity rotation shipped exactly that - both fighters turned away, most obviously the boss, which
    /// the camera sees from the front. Every other test passed, because nothing measured the body.
    /// </para>
    /// <para>
    /// Measured in the bind pose, from the shoulders and the hips, so a clip's stance twist cannot hide a
    /// flip or fake one.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class RigFacingTests
    {
        /// <summary>About 25 degrees of tolerance: enough for a rig's natural asymmetry, far from a flip.</summary>
        private const float MinimumAgreement = 0.9f;

        [TestCase("Assets/_Project/Prefabs/Player.prefab")]
        [TestCase("Assets/_Project/Prefabs/Boss.prefab")]
        public void TheBodyFacesTheWayTheCharacterFaces(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, prefabPath + " is missing.");

            var instance = (GameObject)Object.Instantiate(prefab);

            try
            {
                instance.transform.rotation = Quaternion.identity;

                Animator animator = instance.GetComponentInChildren<Animator>();
                Assume.That(animator != null && animator.isHuman, "The character is not rigged; nothing to measure.");

                animator.Rebind();

                Vector3 shoulders = animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position -
                                    animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                Vector3 hips = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position -
                               animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;

                // A body facing +Z has its right side at +X, and +X cross +Y is +Z: right x up is the facing.
                float chest = Vector3.Dot(Vector3.Cross(shoulders, Vector3.up).normalized, instance.transform.forward);
                float pelvis = Vector3.Dot(Vector3.Cross(hips, Vector3.up).normalized, instance.transform.forward);

                Assert.GreaterOrEqual(chest, MinimumAgreement,
                    $"The chest faces away from the character's forward (agreement {chest:F2}).");
                Assert.GreaterOrEqual(pelvis, MinimumAgreement,
                    $"The hips face away from the character's forward (agreement {pelvis:F2}).");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
