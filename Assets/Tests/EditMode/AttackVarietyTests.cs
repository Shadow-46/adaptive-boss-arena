using System.Collections.Generic;
using System.Linq;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.AI.States;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Learning;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Pins that the boss mixes its blows rather than throwing the same one again and again.
    /// </summary>
    /// <remarks>
    /// Played against the real phase movesets, at the distances a fight actually happens at, because the
    /// repetition players noticed came from the authored weights, not from any synthetic catalogue.
    /// </remarks>
    public sealed class AttackVarietyTests
    {
        private const string ConfigPath = "Assets/_Project/ScriptableObjects/Config/DefaultBossConfig.asset";
        private const int Picks = 500;

        /// <summary>The most any one move may make up of a long run of picks.</summary>
        private const float MostAnyMoveMayTake = 0.45f;

        private static readonly float[] Distances = { 1.5f, 2.5f, 4f, 6f };

        private static BossConfig LoadConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<BossConfig>(ConfigPath);
            Assert.IsNotNull(config, "Run the full setup first: the boss config is generated.");
            return config;
        }

        private static BossTuning UnlearnedTuning() => new BossTuning(3f, 0.5f);

        /// <summary>Throws a long run of openers, recording each as the boss does.</summary>
        private static List<AttackDefinition> Run(
            BossPhaseDefinition phase, float distance, BossTuning tuning, uint seed)
        {
            var selector = new BossAttackSelector(new XorShiftRandomProvider(seed));
            var recent = new RecentMoves();
            var thrown = new List<AttackDefinition>(Picks);

            for (int i = 0; i < Picks; i++)
            {
                AttackDefinition pick = selector.Select(phase.Attacks, phase.SignatureAttack, distance, tuning, recent);
                recent.Record(pick);
                thrown.Add(pick);
            }

            return thrown;
        }

        private static string Histogram(IEnumerable<AttackDefinition> thrown) =>
            string.Join(", ", thrown.GroupBy(a => a.name).OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key} {g.Count()}"));

        [Test]
        public void NoMoveIsEverThrownThreeTimesRunning()
        {
            BossConfig config = LoadConfig();

            for (int p = 0; p < config.Phases.Length; p++)
            {
                foreach (float distance in Distances)
                {
                    List<AttackDefinition> thrown = Run(config.Phases[p], distance, UnlearnedTuning(), 11u);

                    for (int i = 2; i < thrown.Count; i++)
                    {
                        Assert.IsFalse(thrown[i] == thrown[i - 1] && thrown[i] == thrown[i - 2],
                            $"Phase {p} at {distance} m threw {thrown[i].name} three times running.");
                    }
                }
            }
        }

        [Test]
        public void NoMoveDominatesAnyDistance()
        {
            BossConfig config = LoadConfig();

            for (int p = 0; p < config.Phases.Length; p++)
            {
                foreach (float distance in Distances)
                {
                    List<AttackDefinition> thrown = Run(config.Phases[p], distance, UnlearnedTuning(), 23u);
                    TestContext.WriteLine($"Phase {p} at {distance} m: {Histogram(thrown)}");

                    int most = thrown.GroupBy(a => a).Max(g => g.Count());
                    Assert.LessOrEqual(most, Picks * MostAnyMoveMayTake,
                        $"Phase {p} at {distance} m leans on one move: {Histogram(thrown)}");
                    Assert.GreaterOrEqual(thrown.Distinct().Count(), 3,
                        $"Phase {p} at {distance} m throws fewer than three moves: {Histogram(thrown)}");
                }
            }
        }

        [Test]
        public void ALearnedAnswerLeadsTheMix_ButNeverBecomesTheOnlyMove()
        {
            BossConfig config = LoadConfig();
            BossPhaseDefinition phase = config.Phases[1];
            AttackDefinition answer = phase.Attacks.FirstOrDefault(a => a != null && (a.Answers & HabitAnswer.Guard) != 0);
            Assert.IsNotNull(answer, "The second phase should own a move that answers a raised guard.");

            BossTuning tuning = UnlearnedTuning();
            tuning.SetTarget(BossTuningParameter.AnswerGuardWeight, 2f);
            tuning.SnapToTargets();

            List<AttackDefinition> learned = Run(phase, 2.5f, tuning, 31u);
            List<AttackDefinition> unlearned = Run(phase, 2.5f, UnlearnedTuning(), 31u);
            TestContext.WriteLine($"Learned guard answer: {Histogram(learned)}");

            int learnedCount = learned.Count(a => (a.Answers & HabitAnswer.Guard) != 0);
            int unlearnedCount = unlearned.Count(a => (a.Answers & HabitAnswer.Guard) != 0);

            Assert.Greater(learnedCount, unlearnedCount * 1.5f, "Learning the guard did not change what he throws.");
            Assert.LessOrEqual(learned.Count(a => a == answer), Picks / 2, "The learned answer became a loop.");
        }

        [Test]
        public void AChainNeverRepeatsTheBlowItJustThrew_WhenItHasAChoice()
        {
            BossConfig config = LoadConfig();
            AttackDefinition[] moveset = config.Phases[config.Phases.Length - 1].Attacks;
            var random = new XorShiftRandomProvider(5u);

            for (int run = 0; run < 200; run++)
            {
                var recent = new RecentMoves();
                AttackDefinition swing = moveset[run % moveset.Length];
                recent.Record(swing);

                for (int hit = 0; hit < 3; hit++)
                {
                    AttackDefinition next = BossAttackState.PickFollowUp(swing, moveset, random, recent);
                    Assert.IsFalse(recent.IsThirdInARow(next), $"The chain threw {next.name} three times running.");
                    recent.Record(next);
                    swing = next;
                }
            }
        }

        [Test]
        public void RecentMoves_CutsTheLastBlowHardest_AndForgetsAfterFour()
        {
            AttackDefinition a = ScriptableObject.CreateInstance<AttackDefinition>();
            AttackDefinition b = ScriptableObject.CreateInstance<AttackDefinition>();
            AttackDefinition c = ScriptableObject.CreateInstance<AttackDefinition>();
            var recent = new RecentMoves();

            recent.Record(a);
            Assert.Less(recent.WeightFor(a), recent.WeightFor(b));

            recent.Record(a);
            Assert.AreEqual(0f, recent.WeightFor(a), "Two in a row must rule out a third.");

            for (int i = 0; i < RecentMoves.Depth; i++)
            {
                recent.Record(i % 2 == 0 ? b : c);
            }

            Assert.AreEqual(1f, recent.WeightFor(a), "A move four blows back is forgotten.");
        }

        [Test]
        public void AnUnlearnedBossFeints_Rarely()
        {
            BossTuning tuning = UnlearnedTuning();
            float feint = tuning.Get(BossTuningParameter.FeintChance);

            Assert.Greater(feint, 0f, "The feint is part of how he fights from the start.");
            Assert.Less(feint, 0.15f, "An unlearned boss feints rarely.");
        }
    }

    /// <summary>
    /// Pins the boss's footwork between blows: it answers where the knight is, and its rhythm keeps changing.
    /// </summary>
    public sealed class BossFootworkTests
    {
        [Test]
        public void AKnightBehindHim_IsAnsweredWithATurn()
        {
            Assert.AreEqual(PacingBeat.Turn, BossPacing.NextBeat(3f, 3f, 150f, PacingBeat.Circle, 0.5f));
        }

        [Test]
        public void HeNeverTurnsTwiceRunning()
        {
            Assert.AreNotEqual(PacingBeat.Turn, BossPacing.NextBeat(3f, 3f, 150f, PacingBeat.Turn, 0.5f));
        }

        [Test]
        public void ACrowdingKnight_IsAnsweredByGivingGround()
        {
            for (float roll = 0f; roll < 1f; roll += 0.1f)
            {
                PacingBeat beat = BossPacing.NextBeat(1.2f, 3f, 0f, PacingBeat.Circle, roll);
                Assert.That(beat == PacingBeat.HopBack || beat == PacingBeat.GiveGround,
                    $"Crowded at roll {roll}, he chose {beat}.");
            }
        }

        [Test]
        public void AtHisRange_HeCircles_AndSometimesStopsToMeasure()
        {
            var seen = new HashSet<PacingBeat>();
            var random = new XorShiftRandomProvider(9u);
            PacingBeat beat = PacingBeat.Circle;

            for (int i = 0; i < 200; i++)
            {
                beat = BossPacing.NextBeat(3.5f, 3f, 10f, beat, random.NextFloat(0f, 1f));
                seen.Add(beat);
            }

            Assert.That(seen, Has.Member(PacingBeat.Circle));
            Assert.That(seen, Has.Member(PacingBeat.Pause));
            Assert.That(seen, Has.No.Member(PacingBeat.HopBack), "He hopped away from a knight who was not near.");
        }

        [Test]
        public void EveryBeatEndsWithinThreeSeconds()
        {
            foreach (PacingBeat beat in System.Enum.GetValues(typeof(PacingBeat)))
            {
                Assert.That(BossPacing.BeatSeconds(beat, 0.99f), Is.InRange(0.3f, 3f), beat.ToString());
            }
        }
    }
}
