using AdaptiveBossArena.Combat.Feel;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Attaches the swing ribbon to a combatant's visual hierarchy.
    /// </summary>
    /// <remarks>
    /// Shared by both prefab builders because the player and the boss want the same rig at different
    /// sizes and colours. Keeping it in one place means the ribbon's shape is tuned once rather than
    /// drifting between the two characters.
    /// </remarks>
    public static class WeaponTrailBuilder
    {
        /// <summary>Adds a trail emitter at the point a weapon would trace through.</summary>
        /// <param name="visualRoot">The combatant's visual root, which turns as they face.</param>
        /// <param name="tipOffset">Where the weapon tip sits, in the visual root's local space.</param>
        /// <param name="color">Colour at the head of the ribbon.</param>
        /// <param name="width">Ribbon width at its head.</param>
        public static void Attach(Transform visualRoot, Vector3 tipOffset, Color color, float width)
        {
            var trailObject = new GameObject("WeaponTrail");
            trailObject.transform.SetParent(visualRoot, false);
            trailObject.transform.localPosition = tipOffset;

            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();

            // Short-lived on purpose. A long trail turns every swing into a smear and stops the
            // individual hits of a combo being distinguishable from one another.
            trail.time = 0.22f;
            trail.minVertexDistance = 0.02f;
            trail.autodestruct = false;
            trail.emitting = false;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            // Tapers to nothing at the tail, which is what makes it read as motion rather than as a
            // solid object trailing behind the character.
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f));
            trail.widthMultiplier = width;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color * 0.6f, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.85f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });

            trail.colorGradient = gradient;

            Material material = MaterialLibrary.GetOrCreateWeaponTrail();

            if (material != null)
            {
                trail.sharedMaterial = material;
            }

            trailObject.AddComponent<WeaponTrail>();
        }
    }
}
