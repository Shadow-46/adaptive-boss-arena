using System.Collections.Generic;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Learning;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests that the boss draws the intended conclusions from a fight it has watched.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The learning system is pure C# specifically so this is possible. A synthetic stream of
    /// combat events stands in for a player with a particular habit, and the assertions check that
    /// the boss's picture of them matches. Without this, the only way to know whether the boss is
    /// learning correctly would be to play against it and form an impression.
    /// </para>
    /// <para>
    /// The confidence assertions matter as much as the value ones. A boss that reached a strong
    /// conclusion from two observations would be countering coincidences, which reads to a player as
    /// the boss having seen something it should not have.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class PatternRecognizerTests
    {
        private const float MemoryHalfLife = 45f;

        private CombatMemory _memory;
        private BehaviorTracker _tracker;
        private PatternRecognizer _recognizer;
        private BehaviorProfile _profile;

        [SetUp]
        public void CreateSystem()
        {
            _memory = new CombatMemory();
            _tracker = new BehaviorTracker(_memory, MemoryHalfLife);
            _recognizer = new PatternRecognizer();
            _profile = new BehaviorProfile();
        }

        private void Feed(CombatEventKind kind, float timestamp, CombatantTeam actor = CombatantTeam.Player,
            DamageType damageType = DamageType.Light, Vector3 direction = default) =>
            _tracker.OnCombatEvent(new CombatEvent
            {
                Kind = kind,
                Actor = actor,
                Timestamp = timestamp,
                DamageType = damageType,
                Direction = direction
            });

        private void Analyse(float timeNow) => _recognizer.Analyse(_tracker, _memory, _profile, timeNow);

        [Test]
        public void WithNoObservations_EveryHabitHasZeroConfidence()
        {
            Analyse(0f);

            foreach (BehaviorFeature feature in System.Enum.GetValues(typeof(BehaviorFeature)))
            {
                Assert.AreEqual(0f, _profile.Get(feature).Confidence, 0.001f,
                    $"{feature} claimed confidence without evidence.");
            }
        }

        [Test]
        public void APlayerWhoAlwaysAttacksAfterDodging_IsRecognised()
        {
            // Twenty dodges, each immediately followed by an attack. The habit the brief calls out.
            float time = 0f;
            for (int i = 0; i < 20; i++)
            {
                Feed(CombatEventKind.DodgePerformed, time, direction: Vector3.left);
                Feed(CombatEventKind.AttackStarted, time + 0.2f);
                time += 2f;
            }

            Analyse(time);

            FeatureReading reading = _profile.Get(BehaviorFeature.PostDodgeAggression);

            Assert.Greater(reading.Value, 0.9f, "The follow-up habit should be read as near-total.");
            Assert.Greater(reading.Confidence, 0.7f, "Twenty dodges is ample evidence.");
        }

        [Test]
        public void APlayerWhoNeverAttacksAfterDodging_IsNotAccused()
        {
            float time = 0f;
            for (int i = 0; i < 20; i++)
            {
                Feed(CombatEventKind.DodgePerformed, time, direction: Vector3.left);

                // The attack comes long after the follow-up window has closed.
                Feed(CombatEventKind.AttackStarted, time + 1.5f);
                time += 3f;
            }

            Analyse(time);

            Assert.Less(_profile.Get(BehaviorFeature.PostDodgeAggression).Value, 0.1f);
        }

        [Test]
        public void ASingleDodgeAndAttack_ProducesLowConfidence()
        {
            Feed(CombatEventKind.DodgePerformed, 0f, direction: Vector3.left);
            Feed(CombatEventKind.AttackStarted, 0.2f);

            Analyse(1f);

            FeatureReading reading = _profile.Get(BehaviorFeature.PostDodgeAggression);

            // The value is a perfect one-for-one, but one observation must not be trusted. This is
            // the guard against the boss countering a coincidence.
            Assert.AreEqual(1f, reading.Value, 0.01f);
            Assert.Less(reading.Confidence, 0.15f);
        }

        [Test]
        public void APlayerWhoAlwaysRollsTheSameWay_IsRecognisedAsPredictable()
        {
            float time = 0f;
            for (int i = 0; i < 25; i++)
            {
                Feed(CombatEventKind.DodgePerformed, time, direction: Vector3.left);
                time += 1f;
            }

            Analyse(time);

            FeatureReading reading = _profile.Get(BehaviorFeature.DashDirectionBias);

            Assert.Greater(reading.Value, 0.9f);
            Assert.Greater(reading.Confidence, 0.7f);
        }

        [Test]
        public void APlayerWhoRollsEveryWay_IsNotRecognisedAsPredictable()
        {
            Vector3[] directions =
            {
                Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
                new Vector3(1f, 0f, 1f), new Vector3(-1f, 0f, 1f),
                new Vector3(1f, 0f, -1f), new Vector3(-1f, 0f, -1f)
            };

            float time = 0f;
            for (int i = 0; i < 32; i++)
            {
                Feed(CombatEventKind.DodgePerformed, time, direction: directions[i % directions.Length]);
                time += 0.5f;
            }

            Analyse(time);

            // A boss that claimed to predict evenly distributed movement would be visibly cheating.
            Assert.Less(_profile.Get(BehaviorFeature.DashDirectionBias).Value, 0.1f);
        }

        [Test]
        public void APlayerWhoLeansOnHeavyAttacks_IsRecognised()
        {
            float time = 0f;
            for (int i = 0; i < 20; i++)
            {
                Feed(CombatEventKind.AttackStarted, time, damageType: DamageType.Heavy);
                time += 1.5f;
            }

            Analyse(time);

            FeatureReading reading = _profile.Get(BehaviorFeature.HeavyAttackRatio);

            Assert.Greater(reading.Value, 0.9f);
            Assert.Greater(reading.Confidence, 0.7f);
        }

        [Test]
        public void APlayerWhoAvoidsEverything_IsRecognisedAsEvasive()
        {
            float time = 0f;
            for (int i = 0; i < 15; i++)
            {
                Feed(CombatEventKind.AttackEvaded, time, CombatantTeam.Boss);
                time += 2f;
            }

            Analyse(time);

            FeatureReading reading = _profile.Get(BehaviorFeature.EvasionSuccess);

            Assert.Greater(reading.Value, 0.9f);
            Assert.Greater(reading.Confidence, 0.7f);
        }

        [Test]
        public void APlayerWhoLeansOnHealing_IsRecognised()
        {
            // Four heals in a fight is the point the design treats healing as a defining habit — the
            // boss should read it and be willing to punish the openings it creates.
            float time = 0f;
            for (int i = 0; i < 4; i++)
            {
                Feed(CombatEventKind.HealStarted, time);
                time += 4f;
            }

            Analyse(time);

            FeatureReading reading = _profile.Get(BehaviorFeature.HealFrequency);

            Assert.Greater(reading.Value, 0.9f, "A player healing repeatedly should read as reliant on it.");
            Assert.Greater(reading.Confidence, 0.6f, "Four heals is enough to believe the habit.");
        }

        [Test]
        public void APlayerWhoPerfectDodgesOnRhythm_IsRecognised()
        {
            // Every dash lands the perfect-dodge frame: a flawless, and therefore exploitable, rhythm.
            float time = 0f;
            for (int i = 0; i < 20; i++)
            {
                Feed(CombatEventKind.DodgePerformed, time, direction: Vector3.left);
                Feed(CombatEventKind.PerfectDodge, time + 0.01f, direction: Vector3.left);
                time += 1.5f;
            }

            Analyse(time);

            FeatureReading reading = _profile.Get(BehaviorFeature.DodgeTimingConsistency);

            Assert.Greater(reading.Value, 0.9f, "Flawless dodge timing should read as near-total consistency.");
            Assert.Greater(reading.Confidence, 0.7f, "Twenty dodges is ample evidence.");
        }

        [Test]
        public void APlayerWhoDodgesButRarelyTimesItPerfectly_IsNotAccusedOfRhythm()
        {
            // The same twenty dodges, none of them frame-perfect: the habit the feint counters is
            // absent, and reading it anyway would bait a dodge the player never had.
            float time = 0f;
            for (int i = 0; i < 20; i++)
            {
                Feed(CombatEventKind.DodgePerformed, time, direction: Vector3.left);
                time += 1.5f;
            }

            Analyse(time);

            Assert.Less(_profile.Get(BehaviorFeature.DodgeTimingConsistency).Value, 0.1f);
        }

        [Test]
        public void PreferredDistance_ReflectsWhereThePlayerActuallyStands()
        {
            float time = 0f;
            for (int i = 0; i < 200; i++)
            {
                // A player holding a long distance, sampled over many seconds.
                _tracker.Tick(0.05f, time, separationDistance: 11f);
                time += 0.05f;
            }

            Analyse(time);

            FeatureReading reading = _profile.Get(BehaviorFeature.PreferredDistance);

            Assert.Greater(reading.Value, 0.6f, "A kiting player should read as preferring distance.");
            Assert.Greater(reading.Confidence, 0.5f);
        }

        [Test]
        public void AnAbandonedHabit_Fades()
        {
            // A short-memoried boss, so the decay is observable within a test.
            var memory = new CombatMemory();
            var tracker = new BehaviorTracker(memory, memoryHalfLifeSeconds: 2f);
            var profile = new BehaviorProfile();

            float time = 0f;
            for (int i = 0; i < 30; i++)
            {
                tracker.OnCombatEvent(new CombatEvent
                {
                    Kind = CombatEventKind.DodgePerformed,
                    Actor = CombatantTeam.Player,
                    Timestamp = time,
                    Direction = Vector3.left
                });

                time += 0.2f;
            }

            _recognizer.Analyse(tracker, memory, profile, time);
            float whileHabitual = profile.Get(BehaviorFeature.DashDirectionBias).Confidence;

            // The player stops dodging entirely for a long stretch.
            for (int i = 0; i < 400; i++)
            {
                tracker.Tick(0.05f, time, separationDistance: 5f);
                time += 0.05f;
            }

            _recognizer.Analyse(tracker, memory, profile, time);
            float afterAbandoning = profile.Get(BehaviorFeature.DashDirectionBias).Confidence;

            // Confidence must fall away, or switching tactics would never stop the boss countering
            // a habit the player no longer has.
            Assert.Less(afterAbandoning, whileHabitual);
        }
    }

    /// <summary>
    /// Tests that counter-strategy selection respects its gates and stays reproducible.
    /// </summary>
    [TestFixture]
    public sealed class CounterSelectorTests
    {
        private readonly List<CounterStrategy> _created = new List<CounterStrategy>();

        [TearDown]
        public void DestroyStrategies()
        {
            foreach (CounterStrategy strategy in _created)
            {
                Object.DestroyImmediate(strategy);
            }

            _created.Clear();
        }

        private CounterStrategy MakeStrategy(
            BehaviorFeature feature,
            float threshold,
            float minimumConfidence,
            int minimumPhase,
            float weight = 1f)
        {
            var strategy = ScriptableObject.CreateInstance<CounterStrategy>();
            _created.Add(strategy);

            var serialized = new SerializedObject(strategy);
            serialized.FindProperty("_triggerFeature").enumValueIndex = (int)feature;
            serialized.FindProperty("_comparison").enumValueIndex = (int)FeatureComparison.AtOrAbove;
            serialized.FindProperty("_threshold").floatValue = threshold;
            serialized.FindProperty("_minimumConfidence").floatValue = minimumConfidence;
            serialized.FindProperty("_minimumPhase").intValue = minimumPhase;
            serialized.FindProperty("_selectionWeight").floatValue = weight;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return strategy;
        }

        private static BehaviorProfile ProfileWith(BehaviorFeature feature, float value, int sampleCount)
        {
            var profile = new BehaviorProfile();
            profile.Set(feature, value, sampleCount, halfConfidenceSampleCount: 10);
            return profile;
        }

        [Test]
        public void AStrategyBelowItsThreshold_IsNotSelected()
        {
            CounterStrategy strategy = MakeStrategy(
                BehaviorFeature.HeavyAttackRatio, threshold: 0.8f, minimumConfidence: 0f, minimumPhase: 1);

            var selector = new CounterSelector(new XorShiftRandomProvider(1u));
            BehaviorProfile profile = ProfileWith(BehaviorFeature.HeavyAttackRatio, 0.3f, 100);

            Assert.IsNull(selector.Select(new[] { strategy }, profile, 1, null));
        }

        [Test]
        public void AStrategyWithoutEnoughConfidence_IsNotSelected()
        {
            CounterStrategy strategy = MakeStrategy(
                BehaviorFeature.HeavyAttackRatio, threshold: 0.5f, minimumConfidence: 0.8f, minimumPhase: 1);

            var selector = new CounterSelector(new XorShiftRandomProvider(1u));

            // The habit is strong but rests on a single observation.
            BehaviorProfile profile = ProfileWith(BehaviorFeature.HeavyAttackRatio, 1f, 1);

            Assert.IsNull(selector.Select(new[] { strategy }, profile, 1, null));
        }

        [Test]
        public void AStrategyGatedToALaterPhase_IsNotSelectedEarly()
        {
            CounterStrategy strategy = MakeStrategy(
                BehaviorFeature.HeavyAttackRatio, threshold: 0.5f, minimumConfidence: 0f, minimumPhase: 3);

            var selector = new CounterSelector(new XorShiftRandomProvider(1u));
            BehaviorProfile profile = ProfileWith(BehaviorFeature.HeavyAttackRatio, 1f, 100);

            Assert.IsNull(selector.Select(new[] { strategy }, profile, 1, null));
            Assert.IsNotNull(selector.Select(new[] { strategy }, profile, 3, null));
        }

        [Test]
        public void AnAlreadyAdoptedStrategy_IsNotSelectedAgain()
        {
            CounterStrategy strategy = MakeStrategy(
                BehaviorFeature.HeavyAttackRatio, threshold: 0.5f, minimumConfidence: 0f, minimumPhase: 1);

            var selector = new CounterSelector(new XorShiftRandomProvider(1u));
            BehaviorProfile profile = ProfileWith(BehaviorFeature.HeavyAttackRatio, 1f, 100);

            var adopted = new HashSet<CounterStrategy> { strategy };

            Assert.IsNull(selector.Select(new[] { strategy }, profile, 1, adopted));
        }

        [Test]
        public void SelectionIsReproducibleFromASeed()
        {
            CounterStrategy first = MakeStrategy(
                BehaviorFeature.HeavyAttackRatio, 0.4f, 0f, 1, weight: 1f);

            CounterStrategy second = MakeStrategy(
                BehaviorFeature.HeavyAttackRatio, 0.4f, 0f, 1, weight: 3f);

            var catalogue = new[] { first, second };
            BehaviorProfile profile = ProfileWith(BehaviorFeature.HeavyAttackRatio, 1f, 100);

            var runA = new List<CounterStrategy>();
            var runB = new List<CounterStrategy>();

            var selectorA = new CounterSelector(new XorShiftRandomProvider(4242u));
            var selectorB = new CounterSelector(new XorShiftRandomProvider(4242u));

            for (int i = 0; i < 50; i++)
            {
                runA.Add(selectorA.Select(catalogue, profile, 1, null));
                runB.Add(selectorB.Select(catalogue, profile, 1, null));
            }

            // Probabilistic behaviour that could not be reproduced from a seed would make the boss
            // impossible to test and any bug report impossible to act on.
            CollectionAssert.AreEqual(runA, runB);
        }

        [Test]
        public void SelectionIsNotDeterministicAcrossDifferentSeeds()
        {
            CounterStrategy first = MakeStrategy(BehaviorFeature.HeavyAttackRatio, 0.4f, 0f, 1, 1f);
            CounterStrategy second = MakeStrategy(BehaviorFeature.HeavyAttackRatio, 0.4f, 0f, 1, 1f);

            var catalogue = new[] { first, second };
            BehaviorProfile profile = ProfileWith(BehaviorFeature.HeavyAttackRatio, 1f, 100);

            var selector = new CounterSelector(new XorShiftRandomProvider(99u));
            var seen = new HashSet<CounterStrategy>();

            for (int i = 0; i < 100; i++)
            {
                seen.Add(selector.Select(catalogue, profile, 1, null));
            }

            // If the strongest habit always produced the same answer, the player would learn the
            // lookup table and the fight would become memorisation.
            Assert.AreEqual(2, seen.Count, "Selection among equal candidates should vary.");
        }

        [Test]
        public void APanickedWhiffer_DrawsAWhiffRatioCounter()
        {
            // WhiffRatio was a fully-computed feature with no strategy answering it. CounterWhiffPunish
            // closed that gap; this guards that a strategy keyed on WhiffRatio is genuinely selectable
            // once the habit is both strong and confident.
            CounterStrategy punishWhiffing = MakeStrategy(
                BehaviorFeature.WhiffRatio, threshold: 0.5f, minimumConfidence: 0.4f, minimumPhase: 1);

            var selector = new CounterSelector(new XorShiftRandomProvider(7u));
            BehaviorProfile profile = ProfileWith(BehaviorFeature.WhiffRatio, 0.8f, 100);

            Assert.AreSame(punishWhiffing, selector.Select(new[] { punishWhiffing }, profile, 1, null));
        }
    }

    /// <summary>Tests for the gradual easing that keeps adaptation perceptible rather than abrupt.</summary>
    [TestFixture]
    public sealed class BossTuningTests
    {
        private const float Frame = 1f / 60f;

        [Test]
        public void NewTuning_SitsAtItsBaseline()
        {
            var tuning = new BossTuning(baselinePreferredRange: 3f, baselineAggression: 0.5f);

            Assert.AreEqual(3f, tuning.Get(BossTuningParameter.PreferredRange), 0.001f);
            Assert.AreEqual(0.5f, tuning.Get(BossTuningParameter.Aggression), 0.001f);
            Assert.AreEqual(0f, tuning.Get(BossTuningParameter.ParryChance), 0.001f);
        }

        [Test]
        public void SettingATarget_DoesNotChangeTheValueImmediately()
        {
            var tuning = new BossTuning(3f, 0.5f);
            tuning.SetTarget(BossTuningParameter.ParryChance, 0.9f);

            // An instant change reads as the boss being handed information rather than working it out.
            Assert.AreEqual(0f, tuning.Get(BossTuningParameter.ParryChance), 0.001f);
        }

        [Test]
        public void AValueEasesTowardItsTargetOverTime()
        {
            var tuning = new BossTuning(3f, 0.5f);
            tuning.SetTarget(BossTuningParameter.ParryChance, 1f);

            for (int i = 0; i < 120; i++)
            {
                tuning.Tick(Frame, easeHalfLifeSeconds: 1f, forgetHalfLifeSeconds: 1000f);
            }

            Assert.Greater(tuning.Get(BossTuningParameter.ParryChance), 0.7f);
        }

        [Test]
        public void AnUnreinforcedTarget_DecaysBackToBaseline()
        {
            var tuning = new BossTuning(3f, 0.5f);
            tuning.SetTarget(BossTuningParameter.ParryChance, 1f);

            for (int i = 0; i < 60; i++)
            {
                tuning.Tick(Frame, easeHalfLifeSeconds: 0.5f, forgetHalfLifeSeconds: 1000f);
            }

            float whileReinforced = tuning.Get(BossTuningParameter.ParryChance);

            for (int i = 0; i < 600; i++)
            {
                tuning.Tick(Frame, easeHalfLifeSeconds: 0.5f, forgetHalfLifeSeconds: 2f);
            }

            // Without this, a player countered once would stay countered for the rest of the fight
            // no matter how they changed their play.
            Assert.Less(tuning.Get(BossTuningParameter.ParryChance), whileReinforced * 0.5f);
        }

        [Test]
        public void ResetToBaseline_UndoesEverythingLearned()
        {
            var tuning = new BossTuning(3f, 0.5f);
            tuning.SetTarget(BossTuningParameter.ParryChance, 1f);

            for (int i = 0; i < 120; i++)
            {
                tuning.Tick(Frame, 0.5f, 1000f);
            }

            tuning.ResetToBaseline();

            Assert.AreEqual(0f, tuning.Get(BossTuningParameter.ParryChance), 0.001f);
            Assert.AreEqual(3f, tuning.Get(BossTuningParameter.PreferredRange), 0.001f);
        }

        [Test]
        public void FreshTuning_HasNoCommitment()
        {
            var tuning = new BossTuning(3f, 0.5f);

            // The whole overbalance mechanic keys off this being zero when nothing has been learned,
            // which is what keeps the base fight untouched.
            Assert.AreEqual(0f, tuning.CommitmentIntensity, 0.0001f);
        }

        [Test]
        public void EasingTheCommittingParameters_RaisesCommitmentIntensity()
        {
            var tuning = new BossTuning(3f, baselineAggression: 0.5f);
            tuning.SetTarget(BossTuningParameter.Aggression, 0.85f);
            tuning.SetTarget(BossTuningParameter.GapCloserWeight, 0.7f);
            tuning.SetTarget(BossTuningParameter.ComboExtensionChance, 0.5f);

            for (int i = 0; i < 300; i++)
            {
                tuning.Tick(Frame, easeHalfLifeSeconds: 1f, forgetHalfLifeSeconds: 1000f);
            }

            // A boss that has fully leaned into pressing reads as strongly committed.
            Assert.Greater(tuning.CommitmentIntensity, 0.5f);
        }

        [Test]
        public void NonCommittingParameters_DoNotContributeToCommitment()
        {
            var tuning = new BossTuning(3f, 0.5f);

            // Reading a heavy swing raises ParryChance, but parrying is not committing to a lunge — it
            // must not, on its own, make the boss prone to overbalancing.
            tuning.SetTarget(BossTuningParameter.ParryChance, 1f);

            for (int i = 0; i < 300; i++)
            {
                tuning.Tick(Frame, 1f, 1000f);
            }

            Assert.AreEqual(0f, tuning.CommitmentIntensity, 0.0001f);
        }
    }
}
