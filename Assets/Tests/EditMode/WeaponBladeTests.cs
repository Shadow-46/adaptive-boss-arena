using System.Linq;
using AdaptiveBossArena.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests that each weapon strikes with the blade the player sees: the model held at the grip, pointing down the
    /// blade, and the strike span fitted to it.
    /// </summary>
    /// <remarks>
    /// Written after the greatsword was found striking with a 0.95 m reach shared by every weapon while its blade
    /// was drawn 1.5 m long: a third of what the player saw swing could not hit anything.
    /// </remarks>
    [TestFixture]
    public sealed class WeaponBladeTests
    {
        private const string WeaponFolder = "Assets/_Project/ScriptableObjects/Weapons";

        private static WeaponDefinition[] Weapons() =>
            AssetDatabase.FindAssets("t:WeaponDefinition", new[] { WeaponFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(weapon => weapon != null)
                .ToArray();

        [Test]
        public void EveryWeaponHasABladeSpan()
        {
            WeaponDefinition[] weapons = Weapons();
            Assume.That(weapons.Length, Is.GreaterThan(0), "Run setup to generate the weapons.");

            foreach (WeaponDefinition weapon in weapons)
            {
                Assert.Greater(weapon.BladeTip, weapon.BladeBase, weapon.name);
                Assert.GreaterOrEqual(weapon.BladeBase, 0f, weapon.name);
            }
        }

        [Test]
        public void TheGreatswordReachesFurtherThanTheBlade()
        {
            WeaponDefinition blade = Weapons().FirstOrDefault(weapon => weapon.name == "WeaponBlade");
            WeaponDefinition greatsword = Weapons().FirstOrDefault(weapon => weapon.name == "WeaponGreatsword");
            Assume.That(blade != null && greatsword != null, "Run setup to generate the weapons.");

            Assert.Greater(greatsword.BladeTip, blade.BladeTip + 0.3f);
        }

        [Test]
        public void EachModelIsHeldAtTheGripAndPointsDownItsSpan()
        {
            foreach (WeaponDefinition weapon in Weapons().Where(weapon => weapon.ModelPrefab != null))
            {
                Bounds bounds = BoundsInModelSpace(weapon.ModelPrefab);

                Assert.AreEqual(weapon.BladeTip, bounds.max.z, weapon.BladeTip * 0.12f,
                    $"{weapon.name}: the model's point is not where its blows land.");
                Assert.Less(bounds.min.z, 0.05f, $"{weapon.name}: the grip is not in the hand.");
                Assert.Greater(bounds.size.z, bounds.size.x, $"{weapon.name}: the blade does not run along the mount.");
            }
        }

        private static Bounds BoundsInModelSpace(GameObject prefab)
        {
            var instance = (GameObject)Object.Instantiate(prefab);

            try
            {
                Bounds? total = null;

                foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    foreach (Vector3 vertex in filter.sharedMesh.vertices)
                    {
                        Vector3 point = instance.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));

                        if (total == null)
                        {
                            total = new Bounds(point, Vector3.zero);
                        }
                        else
                        {
                            Bounds grown = total.Value;
                            grown.Encapsulate(point);
                            total = grown;
                        }
                    }
                }

                return total ?? new Bounds();
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
