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
        public void EveryWeapon_PaysAPerfectReadWithACounter_QuickerAndHeavierThanItsFirstCut()
        {
            WeaponDefinition[] weapons = Weapons();
            Assume.That(weapons.Length, Is.GreaterThan(0), "Run setup to generate the weapons.");

            foreach (WeaponDefinition weapon in weapons)
            {
                AttackDefinition counter = weapon.CounterAttack;
                AttackDefinition opener = weapon.LightComboChain[0];

                Assert.IsNotNull(counter, $"{weapon.name} has no counter.");
                Assert.LessOrEqual(counter.StartupSeconds, opener.StartupSeconds, $"{weapon.name}'s counter is slower than its first cut.");
                Assert.Greater(counter.Damage, opener.Damage, $"{weapon.name}'s counter hits no harder than its first cut.");
                Assert.Greater(counter.PoiseDamage, opener.PoiseDamage, $"{weapon.name}'s counter breaks no more stance than its first cut.");
            }
        }

        [Test]
        public void EachWeapon_HasItsSignature()
        {
            WeaponDefinition blade = Weapons().FirstOrDefault(weapon => weapon.name == "WeaponBlade");
            WeaponDefinition greatsword = Weapons().FirstOrDefault(weapon => weapon.name == "WeaponGreatsword");
            WeaponDefinition energy = Weapons().FirstOrDefault(weapon => weapon.name == "WeaponEnergyBlade");
            Assume.That(blade != null && greatsword != null && energy != null, "Run setup to generate the weapons.");

            // The blade strikes out of the roll itself; the heavier weapons wait for its tail.
            Assert.IsNotNull(blade.DashAttack, "The blade has no dash-cut.");
            Assert.Greater(blade.DashAttack.LungeSpeed, 0f, "The dash-cut does not close the gap.");
            Assert.IsNull(greatsword.DashAttack);
            Assert.IsNull(energy.DashAttack);

            // The greatsword wades through blows; the energy blade's finisher reaches out as a wave.
            Assert.AreEqual(DefenceStyle.HyperArmour, greatsword.Defence);

            AttackDefinition finisher = energy.LightComboChain[energy.LightComboChain.Length - 1];
            Assert.Greater(finisher.EffectiveReach, 5f, "The energy blade's finisher does not reach as a wave.");
            Assert.IsTrue(finisher.ShowTelegraph, "The wave is not drawn, so a hit at range comes from nowhere.");
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
