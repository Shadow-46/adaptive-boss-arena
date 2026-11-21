using System.Collections;
using AdaptiveBossArena.AI;
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
        }

        [UnityTest]
        public IEnumerator AKnightFightIsTheFightAsBalanced()
        {
            yield return LoadArena(new RunModifiers());

            var boss = Object.FindAnyObjectByType<BossController>();
            Assert.AreEqual(0, boss.PhaseIndex);
            Assert.AreEqual(1f, boss.GetContext().CooldownScale);
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
