using System.Collections;
using System.Collections.Generic;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Tests the pillars on the fighting floor: where they stand, the cover they give, and the way they come down.
    /// </summary>
    /// <remarks>
    /// Stone on the floor can break a fight as easily as it shapes one: standing on a spawn, it decides the opening;
    /// sheltering after it falls, it is an invisible wall; pinning a walking brute behind it, it is a place to hide
    /// forever. Each of those is held here.
    /// </remarks>
    [TestFixture]
    public sealed class PillarTests
    {
        private const float Radius = 16f;
        private static readonly Vector3 PlayerSpawn = new Vector3(0f, 0f, -6f);
        private static readonly Vector3 BossSpawn = new Vector3(0f, 0f, 6f);

        /// <summary>Closest a pillar may stand to a spawn: room for the opening exchange.</summary>
        private const float SpawnClearance = 4f;

        private readonly List<Destructible> _pillars = new List<Destructible>();

        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            yield return null;
            yield return null;

            _pillars.Clear();
            Transform root = GameObject.Find("Pillars")?.transform;
            Assert.IsNotNull(root, "The arena has no pillars.");

            foreach (Transform pillar in root)
            {
                _pillars.Add(pillar.GetComponent<Destructible>());
            }
        }

        [Test]
        public void FourPillarsStandOnTheFloorClearOfTheSpawns()
        {
            Assert.AreEqual(4, _pillars.Count);

            foreach (Destructible pillar in _pillars)
            {
                Assert.IsNotNull(pillar, "A pillar cannot break.");

                Vector3 at = pillar.Centre;
                Assert.Less(new Vector2(at.x, at.z).magnitude, Radius - 3f, pillar.name + " is not on the floor.");
                Assert.Greater(Flat(at, PlayerSpawn), SpawnClearance, pillar.name + " crowds the knight's spawn.");
                Assert.Greater(Flat(at, BossSpawn), SpawnClearance, pillar.name + " crowds the brute's spawn.");
                Assert.Greater(Mathf.Abs(at.x), 2f, pillar.name + " stands on the line between the spawns.");

                Collider solid = pillar.GetComponent<Collider>();
                Assert.IsNotNull(solid, pillar.name + " does not stop a body.");
                Assert.AreEqual(Layers.Arena, solid.gameObject.layer, pillar.name + " is not stone the fighters meet.");
            }
        }

        [UnityTest]
        public IEnumerator APillarSheltersTheKnightUntilItBreaksAndStandsAgainOnARetry()
        {
            Destructible pillar = _pillars[0];
            Vector3 outward = new Vector3(pillar.Centre.x, 0f, pillar.Centre.z).normalized;
            Vector3 brute = pillar.Centre + outward * 3f;
            Vector3 knight = pillar.Centre - outward * 2f;

            Assert.IsTrue(CoverRules.IsShielded(brute, knight, Layers.ArenaMask), "A standing pillar shelters no one.");

            DestructibleField field = Object.FindAnyObjectByType<DestructibleField>();
            Assert.AreEqual(1, field.BreakWithin(pillar.Centre, 0.1f), "A blow on the pillar did not break it.");

            yield return new WaitForFixedUpdate();

            Assert.IsFalse(pillar.GetComponent<Collider>().enabled, "A broken pillar is still an invisible wall.");
            Assert.IsFalse(CoverRules.IsShielded(brute, knight, Layers.ArenaMask), "A broken pillar still shelters.");

            field.RestoreAll();

            yield return new WaitForFixedUpdate();

            Assert.IsTrue(pillar.GetComponent<Collider>().enabled, "The retry did not raise the pillar again.");
            Assert.IsTrue(CoverRules.IsShielded(brute, knight, Layers.ArenaMask), "The raised pillar shelters no one.");
        }

        [Test]
        public void TheBruteWalksRoundAPillarInsteadOfIntoIt()
        {
            Destructible pillar = _pillars[0];
            Vector3 outward = new Vector3(pillar.Centre.x, 0f, pillar.Centre.z).normalized;
            Vector3 feet = pillar.Centre + outward * 2.4f;
            Vector3 straightIn = -outward;

            Vector3 steered = PillarAvoidance.Steer(feet, straightIn * 0.8f, 0.9f);

            Assert.AreEqual(0.8f, steered.magnitude, 0.001f, "Steering changed how fast the brute walks.");
            Assert.Greater(Vector3.Angle(straightIn, steered), 30f, "The brute walks dead into the pillar.");

            Vector3 away = PillarAvoidance.Steer(feet, outward, 0.9f);
            Assert.Less(Vector3.Angle(outward, away), 0.01f, "Walking away from stone was bent.");
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }
}
