using AdaptiveBossArena.Game;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests when the fight holds the mouse and when a menu gets it back.
    /// </summary>
    [TestFixture]
    public sealed class CursorLockTests
    {
        [Test]
        public void TheFightHoldsThePointer()
        {
            Assert.IsTrue(CursorLock.ShouldCapture(paused: false, outcomeShown: false, settingsOpen: false));
        }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public void AnyMenuGetsThePointerBack(bool paused, bool outcomeShown, bool settingsOpen)
        {
            Assert.IsFalse(CursorLock.ShouldCapture(paused, outcomeShown, settingsOpen));
        }
    }
}
