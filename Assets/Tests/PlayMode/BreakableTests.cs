using System.Collections;
using System.Linq;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Tests that heavy impacts break the arena's stone, within budget, without touching the fight.
    /// </summary>
    [TestFixture]
    public sealed class BreakableTests
    {
        private DestructibleField _field;
        private DebrisPool _pool;

        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            _field = Object.FindAnyObjectByType<DestructibleField>();
            _pool = Object.FindAnyObjectByType<DebrisPool>();

            Assert.IsNotNull(_field, "The arena has no destructible field.");
            Assert.IsNotNull(_pool, "The arena has no debris pool.");
            Assert.Greater(_field.Destructibles.Length, 0, "Nothing in the arena can break.");
        }

        [UnityTest]
        public IEnumerator ASlamBesideTheParapetBreaksTheStoneThereAndOnlyThere()
        {
            Object.FindAnyObjectByType<HazardField>().Spawn(new Vector3(0f, 0f, 15f), 3f, 0f, 0.5f);

            yield return null;

            Destructible[] broken = _field.Destructibles.Where(d => d.IsBroken).ToArray();

            Assert.Greater(broken.Length, 0, "A slam against the parapet broke nothing.");
            Assert.IsTrue(broken.All(d => d.Centre.z > 8f), "A slam on one side broke stone on the other.");
            Assert.Greater(_pool.ActiveCount, 0, "Stone broke but no debris flew.");
        }

        [UnityTest]
        public IEnumerator BreakingEverythingStaysInsideTheDebrisBudget()
        {
            _field.BreakWithin(Vector3.zero, 100f);

            yield return null;

            Assert.IsTrue(_field.Destructibles.All(d => d.IsBroken));
            Assert.LessOrEqual(_pool.ActiveCount, _pool.Capacity);
        }

        [UnityTest]
        public IEnumerator ARetryMakesTheRoomWholeAgain()
        {
            _field.BreakWithin(Vector3.zero, 100f);

            yield return null;

            Object.FindAnyObjectByType<EncounterDirector>().RestartAttempt();

            yield return null;

            Assert.IsTrue(_field.Destructibles.All(d => !d.IsBroken), "A retry left stone broken.");
            Assert.AreEqual(0, _pool.ActiveCount, "A retry left debris lying around.");
        }

        [UnityTest]
        public IEnumerator LooseStoneAndCorpsesNeverTouchAFighter()
        {
            yield return null;

            int[] fighters =
            {
                Layers.Player, Layers.Boss, Layers.PlayerHurtbox, Layers.BossHurtbox, Layers.PlayerHitbox, Layers.BossHitbox
            };

            foreach (int loose in new[] { Layers.Debris, Layers.Ragdoll })
            {
                foreach (int fighter in fighters)
                {
                    Assert.IsTrue(Physics.GetIgnoreLayerCollision(loose, fighter),
                        $"{LayerMask.LayerToName(loose)} collides with {LayerMask.LayerToName(fighter)}.");
                }

                Assert.IsFalse(Physics.GetIgnoreLayerCollision(loose, Layers.Arena),
                    $"{LayerMask.LayerToName(loose)} falls through the floor.");
            }
        }
    }
}
