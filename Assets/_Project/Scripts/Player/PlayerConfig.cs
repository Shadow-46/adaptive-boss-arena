using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.ScriptableObjects;
using UnityEngine;

namespace AdaptiveBossArena.Player
{
    /// <summary>
    /// Every tunable value describing how the player character handles.
    /// </summary>
    /// <remarks>
    /// Kept as an asset so the feel of the character can be tuned while the game is running and the
    /// result kept, which is the only practical way to arrive at good movement values. No field here
    /// is read by the boss; the AI has no reference to this type or the assembly containing it.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "PlayerConfig",
        menuName = "Adaptive Boss Arena/Player/Player Config",
        order = 0)]
    public sealed class PlayerConfig : IdentifiableSO
    {
        [Header("Vitals")]
        [SerializeField]
        [Tooltip("Maximum health.")]
        private float _maxHealth = 100f;

        [SerializeField]
        [Tooltip("Maximum stamina, the shared budget for dashing and heavy attacks.")]
        private float _maxStamina = 100f;

        [SerializeField]
        [Tooltip("Stamina restored per second once regeneration resumes.")]
        private float _staminaRegenPerSecond = 28f;

        [SerializeField]
        [Tooltip("Pause after spending stamina before regeneration resumes. This is what stops " +
                 "dash-spamming without ever printing a refusal message at the player.")]
        private float _staminaRegenDelaySeconds = 0.6f;

        [Header("Movement")]
        [SerializeField]
        [Tooltip("Top movement speed in world units per second.")]
        private float _moveSpeed = 7f;

        [SerializeField]
        [Tooltip("Time to reach top speed from rest. Small values feel snappy; zero feels robotic.")]
        private float _accelerationSeconds = 0.06f;

        [SerializeField]
        [Tooltip("Time to come to rest from top speed.")]
        private float _decelerationSeconds = 0.08f;

        [SerializeField]
        [Tooltip("How long a shove such as knockback takes to lose half its speed. Longer carries the " +
                 "player further on the same blow.")]
        [Range(0.02f, 0.6f)]
        private float _impulseHalfLifeSeconds = 0.12f;

        [Header("Knockdowns and launches")]
        [SerializeField]
        [Tooltip("Upward speed a launch throws the player at. With gravity 25, 9 m/s is about 0.7 s in the air.")]
        [Range(1f, 20f)]
        private float _launchUpwardSpeed = 9f;

        [SerializeField]
        [Tooltip("Longest the player can stay airborne before landing is assumed. A hard ceiling on airtime, " +
                 "so a body caught on geometry cannot hang in the air.")]
        [Range(0.3f, 3f)]
        private float _maximumAirborneSeconds = 1.2f;

        [SerializeField]
        [Tooltip("Time spent on the floor after a knockdown or a landing. The player cannot be hurt here.")]
        [Range(0.1f, 3f)]
        private float _knockdownSeconds = 0.8f;

        [SerializeField]
        [Tooltip("Time spent rising. Fully invulnerable, and posture refills when it ends.")]
        [Range(0.1f, 2f)]
        private float _getUpSeconds = 0.6f;

        [SerializeField]
        [Tooltip("After standing, knockdowns and launches arrive as knockback for this long, so none can chain.")]
        [Range(0f, 10f)]
        private float _knockdownImmunitySeconds = 2.5f;

        [SerializeField]
        [Tooltip("How much harder a knockback reaction shoves than the hit's ordinary knockback.")]
        [Range(1f, 4f)]
        private float _knockbackReactionMultiplier = 1.6f;

        [SerializeField]
        [Tooltip("Shove speed into a wall at or above which the impact staggers the player.")]
        [Range(1f, 30f)]
        private float _wallImpactSpeed = 7f;

        [SerializeField]
        [Tooltip("How long a hard wall impact staggers the player.")]
        [Range(0.05f, 1.5f)]
        private float _wallImpactStaggerSeconds = 0.3f;

        [SerializeField]
        [Tooltip("Fraction of top speed kept when reversing direction outright at a sprint. Lower " +
                 "makes the knight heavier to turn.")]
        [Range(0f, 1f)]
        private float _turnSpeedFloor = 0.35f;

        [SerializeField]
        [Tooltip("Relative mass in body contact. Compared against the boss's to decide who gives ground.")]
        [Min(0.01f)]
        private float _bodyMass = 1f;

        [SerializeField]
        [Tooltip("Turn rate in degrees per second.")]
        private float _turnSpeedDegreesPerSecond = 1080f;

        [Header("Dash")]
        [SerializeField]
        [Tooltip("Distance covered by a single dash.")]
        private float _dashDistance = 4.5f;

        [SerializeField]
        [Tooltip("Duration of the dash movement.")]
        private float _dashDurationSeconds = 0.18f;

        [SerializeField]
        [Tooltip("Lockout after a dash ends before another may begin.")]
        private float _dashCooldownSeconds = 0.25f;

        [SerializeField]
        [Tooltip("Stamina consumed per dash.")]
        private float _dashStaminaCost = 22f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds of invulnerability from the start of a dodge. Absolute rather than a fraction " +
                 "of the roll, so lengthening the roll can never lengthen the invincibility with it.")]
        private float _invulnerabilitySeconds = 0.135f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Fraction of the roll, from its start, during which the stick can still steer it.")]
        private float _dashSteerFraction = 0.2f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Fraction of the roll after which a buffered attack may cancel the rest of it.")]
        private float _dashCancelFraction = 0.7f;

        [Header("Weapons")]
        [SerializeField]
        [Tooltip("Weapons the player can switch between. The first is drawn at the start of a fight.")]
        private WeaponDefinition[] _weapons = new WeaponDefinition[0];

        [Header("Attacks (fallback)")]
        [SerializeField]
        [Tooltip("Used only when no weapon is equipped, so the character is never left unable to act.")]
        private AttackDefinition[] _lightComboChain = new AttackDefinition[0];

        [SerializeField]
        [Tooltip("Heavy attack. Slow and committed, with a recovery long enough to be punished.")]
        private AttackDefinition _heavyAttack;

        [SerializeField]
        [Tooltip("Special ability attack.")]
        private AttackDefinition _specialAttack;

        [SerializeField]
        [Tooltip("Cooldown on the special ability.")]
        private float _specialCooldownSeconds = 8f;

        [SerializeField]
        [Tooltip("Stronger special used when the focus meter is full. Falls back to the normal " +
                 "special when empty, so a full meter is never wasted.")]
        private AttackDefinition _empoweredSpecialAttack;

        [Header("Focus")]
        [SerializeField]
        [Tooltip("Focus needed to empower a special. Earned by defending well, spent on the payoff.")]
        private float _maxFocus = 100f;

        [SerializeField]
        [Tooltip("Focus gained from a clean deflect.")]
        private float _focusPerDeflect = 34f;

        [SerializeField]
        [Tooltip("Focus gained from a perfect dodge.")]
        private float _focusPerPerfectDodge = 26f;

        [SerializeField]
        [Tooltip("Strike used to punish a broken guard. Falls back to the heavy attack when empty.")]
        private AttackDefinition _riposteAttack;

        [SerializeField]
        [Tooltip("The execution: a devastating multi-hit riposte thrown into a broken guard. Falls " +
                 "back to the ordinary riposte when empty.")]
        private AttackDefinition _executionAttack;

        [Header("Posture")]
        [SerializeField]
        [Tooltip("Posture capacity. Blocking late drains this; running out breaks the guard and " +
                 "leaves the player open to being riposted in turn.")]
        private float _maxPosture = 100f;

        [SerializeField]
        [Tooltip("Posture recovered per second while not blocking.")]
        private float _postureRegenPerSecond = 22f;

        [SerializeField]
        [Tooltip("Posture cost of blocking a hit late rather than deflecting it.")]
        private float _blockPostureCost = 22f;

        [SerializeField]
        [Range(0f, 0.5f)]
        [Tooltip("Fraction of damage that gets through a late block as chip damage.")]
        private float _blockChipFraction = 0.15f;

        [SerializeField]
        [Tooltip("Posture dealt to the attacker by a clean deflect. The core of the deflect economy: " +
                 "standing your ground has to make progress, not merely survive.")]
        private float _deflectPostureDamage = 28f;

        [Header("Healing")]
        [SerializeField]
        [Tooltip("Health restored per use.")]
        private float _healAmount = 35f;

        [SerializeField]
        [Tooltip("Uses available per attempt at the boss.")]
        private int _healCharges = 3;

        [SerializeField]
        [Tooltip("Channel time before the heal lands. Long enough that the boss can legitimately " +
                 "punish a heal it saw start.")]
        private float _healChannelSeconds = 0.9f;

        /// <summary>Maximum health.</summary>
        public float MaxHealth => _maxHealth;

        /// <summary>Maximum stamina.</summary>
        public float MaxStamina => _maxStamina;

        /// <summary>Stamina restored per second.</summary>
        public float StaminaRegenPerSecond => _staminaRegenPerSecond;

        /// <summary>Delay after spending before stamina regeneration resumes.</summary>
        public float StaminaRegenDelaySeconds => _staminaRegenDelaySeconds;

        /// <summary>Top movement speed.</summary>
        public float MoveSpeed => _moveSpeed;

        /// <summary>Time to reach top speed.</summary>
        public float AccelerationSeconds => _accelerationSeconds;

        /// <summary>Time to come to rest.</summary>
        public float DecelerationSeconds => _decelerationSeconds;

        /// <summary>Half-life of an imposed shove such as knockback.</summary>
        public float ImpulseHalfLifeSeconds => _impulseHalfLifeSeconds;

        /// <summary>Upward speed of a launch.</summary>
        public float LaunchUpwardSpeed => _launchUpwardSpeed;

        /// <summary>Ceiling on time spent airborne.</summary>
        public float MaximumAirborneSeconds => _maximumAirborneSeconds;

        /// <summary>Time spent on the floor.</summary>
        public float KnockdownSeconds => _knockdownSeconds;

        /// <summary>Time spent rising from the floor.</summary>
        public float GetUpSeconds => _getUpSeconds;

        /// <summary>How long after standing knockdowns and launches are downgraded.</summary>
        public float KnockdownImmunitySeconds => _knockdownImmunitySeconds;

        /// <summary>Multiplier on knockback for a hit whose reaction is knockback.</summary>
        public float KnockbackReactionMultiplier => _knockbackReactionMultiplier;

        /// <summary>Shove speed into a wall that staggers.</summary>
        public float WallImpactSpeed => _wallImpactSpeed;

        /// <summary>Stagger from a hard wall impact.</summary>
        public float WallImpactStaggerSeconds => _wallImpactStaggerSeconds;

        /// <summary>Fraction of top speed kept when reversing outright at a sprint.</summary>
        public float TurnSpeedFloor => _turnSpeedFloor;

        /// <summary>Relative mass in body contact.</summary>
        public float BodyMass => _bodyMass;

        /// <summary>Turn rate in degrees per second.</summary>
        public float TurnSpeedDegreesPerSecond => _turnSpeedDegreesPerSecond;

        /// <summary>Distance covered by a dash.</summary>
        public float DashDistance => _dashDistance;

        /// <summary>Duration of a dash.</summary>
        public float DashDurationSeconds => _dashDurationSeconds;

        /// <summary>Lockout after a dash before another may begin.</summary>
        public float DashCooldownSeconds => _dashCooldownSeconds;

        /// <summary>Stamina consumed per dash.</summary>
        public float DashStaminaCost => _dashStaminaCost;

        /// <summary>Duration of dodge invulnerability, in seconds from the start of the roll.</summary>
        /// <remarks>
        /// Capped at the roll's own length, so a roll tuned shorter than its invincibility never
        /// leaves the player invincible after it has ended.
        /// </remarks>
        public float InvulnerabilitySeconds => Mathf.Min(_invulnerabilitySeconds, _dashDurationSeconds);

        /// <summary>Fraction of the roll during which the stick can still steer it.</summary>
        public float DashSteerFraction => _dashSteerFraction;

        /// <summary>Fraction of the roll after which a buffered attack may cancel it.</summary>
        public float DashCancelFraction => _dashCancelFraction;


        /// <summary>Weapons the player can switch between.</summary>
        public WeaponDefinition[] Weapons => _weapons;

        /// <summary>Light attack chain in execution order, used when no weapon is equipped.</summary>
        public AttackDefinition[] LightComboChain => _lightComboChain;

        /// <summary>The heavy attack.</summary>
        public AttackDefinition HeavyAttack => _heavyAttack;

        /// <summary>The special ability attack.</summary>
        public AttackDefinition SpecialAttack => _specialAttack;

        /// <summary>Cooldown on the special ability.</summary>
        public float SpecialCooldownSeconds => _specialCooldownSeconds;

        /// <summary>Stronger special used when focus is full, or null to reuse the normal special.</summary>
        public AttackDefinition EmpoweredSpecialAttack => _empoweredSpecialAttack;

        /// <summary>Focus needed to empower a special.</summary>
        public float MaxFocus => _maxFocus;

        /// <summary>Focus gained from a clean deflect.</summary>
        public float FocusPerDeflect => _focusPerDeflect;

        /// <summary>Focus gained from a perfect dodge.</summary>
        public float FocusPerPerfectDodge => _focusPerPerfectDodge;

        /// <summary>Strike used to punish a broken guard.</summary>
        public AttackDefinition RiposteAttack => _riposteAttack;

        /// <summary>The execution used to punish a broken guard, or null to reuse the riposte.</summary>
        public AttackDefinition ExecutionAttack => _executionAttack;

        /// <summary>Posture capacity.</summary>
        public float MaxPosture => _maxPosture;

        /// <summary>Posture recovered per second.</summary>
        public float PostureRegenPerSecond => _postureRegenPerSecond;

        /// <summary>Posture cost of a late block.</summary>
        public float BlockPostureCost => _blockPostureCost;

        /// <summary>Fraction of damage that gets through a late block.</summary>
        public float BlockChipFraction => _blockChipFraction;

        /// <summary>Posture a clean deflect deals to the attacker.</summary>
        public float DeflectPostureDamage => _deflectPostureDamage;

        /// <summary>Health restored per heal.</summary>
        public float HealAmount => _healAmount;

        /// <summary>Heal uses available per attempt.</summary>
        public int HealCharges => _healCharges;

        /// <summary>Channel time before a heal lands.</summary>
        public float HealChannelSeconds => _healChannelSeconds;
    }
}
