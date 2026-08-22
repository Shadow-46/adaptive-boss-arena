using AdaptiveBossArena.Combat;
using UnityEditor;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Asserts that the three weapons actually defend differently in the shipped data.
    /// </summary>
    /// <remarks>
    /// The rules that make the weapons distinct are covered by <see cref="DefenceResolverTests"/>,
    /// but a rule applied to identical numbers produces identical weapons. These values reached the
    /// runtime through a property that read the character config instead of the drawn weapon, so
    /// every deflect paid out the same 28 posture and the authored differences were dead data — a
    /// failure invisible from code, since both paths compiled and both returned a plausible number.
    /// </remarks>
    [TestFixture]
    public sealed class WeaponDefenceTests
    {
        private const string WeaponFolder = "Assets/_Project/ScriptableObjects/Weapons";

        private static WeaponDefinition Load(string assetName)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                $"{WeaponFolder}/{assetName}.asset");

            Assert.IsNotNull(weapon, $"Generated weapon '{assetName}' is missing.");

            return weapon;
        }

        [Test]
        public void EachWeaponDeflectsForItsOwnPosture()
        {
            Assert.AreEqual(28f, Load("WeaponBlade").DeflectPostureDamage, 0.001f);
            Assert.AreEqual(16f, Load("WeaponEnergyBlade").DeflectPostureDamage, 0.001f);
        }

        [Test]
        public void TheGreatswordCannotDeflectAtAll()
        {
            WeaponDefinition greatsword = Load("WeaponGreatsword");

            // Its zero posture payout is consistent rather than a hole: hyper-armour has no deflect
            // branch to reach, so the number is never read. Asserting both together is what stops
            // someone "fixing" the zero and creating a weapon that parries for nothing.
            Assert.IsFalse(greatsword.CanDeflect);
            Assert.AreEqual(0f, greatsword.DeflectPostureDamage, 0.001f);
        }

        [Test]
        public void TheTwoParryingWeaponsTradeWindowAgainstReward()
        {
            WeaponDefinition blade = Load("WeaponBlade");
            WeaponDefinition energy = Load("WeaponEnergyBlade");

            // The trade that makes the choice mean something: the energy blade forgives worse timing
            // and pays less for it. If either half of that inverted, one weapon would be strictly
            // better on defence.
            Assert.Greater(energy.DeflectWindowSeconds, blade.DeflectWindowSeconds);
            Assert.Less(energy.DeflectPostureDamage, blade.DeflectPostureDamage);
        }
    }
}
