using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>Dresses the knight in the tint the player chose on the title screen.</summary>
    /// <remarks>
    /// <para>
    /// Cosmetic only, and applied from the composition root because only it knows both the run's choice and
    /// the knight's body. The armour's own materials are shared assets, so each skinned renderer gets its own
    /// instance with the base colour multiplied. A property block would be cheaper, but these shaders were
    /// found to ignore per-renderer overrides in Stage O, and the knight has a handful of renderers at most.
    /// </para>
    /// <para>
    /// The plain Steel tint does nothing at all, so a player who never unlocks a tint keeps the shared
    /// materials and the batching they allow.
    /// </para>
    /// </remarks>
    public static class KnightTintApplier
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Applies a tint to the knight's armour and blade trail.</summary>
        /// <param name="knight">The knight's root object.</param>
        /// <param name="tint">The tint to wear.</param>
        public static void Apply(GameObject knight, KnightTint tint)
        {
            if (knight == null || tint.IsDefault)
            {
                return;
            }

            var armour = new Color(tint.R, tint.G, tint.B, 1f);

            foreach (SkinnedMeshRenderer body in knight.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Renderer.materials hands back per-renderer instances, created once; later calls reuse them.
                foreach (Material material in body.materials)
                {
                    if (material != null && material.HasProperty(BaseColorId))
                    {
                        material.SetColor(BaseColorId, material.GetColor(BaseColorId) * armour);
                    }
                }
            }

            var glow = new Color(tint.GlowR, tint.GlowG, tint.GlowB, 1f);

            foreach (WeaponTrail trail in knight.GetComponentsInChildren<WeaponTrail>(true))
            {
                trail.SetColour(glow);
            }
        }
    }
}
