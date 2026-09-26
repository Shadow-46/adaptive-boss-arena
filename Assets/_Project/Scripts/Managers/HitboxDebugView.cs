using System;
using System.Collections.Generic;
using AdaptiveBossArena.Combat;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Draws every striking part and every hurtbox in the fight, for checking a hit against what was seen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Off unless asked for, with <c>-debugHits</c> on the command line or <c>?debughits</c> in the browser's
    /// address. The player reported blows that hurt without touching them; now that hits are decided by the parts
    /// that swing, this is how anyone - the player, or a frame capture - can see the capsule a blow was decided on
    /// at the moment it landed.
    /// </para>
    /// <para>
    /// A striking part shows red while its blow is live and grey otherwise; hurtboxes show green. Presentation
    /// only: it reads positions and never touches combat.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HitboxDebugView : MonoBehaviour
    {
        private const string CommandLineFlag = "-debugHits";
        private const string QueryFlag = "debughits";

        /// <summary>How long after its last live frame a part still shows as live, so a one-frame blow is visible.</summary>
        private const float LiveHoldSeconds = 0.12f;

        private static readonly Color LiveColour = new Color(1f, 0.15f, 0.1f, 0.65f);
        private static readonly Color IdleColour = new Color(0.8f, 0.8f, 0.8f, 0.2f);
        private static readonly Color HurtboxColour = new Color(0.2f, 1f, 0.3f, 0.2f);

        [SerializeField]
        [Tooltip("Translucent, unlit material the shapes are drawn with. Assigned by the scene generator.")]
        private Material _material;

        private readonly List<(StrikeVolume Part, Renderer Shape)> _parts = new List<(StrikeVolume, Renderer)>();
        private MaterialPropertyBlock _block;

        /// <summary>Whether the view was asked for.</summary>
        /// <param name="arguments">The process's command line.</param>
        /// <param name="url">The page address, in the browser.</param>
        /// <returns>True when either carries the flag.</returns>
        public static bool IsRequested(string[] arguments, string url)
        {
            if (arguments != null)
            {
                foreach (string argument in arguments)
                {
                    if (string.Equals(argument, CommandLineFlag, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            int query = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');

            return query >= 0 && url.Substring(query).IndexOf(QueryFlag, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Assigns the material. Used by the scene generator.</summary>
        /// <param name="material">A translucent unlit material.</param>
        public void SetMaterial(Material material) => _material = material;

        private void Awake()
        {
            if (!IsRequested(Environment.GetCommandLineArgs(), Application.absoluteURL) || _material == null)
            {
                enabled = false;
            }
        }

        private void Start()
        {
            _block = new MaterialPropertyBlock();

            foreach (StrikeVolume part in FindObjectsByType<StrikeVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                _parts.Add((part, MakeShape(PrimitiveType.Capsule, transform, "Strike " + part.Part)));
            }

            foreach (Hurtbox hurtbox in FindObjectsByType<Hurtbox>(FindObjectsSortMode.None))
            {
                ShowHurtbox(hurtbox);
            }
        }

        private void LateUpdate()
        {
            foreach ((StrikeVolume part, Renderer shape) in _parts)
            {
                bool shown = part != null && part.IsAvailable;
                shape.enabled = shown;

                if (!shown)
                {
                    continue;
                }

                part.Segment(out Vector3 start, out Vector3 end);
                Vector3 along = end - start;
                float length = along.magnitude;

                // A capsule primitive is two units tall along its own up, one wide.
                shape.transform.SetPositionAndRotation(
                    (start + end) * 0.5f,
                    length > 0.0001f ? Quaternion.FromToRotation(Vector3.up, along / length) : Quaternion.identity);
                shape.transform.localScale = new Vector3(
                    part.Radius * 2f, (length + part.Radius * 2f) * 0.5f, part.Radius * 2f);

                bool live = part.LastLiveTime >= 0f && Time.time - part.LastLiveTime <= LiveHoldSeconds;
                Tint(shape, live ? LiveColour : IdleColour);
            }
        }

        /// <summary>Draws a hurtbox's collider in place, parented so it follows its owner.</summary>
        private void ShowHurtbox(Hurtbox hurtbox)
        {
            switch (hurtbox.GetComponent<Collider>())
            {
                case SphereCollider sphere:
                {
                    Renderer shape = MakeShape(PrimitiveType.Sphere, hurtbox.transform, "Hurtbox");
                    shape.transform.localPosition = sphere.center;
                    shape.transform.localScale = Vector3.one * sphere.radius * 2f;
                    Tint(shape, HurtboxColour);
                    break;
                }

                case CapsuleCollider capsule:
                {
                    Renderer shape = MakeShape(PrimitiveType.Capsule, hurtbox.transform, "Hurtbox");
                    shape.transform.localPosition = capsule.center;
                    shape.transform.localRotation = capsule.direction == 0
                        ? Quaternion.Euler(0f, 0f, 90f)
                        : capsule.direction == 2 ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
                    shape.transform.localScale = new Vector3(
                        capsule.radius * 2f, Mathf.Max(capsule.height, capsule.radius * 2f) * 0.5f, capsule.radius * 2f);
                    Tint(shape, HurtboxColour);
                    break;
                }
            }
        }

        private Renderer MakeShape(PrimitiveType type, Transform parent, string shapeName)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = "[Debug] " + shapeName;

            // Drawn, never touched: the primitive's own collider would join the physics the fight is decided on.
            Destroy(shape.GetComponent<Collider>());

            shape.transform.SetParent(parent, false);

            var renderer = shape.GetComponent<Renderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return renderer;
        }

        private void Tint(Renderer shape, Color colour)
        {
            _block.SetColor("_BaseColor", colour);
            shape.SetPropertyBlock(_block);
        }
    }
}
