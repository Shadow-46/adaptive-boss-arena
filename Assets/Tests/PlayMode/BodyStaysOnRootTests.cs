using System.Collections;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Asserts a fighter's body plays every travelling clip over its own root, never away from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Mixamo attack and roll clips carry their travel across the floor. Baked into the pose, that travel
    /// carried the body metres away from the root the motor moves and the hurtbox rides on: a slide attack put the
    /// brute's hips metres ahead of where it could be hit, the body lunged twice - once by the motor, once by the
    /// clip - and snapped back when the clip ended. That was the sliding and popping the player reported.
    /// </para>
    /// <para>
    /// Posed at runtime through the real Animator, because that is where the travel either is or is not: an editor
    /// sample of the same clip applies root motion by different rules.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class BodyStaysOnRootTests
    {
        /// <summary>
        /// How far the hips may lean out from the root. A lunge or a crouch shifts them a little; a baked slide
        /// carried them metres.
        /// </summary>
        private const float MaxHipsOffset = 0.9f;

        private const int Samples = 20;

        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheBruteNeverLeavesItsRoot()
        {
            var boss = Object.FindAnyObjectByType<BossController>();
            Assert.IsNotNull(boss, "No boss in the arena scene.");
            yield return AssertStaysOnRoot(boss.transform, "Dash", "Spin", "Leap", "Grab", "Overhead", "Light2");
        }

        [UnityTest]
        public IEnumerator TheKnightNeverLeavesItsRoot()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player, "No player in the arena scene.");
            yield return AssertStaysOnRoot(player.transform, CharacterAnimatorParameters.RollState, "Light1", "Heavy", "Riposte");
        }

        private static IEnumerator AssertStaysOnRoot(Transform root, params string[] states)
        {
            Animator animator = root.GetComponentInChildren<Animator>();
            Assume.That(animator != null && animator.isHuman, "Not rigged.");

            // Nothing else may drive the pose while it is being measured.
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>())
            {
                behaviour.enabled = false;
            }

            yield return null;

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            int attackTime = Animator.StringToHash(CharacterAnimatorParameters.AttackTime);
            var failures = new System.Collections.Generic.List<string>();

            foreach (string state in states)
            {
                if (!animator.HasState(0, Animator.StringToHash(state)))
                {
                    continue;
                }

                float worst = 0f;

                for (int i = 0; i < Samples; i++)
                {
                    float fraction = i / (Samples - 1f);
                    animator.SetFloat(attackTime, fraction);
                    animator.Play(state, 0, fraction);
                    animator.Update(0f);

                    Vector3 offset = hips.position - root.position;
                    offset.y = 0f;
                    worst = Mathf.Max(worst, offset.magnitude);
                }

                if (worst > MaxHipsOffset)
                {
                    failures.Add($"{state} ({worst:F2} m)");
                }
            }

            Assert.IsEmpty(failures,
                "These clips carry the body away from the root its hurtbox rides on: " + string.Join(", ", failures));
        }
    }
}
