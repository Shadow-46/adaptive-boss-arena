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

        [TestCase("BossSweep")]
        [TestCase("BossJab")]
        [TestCase("BossPerilousOverhead")]
        [TestCase("BossDelayedOverhead")]
        [TestCase("BossSpinCleave")]
        [TestCase("BossCharge")]
        [TestCase("BossKick")]
        [TestCase("BossGrab")]
        [TestCase("BossLeapSmash")]
        public void TheBossPlansEachBlowFromAReachItsBodyHas(string attackName)
        {
            // The boss plans every swing from this range. Measured from a direct clip sample it kept each clip's
            // travel across the floor, which play discards, and planned a slide attack from over six metres - it
            // would have opened from out of reach and cut air. A great sword on this body covers under four and
            // a half metres, knight included.
            var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>($"{AttackFolder}/{attackName}.asset");

            Assert.IsNotNull(attack, attackName + " was not generated.");
            Assert.That(attack.Range, Is.InRange(1f, 4.5f),
                attackName + " is planned from a reach its body does not have.");
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
