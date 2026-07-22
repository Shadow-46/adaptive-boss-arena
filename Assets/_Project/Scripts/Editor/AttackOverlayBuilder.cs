using AdaptiveBossArena.Combat.Feel;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Attaches the ground overlay that makes a combatant's attacks visible.
    /// </summary>
    /// <remarks>
    /// Shared by both prefab builders because the requirement is identical and the consequence of
    /// omitting it is severe: an attack with no overlay produces damage and hit-stop but nothing to
    /// look at, leaving the player unable to tell whether they attacked at all.
    /// </remarks>
    internal static class AttackOverlayBuilder
    {
        /// <summary>Adds the overlay as a child of a combatant root.</summary>
        /// <param name="parent">The combatant's transform.</param>
        public static void Attach(Transform parent)
        {
            var overlayObject = new GameObject("AttackOverlay");
            overlayObject.transform.SetParent(parent, false);

            var visualizer = overlayObject.AddComponent<AttackVisualizer>();
            visualizer.SetMaterial(MaterialLibrary.GetOrCreateAttackOverlay());
        }
    }
}
