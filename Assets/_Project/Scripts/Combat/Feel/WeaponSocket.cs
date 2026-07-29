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

        /// <summary>Shows a weapon model here, replacing whatever was shown before.</summary>
        /// <remarks>
        /// Passing null clears the socket, which is the current behaviour for every weapon since none
        /// carries a model yet. The old instance is destroyed rather than pooled: weapon swaps are
        /// gated to the idle and move states and so are rare, and a live instance left parented would
        /// stack blades on every switch.
        /// </remarks>
        /// <param name="modelPrefab">The weapon model to instantiate, or null to show nothing.</param>
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
