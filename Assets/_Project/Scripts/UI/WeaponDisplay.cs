using AdaptiveBossArena.Core.Events;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveBossArena.UI
{
    /// <summary>
    /// Shows the name of the weapon currently in hand.
    /// </summary>
    /// <remarks>
    /// The three weapons swing genuinely different movesets now, so which one is drawn is real
    /// information rather than a cosmetic choice. The swing colour hints at it, but a player mid-fight
    /// should not have to infer their own loadout from a flash — a quiet label near their vitals says
    /// it plainly. It updates on the same "weapon drawn" signal that fires on the opening equip and on
    /// every swap, so it always names the current weapon.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WeaponDisplay : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Carries the name of a newly drawn weapon.")]
        private StringEventChannel _weaponChannel;

        [SerializeField]
        [Tooltip("Label the weapon name is written into.")]
        private Text _label;

        private void OnEnable()
        {
            if (_weaponChannel != null)
            {
                _weaponChannel.Raised += OnWeaponDrawn;
            }
        }

        private void OnDisable()
        {
            if (_weaponChannel != null)
            {
                _weaponChannel.Raised -= OnWeaponDrawn;
            }
        }

        /// <summary>Assigns the channel and label. Used by the interface generator.</summary>
        /// <param name="weaponChannel">Weapon-drawn channel.</param>
        /// <param name="label">Label to write into.</param>
        public void Bind(StringEventChannel weaponChannel, Text label)
        {
            _weaponChannel = weaponChannel;
            _label = label;
        }

        private void OnWeaponDrawn(string weaponName)
        {
            if (_label != null)
            {
                _label.text = weaponName;
            }
        }
    }
}
