using System;
using AdaptiveBossArena.Core.Combat;
using UnityEngine;

namespace AdaptiveBossArena.Combat.Vitals
{
    /// <summary>
    /// The hidden pool that decides whether a hit interrupts its target.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Poise is what makes trading blows a decision instead of a reflex. Because it regenerates
    /// while the owner is not being hit, scattered chip damage never staggers anything, but a
    /// committed combo does. The player therefore has to choose between poking safely and
    /// committing far enough to break the boss's stance.
    /// </para>
    /// <para>
    /// A break empties the pool and starts a recovery period during which no further poise damage
    /// accumulates. Without that, a combo landing during the stagger would chain breaks together and
    /// remove the target from the fight entirely.
    /// </para>
    /// </remarks>
    public sealed class PoisePool : IPoise
    {
        private readonly float _regenPerSecond;
        private readonly float _breakRecoverySeconds;

        private float _current;
        private float _maximum;
        private float _brokenFor;

        /// <summary>Creates a pool at full poise.</summary>
        /// <param name="maximum">Poise damage required to break the owner's stance. Must be positive.</param>
        /// <param name="regenPerSecond">Poise recovered per second while not being hit.</param>
        /// <param name="breakRecoverySeconds">How long a break lasts before poise starts rebuilding.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the maximum is not positive.</exception>
        public PoisePool(float maximum, float regenPerSecond, float breakRecoverySeconds)
        {
            if (maximum <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximum), maximum, "Maximum poise must be greater than zero.");
            }

            _maximum = maximum;
            _current = maximum;
            _regenPerSecond = Mathf.Max(0f, regenPerSecond);
            _breakRecoverySeconds = Mathf.Max(0f, breakRecoverySeconds);
        }

        /// <inheritdoc />
        public float Current => _current;

        /// <inheritdoc />
        public float Maximum => _maximum;

        /// <inheritdoc />
        public float Normalized => _maximum <= 0f ? 0f : Mathf.Clamp01(_current / _maximum);

        /// <inheritdoc />
        public bool IsBroken => _brokenFor > 0f;

        /// <inheritdoc />
        public event Action<ResourceChangedArgs> Changed;

        /// <inheritdoc />
        public event Action Broken;

        /// <inheritdoc />
        public bool ApplyPoiseDamage(float amount)
        {
            // Poise damage during a break is discarded rather than banked. Accumulating it would let
            // a combo landing on a staggered target chain straight into a second break.
            if (amount <= 0f || IsBroken)
            {
                return false;
            }

            float previous = _current;
            _current = Mathf.Max(0f, _current - amount);

            Changed?.Invoke(ResourceChangedArgs.Create(previous, _current, _maximum));

            if (_current > 0f)
            {
                return false;
            }

            _brokenFor = _breakRecoverySeconds;
            Broken?.Invoke();
            return true;
        }

        /// <summary>Advances break recovery, then poise regeneration.</summary>
        /// <param name="deltaTimeSeconds">Elapsed scaled time.</param>
        public void Tick(float deltaTimeSeconds)
        {
            if (deltaTimeSeconds <= 0f)
            {
                return;
            }

            if (_brokenFor > 0f)
            {
                _brokenFor -= deltaTimeSeconds;

                if (_brokenFor <= 0f)
                {
                    // Poise returns full at the end of a break rather than rebuilding from zero, so
                    // a recovered combatant is not immediately re-breakable by a single light hit.
                    _brokenFor = 0f;
                    SetCurrent(_maximum);
                }

                return;
            }

            if (_current < _maximum)
            {
                SetCurrent(Mathf.Min(_maximum, _current + _regenPerSecond * deltaTimeSeconds));
            }
        }

        /// <summary>Restores full poise and clears any break, for use when a fight restarts.</summary>
        public void ResetToFull()
        {
            _brokenFor = 0f;
            SetCurrent(_maximum);
        }

        private void SetCurrent(float value)
        {
            float previous = _current;
            _current = value;

            if (!Mathf.Approximately(previous, _current))
            {
                Changed?.Invoke(ResourceChangedArgs.Create(previous, _current, _maximum));
            }
        }
    }
}
