using System.Collections.Generic;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests that a scar lies where a blow struck the floor, and never over the ground the knight fights from.
    /// </summary>
    /// <remarks>
    /// Every scar was centred on the boss, three to four metres wide, lasting four seconds and ignoring a
    /// dodge. A knight's sword reaches a little over two metres, so after any slam every place the brute could
    /// be struck from was burning: closing in to attack cost health on its own.
    /// </remarks>
    [TestFixture]
    public sealed class HazardPlacementTests
    {
        private const string AttackFolder = "Assets/_Project/ScriptableObjects/Attacks";

        /// <summary>The knight's longest opening swing, PlayerLight3's reach.</summary>
        private const float LongestOpeningSwing = 2.4f;

        /// <summary>The nearest a knight can stand: the two bodies touching.</summary>
        private const float Nearest = BossPrefabBuilder.HurtboxRadius + PlayerPrefabBuilder.HurtboxRadius;

        /// <summary>The furthest a knight's longest opening swing still lands from.</summary>
        private const float Furthest = BossPrefabBuilder.HurtboxRadius + LongestOpeningSwing;

        private static IEnumerable<AttackDefinition> ScarringAttacks()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AttackDefinition", new[] { AttackFolder }))
            {
                var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(AssetDatabase.GUIDToAssetPath(guid));

                if (attack != null && attack.LeavesHazard)
                {
                    yield return attack;
                }
            }
        }

        [Test]
        public void SomeBlowsStillScarTheFloor()
        {
            Assume.That(AssetDatabase.FindAssets("t:AttackDefinition", new[] { AttackFolder }).Length > 0,
                "The attack assets have not been generated.");

            Assert.IsNotEmpty(new List<AttackDefinition>(ScarringAttacks()), "No attack leaves a hazard any more.");
        }

        [Test]
        public void AKnightBesideOrBehindTheBossIsNeverInAScar()
        {
            foreach (AttackDefinition attack in ScarringAttacks())
            {
                Vector3 centre = attack.HazardCentre(Vector3.zero, Vector3.forward);

                foreach (float angle in new[] { 90f, -90f, 135f, -135f, 180f })
                {
                    for (float distance = Nearest; distance <= Furthest; distance += 0.1f)
                    {
                        Vector3 knight = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                        float gap = Vector3.Distance(knight, centre) - PlayerPrefabBuilder.HurtboxRadius;

                        Assert.Greater(gap, attack.HazardRadius,
                            $"{attack.name}: a knight {distance:0.0} m away at {angle} degrees stands in the scar.");
                    }
                }
            }
        }

        [Test]
        public void TheScarLiesAheadWhereTheBlowLanded()
        {
            foreach (AttackDefinition attack in ScarringAttacks())
            {
                Vector3 centre = attack.HazardCentre(new Vector3(4f, 0f, -2f), Vector3.right);

                Assert.Greater(centre.x, 4f + attack.HazardRadius * 0.5f, $"{attack.name}'s scar is not ahead of it.");
                Assert.AreEqual(-2f, centre.z, 0.001f, $"{attack.name}'s scar is off to one side.");
            }
        }
    }
}
