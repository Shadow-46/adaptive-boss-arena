using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Pins the control layout the player chose, in the generated input actions.
    /// </summary>
    /// <remarks>
    /// Mouse block and parry: left click attacks, Shift with left click is heavy, right mouse guards, Tab or the
    /// middle button locks on, and the mouse turns the camera. Every action also has a gamepad binding.
    /// </remarks>
    [TestFixture]
    public sealed class InputLayoutTests
    {
        private const string AssetPath = "Assets/_Project/Settings/PlayerControls.inputactions";

        private static readonly Regex Binding = new Regex(
            @"""path"":[ ]*""(?<path>[^""]*)""[^{}]*?""groups"":[ ]*""(?<groups>[^""]*)""[^{}]*?""action"":[ ]*""(?<action>[^""]*)""");

        private static (string Path, string Groups, string Action)[] Bindings()
        {
            Assume.That(File.Exists(AssetPath), "Setup has not generated the input actions.");

            return Binding.Matches(File.ReadAllText(AssetPath))
                .Cast<Match>()
                .Select(m => (m.Groups["path"].Value, m.Groups["groups"].Value, m.Groups["action"].Value))
                .ToArray();
        }

        [TestCase("LightAttack", "<Mouse>/leftButton")]
        [TestCase("Guard", "<Mouse>/rightButton")]
        [TestCase("Dash", "<Keyboard>/space")]
        [TestCase("Special", "<Keyboard>/e")]
        [TestCase("LockOn", "<Keyboard>/tab")]
        [TestCase("LockOn", "<Mouse>/middleButton")]
        [TestCase("Look", "<Mouse>/delta")]
        public void TheMouseLayoutIsBound(string action, string path)
        {
            Assert.IsTrue(Bindings().Any(b => b.Action == action && b.Path == path), $"{action} is not on {path}.");
        }

        [Test]
        public void HeavyIsShiftWithTheLeftButton()
        {
            var heavy = Bindings().Where(b => b.Action == "HeavyAttack").ToArray();

            Assert.IsTrue(heavy.Any(b => b.Path == "<Keyboard>/leftShift"), "Shift is not the heavy modifier.");
            Assert.IsTrue(heavy.Any(b => b.Path == "<Mouse>/leftButton"), "The heavy attack is not on left click.");
        }

        [TestCase("Move")]
        [TestCase("Look")]
        [TestCase("Dash")]
        [TestCase("Guard")]
        [TestCase("LightAttack")]
        [TestCase("HeavyAttack")]
        [TestCase("Special")]
        [TestCase("Heal")]
        [TestCase("LockOn")]
        public void EveryActionHasAGamepadBinding(string action)
        {
            Assert.IsTrue(Bindings().Any(b => b.Action == action && b.Groups.Contains("Gamepad")), $"{action} has no gamepad binding.");
        }
    }
}
