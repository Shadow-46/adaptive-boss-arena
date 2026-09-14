using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Breaks the arena's stone where the fight's heaviest blows land.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two things break stone: a ground impact that leaves a scar - the slam, the shockwave and the
    /// Cataclysmic Wave - and a body driven into the wall. Both are already announced, by the hazard field
    /// and by the combat event bus, so this only listens. It lives in the composition root because it
    /// needs both, and neither the combat code nor the arena should know about the other.
    /// </para>
    /// <para>
    /// Presentation only. Listening to the immediate event bus is fine here for the reason it is forbidden
    /// on the AI side: nothing the boss decides can depend on it. Its random stream is its own, seeded
    /// once, rather than the fight's shared provider - debris drawing numbers from the boss's stream would
    /// make its choices depend on how much stone happened to break.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DestructibleField : MonoBehaviour
    {
        private const uint DebrisSeed = 772026u;

        [SerializeField]
        [Tooltip("Where broken pieces come from.")]
        private DebrisPool _pool;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Extra reach beyond a ground scar's radius, so a slam against the parapet cracks it.")]
        private float _impactReachMargin = 0.8f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How far along the wall a body driven into it breaks stone.")]
        private float _wallImpactReach = 2.2f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Typical launch speed of a broken piece, in metres per second.")]
        private float _pieceSpeed = 4.5f;

        private readonly XorShiftRandomProvider _random = new XorShiftRandomProvider(DebrisSeed);

        private Destructible[] _destructibles = new Destructible[0];
        private ICombatEventBus _events;
        private HazardField _hazards;

        /// <summary>Every breakable in the arena.</summary>
        public Destructible[] Destructibles => _destructibles;

        /// <summary>Assigns the pool. Used by the scene generator.</summary>
        /// <param name="pool">Where broken pieces come from.</param>
        public void Bind(DebrisPool pool) => _pool = pool;

        private void Start()
        {
            _destructibles = FindObjectsByType<Destructible>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            if (ServiceRegistry.Current != null && ServiceRegistry.Current.TryGet(out _events))
            {
                _events.EventRecorded += OnCombatEvent;
            }

            _hazards = FindAnyObjectByType<HazardField>();

            if (_hazards != null)
            {
                _hazards.Spawned += OnHazardSpawned;
            }
        }

        private void OnDestroy()
        {
            if (_events != null)
            {
                _events.EventRecorded -= OnCombatEvent;
            }

            if (_hazards != null)
            {
                _hazards.Spawned -= OnHazardSpawned;
            }
        }

        /// <summary>Breaks every intact stone within reach of an impact.</summary>
        /// <param name="impact">Where the blow landed.</param>
        /// <param name="reach">How far across the floor it breaks things.</param>
        /// <returns>How many stones broke.</returns>
        public int BreakWithin(Vector3 impact, float reach)
        {
            int broken = 0;

            foreach (Destructible destructible in _destructibles)
            {
                if (destructible != null &&
                    DestructionRules.Reaches(impact, reach, destructible.Centre) &&
                    destructible.Break(impact, _pool, _random, _pieceSpeed))
                {
                    broken++;
                }
            }

            return broken;
        }

        /// <summary>Makes the room whole again and clears its debris, for a retry.</summary>
        public void RestoreAll()
        {
            foreach (Destructible destructible in _destructibles)
            {
                if (destructible != null)
                {
                    destructible.Restore();
                }
            }

            if (_pool != null)
            {
                _pool.Clear();
            }

            // Reseeded, so every attempt breaks stone the same way.
            _random.Reseed(DebrisSeed);
        }

        private void OnCombatEvent(CombatEvent combatEvent)
        {
            if (combatEvent.Kind == CombatEventKind.WallImpact)
            {
                BreakWithin(combatEvent.Position, _wallImpactReach);
            }
        }

        private void OnHazardSpawned(Vector3 centre, float radius) => BreakWithin(centre, radius + _impactReachMargin);
    }
}
