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

        /// <summary>Starts a roll directly, so the test does not depend on simulated input.</summary>
        private Player.States.PlayerDashState ForceRoll(out PlayerContext context)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

            context = (PlayerContext)typeof(PlayerController).GetField("_context", flags).GetValue(_player);
            var machine = (Core.StateMachine.StateMachine<PlayerContext>)
                typeof(PlayerController).GetField("_machine", flags).GetValue(_player);
            var roll = (Player.States.PlayerDashState)
                typeof(PlayerController).GetField("_dashState", flags).GetValue(_player);

            machine.ForceState(roll);

            return roll;
        }

        [UnityTest]
        public IEnumerator TheRollKeepsItsDistanceAndItsInvincibilityWindow()
        {
            // The dodge became a longer roll. Two numbers the boss was tuned against must not move
            // with it: how far a dodge carries the player, and when its invincibility ends.
            Assert.IsNotNull(_player);
            yield return WaitForTheFightToStart();

            PlayerContext context = ForceRollAwayFromTheBoss();
            Vector3 start = _player.transform.position;
            float startedAt = context.Time.CombatTime;
            float lastInvincible = 0f;

            while (context.Time.CombatTime - startedAt < context.Config.DashDurationSeconds + 0.05f)
            {
                if (context.IsInvulnerable)
                {
                    lastInvincible = context.Time.CombatTime - startedAt;
                }

                yield return null;
            }

            Vector3 travelled = _player.transform.position - start;
            travelled.y = 0f;

            Assert.AreEqual(context.Config.DashDistance, travelled.magnitude, 0.6f,
                "The roll no longer carries the player its configured distance.");
            Assert.AreEqual(0.135f, lastInvincible, 0.03f,
                "The roll's invincibility no longer ends where the boss was tuned to expect.");
        }

        [UnityTest]
        public IEnumerator ABufferedAttackComesOutOfTheRollsTail()
        {
            Assert.IsNotNull(_player);
            yield return WaitForTheFightToStart();

            PlayerContext context = ForceRollAwayFromTheBoss();
            float startedAt = context.Time.CombatTime;

            // Pressed during the tail, where the roll allows a cancel.
            while (context.Time.CombatTime - startedAt < context.Config.DashDurationSeconds * 0.75f)
            {
                yield return null;
            }

            context.InputBuffer.Record(Player.Controls.PlayerInputAction.LightAttack, context.Time.CombatTime);

            for (int i = 0; i < 3; i++)
            {
                yield return null;
            }

            Assert.IsTrue(context.Attacks.IsRunning, "An attack pressed in the roll's tail did not come out.");
            Assert.Less(context.Time.CombatTime - startedAt, context.Config.DashDurationSeconds + 0.02f,
                "The attack waited for the roll to finish instead of cancelling it.");
        }

        private PlayerContext ForceRollAwayFromTheBoss()
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var context = (PlayerContext)typeof(PlayerController).GetField("_context", flags).GetValue(_player);

            // Faces away from the boss toward open floor, so neither the boss nor a wall shortens it.
            context.Motor.SnapToDirection(Vector3.back);
            ForceRoll(out _);

            return context;
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
