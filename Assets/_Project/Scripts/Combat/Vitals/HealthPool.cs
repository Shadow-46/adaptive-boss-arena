using System;
using AdaptiveBossArena.Core.Combat;
using UnityEngine;

namespace AdaptiveBossArena.Combat.Vitals
{
    /// <summary>
    /// A health pool with change and death notification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain class rather than a component, and deliberately placed in the Combat assembly so the
    /// player and the boss share one implementation. Health that behaves subtly differently for the
    /// two combatants is a class of bug that only shows up in edge cases such as simultaneous
    /// lethal hits.
    /// </para>
    /// <para>
    /// The pool knows nothing about invincibility frames, perfect dodges or resistances. Those are
    /// decisions about whether a hit should land at all, and they belong to the owner's
    /// <see cref="IDamageable"/> implementation. This type only moves the number.
    /// </para>
    /// </remarks>
    public sealed class HealthPool : IHealth
    {
        private readonly CombatantTeam _team;
        private float _current;
        private float _maximum;

        /// <summary>Creates a pool filled to its maximum.</summary>
        /// <param name="maximum">Capacity of the pool. Must be positive.</param>
        /// <param name="team">Team the owner fights for, reported with the death event.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the maximum is not positive.</exception>
        public HealthPool(float maximum, CombatantTeam team)
        {
            if (maximum <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximum), maximum, "Maximum health must be greater than zero.");
            }

            _maximum = maximum;
            _current = maximum;
            _team = team;
        }

        /// <inheritdoc />
        public float Current => _current;

        /// <inheritdoc />
        public float Maximum => _maximum;

        /// <inheritdoc />
        public float Normalized => _maximum <= 0f ? 0f : Mathf.Clamp01(_current / _maximum);

        /// <inheritdoc />
        public bool IsAlive => _current > 0f;

        /// <inheritdoc />
        public event Action<ResourceChangedArgs> Changed;

        /// <inheritdoc />
        public event Action<DeathArgs> Died;

        /// <summary>
        /// Removes health and reports how much was actually taken.
        /// </summary>
        /// <param name="amount">Health to remove. Non-positive values are ignored.</param>
        /// <param name="cause">The hit responsible, forwarded to the death event for attribution.</param>
        /// <returns>Health actually removed after clamping at zero.</returns>
        public float Reduce(float amount, in DamageInfo cause)
        {
            if (amount <= 0f || !IsAlive)
            {
                return 0f;
            }

            float previous = _current;
            _current = Mathf.Max(0f, _current - amount);
            float applied = previous - _current;

            Changed?.Invoke(ResourceChangedArgs.Create(previous, _current, _maximum));

            // Death is raised after the change notification so that a health bar has already been
            // updated to zero by the time a death screen reacts.
            if (_current <= 0f)
            {
                Died?.Invoke(new DeathArgs
                {
                    Team = _team,
                    KillerInstanceId = cause.SourceInstanceId,
                    FinalBlow = cause
                });
            }

            return applied;
        }

        /// <inheritdoc />
        public float Heal(float amount)
        {
            // Healing the dead would silently revive a combatant whose death has already been
            // announced, leaving listeners in an inconsistent state.
            if (amount <= 0f || !IsAlive)
            {
                return 0f;
            }

            float previous = _current;
            _current = Mathf.Min(_maximum, _current + amount);
            float restored = _current - previous;

            if (restored > 0f)
            {
                Changed?.Invoke(ResourceChangedArgs.Create(previous, _current, _maximum));
            }

            return restored;
        }

        /// <summary>Refills the pool, for example when a fight restarts.</summary>
        public void ResetToFull()
        {
            float previous = _current;
            _current = _maximum;
            Changed?.Invoke(ResourceChangedArgs.Create(previous, _current, _maximum));
        }

        /// <summary>Changes capacity, scaling the current amount to preserve the fill fraction.</summary>
        /// <param name="maximum">New capacity. Must be positive.</param>
        public void SetMaximum(float maximum)
        {
            if (maximum <= 0f)
            {
                return;
            }

            float fraction = Normalized;
            float previous = _current;

            _maximum = maximum;
            _current = _maximum * fraction;

            Changed?.Invoke(ResourceChangedArgs.Create(previous, _current, _maximum));
        }
    }
}
