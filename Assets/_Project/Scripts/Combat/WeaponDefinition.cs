using AdaptiveBossArena.Core.ScriptableObjects;
using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>How a weapon answers an incoming attack.</summary>
    /// <remarks>
    /// Each weapon defends differently on purpose. If all three deflected identically the choice
    /// would be cosmetic, and the boss's adaptation would have nothing meaningful to read from which
    /// one the player favours.
    /// </remarks>
    public enum DefenceStyle
    {
        /// <summary>A tight deflect window rewarded with a riposte. Demanding and decisive.</summary>
        Deflect = 0,

        /// <summary>
        /// No parry. Attacks carry through incoming hits instead.
        /// </summary>
        /// <remarks>
        /// Trades safety for certainty: this weapon wins exchanges it commits to, and loses badly
        /// when the commitment was wrong.
        /// </remarks>
        HyperArmour = 1,

        /// <summary>A guard that can be held, draining stamina, rather than timed precisely.</summary>
        SustainedGuard = 2
    }

    /// <summary>
    /// A complete weapon: its moveset, its defence, and how it changes the fight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The moveset lives here rather than on the character, so switching weapon genuinely changes
    /// how the player fights rather than reskinning the same attacks.
    /// </para>
    /// <para>
    /// Three weapons is a deliberate ceiling, not a starting point. The boss builds its picture of
    /// the player from repetition within a fight of a few minutes; spreading that same budget across
    /// a dozen weapons would leave every habit below the confidence threshold and the boss would
    /// quietly stop adapting at all. Three is enough for the choice to matter and few enough that
    /// each still gets seen often enough to be learned from.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "WeaponDefinition",
        menuName = "Adaptive Boss Arena/Combat/Weapon Definition",
        order = 1)]
    public sealed class WeaponDefinition : IdentifiableSO
    {
        [Header("Identity")]
        [SerializeField]
        [Tooltip("Name shown when the weapon is drawn.")]
        private string _displayName = "Unnamed Weapon";

        [SerializeField]
        [Tooltip("Colour used for this weapon's attack overlays and trail.")]
        private Color _signatureColor = new Color(0.55f, 0.85f, 1f, 0.6f);

        [SerializeField]
        [Tooltip("Optional model shown in the character's hand while this weapon is drawn. Left empty " +
                 "the weapon reads as its colour and trail only; drop a mesh prefab in to make it " +
                 "appear, with no other change.")]
        private GameObject _modelPrefab;

        [Header("Moveset")]
        [SerializeField]
        [Tooltip("Light attack chain, executed in order while the combo window is met.")]
        private AttackDefinition[] _lightComboChain = new AttackDefinition[0];

        [SerializeField]
        [Tooltip("Heavy attack.")]
        private AttackDefinition _heavyAttack;

        [SerializeField]
        [Tooltip("Special ability attack.")]
        private AttackDefinition _specialAttack;

        [SerializeField]
        [Tooltip("Stronger special used when the focus meter is full. Optional; falls back to the " +
                 "normal special so a full meter is never wasted.")]
        private AttackDefinition _empoweredSpecialAttack;

        [SerializeField]
        [Tooltip("Strike used to punish a broken guard.")]
        private AttackDefinition _riposteAttack;

        [Header("Defence")]
        [SerializeField]
        [Tooltip("How this weapon answers an incoming attack.")]
        private DefenceStyle _defence = DefenceStyle.Deflect;

        [SerializeField]
        [Range(0.05f, 0.4f)]
        [Tooltip("Deflect window. Shorter is more demanding and, by design, more rewarding.")]
        private float _deflectWindowSeconds = 0.15f;

        [SerializeField]
        [Tooltip("Posture dealt to the attacker by a clean deflect with this weapon.")]
        private float _deflectPostureDamage = 24f;

        [Header("Handling")]
        [SerializeField]
        [Range(0.5f, 1.5f)]
        [Tooltip("Multiplier on movement speed while this weapon is drawn. Heavier weapons slow you.")]
        private float _moveSpeedMultiplier = 1f;

        [SerializeField]
        [Range(0.5f, 2f)]
        [Tooltip("Multiplier on stamina costs. Heavier weapons cost more per swing.")]
        private float _staminaCostMultiplier = 1f;

        /// <summary>Name shown when the weapon is drawn.</summary>
        public string DisplayName => _displayName;

        /// <summary>Colour used for this weapon's attack overlays.</summary>
        public Color SignatureColor => _signatureColor;

        /// <summary>Optional model shown in hand while drawn, or null to show none.</summary>
        public GameObject ModelPrefab => _modelPrefab;

        /// <summary>Light attack chain in execution order.</summary>
        public AttackDefinition[] LightComboChain => _lightComboChain;

        /// <summary>The heavy attack.</summary>
        public AttackDefinition HeavyAttack => _heavyAttack;

        /// <summary>The special ability attack.</summary>
        public AttackDefinition SpecialAttack => _specialAttack;

        /// <summary>Stronger special used when focus is full, or null to reuse the normal special.</summary>
        public AttackDefinition EmpoweredSpecialAttack => _empoweredSpecialAttack;

        /// <summary>Strike used to punish a broken guard.</summary>
        public AttackDefinition RiposteAttack => _riposteAttack;

        /// <summary>How this weapon answers an incoming attack.</summary>
        public DefenceStyle Defence => _defence;

        /// <summary>Deflect window in seconds.</summary>
        public float DeflectWindowSeconds => _deflectWindowSeconds;

        /// <summary>Posture a clean deflect deals.</summary>
        public float DeflectPostureDamage => _deflectPostureDamage;

        /// <summary>Movement speed multiplier while drawn.</summary>
        public float MoveSpeedMultiplier => _moveSpeedMultiplier;

        /// <summary>Stamina cost multiplier while drawn.</summary>
        public float StaminaCostMultiplier => _staminaCostMultiplier;

        /// <summary>True when this weapon can deflect at all.</summary>
        public bool CanDeflect => _defence != DefenceStyle.HyperArmour;
    }
}
