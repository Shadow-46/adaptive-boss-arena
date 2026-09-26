using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Marks the brute with a small ring while the camera is locked on to it.
    /// </summary>
    /// <remarks>
    /// Lock-on changes how the camera and the knight behave - the view tracks the brute, the knight faces it and
    /// strafes - so whether it is engaged has to be visible at a glance, not inferred from how the camera moves.
    /// Presentation only: it reads the camera's state and never changes it.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LockOnReticle : MonoBehaviour
    {
        /// <summary>Height on the brute the ring sits at: its chest, where the eye already goes.</summary>
        private const float ChestHeight = 1.9f;

        private const int RingTextureSize = 64;

        [SerializeField]
        [Tooltip("The camera whose lock-on this shows.")]
        private ArenaCameraRig _rig;

        [SerializeField]
        [Tooltip("What the camera locks on to.")]
        private Transform _target;

        [SerializeField]
        [Tooltip("The ring drawn over the target.")]
        private RectTransform _marker;

        private Canvas _canvas;
        private RectTransform _canvasRect;

        /// <summary>Assigns the camera, the target and the ring. Used by the scene generator.</summary>
        /// <param name="rig">The camera rig.</param>
        /// <param name="target">The brute.</param>
        /// <param name="marker">The ring's rect, under the HUD canvas.</param>
        public void Bind(ArenaCameraRig rig, Transform target, RectTransform marker)
        {
            _rig = rig;
            _target = target;
            _marker = marker;
        }

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;

            // The ring is drawn in code rather than imported, like every other asset here.
            if (_marker != null && _marker.TryGetComponent(out Image image))
            {
                image.sprite = RingSprite();
                image.raycastTarget = false;
            }
        }

        private void LateUpdate()
        {
            if (_marker == null)
            {
                return;
            }

            Camera view = Camera.main;
            bool show = _rig != null && _target != null && _rig.IsLockedOn && view != null && _canvasRect != null;

            Vector3 screen = show ? view.WorldToScreenPoint(_target.position + Vector3.up * ChestHeight) : Vector3.zero;

            // Behind the camera, the projection lands in mirror image on screen; hide rather than point the wrong way.
            show &= screen.z > 0f;

            if (_marker.gameObject.activeSelf != show)
            {
                _marker.gameObject.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            Camera canvasCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, canvasCamera, out Vector2 local))
            {
                _marker.anchoredPosition = local;
            }
        }

        /// <summary>A thin bright ring with a soft edge.</summary>
        private static Sprite RingSprite()
        {
            var texture = new Texture2D(RingTextureSize, RingTextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                name = "LockOnRing"
            };

            float centre = (RingTextureSize - 1) * 0.5f;
            float outer = centre;
            float inner = centre * 0.72f;

            for (int y = 0; y < RingTextureSize; y++)
            {
                for (int x = 0; x < RingTextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre));
                    float alpha = Mathf.Clamp01(Mathf.Min(outer - distance, distance - inner));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            return Sprite.Create(
                texture, new Rect(0f, 0f, RingTextureSize, RingTextureSize), new Vector2(0.5f, 0.5f));
        }
    }
}
