using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Asserts every clip a fighter plays keeps its body facing the way the character faces.
    /// </summary>
    /// <remarks>
    /// The bind pose can face forward while a clip turns the whole body away: a clip carries the body
    /// orientation it was captured with, and one that ends turned round leaves the fighter facing away from
    /// the fight. A capture of the running build showed the knight facing the camera just after being hit.
    /// Checking the bind pose alone cannot see it; only playing each state and measuring the hips can.
    /// </remarks>
    [TestFixture]
    public sealed class ClipFacingTests
    {
        /// <summary>
        /// Within about 70 degrees. A sword and shield guard stands side-on (measured at 68 degrees); a wrong
        /// orientation turns the body round, which scores near minus one.
        /// </summary>
        private const float MinimumAgreement = 0.3f;

        [TestCase("Assets/_Project/Prefabs/Player.prefab")]
        [TestCase("Assets/_Project/Prefabs/Boss.prefab")]
        public void EveryStateFacesTheCharactersForward(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = (GameObject)Object.Instantiate(prefab);

            try
            {
                instance.transform.rotation = Quaternion.identity;
                Animator animator = instance.GetComponentInChildren<Animator>();
                Assume.That(animator != null && animator.isHuman && animator.runtimeAnimatorController != null, "Not rigged.");

                var controller = (AnimatorController)animator.runtimeAnimatorController;
                var failures = new System.Collections.Generic.List<string>();

                foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
                {
                    string state = child.state.name;

                    // Falls and deaths end facing wherever the body fell; they are not a stance.
                    if (state == "Death" || state == "Airborne" || state == "Roll")
                    {
                        continue;
                    }

                    animator.Rebind();

                    // Rising from the floor is judged where it ends, standing: a get-up that finishes turned round
                    // hands control back to a knight facing away from the fight.
                    if (state == "KnockedDown")
                    {
                        animator.SetFloat("ReactionTime", 1f);
                    }

                    animator.Play(state, 0, 0.15f);
                    animator.Update(0f);

                    Vector3 hips = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position -
                                   animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
                    float agreement = Vector3.Dot(Vector3.Cross(hips, Vector3.up).normalized, instance.transform.forward);

                    if (agreement < MinimumAgreement)
                    {
                        failures.Add($"{state} ({agreement:F2})");
                    }
                }

                Assert.IsEmpty(failures, "These states turn the body away from the character's forward: " + string.Join(", ", failures));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
