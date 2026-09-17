using System.Collections;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Tests that the punish for breaking the boss's guard is offered, not taken automatically.
    /// </summary>
    /// <remarks>
    /// The riposte used to fire itself the instant the guard broke, from any state and any distance, interrupting
    /// whatever the player was doing - the best moment in the fight happened without them. It now waits for the
    /// player to walk in, face the boss and press attack.
    /// </remarks>
    [TestFixture]
    public sealed class RiposteOfferTests
    {
        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            float waited = 0f;

            do
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            while (Time.timeScale <= 0f && waited < 15f);
        }

        [UnityTest]
        public IEnumerator ABrokenGuardIsNotPunishedFromAcrossTheArena()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            var boss = Object.FindAnyObjectByType<BossController>();

            Assert.IsNotNull(player);
            Assert.IsNotNull(boss);

            // Well out of reach, so the offer must not stand however broken the guard is.
            boss.transform.position = player.transform.position + Vector3.forward * 9f;
            boss.Poise.ApplyPoiseDamage(1000f);

            yield return null;

            Assert.IsTrue(boss.Poise.IsBroken, "The boss's guard did not break.");

            float watched = 0f;

            while (watched < 0.6f)
            {
                yield return null;
                watched += Time.deltaTime;

                Assert.IsFalse(player.RiposteOffered, "A riposte was offered from across the arena.");
            }

            Assert.IsTrue(boss.Poise.IsBroken, "The break was spent without the player pressing anything.");
        }
    }
}
