using System.Collections;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>Checks the perilous glint is wired to the boss's wind-ups in the shipped arena.</summary>
    [TestFixture]
    public sealed class PerilousFlareWiringTests
    {
        private const int WarmUpFrames = 10;

        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }
        }

        private static void Publish(CombatantTeam actor, bool unblockable)
        {
            ServiceRegistry.Current.Get<ICombatEventBus>().Publish(new CombatEvent
            {
                Kind = CombatEventKind.AttackStarted,
                Actor = actor,
                Unblockable = unblockable,
                Position = Vector3.zero,
                Direction = Vector3.forward
            });
        }

        [UnityTest]
        public IEnumerator AnUnblockableWindUpBurstsTheGlint()
        {
            var flare = Object.FindAnyObjectByType<PerilousFlare>();
            Assert.IsNotNull(flare, "No perilous glint in the arena.");
            Assert.IsFalse(flare.IsShowing);

            Publish(CombatantTeam.Boss, unblockable: true);
            yield return null;

            Assert.IsTrue(flare.IsShowing, "An unblockable wind-up did not burst the glint.");
        }

        [UnityTest]
        public IEnumerator AnOrdinaryWindUpOrTheKnightsOwnDoesNot()
        {
            var flare = Object.FindAnyObjectByType<PerilousFlare>();

            Publish(CombatantTeam.Boss, unblockable: false);
            Publish(CombatantTeam.Player, unblockable: true);
            yield return null;

            Assert.IsFalse(flare.IsShowing, "The glint fired for a blow that can be blocked.");
        }
    }
}
