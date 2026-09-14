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
            float standing = hips.position.y;

            _player.TakeDamage(KillingBlow);

            Assert.IsTrue(_ragdoll.IsActive, "The killing blow did not wake the ragdoll.");

            float waited = 0f;

            while (waited < 2f)
            {
                waited += Time.unscaledDeltaTime;

                Assert.IsFalse(float.IsNaN(hips.position.y), "The ragdoll produced a NaN position.");

                yield return null;
            }

            Assert.Less(hips.position.y, standing - 0.25f, "The hips never fell.");

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
