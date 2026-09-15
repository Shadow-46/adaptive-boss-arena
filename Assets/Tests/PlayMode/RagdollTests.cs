using System.Collections;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Tests that a fighter's body falls physically on death and stands up again for a retry.
    /// </summary>
    [TestFixture]
    public sealed class RagdollTests
    {
        private PlayerController _player;
        private RagdollActivator _ragdoll;

        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            _player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(_player, "No player in the arena scene.");

            _ragdoll = _player.GetComponentInChildren<RagdollActivator>();
            Assert.IsNotNull(_ragdoll, "The rigged player was built without a ragdoll.");

            float waited = 0f;

            while (Time.timeScale <= 0f && waited < 15f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static DamageInfo KillingBlow => new DamageInfo
        {
            Amount = 10000f,
            Type = DamageType.BossMelee,
            SourceTeam = CombatantTeam.Boss,
            SourceInstanceId = 99,
            HitDirection = Vector3.forward,
            KnockbackSpeed = 6f,
            Stagger = StaggerStrength.None
        };

        [UnityTest]
        public IEnumerator TheBodyIsDormantWhileTheFighterLives()
        {
            yield return null;

            Assert.AreEqual(11, _ragdoll.Bodies.Length, "The ragdoll is missing bones.");

            foreach (Rigidbody body in _ragdoll.Bodies)
            {
                Assert.IsTrue(body.isKinematic, body.name + " is simulated on a living fighter.");
                Assert.IsFalse(body.GetComponent<Collider>().enabled, body.name + " collides on a living fighter.");
            }
        }

        [UnityTest]
        public IEnumerator DeathDropsTheBodyAndARetryStandsItBackUp()
        {
            Rigidbody hips = _ragdoll.Bodies[0];
            Rigidbody head = _ragdoll.Bodies[2];

            // Measured by the head, not the hips. How high the hips rest depends on how the body lands - one run
            // left the corpse face-down and kneeling, head on the floor and hips still 0.58 m up - while a head
            // that has dropped most of a metre means the body is down whichever way it fell.
            float standingHead = head.position.y;

            _player.TakeDamage(KillingBlow);

            Assert.IsTrue(_ragdoll.IsActive, "The killing blow did not wake the ragdoll.");

            // Game time, not real time: a death plays in slow motion, so a fixed real-time window catches the
            // body part-way down and passes or fails depending on how the slow motion happened to fall.
            float waited = 0f, realWaited = 0f;

            while (waited < 1.2f && realWaited < 10f)
            {
                waited += Time.deltaTime;
                realWaited += Time.unscaledDeltaTime;

                Assert.IsFalse(float.IsNaN(hips.position.y), "The ragdoll produced a NaN position.");

                yield return null;
            }

            Assert.Less(head.position.y, standingHead - 0.6f,
                $"The body never went down: head {head.position.y:F2} m from {standingHead:F2}, hips {hips.position.y:F2} m, " +
                $"after {waited:F2} s game time / {realWaited:F2} s real, time scale {Time.timeScale:F2}, " +
                $"player at {_player.transform.position}.");

            _player.ResetForNewAttempt(new Vector3(0f, 0f, -6f));

            yield return null;

            Assert.IsFalse(_ragdoll.IsActive);
            Assert.IsTrue(_player.GetComponentInChildren<Animator>().enabled, "The retry left the Animator off.");

            foreach (Rigidbody body in _ragdoll.Bodies)
            {
                Assert.IsTrue(body.isKinematic, body.name + " is still simulated after a retry.");
            }
        }
    }
}
