using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Combat.Feel;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Sets each of the brute's struck blows' reach to how far its striking parts actually get.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Once hits were decided by the blade rather than by a wedge, the authored ranges became promises the body
    /// could not keep: the sweep was authored at four metres and the great sword reaches about three. The boss
    /// plans every swing from that range, so it would stand out of reach and cut air - and a whiff builds its
    /// resolve, so it would also be rewarded for it.
    /// </para>
    /// <para>
    /// So the reach is measured instead. The generated brute - with its real cleaver and striking parts - is
    /// posed through each attack's clip around the moment the blow lands, and the furthest the parts get from
    /// its centre, plus the knight's own thickness, becomes the range. Measured data, so it is written on every
    /// setup, after the prefab exists. Nothing is learned from the player: this is the brute's own body.
    /// </para>
    /// </remarks>
    public static class AttackReachMeasure
    {
        /// <summary>How much of the clip either side of the contact moment counts as the blow.</summary>
        private const float ContactHalfWindow = 0.1f;

        private const int Samples = 12;

        /// <summary>Measures every struck boss attack and writes its range.</summary>
        [MenuItem(EditorMenus.Setup + "Measure Boss Reach", priority = EditorMenus.SetupPriorityBuildScene - 1)]
        public static void MeasureBossReach()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabBuilder.PrefabPath);
            var config = GeneratedAssets.Config<CharacterAnimationConfig>("DefaultBossAnimation");
            var controller = config != null ? config.AnimatorController as AnimatorController : null;

            if (prefab == null || controller == null)
            {
                return;
            }

            GameObject body = Object.Instantiate(prefab);

            try
            {
                Animator animator = body.GetComponentInChildren<Animator>();
                StrikeVolume[] strikers = body.GetComponentsInChildren<StrikeVolume>(includeInactive: true);

                if (animator == null || strikers.Length == 0)
                {
                    return;
                }

                foreach (AttackClipBinding binding in config.AttackClips)
                {
                    AttackDefinition attack = binding.Attack;

                    if (attack == null || attack.Strikers == StrikerParts.None ||
                        !animator.HasState(0, Animator.StringToHash(binding.State)))
                    {
                        continue;
                    }

                    float contact = binding.ContactFraction > 0f ? binding.ContactFraction : ClipContact.DefaultContact;
                    float reach = Reach(body.transform, animator, binding.State, contact, attack.Strikers, strikers);

                    if (reach <= 0f)
                    {
                        continue;
                    }

                    using (AssetAuthoring.AssetWriter writer = AssetAuthoring.Edit(attack))
                    {
                        writer.Float("_range", reach + PlayerPrefabBuilder.HurtboxRadius);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(body);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>The furthest, across the ground, the attack's parts get from the body's centre around contact.</summary>
        /// <remarks>
        /// Posed through the Animator exactly as play poses it - the attack state, scrubbed by its time parameter,
        /// with root motion discarded - rather than by sampling the clip directly. A direct sample keeps the
        /// clip's travel across the floor, which play throws away, and measured a slide attack at over six metres.
        /// </remarks>
        private static float Reach(
            Transform root, Animator animator, string state, float contact, StrikerParts parts, StrikeVolume[] strikers)
        {
            float reach = 0f;
            int attackTime = Animator.StringToHash(CharacterAnimatorParameters.AttackTime);

            animator.Rebind();

            for (int i = 0; i < Samples; i++)
            {
                float fraction = Mathf.Clamp01(contact + Mathf.Lerp(-ContactHalfWindow, ContactHalfWindow, i / (Samples - 1f)));
                animator.SetFloat(attackTime, fraction);
                animator.Play(state, 0, fraction);
                animator.Update(0f);

                foreach (StrikeVolume striker in strikers)
                {
                    if ((parts & striker.Part) == 0)
                    {
                        continue;
                    }

                    striker.Segment(out Vector3 start, out Vector3 end);
                    reach = Mathf.Max(reach, Across(root.position, start) + striker.Radius, Across(root.position, end) + striker.Radius);
                }
            }

            return reach;
        }

        private static float Across(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            offset.y = 0f;

            return offset.magnitude;
        }
    }
}
