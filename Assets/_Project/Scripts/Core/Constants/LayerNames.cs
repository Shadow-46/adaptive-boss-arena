using UnityEngine;

namespace AdaptiveBossArena.Core.Constants
{
    /// <summary>
    /// Names of the physics layers the project defines.
    /// </summary>
    /// <remarks>
    /// The project configurator creates these layers and configures the collision matrix from this
    /// list, so the names here are the single definition rather than a copy of what someone set in
    /// the inspector.
    /// </remarks>
    public static class LayerNames
    {
        /// <summary>The player's body collider.</summary>
        public const string Player = "Player";

        /// <summary>Volumes that deal damage on behalf of the player.</summary>
        public const string PlayerHitbox = "PlayerHitbox";

        /// <summary>Volumes on the player that receive damage.</summary>
        public const string PlayerHurtbox = "PlayerHurtbox";

        /// <summary>The boss's body collider.</summary>
        public const string Boss = "Boss";

        /// <summary>Volumes that deal damage on behalf of the boss.</summary>
        public const string BossHitbox = "BossHitbox";

        /// <summary>Volumes on the boss that receive damage.</summary>
        public const string BossHurtbox = "BossHurtbox";

        /// <summary>Arena floor and walls.</summary>
        public const string Arena = "Arena";

        /// <summary>Travelling damage volumes.</summary>
        public const string Projectile = "Projectile";

        /// <summary>Every custom layer, in the order the configurator assigns them.</summary>
        public static readonly string[] All =
        {
            Player,
            PlayerHitbox,
            PlayerHurtbox,
            Boss,
            BossHitbox,
            BossHurtbox,
            Arena,
            Projectile
        };
    }

    /// <summary>
    /// Resolved layer indices and masks, cached after first use.
    /// </summary>
    /// <remarks>
    /// <see cref="LayerMask.NameToLayer"/> performs a string comparison against the project's layer
    /// table. Calling it from a hitbox that overlaps several times per frame is wasteful, so
    /// resolution happens once and the results are reused.
    /// </remarks>
    public static class Layers
    {
        private static bool _initialised;

        private static int _player;
        private static int _playerHitbox;
        private static int _playerHurtbox;
        private static int _boss;
        private static int _bossHitbox;
        private static int _bossHurtbox;
        private static int _arena;
        private static int _projectile;

        /// <summary>Index of the player body layer.</summary>
        public static int Player => Resolved(ref _player);

        /// <summary>Index of the player hitbox layer.</summary>
        public static int PlayerHitbox => Resolved(ref _playerHitbox);

        /// <summary>Index of the player hurtbox layer.</summary>
        public static int PlayerHurtbox => Resolved(ref _playerHurtbox);

        /// <summary>Index of the boss body layer.</summary>
        public static int Boss => Resolved(ref _boss);

        /// <summary>Index of the boss hitbox layer.</summary>
        public static int BossHitbox => Resolved(ref _bossHitbox);

        /// <summary>Index of the boss hurtbox layer.</summary>
        public static int BossHurtbox => Resolved(ref _bossHurtbox);

        /// <summary>Index of the arena geometry layer.</summary>
        public static int Arena => Resolved(ref _arena);

        /// <summary>Index of the projectile layer.</summary>
        public static int Projectile => Resolved(ref _projectile);

        /// <summary>Mask matching everything a player attack may damage.</summary>
        public static int PlayerAttackMask => 1 << BossHurtbox;

        /// <summary>Mask matching everything a boss attack may damage.</summary>
        public static int BossAttackMask => 1 << PlayerHurtbox;

        /// <summary>Mask matching solid geometry that blocks movement and line of sight.</summary>
        public static int ArenaMask => 1 << Arena;

        /// <summary>Forces the cache to rebuild, for use after the configurator edits the layer table.</summary>
        public static void InvalidateCache() => _initialised = false;

        /// <summary>Returns a cached index, resolving the whole set on first access.</summary>
        private static int Resolved(ref int field)
        {
            if (!_initialised)
            {
                ResolveAll();
            }

            return field;
        }

        /// <summary>Resolves every layer index in one pass and reports any that are missing.</summary>
        private static void ResolveAll()
        {
            _initialised = true;

            _player = Lookup(LayerNames.Player);
            _playerHitbox = Lookup(LayerNames.PlayerHitbox);
            _playerHurtbox = Lookup(LayerNames.PlayerHurtbox);
            _boss = Lookup(LayerNames.Boss);
            _bossHitbox = Lookup(LayerNames.BossHitbox);
            _bossHurtbox = Lookup(LayerNames.BossHurtbox);
            _arena = Lookup(LayerNames.Arena);
            _projectile = Lookup(LayerNames.Projectile);
        }

        /// <summary>Resolves one layer, warning loudly when the project has not been configured.</summary>
        private static int Lookup(string layerName)
        {
            int index = LayerMask.NameToLayer(layerName);

            if (index < 0)
            {
                Debug.LogError(
                    $"Layer '{layerName}' does not exist. " +
                    "Run 'Adaptive Boss Arena/Setup/Configure Project' to create the project's layers.");
                return 0;
            }

            return index;
        }
    }
}
