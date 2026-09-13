using System.Collections;
using System.Reflection;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Asserts the rigged characters are wired through, in the shipped scene.
    /// </summary>
    /// <remarks>
    /// Every link from imported model to moving limbs fails silently: an Animator with no controller
    /// stands in a T-pose, a crossfade to a missing state freezes the last pose, a socket on the wrong
    /// transform leaves the blade floating while the arm swings. None of it throws. These walk the chain
    /// in the real scene.
    /// </remarks>
    [TestFixture]
    public sealed class RiggedCharacterTests
    {
        private PlayerController _player;
        private BossController _boss;

        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            _player = Object.FindAnyObjectByType<PlayerController>();
            _boss = Object.FindAnyObjectByType<BossController>();
        }

        private static IEnumerator WaitForTheFightToStart()
        {
            float waited = 0f;

            while (Time.timeScale <= 0f && waited < 15f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        [Test]
        public void BothFightersAnimateThroughAHumanRig()
        {
            foreach (Component fighter in new Component[] { _player, _boss })
            {
                Assert.IsNotNull(fighter, "A fighter is missing from the arena.");

                var animator = fighter.GetComponentInChildren<Animator>();
                Assert.IsNotNull(animator, fighter.name + " has no Animator; it is still a primitive body.");
                Assert.IsTrue(animator.isHuman, fighter.name + " is not on a humanoid avatar.");
                Assert.IsNotNull(animator.runtimeAnimatorController, fighter.name + " has no controller and would T-pose.");
                Assert.IsFalse(animator.applyRootMotion, fighter.name + " lets clips move the character.");

                var bridge = fighter.GetComponentInChildren<CharacterAnimationBridge>();
                Assert.IsTrue(bridge != null && bridge.HasSkeleton, fighter.name + " is not driving its rig.");
            }
        }

        [Test]
        public void TheKnightsBladeIsHeldInItsRightHand()
        {
            var animator = _player.GetComponentInChildren<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var socket = _player.GetComponentInChildren<WeaponSocket>();

            Assert.IsNotNull(hand, "The rig has no right hand bone.");
            Assert.IsNotNull(socket, "The knight has no weapon socket.");
            Assert.IsTrue(socket.transform.IsChildOf(hand), "The blade is not held in the hand, so it floats while the arm swings.");
        }

        [UnityTest]
        public IEnumerator AnAttackScrubsItsClipAndFreezesOnHitStop()
        {
            yield return WaitForTheFightToStart();

            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var context = (PlayerContext)typeof(PlayerController).GetField("_context", flags).GetValue(_player);
            var animator = _player.GetComponentInChildren<Animator>();

            context.InputBuffer.Record(Player.Controls.PlayerInputAction.LightAttack, context.Time.CombatTime);

            float waited = 0f;

            while (!context.Attacks.IsRunning && waited < 1f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(context.Attacks.IsRunning, "The attack never started.");

            yield return null;
            yield return null;

            float scrubbed = animator.GetFloat(CharacterAnimatorParameters.AttackTime);
            Assert.Greater(scrubbed, 0f, "The attack clip is not being scrubbed by the attack's timeline.");

            Assert.IsTrue(ServiceRegistry.Current.TryGet(out ITimeService time));
            time.RequestHitStop(0.5f);

            yield return null;
            float frozenAt = animator.GetFloat(CharacterAnimatorParameters.AttackTime);

            yield return new WaitForSecondsRealtime(0.2f);

            Assert.AreEqual(frozenAt, animator.GetFloat(CharacterAnimatorParameters.AttackTime), 0.01f,
                "The swing kept playing through the hit-stop instead of freezing on the impact.");
        }
    }
}
