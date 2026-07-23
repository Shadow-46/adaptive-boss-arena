using AdaptiveBossArena.Core.ScriptableObjects;
using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Tunable magnitudes for a character's procedural animation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole point of this game's characters being primitives is that they are cheap to move in
    /// code, so every bit of life they have comes from <see cref="CharacterAnimator"/> reading these
    /// numbers. Kept as an asset so the feel can be tuned in play mode and kept — the same reason
    /// <see cref="AdaptiveBossArena.Player.PlayerConfig"/> is an asset.
    /// </para>
    /// <para>
    /// Everything here is a <em>magnitude</em>, never a rule. The animation is presentation only: it
    /// changes how a swing looks, never when its hitbox opens. That timing lives in the attack's own
    /// frame data, which the animator reads but never alters.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "CharacterAnimationConfig",
        menuName = "Adaptive Boss Arena/Feel/Character Animation Config",
        order = 10)]
    public sealed class CharacterAnimationConfig : IdentifiableSO
    {
        [Header("Idle")]
        [SerializeField]
        [Tooltip("Vertical breathing amplitude while standing, in world units.")]
        private float _idleBobAmplitude = 0.04f;

        [SerializeField]
        [Tooltip("Breaths per second while idle.")]
        private float _idleBobFrequency = 1.4f;

        [SerializeField]
        [Tooltip("Gentle side-to-side sway while idle, in degrees.")]
        private float _idleSwayDegrees = 2f;

        [Header("Movement")]
        [SerializeField]
        [Tooltip("Forward lean at full running speed, in degrees. This is most of what makes a slide " +
                 "read as a run.")]
        private float _moveLeanDegrees = 11f;

        [Header("Attack — anticipation (startup)")]
        [SerializeField]
        [Tooltip("How far the body coils back and down while winding up, in world units.")]
        private float _anticipationCrouch = 0.13f;

        [SerializeField]
        [Range(0f, 0.5f)]
        [Tooltip("How much the body squashes (wider, shorter) while winding up.")]
        private float _anticipationSquash = 0.14f;

        [SerializeField]
        [Tooltip("How far the body leans back while winding up, in degrees.")]
        private float _anticipationLeanDegrees = 15f;

        [Header("Attack — strike (active)")]
        [SerializeField]
        [Tooltip("How far the body lunges forward on the live frames, in world units. The snap comes " +
                 "from the easing, not from this value.")]
        private float _strikeLunge = 0.38f;

        [SerializeField]
        [Range(0f, 0.5f)]
        [Tooltip("How much the body stretches (taller, thinner) as it commits to the blow.")]
        private float _strikeStretch = 0.16f;

        [SerializeField]
        [Tooltip("How far the body leans into the blow, in degrees.")]
        private float _strikeLeanDegrees = 19f;

        [SerializeField]
        [Range(1f, 3f)]
        [Tooltip("Multiplier applied to anticipation and strike for heavy attacks and abilities, so " +
                 "weight reads without new poses.")]
        private float _heavyMultiplier = 1.5f;

        [Header("Guard & dash")]
        [SerializeField]
        [Tooltip("How far the body sinks into a braced stance while guarding, in world units.")]
        private float _guardCrouch = 0.1f;

        [SerializeField]
        [Tooltip("Forward brace lean while guarding, in degrees.")]
        private float _guardLeanDegrees = 9f;

        [SerializeField]
        [Tooltip("Lean into the direction of travel during a dash, in degrees.")]
        private float _dashLeanDegrees = 24f;

        [SerializeField]
        [Range(0f, 0.5f)]
        [Tooltip("Stretch along the dash, selling the burst of speed.")]
        private float _dashStretch = 0.2f;

        [Header("Heal")]
        [SerializeField]
        [Tooltip("How far the body drops into a vulnerable crouch while channelling a heal.")]
        private float _healCrouch = 0.18f;

        [Header("Hit reaction")]
        [SerializeField]
        [Tooltip("How far a struck body is thrown along the blow, in world units.")]
        private float _recoilDistance = 0.3f;

        [SerializeField]
        [Range(0.05f, 0.6f)]
        [Tooltip("How long a hit recoil takes to spring back.")]
        private float _recoilDurationSeconds = 0.22f;

        [SerializeField]
        [Range(0f, 0.5f)]
        [Tooltip("Squash flinch at the moment of being hit.")]
        private float _recoilSquash = 0.13f;

        [Header("Death")]
        [SerializeField]
        [Tooltip("How far the body topples when defeated, in degrees.")]
        private float _deathFallDegrees = 82f;

        [SerializeField]
        [Tooltip("How far the body sinks as it falls, in world units.")]
        private float _deathSink = 0.35f;

        [Header("Flourish (phase transition)")]
        [SerializeField]
        [Tooltip("How far the boss rears back during a phase transition, in degrees.")]
        private float _flourishRearDegrees = 26f;

        [SerializeField]
        [Range(0f, 0.6f)]
        [Tooltip("Stretch during a phase-transition rear-up.")]
        private float _flourishStretch = 0.24f;

        [SerializeField]
        [Range(0.1f, 2f)]
        [Tooltip("How long a phase-transition flourish lasts.")]
        private float _flourishDurationSeconds = 0.7f;

        [Header("Easing (half-lives)")]
        [SerializeField]
        [Range(0.01f, 0.4f)]
        [Tooltip("Position easing half-life. Lower is snappier; the boss usually wants this higher " +
                 "than the player to feel heavy.")]
        private float _positionHalfLife = 0.05f;

        [SerializeField]
        [Range(0.01f, 0.4f)]
        [Tooltip("Rotation easing half-life.")]
        private float _rotationHalfLife = 0.05f;

        [SerializeField]
        [Range(0.01f, 0.4f)]
        [Tooltip("Scale (squash & stretch) easing half-life.")]
        private float _scaleHalfLife = 0.06f;

        /// <summary>Vertical breathing amplitude while idle.</summary>
        public float IdleBobAmplitude => _idleBobAmplitude;

        /// <summary>Breaths per second while idle.</summary>
        public float IdleBobFrequency => _idleBobFrequency;

        /// <summary>Idle sway magnitude in degrees.</summary>
        public float IdleSwayDegrees => _idleSwayDegrees;

        /// <summary>Forward lean at full running speed, in degrees.</summary>
        public float MoveLeanDegrees => _moveLeanDegrees;

        /// <summary>Coil-back distance during an attack wind-up.</summary>
        public float AnticipationCrouch => _anticipationCrouch;

        /// <summary>Squash amount during an attack wind-up.</summary>
        public float AnticipationSquash => _anticipationSquash;

        /// <summary>Lean-back angle during an attack wind-up.</summary>
        public float AnticipationLeanDegrees => _anticipationLeanDegrees;

        /// <summary>Forward lunge distance on the live frames.</summary>
        public float StrikeLunge => _strikeLunge;

        /// <summary>Stretch amount as the blow commits.</summary>
        public float StrikeStretch => _strikeStretch;

        /// <summary>Lean-into angle on the live frames.</summary>
        public float StrikeLeanDegrees => _strikeLeanDegrees;

        /// <summary>Multiplier applied to heavy attacks and abilities.</summary>
        public float HeavyMultiplier => _heavyMultiplier;

        /// <summary>Braced-stance sink distance while guarding.</summary>
        public float GuardCrouch => _guardCrouch;

        /// <summary>Brace lean while guarding.</summary>
        public float GuardLeanDegrees => _guardLeanDegrees;

        /// <summary>Lean into travel during a dash.</summary>
        public float DashLeanDegrees => _dashLeanDegrees;

        /// <summary>Stretch along a dash.</summary>
        public float DashStretch => _dashStretch;

        /// <summary>Vulnerable-crouch depth while healing.</summary>
        public float HealCrouch => _healCrouch;

        /// <summary>Hit-recoil throw distance.</summary>
        public float RecoilDistance => _recoilDistance;

        /// <summary>Hit-recoil spring-back duration.</summary>
        public float RecoilDurationSeconds => _recoilDurationSeconds;

        /// <summary>Squash flinch on being hit.</summary>
        public float RecoilSquash => _recoilSquash;

        /// <summary>Topple angle when defeated.</summary>
        public float DeathFallDegrees => _deathFallDegrees;

        /// <summary>Sink distance when defeated.</summary>
        public float DeathSink => _deathSink;

        /// <summary>Rear-back angle during a phase transition.</summary>
        public float FlourishRearDegrees => _flourishRearDegrees;

        /// <summary>Stretch during a phase transition.</summary>
        public float FlourishStretch => _flourishStretch;

        /// <summary>Duration of a phase-transition flourish.</summary>
        public float FlourishDurationSeconds => _flourishDurationSeconds;

        /// <summary>Position easing half-life.</summary>
        public float PositionHalfLife => _positionHalfLife;

        /// <summary>Rotation easing half-life.</summary>
        public float RotationHalfLife => _rotationHalfLife;

        /// <summary>Scale easing half-life.</summary>
        public float ScaleHalfLife => _scaleHalfLife;
    }
}
