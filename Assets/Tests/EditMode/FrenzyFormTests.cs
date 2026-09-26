using System.Linq;
using AdaptiveBossArena.Combat.Feel;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Asserts the brute's bare-handed final form can play everything its armed form can.
    /// </summary>
    /// <remarks>
    /// The frenzy is a second controller swapped onto the same rig mid-fight, and nothing that drives it knows
    /// which one is playing: the bridge asks for a state by name either way. A state missing from the frenzy
    /// would therefore not fail to compile or to generate - it would simply leave the boss standing still in
    /// the last quarter of the fight, which is exactly where it can least afford to.
    /// </remarks>
    [TestFixture]
    public sealed class FrenzyFormTests
    {
        private const string BossAnimationConfig =
            "Assets/_Project/ScriptableObjects/Config/DefaultBossAnimation.asset";

        [Test]
        public void TheFrenzyCanPlayEveryStateTheArmedFormCan()
        {
            var config = AssetDatabase.LoadAssetAtPath<CharacterAnimationConfig>(BossAnimationConfig);
            Assert.IsNotNull(config, "The boss animation config is missing.");

            var armed = config.AnimatorController as AnimatorController;
            var frenzied = config.FrenzyController as AnimatorController;

            // Absent on a checkout without the licensed art, where the brute has no second form at all.
            Assume.That(armed != null && frenzied != null, "The brute is not rigged in this checkout.");

            string[] armedStates = StateNames(armed);
            string[] frenziedStates = StateNames(frenzied);

            Assert.IsNotEmpty(armedStates, "The armed controller has no states.");

            CollectionAssert.IsSubsetOf(
                armedStates, frenziedStates,
                "The frenzy cannot play every state the armed brute can, so it would stand still on those moves.");
        }

        [Test]
        public void TheFrenzyIsADifferentBodyRatherThanTheSameOneRenamed()
        {
            var config = AssetDatabase.LoadAssetAtPath<CharacterAnimationConfig>(BossAnimationConfig);
            var armed = config != null ? config.AnimatorController as AnimatorController : null;
            var frenzied = config != null ? config.FrenzyController as AnimatorController : null;

            Assume.That(armed != null && frenzied != null, "The brute is not rigged in this checkout.");
            Assert.AreNotSame(armed, frenzied, "The frenzy is the armed controller, so nothing changes at the swap.");

            // The whole point of the last phase: it is not swinging the sword any more.
            Assert.AreNotEqual(
                MotionName(armed, "Heavy"), MotionName(frenzied, "Heavy"),
                "The frenzy's heavy swing is the armed one, so it fights the same way bare-handed.");
        }

        private static string[] StateNames(AnimatorController controller) =>
            controller.layers[0].stateMachine.states.Select(child => child.state.name).ToArray();

        private static string MotionName(AnimatorController controller, string state) =>
            controller.layers[0].stateMachine.states
                .Where(child => child.state.name == state)
                .Select(child => child.state.motion != null ? child.state.motion.name : null)
                .FirstOrDefault();
    }
}
