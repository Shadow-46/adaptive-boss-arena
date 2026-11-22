using System.Collections;
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
    /// Loads the shipped arena under each new run mode and checks the composition root pushed the numbers in.
    /// </summary>
    /// <remarks>
    /// Opening in a later phase is the riskiest wiring here. A phase change made Igris invulnerable once before
    /// when it fired at the wrong moment, so the rematch must open in phase two without the escalation's roar,
    /// invulnerability or shockwave.
    /// </remarks>
    [TestFixture]
    public sealed class RunModeTests
    {
        private const int WarmUpFrames = 10;

        private RunModifiers _saved;

        [SetUp]
        public void RememberSettings() => _saved = RunSettings.Current;

        [TearDown]
        public void RestoreSettings() => RunSettings.Current = _saved ?? new RunModifiers();

        private static IEnumerator LoadArena(RunModifiers modifiers)
        {
            RunSettings.Current = modifiers;
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AscendedOpensInTheSecondPhaseQuietly()
        {
            yield return LoadArena(new RunModifiers { Ascended = true });

            var boss = Object.FindAnyObjectByType<BossController>();
            Assert.IsNotNull(boss);
            Assert.AreEqual(RunModifiers.AscendedOpeningPhase, boss.PhaseIndex, "The rematch opens in phase two.");
            Assert.AreEqual(0f, boss.PhaseTransitionInvulnRemaining, "Opening in phase two is not an escalation.");
            Assert.AreEqual(1f, boss.Health.Normalized, 1e-4f, "He opens at full health.");
            Assert.Less(boss.GetContext().CooldownScale, 1f, "An Ascended Igris rests less between swings.");
            Assert.IsFalse(boss.LastPhaseChangeWasEscalation, "The opening was announced as an escalation.");
            Assert.AreEqual(1, boss.GetContext().ComboCeilingBonus, "An Ascended Igris chains one blow longer.");

            // The set piece's own cut would hold the camera on him over the entrance.
            var camera = Object.FindAnyObjectByType<Game.ArenaCameraRig>();
            Assert.IsNotNull(camera);
            Assert.IsFalse(camera.IsPlayingPhaseShot, "The phase transformation played over the Ascended entrance.");
        }

        [UnityTest]
        public IEnumerator AKnightFightIsTheFightAsBalanced()
        {
            yield return LoadArena(new RunModifiers());

            var boss = Object.FindAnyObjectByType<BossController>();
            Assert.AreEqual(0, boss.PhaseIndex);
            Assert.AreEqual(1f, boss.GetContext().CooldownScale);

            // Every test writes its records to a throwaway folder, never to the player's own save.
            Assert.IsTrue(ServiceRegistry.Current.TryGet(out ISaveService saves));
            Assert.AreEqual(TestSaveIsolation.Folder, ((Game.JsonSaveService)saves).RootFolder,
                "The tests are writing into the player's real save.");
        }

        [UnityTest]
        public IEnumerator AnUnlockedTintSurvivesAHit()
        {
            yield return LoadArena(new RunModifiers());
            Color plain = Object.FindAnyObjectByType<PlayerController>().GetComponentInChildren<HitFlash>().BaseColorAt(0);

            yield return LoadArena(new RunModifiers { KnightTintId = "gilded" });
            HitFlash flash = Object.FindAnyObjectByType<PlayerController>().GetComponentInChildren<HitFlash>();
            Color tinted = flash.BaseColorAt(0);
            Assert.AreNotEqual(plain, tinted, "The tint did not reach the knight.");

            // A blow flashes the body and then restores its resting colour; the resting colour must be the tint.
            flash.Play();

            // Real seconds, not frames: a headless run turns sixty frames in less time than the flash lasts.
            yield return new WaitForSecondsRealtime(0.6f);
            yield return null;

            Renderer body = flash.BodyAt(0);
            var block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            Color shown = block.GetColor("_BaseColor");
            Assert.AreEqual(tinted.r, shown.r, 0.02f, "The first blow wiped the tint.");
            Assert.AreEqual(tinted.b, shown.b, 0.02f, "The first blow wiped the tint.");
        }

        [UnityTest]
        public IEnumerator StoryHandsTheKnightTwoMoreFlasks()
        {
            yield return LoadArena(new RunModifiers());
            int knightFlasks = Object.FindAnyObjectByType<PlayerController>().HealChargesRemaining;

            yield return LoadArena(new RunModifiers { Difficulty = Difficulty.Story });
            var player = Object.FindAnyObjectByType<PlayerController>();

            Assert.AreEqual(knightFlasks + DifficultyProfile.Story.HealChargeBonus, player.HealChargesRemaining);
            Assert.AreEqual(player.HealChargeCapacity, player.HealChargesRemaining);
        }
    }
}
