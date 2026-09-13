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

        /// <summary>Waits until the intro releases and the combat clock is actually running.</summary>
        /// <remarks>
        /// The encounter opens frozen for its ready-fight intro, so the player's state machine is not
        /// ticking yet. A stagger requested then sits unconsumed and never becomes a state, which reads
        /// exactly like a stagger that was never requested. Waiting for the clock rather than a fixed
        /// frame count keeps this independent of how long the intro is tuned to run.
        /// </remarks>
        private static IEnumerator WaitForTheFightToStart()
        {
            const float GiveUpAfterSeconds = 15f;

            float waited = 0f;

            while (Time.timeScale <= 0f && waited < GiveUpAfterSeconds)
            {
                waited += Time.unscaledDeltaTime;

                yield return null;
            }

            Assert.Less(waited, GiveUpAfterSeconds, "The fight never started.");
        }

        /// <summary>Lets the deferred stagger request become a state.</summary>
        private static IEnumerator LetReactionsResolve()
        {
            for (int i = 0; i < 3; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ARecoilHoldsForTheWholeFreezeItCausedInsteadOfFadingThroughIt()
        {
            // The recoil's envelope used to count down on real time. A heavy hit's freeze lasts about
            // as long as the recoil does, so by the time the world resumed the shove was already
            // gone - the freeze and the impact played one after the other and neither read as a hit.
            Assert.IsNotNull(_player, "No player in the arena scene.");
            yield return WaitForTheFightToStart();

            var body = _player.GetComponentInChildren<Combat.Feel.CharacterAnimator>();
            Assert.IsNotNull(body, "The player has no procedural animator.");

            Assert.IsTrue(
                Core.Services.ServiceRegistry.Current.TryGet(out Core.Services.ITimeService time),
                "No time service.");

            Vector3 before = body.transform.localPosition;

            time.RequestHitStop(0.5f);
            _player.TakeDamage(BossBlow(1f));

            // Well past the 0.22 s the recoil lasts in real time, but inside the freeze.
            yield return new WaitForSecondsRealtime(0.3f);

            Vector3 shove = body.transform.localPosition - before;
            shove.y = 0f;

            Assert.Greater(
                shove.magnitude, 0.15f,
                "The recoil had faded before the freeze it belongs to was over.");
        }

        [UnityTest]
        public IEnumerator ASingleHitDoesNotStopThePlayer()
        {
            Assert.IsNotNull(_player, "No player in the arena scene.");
            yield return WaitForTheFightToStart();

            Assert.Greater(_player.Posture.Current, 30f, "The player began with no posture to spend.");

            _player.TakeDamage(BossBlow(30f));

            yield return LetReactionsResolve();

            Assert.IsFalse(
                IsStaggered,
                "One hit interrupted a player whose posture was nowhere near spent.");

            Assert.Less(_player.Posture.Current, _player.Posture.Maximum, "The hit cost no posture.");
        }

        [UnityTest]
        public IEnumerator EnoughHitsInARowStillBreakThePlayer()
        {
            Assert.IsNotNull(_player, "No player in the arena scene.");
            yield return WaitForTheFightToStart();

            // Absorbing hits must not mean ignoring them. A sustained flurry has to land eventually,
            // or the player simply cannot be pressured.
            for (int i = 0; i < 5; i++)
            {
                _player.TakeDamage(BossBlow(30f));
            }

            Assert.IsTrue(_player.Posture.IsBroken, "A sustained flurry never broke the player's posture.");

            yield return LetReactionsResolve();

            Assert.IsTrue(IsStaggered, "The posture broke but the player was never interrupted.");
        }

        [UnityTest]
        public IEnumerator BeingHitWhileBrokenDoesNotHoldThePlayerThere()
        {
            Assert.IsNotNull(_player, "No player in the arena scene.");
            yield return WaitForTheFightToStart();

            for (int i = 0; i < 5; i++)
            {
                _player.TakeDamage(BossBlow(30f));
            }

            yield return LetReactionsResolve();
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
