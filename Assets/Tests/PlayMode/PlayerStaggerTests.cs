using System.Collections;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Tests that the player can be interrupted but not held there indefinitely.
    /// </summary>
    /// <remarks>
    /// The play-test complaint was "I feel stuck for a second before the next move" and "when I'm
    /// knocked I'm not able to come down". Both were the same fault: every landed hit called for a
    /// stagger outright, and the stagger state extends itself when another arrives, so a boss
    /// landing hits faster than 0.35s apart held the player in place with no way out. The boss was
    /// never exposed to this, because its identical situation ran through a poise pool that discards
    /// poise damage while broken.
    /// </remarks>
    [TestFixture]
    public sealed class PlayerStaggerTests
    {
        private const int WarmUpFrames = 10;

        private PlayerController _player;

        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            _player = Object.FindAnyObjectByType<PlayerController>();
        }

        /// <summary>A boss blow of the kind that used to interrupt outright, every time.</summary>
        private static DamageInfo BossBlow(float poiseDamage) => new DamageInfo
        {
            Amount = 1f,
            Type = DamageType.BossMelee,
            SourceTeam = CombatantTeam.Boss,
            SourceInstanceId = 99,
            HitDirection = Vector3.forward,
            PoiseDamage = poiseDamage,
            Stagger = StaggerStrength.Interrupt
        };

        private bool IsStaggered =>
            _player.CaptureObservation(0f).ActionState == ObservableActionState.Staggered;

        [UnityTest]
        public IEnumerator ASingleHitDoesNotStopThePlayer()
        {
            Assert.IsNotNull(_player, "No player in the arena scene.");
            Assert.Greater(_player.Posture.Current, 30f, "The player began with no posture to spend.");

            _player.TakeDamage(BossBlow(30f));

            yield return null;

            Assert.IsFalse(
                IsStaggered,
                "One hit interrupted a player whose posture was nowhere near spent.");

            Assert.Less(_player.Posture.Current, _player.Posture.Maximum, "The hit cost no posture.");
        }

        [UnityTest]
        public IEnumerator EnoughHitsInARowStillBreakThePlayer()
        {
            Assert.IsNotNull(_player, "No player in the arena scene.");

            // Absorbing hits must not mean ignoring them. A sustained flurry has to land eventually,
            // or the player simply cannot be pressured.
            for (int i = 0; i < 5; i++)
            {
                _player.TakeDamage(BossBlow(30f));
            }

            yield return null;

            Assert.IsTrue(IsStaggered, "A sustained flurry never broke the player's posture.");
        }

        [UnityTest]
        public IEnumerator BeingHitWhileBrokenDoesNotHoldThePlayerThere()
        {
            Assert.IsNotNull(_player, "No player in the arena scene.");

            for (int i = 0; i < 5; i++)
            {
                _player.TakeDamage(BossBlow(30f));
            }

            yield return null;
            Assert.IsTrue(IsStaggered, "Setup failed: the player was not broken to begin with.");

            // The bug, exactly: keep hitting a player who is already down. Every one of these used
            // to call for a fresh stagger and the state took the longer of the two, so the player
            // never got up for as long as the boss kept swinging.
            float waited = 0f;

            while (waited < 4f)
            {
                _player.TakeDamage(BossBlow(30f));

                waited += Time.deltaTime;

                yield return null;

                if (!IsStaggered)
                {
                    break;
                }
            }

            Assert.IsFalse(
                IsStaggered,
                "The player was still pinned after four seconds of being hit while down.");
        }
    }
}
