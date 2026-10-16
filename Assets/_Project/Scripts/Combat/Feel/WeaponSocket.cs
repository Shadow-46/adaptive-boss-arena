using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// A mount point on the visual root where a weapon model is shown, and swapped when the weapon
    /// changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The weapon-art seam. Today a weapon reads only as its swing colour and trail; a
    /// <see cref="WeaponDefinition"/> carries no mesh, so <see cref="Equip"/> is handed null and shows
    /// nothing. Assign a model prefab to a weapon, and the same swap that already recolours the trail
    /// now also puts a blade in the character's hand — no other code changes.
    /// </para>
    /// <para>
    /// The socket lives on the visual root, one level below the character root, so a held model rides
    /// the procedural lunge and, later, can be re-parented to a hand bone of an imported rig without
    /// the controller knowing or caring where it ended up.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WeaponSocket : MonoBehaviour
    {
        private GameObject _current;

        /// <summary>Draws a weapon: its model in the hand, and its blade's span on the strike volume and the trail.</summary>
        /// <param name="modelPrefab">The weapon's model, or null for none.</param>
        /// <param name="bladeBase">Where its blade begins, in metres from the grip.</param>
        /// <param name="bladeTip">Where its point is.</param>
        public void Equip(GameObject modelPrefab, float bladeBase, float bladeTip)
        {
            Equip(modelPrefab);

            // The blow is decided along the blade the hand now holds, and the smear follows it: a greatsword
            // drawn after a short blade must not keep striking with the short blade's reach.
            foreach (StrikeVolume volume in GetComponentsInChildren<StrikeVolume>(true))
            {
                if (volume.Part == StrikerParts.Weapon)
                {
                    volume.Configure(volume.Part, bladeBase, bladeTip, volume.Radius);
                }
            }

            foreach (WeaponTrail trail in GetComponentsInChildren<WeaponTrail>(true))
            {
                trail.SetSpan(bladeBase, bladeTip);
            }
        }

        /// <summary>Draws a model in the hand, leaving the strike and trail spans as they are.</summary>
        /// <param name="modelPrefab">The weapon's model, or null for none.</param>
        public void Equip(GameObject modelPrefab)
        {
            if (_current != null)
            {
                Destroy(_current);
                _current = null;
            }

            if (modelPrefab == null)
            {
                return;
            }

            _current = Instantiate(modelPrefab, transform);
            _current.transform.localPosition = Vector3.zero;
            _current.transform.localRotation = Quaternion.identity;
        }
    }
}
