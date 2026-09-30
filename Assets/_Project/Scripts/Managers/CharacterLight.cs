using AdaptiveBossArena.Core.Constants;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// A soft light from the camera's side that reaches only the fighters, so the face of them the camera sees is
    /// never lost.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The moon lights the nave from one fixed direction. Whenever the fight turned so the camera looked at the
    /// side of a fighter facing away from it, that side was lit by the darkened ambient alone: rendered from the
    /// front, the knight's plate was a black shape on black. Every action game with a moving camera solves this
    /// the same way, with a light that follows the camera and belongs to the characters.
    /// </para>
    /// <para>
    /// It comes from above and a little to one side of the camera rather than straight along its view, so it
    /// models form - a flat frontal light erases the plate's shape. It reaches only the
    /// <see cref="RenderingLayers.CharacterMask"/> layer, so the floor and walls stay exactly as the moon and the
    /// fires leave them, and it is pure presentation: the fighters it lights are equally visible to both sides.
    /// </para>
    /// <para>
    /// Oriented in <c>LateUpdate</c>, after the camera rig has moved for the frame, so the light never trails the
    /// view by a frame.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Light))]
    [DisallowMultipleComponent]
    public sealed class CharacterLight : MonoBehaviour
    {
        /// <summary>How far above the camera's line the light comes from, in degrees.</summary>
        public const float Elevation = 35f;

        /// <summary>How far to the camera's right the light comes from, in degrees, so it models form.</summary>
        public const float SideAngle = 30f;

        [SerializeField]
        [Tooltip("The camera the light follows. Falls back to the main camera.")]
        private Camera _camera;

        /// <summary>Assigns the camera the light follows. Used by the scene generator.</summary>
        /// <param name="followed">The gameplay camera.</param>
        public void Bind(Camera followed) => _camera = followed;

        /// <summary>The light's rotation for a camera looking along a direction.</summary>
        /// <param name="cameraForward">The camera's forward vector; its tilt is ignored.</param>
        /// <returns>A rotation whose forward travels from behind and above the camera's shoulder into the scene.</returns>
        public static Quaternion OrientationFor(Vector3 cameraForward)
        {
            cameraForward.y = 0f;

            if (cameraForward.sqrMagnitude < Mathf.Epsilon)
            {
                cameraForward = Vector3.forward;
            }

            return Quaternion.LookRotation(cameraForward.normalized) * Quaternion.Euler(Elevation, -SideAngle, 0f);
        }

        private void LateUpdate()
        {
            Camera followed = _camera != null ? _camera : Camera.main;

            if (followed != null)
            {
                transform.rotation = OrientationFor(followed.transform.forward);
            }
        }
    }
}
