using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// A lingering patch of dangerous ground: it warns, then chips at anything standing in it, then
    /// fades.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hazards damage the <em>player only</em> — they carry the boss's team and query the player
    /// hurtbox layer — which is the genre convention that a boss is unharmed by its own fire, and,
    /// more importantly here, keeps the arena's new spatial pressure entirely on the player's side of
    /// the firewall. The AI never learns hazards exist.
    /// </para>
    /// <para>
    /// The damage sets <see cref="DamageInfo.IgnoresInvulnerability"/>, so a dash cannot roll through
    /// a hazard: the answer to a scarred floor is to <em>leave</em> it, not to time an i-frame. It
    /// deals no poise damage and never staggers, so standing in one for a moment costs health and
    /// position, not control — pressure, not a stun-lock. Ticks on <see cref="ITimeService.DeltaTime"/>
    /// so a hazard freezes with the world during hit-stop.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HazardZone : MonoBehaviour
    {
        /// <summary>Telegraph before the zone bites, long enough to read and step out of.</summary>
        private const float WarningSeconds = 0.55f;

        /// <summary>Harmless dissipation after the active window.</summary>
        private const float FadeSeconds = 0.5f;

        /// <summary>Seconds between damage ticks. Paced, so a hazard chips rather than shreds.</summary>
        private const float DamageIntervalSeconds = 0.45f;

        /// <summary>Gentle outward shove on a tick, nudging the player toward the edge of the zone.</summary>
        private const float HazardKnockback = 2.5f;

        /// <summary>Height the disc floats above the floor, to avoid z-fighting with it.</summary>
        private const float DiscLift = 0.02f;

        /// <summary>Thinness of the disc primitive.</summary>
        private const float DiscThickness = 0.02f;

        /// <summary>Peak opacity of the disc while active.</summary>
        private const float MaxAlpha = 0.5f;

        private static readonly Color HazardColor = new Color(1f, 0.35f, 0.18f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly Collider[] _overlap = new Collider[8];

        private Transform _disc;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _properties;
        private ITimeService _time;

        private HazardTicker _ticker;
        private Vector3 _center;
        private float _radius;
        private float _damagePerTick;

        /// <summary>Builds the disc visual once, sharing a material with the rest of the field.</summary>
        /// <param name="material">The transparent material every zone tints and reuses.</param>
        public void Construct(Material material)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";

            // The primitive's capsule collider would intercept attacks and movement; a hazard is a
            // trigger of its own making, resolved by overlap query, so the visual carries no collider.
            Destroy(disc.GetComponent<Collider>());

            disc.transform.SetParent(transform, worldPositionStays: false);

            _renderer = disc.GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            // Per-zone colour without a material instance per zone, so several hazards can fade
            // independently while sharing one transparent material.
            _properties = new MaterialPropertyBlock();

            _disc = disc.transform;

            gameObject.SetActive(false);
        }

        /// <summary>Lights the zone up at a position and starts its life.</summary>
        /// <param name="center">World-space centre at floor height.</param>
        /// <param name="radius">Radius of the danger area.</param>
        /// <param name="damagePerTick">Damage dealt to anything inside on each tick.</param>
        /// <param name="durationSeconds">How long the zone stays dangerous.</param>
        public void Activate(Vector3 center, float radius, float damagePerTick, float durationSeconds)
        {
            _center = center;
            _radius = Mathf.Max(0.1f, radius);
            _damagePerTick = Mathf.Max(0f, damagePerTick);
            _ticker = new HazardTicker(WarningSeconds, Mathf.Max(0.1f, durationSeconds), FadeSeconds, DamageIntervalSeconds);

            transform.position = new Vector3(center.x, GameplayConstants.GroundPlaneHeight + DiscLift, center.z);

            // A Unity cylinder is one unit in diameter and two tall, so the horizontal scale is the
            // full diameter and the vertical scale is half the thickness.
            if (_disc != null)
            {
                _disc.localScale = new Vector3(_radius * 2f, DiscThickness * 0.5f, _radius * 2f);
            }

            gameObject.SetActive(true);
            ApplyVisual(0f);
        }

        private void Update()
        {
            if (_ticker == null)
            {
                return;
            }

            _time ??= ServiceRegistry.Current != null &&
                      ServiceRegistry.Current.TryGet(out ITimeService time)
                ? time
                : null;

            float deltaTime = _time?.DeltaTime ?? Time.deltaTime;

            if (_ticker.Advance(deltaTime))
            {
                DamageOccupants();
            }

            ApplyVisual(_ticker.VisualPresence);

            if (_ticker.IsExpired)
            {
                _ticker = null;
                gameObject.SetActive(false);
            }
        }

        /// <summary>Applies one tick of damage to every player hurtbox inside the zone.</summary>
        private void DamageOccupants()
        {
            int count = Physics.OverlapSphereNonAlloc(
                _center, _radius, _overlap, Layers.BossAttackMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Hurtbox hurtbox = _overlap[i].GetComponentInParent<Hurtbox>();

                if (hurtbox == null)
                {
                    continue;
                }

                Vector3 outward = hurtbox.transform.position - _center;
                outward.y = 0f;
                outward = outward.sqrMagnitude > Mathf.Epsilon ? outward.normalized : Vector3.forward;

                hurtbox.Receive(new DamageInfo
                {
                    Amount = _damagePerTick,
                    Type = DamageType.Light,
                    SourceTeam = CombatantTeam.Boss,
                    SourceInstanceId = GetInstanceID(),
                    HitPoint = hurtbox.transform.position,
                    HitDirection = outward,
                    KnockbackSpeed = HazardKnockback,
                    PoiseDamage = 0f,
                    Stagger = StaggerStrength.None,
                    HitStopSeconds = 0f,
                    IgnoresInvulnerability = true
                });
            }
        }

        /// <summary>Fades the disc's opacity to match the hazard's presence.</summary>
        private void ApplyVisual(float presence)
        {
            if (_renderer == null)
            {
                return;
            }

            Color color = HazardColor;
            color.a = presence * MaxAlpha;

            _properties.SetColor(BaseColorId, color);
            _renderer.SetPropertyBlock(_properties);
        }
    }
}
