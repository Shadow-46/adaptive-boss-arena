using AdaptiveBossArena.Core.Combat;
using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// A volume on a combatant that can receive hits and forwards them to its owner.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Separating the receiving volume from the combatant lets one character carry several, each
    /// with its own multiplier. That is how a boss gets a weak point worth aiming at without the
    /// damage pipeline learning anything about anatomy.
    /// </para>
    /// <para>
    /// The owner is resolved once at initialisation by walking up the hierarchy. Resolving per hit
    /// would repeat that search during the busiest moments of combat, and caching it means a
    /// misconfigured prefab reports itself at startup rather than on first contact.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class Hurtbox : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Damage scaling for hits landing here. Above one marks a weak point; below one " +
                 "marks armour.")]
        [Range(0.1f, 4f)]
        private float _damageMultiplier = 1f;

        private IDamageable _owner;

        /// <summary>The combatant this volume belongs to.</summary>
        public IDamageable Owner => _owner;

        /// <summary>Damage scaling applied to hits landing here.</summary>
        public float DamageMultiplier => _damageMultiplier;

        /// <summary>True when this volume is wired to a combatant.</summary>
        public bool IsValid => _owner != null;

        private void Awake()
        {
            _owner = GetComponentInParent<IDamageable>();

            if (_owner == null)
            {
                Debug.LogError(
                    $"[Adaptive Boss Arena] Hurtbox on '{name}' found no IDamageable in its parents. " +
                    "Hits landing here will be discarded.",
                    this);
            }
        }

        /// <summary>
        /// Offers a hit to the owning combatant, scaled by this volume's multiplier.
        /// </summary>
        /// <param name="damage">The incoming hit.</param>
        /// <returns>
        /// The owner's decision about the hit. The owner, not this volume, decides whether it lands;
        /// invincibility frames and perfect dodges are resolved there.
        /// </returns>
        public DamageResult Receive(in DamageInfo damage)
        {
            if (_owner == null)
            {
                return DamageResult.NoDamage(DamageOutcome.Ignored);
            }

            if (Mathf.Approximately(_damageMultiplier, 1f))
            {
                return _owner.TakeDamage(damage);
            }

            DamageInfo scaled = damage.WithAmount(damage.Amount * _damageMultiplier);
            return _owner.TakeDamage(scaled);
        }
    }
}
