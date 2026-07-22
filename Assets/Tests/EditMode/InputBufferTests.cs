using AdaptiveBossArena.Player.Controls;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests for the input buffer that carries a press across a moment the character could not act.
    /// </summary>
    /// <remarks>
    /// Buffering is invisible when correct and unmistakable when broken: the player concludes the
    /// game dropped their input. These tests pin the two properties that make it feel right — a
    /// press survives a short lockout, and a press is spent exactly once.
    /// </remarks>
    [TestFixture]
    public sealed class InputBufferTests
    {
        private const float Window = 0.15f;

        /// <summary>Scripted input source so the buffer can be exercised without a device.</summary>
        private sealed class FakePlayerInput : IPlayerInput
        {
            private PlayerInputAction? _pressedThisFrame;

            public Vector2 MoveDirection { get; set; }

            public bool IsEnabled { get; private set; } = true;

            public void Press(PlayerInputAction action) => _pressedThisFrame = action;

            public void ReleaseAll() => _pressedThisFrame = null;

            public bool WasPressedThisFrame(PlayerInputAction action) => _pressedThisFrame == action;

            public bool IsHeld(PlayerInputAction action) => _pressedThisFrame == action;

            public void SetEnabled(bool enabled) => IsEnabled = enabled;
        }

        [Test]
        public void FreshBuffer_HoldsNothing()
        {
            var buffer = new InputBuffer(Window);

            Assert.IsFalse(buffer.HasBuffered(PlayerInputAction.Dash, 0f));
            Assert.IsFalse(buffer.TryConsume(PlayerInputAction.Dash, 0f));
        }

        [Test]
        public void PressWithinWindow_CanBeConsumed()
        {
            var buffer = new InputBuffer(Window);
            buffer.Record(PlayerInputAction.Dash, 10f);

            Assert.IsTrue(buffer.TryConsume(PlayerInputAction.Dash, 10f + Window * 0.5f));
        }

        [Test]
        public void PressAtExactlyTheWindowEdge_IsStillValid()
        {
            var buffer = new InputBuffer(Window);
            buffer.Record(PlayerInputAction.Dash, 10f);

            // An off-by-one at the boundary is the difference between a forgiving buffer and one
            // that drops a press the player believes they made in time.
            Assert.IsTrue(buffer.TryConsume(PlayerInputAction.Dash, 10f + Window));
        }

        [Test]
        public void PressBeyondTheWindow_HasExpired()
        {
            var buffer = new InputBuffer(Window);
            buffer.Record(PlayerInputAction.Dash, 10f);

            Assert.IsFalse(buffer.TryConsume(PlayerInputAction.Dash, 10f + Window + 0.01f));
        }

        [Test]
        public void Consuming_SpendsThePressExactlyOnce()
        {
            var buffer = new InputBuffer(Window);
            buffer.Record(PlayerInputAction.Dash, 10f);

            Assert.IsTrue(buffer.TryConsume(PlayerInputAction.Dash, 10f));

            // Two states acting on one press would let a single tap fire a dash and an attack.
            Assert.IsFalse(buffer.TryConsume(PlayerInputAction.Dash, 10f));
        }

        [Test]
        public void HasBuffered_DoesNotSpendThePress()
        {
            var buffer = new InputBuffer(Window);
            buffer.Record(PlayerInputAction.Dash, 10f);

            Assert.IsTrue(buffer.HasBuffered(PlayerInputAction.Dash, 10f));
            Assert.IsTrue(buffer.TryConsume(PlayerInputAction.Dash, 10f));
        }

        [Test]
        public void ActionsAreBufferedIndependently()
        {
            var buffer = new InputBuffer(Window);
            buffer.Record(PlayerInputAction.Dash, 10f);
            buffer.Record(PlayerInputAction.HeavyAttack, 10f);

            Assert.IsTrue(buffer.TryConsume(PlayerInputAction.Dash, 10f));
            Assert.IsTrue(buffer.TryConsume(PlayerInputAction.HeavyAttack, 10f));
        }

        [Test]
        public void RepeatedPresses_KeepOnlyTheMostRecent()
        {
            var buffer = new InputBuffer(Window);

            buffer.Record(PlayerInputAction.LightAttack, 10f);
            buffer.Record(PlayerInputAction.LightAttack, 10.05f);

            Assert.IsTrue(buffer.TryConsume(PlayerInputAction.LightAttack, 10.05f));

            // Queuing presses would let a mashed button drive the character on its own after a long
            // animation. Only one press is ever held per action.
            Assert.IsFalse(buffer.TryConsume(PlayerInputAction.LightAttack, 10.05f));
        }

        [Test]
        public void Clear_DiscardsEverything()
        {
            var buffer = new InputBuffer(Window);
            buffer.Record(PlayerInputAction.Dash, 10f);
            buffer.Record(PlayerInputAction.Special, 10f);

            buffer.Clear();

            Assert.IsFalse(buffer.HasBuffered(PlayerInputAction.Dash, 10f));
            Assert.IsFalse(buffer.HasBuffered(PlayerInputAction.Special, 10f));
        }

        [Test]
        public void Discard_RemovesOneActionOnly()
        {
            var buffer = new InputBuffer(Window);
            buffer.Record(PlayerInputAction.Dash, 10f);
            buffer.Record(PlayerInputAction.Heal, 10f);

            buffer.Discard(PlayerInputAction.Dash);

            Assert.IsFalse(buffer.HasBuffered(PlayerInputAction.Dash, 10f));
            Assert.IsTrue(buffer.HasBuffered(PlayerInputAction.Heal, 10f));
        }

        [Test]
        public void RecordPressesFrom_CapturesEveryActionFiredThisFrame()
        {
            var buffer = new InputBuffer(Window);
            var input = new FakePlayerInput();

            input.Press(PlayerInputAction.Special);
            buffer.RecordPressesFrom(input, 5f);

            Assert.IsTrue(buffer.HasBuffered(PlayerInputAction.Special, 5f));
            Assert.IsFalse(buffer.HasBuffered(PlayerInputAction.Dash, 5f));
        }

        [Test]
        public void RecordPressesFrom_WithNothingPressed_RecordsNothing()
        {
            var buffer = new InputBuffer(Window);
            var input = new FakePlayerInput();

            input.ReleaseAll();
            buffer.RecordPressesFrom(input, 5f);

            Assert.IsFalse(buffer.HasBuffered(PlayerInputAction.Dash, 5f));
        }
    }
}
