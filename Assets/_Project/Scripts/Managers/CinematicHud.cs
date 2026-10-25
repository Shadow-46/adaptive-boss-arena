using AdaptiveBossArena.UI;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Fades the fight's bars out while the camera plays a directed shot, and back in when the fight resumes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The entrance, a phase's turn and the finisher are framed like film, and health and stamina bars across the
    /// bottom of a shot of the boss roaring read as a game paused over a cut scene. The big action games clear the
    /// screen for these moments; so does this. The titles the moments carry - the boss's name, the phase's - stay,
    /// because they are the point of the shot.
    /// </para>
    /// <para>
    /// Every one of these shots ends before the boss can strike again, so the bars are always back by the time
    /// the player needs them. Runs on unscaled time, so a moment's slow motion does not hold the bars away longer.
    /// Found at runtime rather than wired by the scene builder, so the generated scenes are untouched.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CinematicHud : MonoBehaviour
    {
        /// <summary>How fast the bars fade out and back, in alpha per second.</summary>
        private const float FadePerSecond = 6f;

        private ArenaCameraRig _camera;
        private CanvasGroup[] _groups;
        private float _alpha = 1f;

        /// <summary>The bars' current opacity, zero hidden to one shown. For tests and debug tooling.</summary>
        public float Alpha => _alpha;

        private void Update()
        {
            if (!Resolve())
            {
                return;
            }

            float target = _camera.IsPlayingShot ? 0f : 1f;
            _alpha = Mathf.MoveTowards(_alpha, target, FadePerSecond * Time.unscaledDeltaTime);

            foreach (CanvasGroup group in _groups)
            {
                if (group != null)
                {
                    group.alpha = _alpha;
                }
            }
        }

        /// <summary>Finds the camera and gives each bar and the weapon display a group to fade, once.</summary>
        private bool Resolve()
        {
            if (_camera == null)
            {
                _camera = FindAnyObjectByType<ArenaCameraRig>();
            }

            if (_groups == null)
            {
                ResourceBar[] bars = FindObjectsByType<ResourceBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                WeaponDisplay[] weapons = FindObjectsByType<WeaponDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                if (bars.Length == 0)
                {
                    return false;
                }

                _groups = new CanvasGroup[bars.Length + weapons.Length];

                for (int i = 0; i < bars.Length; i++)
                {
                    _groups[i] = GroupOn(bars[i].gameObject);
                }

                for (int i = 0; i < weapons.Length; i++)
                {
                    _groups[bars.Length + i] = GroupOn(weapons[i].gameObject);
                }
            }

            return _camera != null;
        }

        private static CanvasGroup GroupOn(GameObject target)
        {
            var group = target.GetComponent<CanvasGroup>();

            if (group == null)
            {
                group = target.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            return group;
        }
    }
}
