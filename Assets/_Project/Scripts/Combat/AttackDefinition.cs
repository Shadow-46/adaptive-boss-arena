using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.ScriptableObjects;
using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>Geometry used to test what an attack strikes.</summary>
    public enum AttackShape
    {
        /// <summary>Radial burst centred on an offset from the attacker.</summary>
        Sphere = 0,

        /// <summary>Rectangular volume, suited to thrusts and sweeps along one axis.</summary>
        Box = 1,

        /// <summary>Wedge in front of the attacker, the natural shape for a top-down melee swing.</summary>
        Arc = 2
    }

    /// <summary>
    /// Complete definition of one attack, authored as an asset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Timing is expressed as three phases in seconds rather than as animation events. The project
    /// prototypes with primitive shapes and has no animation clips to hang events on, and tying
    /// frame data to clips would mean re-authoring every attack when real animation arrives.
    /// Instead an <see cref="IAttackTimeline"/> raises the same callbacks an animation event would,
    /// and swapping to animation-driven timing later replaces the driver and nothing else.
    /// </para>
    /// <para>
    /// The three phases are the entire language of the combat system. Startup is the commitment the
    /// player reads and reacts to, active is the only window that can damage, and recovery is the
    /// punish window that makes a whiffed heavy attack a real mistake. Adjusting the ratio between
    /// them is how an attack is made fair or oppressive.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "AttackDefinition",
        menuName = "Adaptive Boss Arena/Combat/Attack Definition",
        order = 0)]
    public sealed class AttackDefinition : IdentifiableSO
    {
        [Header("Presentation")]
        [SerializeField]
        [Tooltip("Name shown in debug tooling and the boss's tell readout.")]
        private string _displayName = "Unnamed Attack";

        [SerializeField]
        [Tooltip("Optional effect spawned at the attacker when this attack begins. Empty today; drop " +
                 "an ability particle prefab in to give a special its own signature cast, with no " +
                 "other change. Shared by player and boss.")]
        private GameObject _castVfxPrefab;

        [Header("Damage")]
        [SerializeField]
        [Tooltip("Health removed on a clean hit.")]
        private float _damage = 10f;

        [SerializeField]
        [Tooltip("Category, used both for resistances and for behaviour analysis.")]
        private DamageType _damageType = DamageType.Light;

        [SerializeField]
        [Tooltip("Poise damage dealt. Enough accumulated poise damage staggers the target.")]
        private float _poiseDamage = 10f;

        [SerializeField]
        [Tooltip("How forcefully this hit interrupts what the target was doing.")]
        private StaggerStrength _stagger = StaggerStrength.Flinch;

        [SerializeField]
        [Tooltip("Speed imparted to the target away from the impact, in world units per second.")]
        private float _knockbackSpeed = 4f;

        [Header("Timing (seconds)")]
        [SerializeField]
        [Range(0.02f, 2f)]
        [Tooltip("Wind-up before the hitbox activates. This is the window the opponent reads and reacts to.")]
        private float _startupSeconds = 0.2f;

        [SerializeField]
        [Range(0.02f, 1f)]
        [Tooltip("How long the hitbox stays live. Longer windows are more forgiving to the attacker.")]
        private float _activeSeconds = 0.1f;

        [SerializeField]
        [Range(0.02f, 2f)]
        [Tooltip("Lockout after the hitbox closes. This is the punish window a whiff opens up.")]
        private float _recoverySeconds = 0.3f;

        [Header("Hitbox")]
        [SerializeField]
        [Tooltip("Volume used to test what this attack strikes.")]
        private AttackShape _shape = AttackShape.Arc;

        [SerializeField]
        [Tooltip("Offset from the attacker's origin, in local space.")]
        private Vector3 _offset = new Vector3(0f, 1f, 1f);

        [SerializeField]
        [Tooltip("Reach of the volume. Interpreted as radius for sphere and arc shapes.")]
        private float _range = 2f;

        [SerializeField]
        [Range(10f, 360f)]
        [Tooltip("Angular width of an arc, in degrees. Unused by other shapes.")]
        private float _arcDegrees = 110f;

        [SerializeField]
        [Tooltip("Half-extents of a box volume. Unused by other shapes.")]
        private Vector3 _boxHalfExtents = new Vector3(0.6f, 0.6f, 1f);

        [Header("Cost and Movement")]
        [SerializeField]
        [Tooltip("Stamina consumed on use. Boss attacks leave this at zero.")]
        private float _staminaCost;

        [SerializeField]
        [Tooltip("Forward impulse applied during startup, used for lunges and gap-closers.")]
        private float _lungeSpeed;

        [Header("Feel")]
        [SerializeField]
        [Range(0f, 0.2f)]
        [Tooltip("Frames of freeze on a successful hit. The strongest single lever on impact weight.")]
        private float _hitStopSeconds = 0.06f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Camera trauma added on a successful hit.")]
        private float _cameraTrauma = 0.2f;

        [Header("Ground Hazard")]
        [SerializeField]
        [Tooltip("When true, this attack scars the ground with a lingering hazard when it lands. " +
                 "Boss attacks only; a hazard field must be present for it to appear.")]
        private bool _leavesHazard;

        [SerializeField]
        [Tooltip("Radius of the hazard left behind.")]
        private float _hazardRadius = 2.5f;

        [SerializeField]
        [Tooltip("Damage the hazard deals each tick. Kept low: the threat is being cornered, not the " +
                 "chip itself.")]
        private float _hazardDamagePerTick = 4f;

        [SerializeField]
        [Tooltip("How long the hazard stays dangerous, in seconds.")]
        private float _hazardDurationSeconds = 3f;

        [Header("Defence")]
        [SerializeField]
        [Tooltip("When true, a raised guard does not stop this attack — it must be dodged. A dash " +
                 "still avoids it. The 'perilous' attack; pair it with a distinct telegraph.")]
        private bool _unblockable;

        [Header("Telegraph")]
        [SerializeField]
        [Tooltip("Colour of the ground telegraph drawn during startup. Boss attacks only.")]
        private Color _telegraphColor = new Color(1f, 0.35f, 0.2f, 0.55f);

        [SerializeField]
        [Tooltip("Whether a ground telegraph is drawn during this attack's startup.")]
        private bool _showTelegraph = true;

        [Header("Combo")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Point within recovery, as a fraction, at which the next combo input is accepted.")]
        private float _comboWindowStart = 0.1f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Point within recovery, as a fraction, after which the combo is dropped.")]
        private float _comboWindowEnd = 0.85f;

        /// <summary>Name shown in debug tooling.</summary>
        public string DisplayName => _displayName;

        /// <summary>Optional effect spawned at the attacker when the attack begins, or null.</summary>
        public GameObject CastVfxPrefab => _castVfxPrefab;

        /// <summary>Whether this attack scars the ground with a lingering hazard on landing.</summary>
        public bool LeavesHazard => _leavesHazard;

        /// <summary>Radius of the hazard left behind.</summary>
        public float HazardRadius => _hazardRadius;

        /// <summary>Damage the hazard deals each tick.</summary>
        public float HazardDamagePerTick => _hazardDamagePerTick;

        /// <summary>How long the hazard stays dangerous, in seconds.</summary>
        public float HazardDurationSeconds => _hazardDurationSeconds;

        /// <summary>Health removed on a clean hit.</summary>
        public float Damage => _damage;

        /// <summary>Category of this attack.</summary>
        public DamageType DamageType => _damageType;

        /// <summary>Poise damage dealt.</summary>
        public float PoiseDamage => _poiseDamage;

        /// <summary>How forcefully this hit interrupts the target.</summary>
        public StaggerStrength Stagger => _stagger;

        /// <summary>Knockback speed imparted to the target.</summary>
        public float KnockbackSpeed => _knockbackSpeed;

        /// <summary>Wind-up duration in seconds.</summary>
        public float StartupSeconds => _startupSeconds;

        /// <summary>Duration the hitbox is live, in seconds.</summary>
        public float ActiveSeconds => _activeSeconds;

        /// <summary>Lockout duration after the hitbox closes, in seconds.</summary>
        public float RecoverySeconds => _recoverySeconds;

        /// <summary>Total duration of the attack from input to actionable, in seconds.</summary>
        public float TotalSeconds => _startupSeconds + _activeSeconds + _recoverySeconds;

        /// <summary>Time from the start of the attack at which the hitbox opens.</summary>
        public float ActiveStartSeconds => _startupSeconds;

        /// <summary>Time from the start of the attack at which the hitbox closes.</summary>
        public float ActiveEndSeconds => _startupSeconds + _activeSeconds;

        /// <summary>Volume used to test what this attack strikes.</summary>
        public AttackShape Shape => _shape;

        /// <summary>Offset of the volume from the attacker's origin, in local space.</summary>
        public Vector3 Offset => _offset;

        /// <summary>Reach of the volume.</summary>
        public float Range => _range;

        /// <summary>Angular width of an arc volume, in degrees.</summary>
        public float ArcDegrees => _arcDegrees;

        /// <summary>Half-extents of a box volume.</summary>
        public Vector3 BoxHalfExtents => _boxHalfExtents;

        /// <summary>Stamina consumed on use.</summary>
        public float StaminaCost => _staminaCost;

        /// <summary>Forward impulse applied during startup.</summary>
        public float LungeSpeed => _lungeSpeed;

        /// <summary>Freeze duration applied on a successful hit.</summary>
        public float HitStopSeconds => _hitStopSeconds;

        /// <summary>Camera trauma added on a successful hit.</summary>
        public float CameraTrauma => _cameraTrauma;

        /// <summary>Colour of the startup telegraph.</summary>
        public Color TelegraphColor => _telegraphColor;

        /// <summary>Whether a startup telegraph is drawn.</summary>
        public bool ShowTelegraph => _showTelegraph;

        /// <summary>Whether a raised guard fails to stop this attack, so it must be dodged.</summary>
        public bool Unblockable => _unblockable;

        /// <summary>Absolute time at which the combo window opens, measured from attack start.</summary>
        public float ComboWindowStartSeconds => ActiveEndSeconds + _recoverySeconds * _comboWindowStart;

        /// <summary>Absolute time at which the combo window closes, measured from attack start.</summary>
        public float ComboWindowEndSeconds => ActiveEndSeconds + _recoverySeconds * _comboWindowEnd;

        /// <summary>Builds the hit description this attack deals.</summary>
        /// <param name="sourceTeam">Team the attack is performed by.</param>
        /// <param name="sourceInstanceId">Instance identifier of the attacker.</param>
        /// <returns>A populated hit description, awaiting contact-point data.</returns>
        public DamageInfo BuildDamageInfo(CombatantTeam sourceTeam, int sourceInstanceId) => new DamageInfo
        {
            Amount = _damage,
            Type = _damageType,
            SourceTeam = sourceTeam,
            SourceInstanceId = sourceInstanceId,
            PoiseDamage = _poiseDamage,
            Stagger = _stagger,
            KnockbackSpeed = _knockbackSpeed,
            HitStopSeconds = _hitStopSeconds,
            Unblockable = _unblockable,
            HitDirection = Vector3.forward
        };

#if UNITY_EDITOR
        /// <inheritdoc />
        protected override void OnValidate()
        {
            base.OnValidate();

            // An inverted combo window silently disables comboing rather than erroring, which is a
            // frustrating thing to debug from the symptom.
            if (_comboWindowEnd < _comboWindowStart)
            {
                _comboWindowEnd = _comboWindowStart;
            }
        }
#endif
    }
}
