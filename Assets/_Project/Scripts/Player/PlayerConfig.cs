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
        [Range(0f, 1f)]
        [Tooltip("Fraction of the dash, measured from its start, during which the player is " +
                 "invulnerable. Below one so that dashing into an attack late still gets punished.")]
        private float _invulnerabilityFraction = 0.75f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Fraction of dash speed retained when the dash ends. Above zero so the character " +
                 "flows out of a dash instead of hitting a wall of friction.")]
        private float _dashExitSpeedFraction = 0.35f;

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

        /// <summary>Duration of dash invulnerability, in seconds.</summary>
        public float InvulnerabilitySeconds => _dashDurationSeconds * _invulnerabilityFraction;

        /// <summary>Average dash speed implied by its distance and duration.</summary>
        public float DashSpeed => _dashDurationSeconds <= 0f ? 0f : _dashDistance / _dashDurationSeconds;

        /// <summary>Fraction of dash speed carried into the state that follows a dash.</summary>
        public float DashExitSpeedFraction => _dashExitSpeedFraction;

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
