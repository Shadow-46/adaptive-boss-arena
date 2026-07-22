using System;
using AdaptiveBossArena.Core.Combat;
using UnityEngine;

namespace AdaptiveBossArena.Combat.Vitals
{
    /// <summary>
    /// A stamina pool that regenerates after a pause following each spend.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The regeneration delay is the whole design of this type. Without it, stamina merely slows
    /// dashing down; with it, spending stamina carries a commitment, because a dash taken now is a
    /// dash unavailable in the second that follows. That is what makes evasion a resource decision
    /// rather than a reflex.
    /// </para>
    /// <para>
    /// It also refuses to print a failure at the player. A dash with insufficient stamina simply
    /// does not happen; the feedback is the character not moving, not a message.
    /// </para>
    /// </remarks>
    public sealed class StaminaPool : IStaminaPool
    {
        private readonly float _regenPerSecond;
        private readonly float _regenDelaySeconds;

        private float _current;
        private float _maximum;
        private float _regenBlockedFor;

        /// <summary>Creates a pool filled to its maximum.</summary>
        /// <param name="maximum">Capacity of the pool. Must be positive.</param>
        /// <param name="regenPerSecond">Amount restored per second once regeneration resumes.</param>
        /// <param name="regenDelaySeconds">Pause after a spend before regeneration resumes.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the maximum is not positive.</exception>
        public StaminaPool(float maximum, float regenPerSecond, float regenDelaySeconds)
        {
            if (maximum <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximum), maximum, "Maximum stamina must be greater than zero.");
            }

            _maximum = maximum;
            _current = maximum;
            _regenPerSecond = Mathf.Max(0f, regenPerSecond);
            _regenDelaySeconds = Mathf.Max(0f, regenDelaySeconds);
        }

        /// <inheritdoc />
        public float Current => _current;

        /// <inheritdoc />
        public float Maximum => _maximum;

        /// <inheritdoc />
        public float Normalized => _maximum <= 0f ? 0f : Mathf.Clamp01(_current / _maximum);

        /// <summary>True while the post-spend pause is still running.</summary>
        public bool IsRegenerationBlocked => _regenBlockedFor > 0f;

        /// <inheritdoc />
        public event Action<ResourceChangedArgs> Changed;

        /// <inheritdoc />
        public bool CanSpend(float amount) => amount <= 0f || _current >= amount;

        /// <inheritdoc />
        public bool TrySpend(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (_current < amount)
            {
                return false;
            }

            float previous = _current;
            _current -= amount;
            _regenBlockedFor = _regenDelaySeconds;

            Changed?.Invoke(ResourceChangedArgs.Create(previous, _current, _maximum));
            return true;
        }

        /// <inheritdoc />
        public void Restore(float amount)
        {
            if (amount <= 0f || _current >= _maximum)
            {
                return;
            }

            float previous = _current;
            _current = Mathf.Min(_maximum, _current + amount);

            Changed?.Invoke(ResourceChangedArgs.Create(previous, _current, _maximum));
        }

        /// <summary>
        /// Advances the regeneration pause and then regeneration itself.
        /// </summary>
        /// <param name="deltaTimeSeconds">
        /// Elapsed scaled time, so that stamina does not quietly refill during a hit-stop.
        /// </param>
        public void Tick(float deltaTimeSeconds)
        {
            if (deltaTimeSeconds <= 0f)
            {
                return;
            }

            if (_regenBlockedFor > 0f)
            {
                _regenBlockedFor -= deltaTimeSeconds;

                // The pause and the regeneration it gates never run in the same step, which keeps
                // the delay honest rather than letting a long frame consume both.
                return;
            }

            Restore(_regenPerSecond * deltaTimeSeconds);
        }

        /// <summary>Refills the pool and clears any pending regeneration pause.</summary>
        public void ResetToFull()
        {
            float previous = _current;
            _current = _maximum;
            _regenBlockedFor = 0f;

            Changed?.Invoke(ResourceChangedArgs.Create(previous, _current, _maximum));
        }
    }
}
