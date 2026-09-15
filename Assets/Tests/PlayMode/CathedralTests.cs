using System.Collections;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Tests that the cathedral dresses the arena without changing the fight.
    /// </summary>
    /// <remarks>
    /// The environment is the easiest place to break the AI without touching its code: one stray collider
    /// on the fighting floor blocks a charge, a dash or a line of sight the boss's positioning assumes is
    /// clear, and every test of the AI itself still passes.
    /// </remarks>
    [TestFixture]
    public sealed class CathedralTests
    {
        private const float Radius = 16f;

        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheCathedralIsInTheScene()
        {
            yield return null;

            Assert.IsNotNull(GameObject.Find("Cathedral"), "The arena was generated without its cathedral.");
        }

        [UnityTest]
        public IEnumerator TheFloorIsSolidEverywhereAFighterCanStand()
        {
            yield return null;

            int mask = 1 << Layers.Arena;

            for (float x = -Radius + 1f; x <= Radius - 1f; x += 2f)
            {
                for (float z = -Radius + 1f; z <= Radius - 1f; z += 2f)
                {
                    if (x * x + z * z > (Radius - 1f) * (Radius - 1f))
                    {
                        continue;
                    }

                    bool hit = Physics.Raycast(new Vector3(x, 5f, z), Vector3.down, out RaycastHit floor, 10f, mask);

                    Assert.IsTrue(hit, $"No floor under ({x}, {z}).");
                    Assert.AreEqual(0f, floor.point.y, 0.05f, $"The floor under ({x}, {z}) is not at ground level.");
                }
            }
        }

        [UnityTest]
        public IEnumerator NothingOfTheRuinStandsOnTheFightingFloor()
        {
            yield return null;

            // For every solid thing on the arena layer, the nearest it comes to the middle of the floor at
            // body height. The floor itself is nearest below that height, and the wall ring is nearest at
            // its radius, so anything else found inside is an intruder. Exact for the boxes the arena uses.
            var body = new Vector3(0f, 1.2f, 0f);

            foreach (Collider collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (collider.gameObject.layer != Layers.Arena || collider.isTrigger)
                {
                    continue;
                }

                Vector3 nearest = collider.ClosestPoint(body);
                float fromCentre = new Vector2(nearest.x, nearest.z).magnitude;

                Assert.IsFalse(
                    nearest.y > 0.3f && fromCentre < Radius - 0.6f,
                    $"{collider.name} stands on the fighting floor, {fromCentre:F1} m from its centre.");
            }

            Collider[] dressing = GameObject.Find("Cathedral").GetComponentsInChildren<Collider>(true);

            Assert.IsEmpty(dressing, "Cathedral dressing carries a collider: " +
                (dressing.Length > 0 ? dressing[0].name : ""));
        }

        [UnityTest]
        public IEnumerator TheBakedLightingSurvivesTheSceneBeingRebuilt()
        {
            // The scene is regenerated from code on every setup, headless and without a GPU to bake with. The
            // bake is only worth anything if each rebuild picks it back up.
            yield return null;

            ReflectionProbe probe = Object.FindAnyObjectByType<ReflectionProbe>();

            Assert.IsNotNull(probe, "The arena has no reflection probe.");
            Assert.AreEqual(UnityEngine.Rendering.ReflectionProbeMode.Custom, probe.mode, "The reflection probe lost its bake.");
            Assert.IsNotNull(probe.customBakedTexture, "The reflection probe has no baked cubemap.");

            Assert.IsNotNull(LightmapSettings.lightProbes, "The arena lost its baked light probes.");
            Assert.Greater(LightmapSettings.lightProbes.count, 0, "The arena lost its baked light probes.");
        }

        [UnityTest]
        public IEnumerator TheHazeIsOnAndNeverBlack()
        {
            yield return null;
            yield return null;

            Assert.IsTrue(RenderSettings.fog, "The haze is off.");
            Assert.GreaterOrEqual(
                HazeColor.Luminance(RenderSettings.fogColor), HazeColor.MinimumLuminance - 0.001f,
                "The haze has gone dark, which is how fog was lost last time.");
        }
    }
}
