using AdaptiveBossArena.Combat;
using NUnit.Framework;
using UnityEditor;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Asserts each generated attack strikes with the part of the body its clip swings.
    /// </summary>
    /// <remarks>
    /// An attack left with no striking part silently falls back to the invisible wedge in front of the attacker,
    /// which is exactly the "it hurt me but never touched me" the player reported - and nothing else would notice.
    /// The first version of this table lost every combined or non-first flag to an asset writer that stored an
    /// enum's position rather than its value, and only the plain weapon attacks came through.
    /// </remarks>
    [TestFixture]
    public sealed class AttackStrikerTests
    {
        private const string AttackFolder = "Assets/_Project/ScriptableObjects/Attacks";

        [TestCase("PlayerLight1", StrikerParts.Weapon)]
        [TestCase("PlayerHeavy", StrikerParts.Weapon)]
        [TestCase("PlayerExecution", StrikerParts.Weapon)]
        [TestCase("BossSweep", StrikerParts.Weapon)]
        [TestCase("BossSpinCleave", StrikerParts.Weapon)]
        [TestCase("BossKick", StrikerParts.RightFoot)]
        [TestCase("BossGrab", StrikerParts.RightHand | StrikerParts.LeftHand)]
        [TestCase("BossLeapSmash", StrikerParts.Body)]
        [TestCase("BossCharge", StrikerParts.Weapon | StrikerParts.Body)]
        public void TheAttackStrikesWithThePartItsClipSwings(string attackName, StrikerParts expected)
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>($"{AttackFolder}/{attackName}.asset");

            Assert.IsNotNull(attack, attackName + " was not generated.");
            Assert.AreEqual(expected, attack.Strikers, attackName + " strikes with the wrong part of the body.");
        }

        [TestCase("BossSlam")]
        [TestCase("BossShockwave")]
        [TestCase("BossPhaseShockwave")]
        [TestCase("PlayerSpecial")]
        public void AnAreaAttackStaysOnTheAreaDrawnForIt(string attackName)
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>($"{AttackFolder}/{attackName}.asset");

            Assert.IsNotNull(attack, attackName + " was not generated.");
            Assert.AreEqual(StrikerParts.None, attack.Strikers,
                attackName + " is an area attack and should be decided by the area on the floor, not a limb.");
        }
    }
}
