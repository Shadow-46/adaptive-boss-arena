using System.Collections;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.Core.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Pins that the fight ends on an execution: the blow that would kill a standing boss brings him to his knee,
    /// broken, and the next blow ends it.
    /// </summary>
    [TestFixture]
    public sealed class FinisherTests
    {
        private const int WarmUpFrames = 10;

        private BossController _boss;

        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            _boss = Object.FindAnyObjectByType<BossController>();
        }

        private static DamageInfo Blow(float amount) =>
            DamageInfo.Create(amount, DamageType.Heavy, CombatantTeam.Player, 1);

        [UnityTest]
        public IEnumerator ALethalBlowOnAStandingBoss_BringsHimToHisKnee_AndTheNextEndsIt()
        {
            Assert.IsNotNull(_boss, "No boss in the arena scene.");
            Assert.IsTrue(_boss.IsInitialised, "The boss did not initialise.");

            DamageResult first = _boss.TakeDamage(Blow(_boss.Health.Current + 100f));

            Assert.AreEqual(DamageOutcome.Applied, first.Outcome);
            Assert.IsFalse(first.WasLethal, "The blow that should have killed him was not held for the finisher.");
            Assert.IsTrue(_boss.IsAlive);
            Assert.IsTrue(_boss.AwaitingFinalBlow);
            Assert.IsTrue(_boss.Poise.IsBroken, "Kneeling, his guard must be broken so the riposte is offered.");

            yield return null;

            DamageResult last = _boss.TakeDamage(Blow(10f));

            Assert.IsTrue(last.WasLethal, "The last blow did not end him.");
            Assert.IsFalse(_boss.IsAlive);
        }

        [UnityTest]
        public IEnumerator AnOrdinaryBlow_IsNotHeldBack()
        {
            Assert.IsNotNull(_boss, "No boss in the arena scene.");

            float before = _boss.Health.Current;
            _boss.TakeDamage(Blow(10f));

            yield return null;

            Assert.IsFalse(_boss.AwaitingFinalBlow);
            Assert.Less(_boss.Health.Current, before);
        }
    }
}
