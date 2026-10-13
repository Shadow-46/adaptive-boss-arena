using AdaptiveBossArena.Combat;
using NUnit.Framework;
using UnityEditor;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests that standing stone stops the blasts thrown along the floor, and never a blade.
    /// </summary>
    /// <remarks>
    /// Cover is the player's answer to the waves. Extended to a swing, a pillar would make every blow of the brute's
    /// sword something to hide from, which is not a fight the blade work was built for.
    /// </remarks>
    [TestFixture]
    public sealed class ShockwaveCoverTests
    {
        private const string Folder = "Assets/_Project/ScriptableObjects/Attacks/";

        [TestCase("BossShockwave", true)]
        [TestCase("BossPhaseShockwave", true)]
        [TestCase("BossEdict", true)]
        [TestCase("BossThrust", false)]
        [TestCase("BossSweep", false)]
        [TestCase("BossSlam", false)]
        [TestCase("BossCharge", false)]
        public void OnlyTheWavesAreStoppedByStone(string attackName, bool blocked)
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(Folder + attackName + ".asset");
            Assume.That(attack != null, "The attack assets have not been generated.");

            Assert.AreEqual(blocked, attack.BlockedByCover);
        }
    }
}
