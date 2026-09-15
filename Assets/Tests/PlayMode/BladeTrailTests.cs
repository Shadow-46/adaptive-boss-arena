using System.Collections;
using AdaptiveBossArena.Combat.Feel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Tests the smear a swinging blade leaves.
    /// </summary>
    /// <remarks>
    /// It was a ribbon behind one fixed point beside the body, which traced a circle around the fighter and
    /// was left hanging in the air as a flat card when a swing ended.
    /// </remarks>
    [TestFixture]
    public sealed class BladeTrailTests
    {
        [UnityTest]
        public IEnumerator ASwingLaysDownASmearAlongTheBladeThatFadesAway()
        {
            var pivot = new GameObject("Blade");
            WeaponTrail trail = pivot.AddComponent<WeaponTrail>();
            trail.Configure(0.3f, 1f, Color.white, null);

            try
            {
                trail.Begin();

                // Sweep the blade through a quarter turn over a fifth of a second.
                for (float t = 0f; t < 0.2f; t += Time.deltaTime)
                {
                    pivot.transform.rotation = Quaternion.Euler(0f, t / 0.2f * 90f, 0f);
                    yield return null;
                }

                Assert.GreaterOrEqual(trail.VisibleSamples, 2, "The swing left no smear.");

                var surface = GameObject.Find("BladeSmear");
                Assert.IsNotNull(surface, "The smear is not drawn in world space.");

                Bounds bounds = surface.GetComponent<MeshFilter>().sharedMesh.bounds;
                Assert.Greater(bounds.max.magnitude, 0.9f, "The smear does not reach the blade's tip.");
                Assert.Less(Vector3.Distance(bounds.center, Vector3.zero), 1f, "The smear is not where the blade swept.");

                float waited = 0f;

                while (waited < 1.5f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }

                Assert.IsFalse(trail.IsEmitting, "The blade kept emitting after its swing.");
                Assert.AreEqual(0, trail.VisibleSamples, "The smear never faded.");
            }
            finally
            {
                Object.Destroy(pivot);
            }
        }
    }
}
