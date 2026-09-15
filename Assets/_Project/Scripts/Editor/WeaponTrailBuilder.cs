using AdaptiveBossArena.Combat.Feel;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Attaches the swing smear to a combatant's weapon.
    /// </summary>
    /// <remarks>
    /// Shared by both prefab builders because the player and the boss want the same effect at different
    /// lengths and colours. Keeping it in one place means the smear is tuned once rather than drifting
    /// between the two characters.
    /// </remarks>
    public static class WeaponTrailBuilder
    {
        /// <summary>Adds a smear that sweeps between two distances along a mount's forward.</summary>
        /// <param name="mount">The transform the blade runs along: a weapon socket, or the body when there is none.</param>
        /// <param name="localPosition">Where the emitter sits relative to the mount.</param>
        /// <param name="baseDistance">Distance along the mount's forward where the smear's inner edge runs.</param>
        /// <param name="tipDistance">Distance along the mount's forward where its outer edge runs.</param>
        /// <param name="colour">Colour at the head of the smear; dim, because it is added as light.</param>
        public static void Attach(Transform mount, Vector3 localPosition, float baseDistance, float tipDistance, Color colour)
        {
            var trailObject = new GameObject("WeaponTrail");
            trailObject.transform.SetParent(mount, false);
            trailObject.transform.localPosition = localPosition;
            trailObject.transform.localRotation = Quaternion.identity;

            trailObject.AddComponent<WeaponTrail>()
                .Configure(baseDistance, tipDistance, colour, MaterialLibrary.GetOrCreateWeaponTrail());
        }
    }
}
