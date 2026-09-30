using AdaptiveBossArena.Core.Constants;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Puts everything a fighter renders on the character rendering layer, so the character light reaches it.
    /// </summary>
    /// <remarks>
    /// One helper for the knight, the brute and every weapon prefab, because a weapon is spawned into a fighter's
    /// hand at runtime from its own prefab: marked only on the fighters, a drawn blade would have been the one dark
    /// thing in a lit hand. The default layer is kept alongside, so the moon, the fires and the phase aura still
    /// reach the fighters as they reach everything else.
    /// </remarks>
    public static class CharacterRendering
    {
        /// <summary>Adds the character layer to every renderer under a root.</summary>
        /// <param name="root">A fighter or weapon being built into a prefab.</param>
        public static void Mark(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.renderingLayerMask |= RenderingLayers.CharacterMask;
            }
        }
    }
}
