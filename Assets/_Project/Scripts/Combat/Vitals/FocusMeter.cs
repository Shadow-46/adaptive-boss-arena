using System;
using AdaptiveBossArena.Core.Combat;
using UnityEngine;

namespace AdaptiveBossArena.Combat.Vitals
{
    /// <summary>
    /// A meter that fills as the player defends well and is spent to empower a special.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It rewards the game's central act — turning the boss's attacks aside — rather than dealing
    /// damage: a clean deflect and a perfect dodge both build focus, so the payoff for reading the
    /// boss is a stronger answer of your own. It empties entirely when the player is staggered, so
    /// focus is composure held under pressure, not a bar that only ever goes up.
    /// </para>
    /// <para>
    /// Pure like the other vitals (<see cref="StaminaPool"/>, <see cref="PoisePool"/>) so its filling,
    /// spending and loss can be pinned by an edit-mode test, and it starts <em>empty</em> because it
    /// is earned within a fight rather than granted at its start.
    /// </para>
    /// </remarks>
    public sealed class FocusMeter
    {
        private readonly float _maximum;
        private readonly float _gainPerDeflect;
        private readonly float _gainPerPerfectDodge;

        private float _current;

        /// <summary>Creates an empty meter.</summary>
        /// <param name="maximum">Focus needed to empower a special. Must be positive.</param>
        /// <param name="gainPerDeflect">Focus gained from a clean deflect.</param>
        /// <param name="gainPerPerfectDodge">Focus gained from a perfect dodge.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the maximum is not positive.</exception>
        public FocusMeter(float maximum, float gainPerDeflect, float gainPerPerfectDodge)
        {
            if (maximum <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximum), maximum, "Maximum focus must be greater than zero.");
            }

            _maximum = maximum;
            _gainPerDeflect = Mathf.Max(0f, gainPerDeflect);
            _gainPerPerfectDodge = Mathf.Max(0f, gainPerPerfectDodge);
            _current = 0f;
        }

        /// <summary>Current focus.</summary>
        public float Current => _current;

        /// <summary>Focus needed to empower a special.</summary>
        public float Maximum => _maximum;

        /// <summary>Current focus as a fraction of the maximum.</summary>
        public float Normalized => _maximum <= 0f ? 0f : Mathf.Clamp01(_current / _maximum);

        /// <summary>True when a special can be empowered.</summary>
        public bool IsFull => _current >= _maximum;

        /// <summary>Raised whenever the focus value changes, for the heads-up display.</summary>
        public event Action<ResourceChangedArgs> Changed;

        /// <summary>Adds the deflect reward.</summary>
        public void AddFromDeflect() => Add(_gainPerDeflect);

        /// <summary>Adds the perfect-dodge reward.</summary>
        public void AddFromPerfectDodge() => Add(_gainPerPerfectDodge);

        /// <summary>
        /// Spends a full meter, reporting whether there was one to spend.
        /// </summary>
        /// <returns>True when the meter was full and has now been emptied.</returns>
        public bool TryConsumeFull()
        {
            if (_current < _maximum)
            {
                return false;
            }

            Set(0f);
            return true;
        }

        /// <summary>Empties the meter, for a stagger or a fresh attempt.</summary>
        public void Reset() => Set(0f);

        /// <summary>Adds focus, clamped to the maximum, raising the change.</summary>
        private void Add(float amount)
        {
            if (amount <= 0f || _current >= _maximum)
            {
                return;
            }

            Set(Mathf.Min(_maximum, _current + amount));
        }

        /// <summary>Sets the value and reports the change when it actually moved.</summary>
        private void Set(float value)
        {
            if (Mathf.Approximately(value, _current))
            {
                return;
            }

            float previous = _current;
            _current = value;

            Changed?.Invoke(ResourceChangedArgs.Create(previous, _current, _maximum));
        }
    }
}
