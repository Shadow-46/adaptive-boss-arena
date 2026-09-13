using AdaptiveBossArena.Combat;
using NUnit.Framework;
using UnityEditor;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Pins which attacks still draw a warning on the ground.
    /// </summary>
    /// <remarks>
    /// With animated bodies, ordinary swings are read from the boss itself, and a wedge under every one
    /// of them would teach the player to watch the floor instead. Only attacks that strike the ground or
    /// refuse a guard keep a ground warning, because those are the ones whose area or rule the body alone
    /// cannot show.
    /// </remarks>
    [TestFixture]
    public sealed class TelegraphAssetTests
    {
        private static AttackDefinition Load(string name) =>
            AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/_Project/ScriptableObjects/Attacks/" + name + ".asset");

        [TestCase("BossSlam")]
        [TestCase("BossShockwave")]
        [TestCase("BossPhaseShockwave")]
        [TestCase("BossPerilousOverhead")]
        public void GroundStrikesAndUnblockablesKeepTheirWarning(string attack)
        {
            Assert.IsTrue(Load(attack).ShowTelegraph, attack + " lost its ground warning.");
        }

        [TestCase("BossSweep")]
        [TestCase("BossCharge")]
        [TestCase("BossJab")]
        [TestCase("PlayerLight1")]
        [TestCase("PlayerHeavy")]
        public void OrdinarySwingsAreReadFromTheBody(string attack)
        {
            Assert.IsFalse(Load(attack).ShowTelegraph, attack + " still draws a ground warning.");
        }
    }
}
