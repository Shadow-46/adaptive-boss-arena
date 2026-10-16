using System.Collections.Generic;
using System.Linq;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Learning;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Plays whole fights against the learning system and checks the boss reads each kind of player for what
    /// they are.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The unit tests check each statistic in isolation; these check the loop end to end, over minutes, with
    /// the generated counter-strategies and the fight's real timings. A scripted player with one habit fights a
    /// boss that swings every couple of seconds, and the assertions are what a player would notice: whether
    /// the boss answered the habit they actually have, and how soon.
    /// </para>
    /// <para>
    /// Written after the boss was found reading an active attacker as passive. Every unit test passed while the
    /// one thing that mattered, the boss's conclusion about a real player, was wrong.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class AdaptationSimulationTests
    {
        private const string StrategyFolder = "Assets/_Project/ScriptableObjects/Strategies";
        private const float Step = 0.05f;

        // The fight's own learning timings, as generated into the boss config.
        private const float AnalysisInterval = 2.5f;
        private const float AdaptationCooldown = 12f;
        private const float MemoryHalfLife = 45f;

        /// <summary>How often the boss swings in these fights, and how long it winds up.</summary>
        private const float SwingEvery = 2.4f;
        private const float Windup = 0.6f;

        /// <summary>What the scripted player does when the boss winds up.</summary>
        private enum Answer
        {
            DodgeOnTime,
            DodgeEarly,
            Guard,
            Nothing
        }

        /// <summary>A scripted player: where they stand, how they move, how they answer and how they attack.</summary>
        private sealed class Player
        {
            public float Distance = 3f;
            public float CircleSpeed;
            public Answer Answer = Answer.DodgeOnTime;
            public Vector3 DodgeDirection = Vector3.zero;
            public float AttackEvery = 1.6f;
            public bool HeavyAttacks;
            public bool GuardsBetweenSwings;
        }

        /// <summary>One fight: the boss's learning loop, a clock and a record of what it adopted.</summary>
        private sealed class Fight
        {
            private readonly CombatMemory _memory = new CombatMemory();
            private readonly BehaviorTracker _tracker;
            private readonly AdaptationManager _manager;
            private readonly List<(float At, string Id)> _adopted = new List<(float, string)>();
            private readonly Player _player;
            private float _time;
            private float _angle;
            private float _nextSwing = 1f;
            private float _nextAttack = 0.5f;
            private int _dodgeFlip;

            public Fight(Player player, int seed = 11)
            {
                _player = player;
                _tracker = new BehaviorTracker(_memory, MemoryHalfLife);
                Tuning = new BossTuning(3f, 0.5f, 0.12f);

                _manager = new AdaptationManager(
                    _memory, _tracker, new BehaviorProfile(), Tuning, LoadStrategies(),
                    new XorShiftRandomProvider((uint)seed), AnalysisInterval, AdaptationCooldown, MemoryHalfLife);

                _manager.StrategyAdopted += strategy => _adopted.Add((_time, strategy.name));
            }

            public BossTuning Tuning { get; }

            public BehaviorProfile Profile => _manager.Profile;

            public float FirstAdoptionOf(string id) =>
                _adopted.Where(entry => entry.Id == id).Select(entry => entry.At)
                    .DefaultIfEmpty(float.PositiveInfinity).First();

            public string Timeline() => string.Join(", ", _adopted.Select(entry => $"{entry.Id}@{entry.At:0}s"));

            public void Run(float seconds, int phase)
            {
                float end = _time + seconds;

                while (_time < end)
                {
                    _time += Step;
                    PlayerActs();
                    BossActs();

                    _manager.Tick(Step, _time, Vector3.zero, Observe(), phase);
                }
            }

            private PlayerObservation Observe()
            {
                _angle += _player.CircleSpeed / Mathf.Max(0.5f, _player.Distance) * Step;
                Vector3 position = new Vector3(Mathf.Sin(_angle), 0f, Mathf.Cos(_angle)) * _player.Distance;
                Vector3 tangent = new Vector3(Mathf.Cos(_angle), 0f, -Mathf.Sin(_angle)) * _player.CircleSpeed;

                bool guarding = _player.GuardsBetweenSwings;

                return new PlayerObservation
                {
                    Timestamp = _time,
                    Position = position,
                    Velocity = tangent,
                    ActionState = guarding ? ObservableActionState.Guarding : ObservableActionState.Moving,
                    IsValid = true
                };
            }

            private void PlayerActs()
            {
                if (_time < _nextAttack)
                {
                    return;
                }

                _nextAttack = _time + _player.AttackEvery;
                Publish(CombatEventKind.AttackStarted, CombatantTeam.Player,
                    _player.HeavyAttacks ? DamageType.Heavy : DamageType.Light);
            }

            private void BossActs()
            {
                if (_time < _nextSwing)
                {
                    return;
                }

                float swingAt = _nextSwing;
                _nextSwing += SwingEvery;

                _tracker.OnCombatEvent(new CombatEvent
                {
                    Kind = CombatEventKind.AttackStarted,
                    Actor = CombatantTeam.Boss,
                    Timestamp = swingAt,
                    WindupSeconds = Windup
                });

                switch (_player.Answer)
                {
                    case Answer.DodgeOnTime:
                    case Answer.DodgeEarly:
                        float lead = _player.Answer == Answer.DodgeEarly ? 0.05f : Windup * 0.75f;
                        Vector3 direction = _player.DodgeDirection != Vector3.zero
                            ? _player.DodgeDirection
                            : (_dodgeFlip++ % 2 == 0 ? Vector3.left : Vector3.right);

                        Publish(CombatEventKind.DodgePerformed, CombatantTeam.Player, DamageType.Light,
                            swingAt + lead, direction);
                        Publish(CombatEventKind.AttackEvaded, CombatantTeam.Boss, DamageType.Light, swingAt + Windup);

                        // A dodged swing also ends touching nothing: the boss must count it once.
                        Publish(CombatEventKind.AttackWhiffed, CombatantTeam.Boss, DamageType.Light, swingAt + 1f);
                        break;

                    case Answer.Guard:
                        Publish(CombatEventKind.Blocked, CombatantTeam.Player, DamageType.Light, swingAt + Windup);
                        Publish(CombatEventKind.AttackWhiffed, CombatantTeam.Boss, DamageType.Light, swingAt + 1f);
                        break;

                    default:
                        Publish(CombatEventKind.AttackLanded, CombatantTeam.Boss, DamageType.Light, swingAt + Windup);
                        break;
                }
            }

            private void Publish(
                CombatEventKind kind, CombatantTeam actor, DamageType damage, float at = -1f,
                Vector3 direction = default) =>
                _tracker.OnCombatEvent(new CombatEvent
                {
                    Kind = kind,
                    Actor = actor,
                    Timestamp = at < 0f ? _time : at,
                    DamageType = damage,
                    Direction = direction
                });

            private static IReadOnlyList<CounterStrategy> LoadStrategies() =>
                AssetDatabase.FindAssets("t:CounterStrategy", new[] { StrategyFolder })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<CounterStrategy>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(strategy => strategy != null)
                    .ToList();
        }

        [SetUp]
        public void RequireStrategies()
        {
            Assume.That(
                AssetDatabase.FindAssets("t:CounterStrategy", new[] { StrategyFolder }).Length,
                Is.GreaterThan(0),
                "Run setup to generate the counter-strategies first.");
        }

        [Test]
        public void AnActiveAllRounderIsNeverReadAsPassive()
        {
            // Attacks every second and a half, dodges every swing on time, both ways: no habit worth countering,
            // and certainly not passivity. The old attack-rate average read exactly this player as passive.
            var fight = new Fight(new Player { AttackEvery = 1.5f });
            fight.Run(180f, phase: 1);

            Assert.AreEqual(float.PositiveInfinity, fight.FirstAdoptionOf("CounterPassivity"),
                "An attacker was read as passive. Adopted: " + fight.Timeline());
            Assert.Greater(fight.Profile.Get(BehaviorFeature.AttackFrequency).Value, 0.4f);
        }

        [Test]
        public void APassivePlayerIsReadAsPassiveWithinHalfAMinute()
        {
            var fight = new Fight(new Player { AttackEvery = 15f, Distance = 4f });
            fight.Run(60f, phase: 1);

            Assert.Less(fight.FirstAdoptionOf("CounterPassivity"), 30f, "Adopted: " + fight.Timeline());
        }

        [Test]
        public void AKiterIsAnsweredWithGapClosers()
        {
            var fight = new Fight(new Player { Distance = 8f, AttackEvery = 6f });
            fight.Run(60f, phase: 1);

            Assert.Less(fight.FirstAdoptionOf("CounterKiting"), 40f, "Adopted: " + fight.Timeline());
        }

        [Test]
        public void ATurtleIsReadFromTheGuard()
        {
            var fight = new Fight(new Player { Answer = Answer.Guard, GuardsBetweenSwings = true });
            fight.Run(60f, phase: 1);

            Assert.Greater(fight.Profile.Get(BehaviorFeature.Turtling).Value, 0.8f);
            Assert.Less(fight.FirstAdoptionOf("CounterGuardReliance"), 45f, "Adopted: " + fight.Timeline());
        }

        [Test]
        public void AnEarlyRollerIsSeenRollingEarly()
        {
            var fight = new Fight(new Player { Answer = Answer.DodgeEarly });
            fight.Run(40f, phase: 1);

            FeatureReading earliness = fight.Profile.Get(BehaviorFeature.DodgeEarliness);
            Assert.Greater(earliness.Value, 0.8f);
            Assert.Greater(earliness.Confidence, 0.5f);
        }

        [Test]
        public void AnOnTimeRollerIsNotCalledEarly()
        {
            var fight = new Fight(new Player());
            fight.Run(40f, phase: 1);

            Assert.Less(fight.Profile.Get(BehaviorFeature.DodgeEarliness).Value, 0.2f);
        }

        [Test]
        public void APlayerCirclingOneWayIsSeenCircling()
        {
            var fight = new Fight(new Player { CircleSpeed = -3f });
            fight.Run(40f, phase: 1);

            FeatureReading circling = fight.Profile.Get(BehaviorFeature.CirclingBias);
            Assert.Greater(circling.Value, 0.8f);
            Assert.Greater(circling.Confidence, 0.5f);
        }

        [Test]
        public void ADodgedSwingIsCountedOnce()
        {
            // Every swing dodged: evasion should read as total, from as many swings as were thrown, not twice
            // as many.
            var fight = new Fight(new Player());
            fight.Run(24f, phase: 1);

            FeatureReading evasion = fight.Profile.Get(BehaviorFeature.EvasionSuccess);
            Assert.Greater(evasion.Value, 0.95f);
            Assert.Less(evasion.SampleCount, 12, "A dodged swing was counted as both evaded and whiffed.");
        }

        [Test]
        public void AHeavySpammerIsParriedMoreInTheSecondPhase()
        {
            var fight = new Fight(new Player { HeavyAttacks = true, AttackEvery = 1.2f });
            fight.Run(60f, phase: 2);

            Assert.Less(fight.FirstAdoptionOf("CounterHeavySpam"), 40f, "Adopted: " + fight.Timeline());
            Assert.Greater(fight.Tuning.Get(BossTuningParameter.ParryChance), 0.2f);
        }

        [Test]
        public void AnAbandonedHabitStopsBeingCountedAgainstThePlayer()
        {
            // A minute of heavy swings, then two minutes of light ones: the heavy read must fade.
            var player = new Player { HeavyAttacks = true, AttackEvery = 1.2f };
            var fight = new Fight(player);
            fight.Run(60f, phase: 2);
            float whileHabitual = fight.Profile.Get(BehaviorFeature.HeavyAttackRatio).Value;

            player.HeavyAttacks = false;
            fight.Run(120f, phase: 2);

            Assert.Greater(whileHabitual, 0.9f);
            Assert.Less(fight.Profile.Get(BehaviorFeature.HeavyAttackRatio).Value, 0.25f,
                "The boss still read a habit the player dropped two minutes ago.");
        }

        [Test]
        public void ATurtleIsMetWithTheBlowsNoGuardStops()
        {
            var fight = new Fight(new Player { Answer = Answer.Guard, GuardsBetweenSwings = true });
            fight.Run(60f, phase: 1);

            Assert.Less(fight.FirstAdoptionOf("CounterTurtling"), 45f, "Adopted: " + fight.Timeline());
            Assert.Greater(fight.Tuning.Get(BossTuningParameter.AnswerGuardWeight), 1f,
                "The boss read the turtle but does not reach for the boot.");
        }

        [Test]
        public void AnEarlyRollerIsMetWithAHeldBlade()
        {
            var fight = new Fight(new Player { Answer = Answer.DodgeEarly });
            fight.Run(60f, phase: 1);

            Assert.Less(fight.FirstAdoptionOf("CounterEarlyRoll"), 45f, "Adopted: " + fight.Timeline());
            Assert.Greater(fight.Tuning.Get(BossTuningParameter.HoldWindupChance), 0.3f);
        }

        [Test]
        public void ACirclerIsCutOff()
        {
            var fight = new Fight(new Player { CircleSpeed = -3f });
            fight.Run(60f, phase: 1);

            Assert.Less(fight.FirstAdoptionOf("CounterCircling"), 45f, "Adopted: " + fight.Timeline());
            Assert.Greater(fight.Tuning.Get(BossTuningParameter.CutOffStrength), 0.5f);
        }

        [Test]
        public void APlayerStandingInItsFaceIsThrownOff()
        {
            var fight = new Fight(new Player { Distance = 1.8f });
            fight.Run(60f, phase: 1);

            Assert.Less(fight.FirstAdoptionOf("CounterCrowding"), 45f, "Adopted: " + fight.Timeline());
        }

    }
}
