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
    /// Tests that a landed blow visibly moves the body without taking the fighter's footing.
    /// </summary>
    /// <remarks>
    /// Before this a hit that did not stagger only nudged the character's whole visual root, which read as the
    /// model twitching rather than a body taking a blow.
    /// </remarks>
    [TestFixture]
    public sealed class HitReactionTests
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
        public IEnumerator ABlowFlinchesTheUpperBodyAndThenSettles()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            var bridge = player.GetComponentInChildren<CharacterAnimationBridge>();
            Assume.That(bridge != null && bridge.HasSkeleton, "The player is not rigged.");

            // The boss would keep landing blows of its own through the wait, each starting a fresh flinch.
            Object.FindAnyObjectByType<AI.BossController>().gameObject.SetActive(false);

            player.TakeDamage(new DamageInfo
            {
                Amount = 1f,
                Type = DamageType.BossMelee,
                SourceTeam = CombatantTeam.Boss,
                SourceInstanceId = 99,
                HitDirection = Vector3.forward,
                Stagger = StaggerStrength.None
            });

            // Over time, not frames: a headless frame lasts about a millisecond, far shorter than the flinch's rise.
            float peak = 0f, watched = 0f;

            while (watched < 0.4f)
            {
                yield return null;
                watched += Time.deltaTime;
                peak = Mathf.Max(peak, bridge.HitWeight);
            }

            Assert.Greater(peak, 0.5f, "A landed blow did not flinch the upper body.");

            yield return new WaitForSeconds(2.5f);

            Assert.AreEqual(0f, bridge.HitWeight, 0.001f, "The flinch never settled back to the stance.");
        }
    }
}
