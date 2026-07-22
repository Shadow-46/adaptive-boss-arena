using System;
using AdaptiveBossArena.Core.Constants;
using UnityEngine;

namespace AdaptiveBossArena.Player.Controls
{
    /// <summary>
    /// Remembers recent action presses so they can be replayed the moment the character is free.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the single highest-value component in the project for perceived responsiveness, and
    /// it is invisible when it works. Without it, an attack pressed one frame before a dash recovery
    /// ends is discarded, and the player is right to conclude the game dropped their input. A
    /// buffer of around 150 milliseconds makes the character feel like it was waiting for the
    /// command rather than ignoring it.
    /// </para>
    /// <para>
    /// A press is stored per action rather than as a queue. Queuing lets a mashed button spend
    /// several presses in sequence after a long animation, which reads as the character acting on
    /// its own. Keeping only the most recent press per action means the buffer forgives late timing
    /// without ever taking over.
    /// </para>
    /// <para>
    /// Consumption is explicit: the state that acts on a buffered input clears it, so no two states
    /// can act on the same press.
    /// </para>
    /// </remarks>
    public sealed class InputBuffer
    {
        /// <summary>Sentinel meaning an action has never been pressed.</summary>
        private const float NeverPressed = float.NegativeInfinity;

        private readonly float _windowSeconds;
        private readonly float[] _pressTimes;

        /// <summary>Creates a buffer.</summary>
        /// <param name="windowSeconds">
        /// How long a press stays valid. Defaults to
        /// <see cref="GameplayConstants.InputBufferSeconds"/>.
        /// </param>
        public InputBuffer(float windowSeconds = GameplayConstants.InputBufferSeconds)
        {
            _windowSeconds = Mathf.Max(0f, windowSeconds);

            int actionCount = Enum.GetValues(typeof(PlayerInputAction)).Length;
            _pressTimes = new float[actionCount];

            Clear();
        }

        /// <summary>How long a press remains valid, in seconds.</summary>
        public float WindowSeconds => _windowSeconds;

        /// <summary>Records that an action was pressed.</summary>
        /// <param name="action">The action pressed.</param>
        /// <param name="timeNow">Current combat-clock time.</param>
        public void Record(PlayerInputAction action, float timeNow) => _pressTimes[(int)action] = timeNow;

        /// <summary>Polls every action from an input source and records any that fired this frame.</summary>
        /// <param name="input">Input source to poll.</param>
        /// <param name="timeNow">Current combat-clock time.</param>
        public void RecordPressesFrom(IPlayerInput input, float timeNow)
        {
            for (int i = 0; i < _pressTimes.Length; i++)
            {
                var action = (PlayerInputAction)i;

                if (input.WasPressedThisFrame(action))
                {
                    _pressTimes[i] = timeNow;
                }
            }
        }

        /// <summary>Tests whether an action is buffered without consuming it.</summary>
        /// <param name="action">The action to test.</param>
        /// <param name="timeNow">Current combat-clock time.</param>
        /// <returns>True when a press is still inside the window.</returns>
        public bool HasBuffered(PlayerInputAction action, float timeNow) =>
            timeNow - _pressTimes[(int)action] <= _windowSeconds;

        /// <summary>
        /// Consumes a buffered action if one is available.
        /// </summary>
        /// <param name="action">The action to consume.</param>
        /// <param name="timeNow">Current combat-clock time.</param>
        /// <returns>True when a press was available and has now been spent.</returns>
        public bool TryConsume(PlayerInputAction action, float timeNow)
        {
            if (!HasBuffered(action, timeNow))
            {
                return false;
            }

            _pressTimes[(int)action] = NeverPressed;
            return true;
        }

        /// <summary>Discards a buffered press without acting on it.</summary>
        /// <param name="action">The action to discard.</param>
        public void Discard(PlayerInputAction action) => _pressTimes[(int)action] = NeverPressed;

        /// <summary>
        /// Discards every buffered press.
        /// </summary>
        /// <remarks>
        /// Called on death and on unpause. Replaying inputs pressed before a pause would have the
        /// character lurch into action the instant play resumes.
        /// </remarks>
        public void Clear()
        {
            for (int i = 0; i < _pressTimes.Length; i++)
            {
                _pressTimes[i] = NeverPressed;
            }
        }
    }
}
