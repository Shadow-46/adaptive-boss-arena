using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Creates the game's long-lived services and drives the clock.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The composition root. It is the only place that decides which concrete implementation stands
    /// behind each service interface, which is what allows every other system to depend on
    /// abstractions without anybody having to construct them.
    /// </para>
    /// <para>
    /// Its execution order is pinned far ahead of everything else for two reasons: services must
    /// exist before any consumer's <c>Start</c> runs, and the time service must advance before any
    /// system reads <see cref="ITimeService.DeltaTime"/> in the same frame. A consumer reading the
    /// clock before it ticks would run a frame behind, which is subtle enough to survive review and
    /// obvious enough to ruin combat timing.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        /// <summary>Runs well ahead of gameplay so services exist and the clock has advanced.</summary>
        public const int ExecutionOrder = -10000;

        [Header("Determinism")]
        [SerializeField]
        [Tooltip("Seed for all gameplay randomness. Zero draws a fresh seed from the system clock. " +
                 "Set a specific value to reproduce a fight exactly.")]
        private int _randomSeed;

        [SerializeField]
        [Tooltip("Log the seed in use at startup, so a fight worth reproducing can be recovered " +
                 "from the player log.")]
        private bool _logSeed = true;

        private TimeService _timeService;
        private CombatEventBus _combatEvents;
        private ServiceRegistry _registry;

        /// <summary>The seed all gameplay randomness was started from.</summary>
        public uint ActiveSeed { get; private set; }

        private void Awake()
        {
            _registry = new ServiceRegistry();

            _timeService = new TimeService(GameplayConstants.FixedTimeStep);
            _registry.Register<ITimeService>(_timeService);

            ActiveSeed = ResolveSeed();
            _registry.Register<IRandomProvider>(new XorShiftRandomProvider(ActiveSeed));

            // Registered here rather than owned by either combatant, because it is the channel
            // through which the boss learns what the player did without either one holding a
            // reference to the other.
            _combatEvents = new CombatEventBus();
            _registry.Register<ICombatEventBus>(_combatEvents);

            _registry.Register<ISaveService>(new JsonSaveService());

            ServiceRegistry.SetCurrent(_registry);

            if (_logSeed)
            {
                Debug.Log($"[Adaptive Boss Arena] Gameplay random seed: {ActiveSeed}", this);
            }
        }

        private void Update() => _timeService.Tick(Time.unscaledDeltaTime);

        private void OnDestroy()
        {
            // Time scale is global engine state, so it must be released rather than abandoned; left
            // at a hit-stop value it would carry a frozen game into the next scene or back into the
            // editor. The service owns that state and restores it, because nothing outside it is
            // permitted to write the engine's time scale directly.
            if (_timeService != null)
            {
                _timeService.SetPaused(false);
                _timeService.ClearTimeEffects();
            }

            // Subscribers are scene objects being torn down alongside this one. Dropping them stops
            // a stale delegate firing against a destroyed target during teardown.
            _combatEvents?.Clear();
            _registry?.Clear();
        }

        /// <summary>Chooses the seed, drawing from the clock when none was specified.</summary>
        private uint ResolveSeed() =>
            _randomSeed != 0
                ? unchecked((uint)_randomSeed)
                : unchecked((uint)System.DateTime.Now.Ticks);
    }
}
