using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.UI;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>Tests for the difficulty modes.</summary>
    /// <remarks>
    /// The point of difficulty is that Knight stays the fight that was balanced, and that every step away
    /// from it moves every number the same way. A Story mode that was easier on one axis and harder on
    /// another would be a different fight, not an easier one.
    /// </remarks>
    [TestFixture]
    public sealed class DifficultyProfileTests
    {
        [Test]
        public void KnightIsTheFightAsBalanced()
        {
            DifficultyProfile knight = DifficultyProfile.Knight;

            Assert.AreEqual(1f, knight.IncomingDamageScale);
            Assert.AreEqual(1f, knight.BossDamageTakenScale);
            Assert.AreEqual(1f, knight.BossCooldownScale);
            Assert.AreEqual(1f, knight.AdaptationRateScale);
            Assert.AreEqual(0, knight.HealChargeBonus);
        }

        [Test]
        public void EveryAxisGetsHarderFromStoryToNightmare()
        {
            DifficultyProfile story = DifficultyProfile.Story;
            DifficultyProfile knight = DifficultyProfile.Knight;
            DifficultyProfile nightmare = DifficultyProfile.Nightmare;

            Assert.Less(story.IncomingDamageScale, knight.IncomingDamageScale);
            Assert.Less(knight.IncomingDamageScale, nightmare.IncomingDamageScale);

            Assert.Greater(story.BossDamageTakenScale, knight.BossDamageTakenScale);
            Assert.Greater(knight.BossDamageTakenScale, nightmare.BossDamageTakenScale);

            Assert.Greater(story.BossCooldownScale, knight.BossCooldownScale);
            Assert.Greater(knight.BossCooldownScale, nightmare.BossCooldownScale);

            Assert.Less(story.AdaptationRateScale, knight.AdaptationRateScale);
            Assert.Less(knight.AdaptationRateScale, nightmare.AdaptationRateScale);

            Assert.Greater(story.HealChargeBonus, knight.HealChargeBonus);
            Assert.Greater(knight.HealChargeBonus, nightmare.HealChargeBonus);
        }

        [Test]
        public void TheCycleVisitsEveryDifficultyAndReturns()
        {
            Difficulty d = Difficulty.Story;
            d = DifficultyProfile.Next(d);
            Assert.AreEqual(Difficulty.Knight, d);
            d = DifficultyProfile.Next(d);
            Assert.AreEqual(Difficulty.Nightmare, d);
            d = DifficultyProfile.Next(d);
            Assert.AreEqual(Difficulty.Story, d);
        }

        [Test]
        public void AnOldRunModifierSaveLoadsAsKnight()
        {
            // Written before difficulty existed: no Difficulty, Ascended or tint fields at all.
            const string legacy = "{\"SchemaVersion\":1,\"FastLearner\":true,\"NoHealing\":false}";
            var loaded = JsonUtility.FromJson<RunModifiers>(legacy);

            Assert.AreEqual(Difficulty.Knight, loaded.Difficulty);
            Assert.IsFalse(loaded.Ascended);
            Assert.AreEqual(TrialBook.DefaultTintId, loaded.KnightTintId);
            Assert.IsTrue(loaded.FastLearner);
        }

        [Test]
        public void ModifiersComposeWithDifficulty()
        {
            var modifiers = new RunModifiers { Difficulty = Difficulty.Nightmare, FragilePlayer = true, FastLearner = true };

            Assert.AreEqual(1.3f * 1.5f, modifiers.IncomingDamageMultiplier, 1e-4f);
            Assert.AreEqual(1.5f * 2f, modifiers.AdaptationRateScale, 1e-4f);
            Assert.AreEqual(-1, modifiers.HealChargeBonus);
        }

        [Test]
        public void AnOrdinaryRunIsUnmodified()
        {
            var modifiers = new RunModifiers();

            Assert.IsFalse(modifiers.AnyActive);
            Assert.AreEqual(0, modifiers.OpeningPhase);
            Assert.AreEqual(1f, modifiers.BossDamageTakenScale);
            Assert.AreEqual(1f, modifiers.BossCooldownScale);
        }

        [Test]
        public void AscendedOpensInTheSecondPhaseAndIsTougher()
        {
            var modifiers = new RunModifiers { Ascended = true };

            Assert.AreEqual(RunModifiers.AscendedOpeningPhase, modifiers.OpeningPhase);
            Assert.Less(modifiers.BossDamageTakenScale, 1f);
            Assert.Less(modifiers.BossCooldownScale, 1f);
            Assert.IsTrue(modifiers.AnyActive);
            CollectionAssert.Contains(modifiers.ActiveLabels(), "Ascended");
        }
    }

    /// <summary>Tests for the per-mode records.</summary>
    [TestFixture]
    public sealed class ModeRecordTests
    {
        private static AttemptSummary Attempt(
            bool won, Difficulty difficulty = Difficulty.Knight, float seconds = 200f, int hits = 3,
            bool ascended = false, int heals = 1, int adaptations = 2, int deflects = 4) =>
            new AttemptSummary(won, difficulty, ascended, seconds, 0.5f, adaptations, hits, heals, deflects, 2, "Blade");

        [Test]
        public void EachDifficultyKeepsItsOwnBests()
        {
            var records = new RecordsData();
            records.RecordAttempt(Attempt(true, Difficulty.Story, seconds: 90f));
            records.RecordAttempt(Attempt(true, Difficulty.Nightmare, seconds: 400f));

            Assert.AreEqual(90f, records.Find(Difficulty.Story, false).FastestVictorySeconds, 1e-4f);
            Assert.AreEqual(400f, records.Find(Difficulty.Nightmare, false).FastestVictorySeconds, 1e-4f);
            Assert.IsNull(records.Find(Difficulty.Knight, false));
        }

        [Test]
        public void LossesCountAsDeathsAndTouchNoBest()
        {
            var records = new RecordsData();
            records.RecordAttempt(Attempt(false));
            records.RecordAttempt(Attempt(false));

            ModeRecord knight = records.Find(Difficulty.Knight, false);
            Assert.AreEqual(2, knight.Attempts);
            Assert.AreEqual(2, knight.Deaths);
            Assert.AreEqual(0, knight.Wins);
            Assert.AreEqual(0f, knight.FastestVictorySeconds);
        }

        [Test]
        public void AWinWithoutBeingHitIsFlawless()
        {
            var records = new RecordsData();
            records.RecordAttempt(Attempt(true, hits: 2));
            Assert.IsFalse(records.Find(Difficulty.Knight, false).FlawlessWin);

            bool improved = records.RecordAttempt(Attempt(true, seconds: 500f, hits: 0));
            Assert.IsTrue(records.Find(Difficulty.Knight, false).FlawlessWin);
            Assert.IsTrue(improved, "A first flawless win is a new best even when slower.");
        }

        [Test]
        public void AscendedIsKeptApartFromTheOrdinaryFight()
        {
            var records = new RecordsData();
            records.RecordAttempt(Attempt(true, ascended: true, seconds: 300f));

            Assert.IsNull(records.Find(Difficulty.Knight, false));
            Assert.AreEqual(1, records.Find(Difficulty.Knight, true).Wins);
        }

        [Test]
        public void AscendedUnlocksOnAKnightOrNightmareWinButNotStory()
        {
            var records = new RecordsData();
            records.RecordAttempt(Attempt(true, Difficulty.Story));
            Assert.IsFalse(records.AscendedUnlocked);

            records.RecordAttempt(Attempt(true, Difficulty.Nightmare));
            Assert.IsTrue(records.AscendedUnlocked);
        }

        [Test]
        public void VersionOneBestsMigrateIntoKnight()
        {
            const string legacy =
                "{\"SchemaVersion\":1,\"HasWon\":true,\"FastestVictorySeconds\":187.5,\"BestRemainingHealth\":0.3," +
                "\"TotalAttempts\":14,\"FewestAdaptationsAllowed\":2}";
            var records = JsonUtility.FromJson<RecordsData>(legacy);

            records.Migrate();

            ModeRecord knight = records.Find(Difficulty.Knight, false);
            Assert.IsNotNull(knight);
            Assert.AreEqual(14, knight.Attempts);
            Assert.AreEqual(1, knight.Wins);
            Assert.AreEqual(187.5f, knight.FastestVictorySeconds, 1e-4f);
            Assert.AreEqual(2, knight.FewestAdaptationsAllowed);
            Assert.AreEqual(RecordsData.CurrentSchemaVersion, records.SchemaVersion);
            Assert.IsTrue(records.AscendedUnlocked, "A veteran who already beat the Knight fight has earned the rematch.");
        }

        [Test]
        public void MigratingTwiceChangesNothing()
        {
            var records = new RecordsData { SchemaVersion = 1, TotalAttempts = 3 };
            records.Migrate();
            records.RecordAttempt(Attempt(false));
            records.Migrate();

            Assert.AreEqual(4, records.Find(Difficulty.Knight, false).Attempts);
        }

        [Test]
        public void PerModeRecordsSurviveASaveRoundTrip()
        {
            var records = new RecordsData();
            records.RecordAttempt(Attempt(true, Difficulty.Nightmare, seconds: 321f, hits: 0));
            records.CompleteTrial("nightmare");

            var loaded = JsonUtility.FromJson<RecordsData>(JsonUtility.ToJson(records));

            Assert.AreEqual(321f, loaded.Find(Difficulty.Nightmare, false).FastestVictorySeconds, 1e-4f);
            Assert.IsTrue(loaded.Find(Difficulty.Nightmare, false).FlawlessWin);
            Assert.IsTrue(loaded.HasCompleted("nightmare"));
        }
    }

    /// <summary>Tests for the trials and the tints they unlock.</summary>
    [TestFixture]
    public sealed class TrialBookTests
    {
        private static AttemptSummary Win(Difficulty difficulty, int hits = 3, int heals = 1, int adaptations = 3,
            int deflects = 2, float seconds = 240f, bool ascended = false) =>
            new AttemptSummary(true, difficulty, ascended, seconds, 0.5f, adaptations, hits, heals, deflects, 1, "Blade");

        [Test]
        public void ALossMeetsNoTrial()
        {
            var loss = new AttemptSummary(false, Difficulty.Nightmare, true, 30f, 0f, 0, 0, 0, 20, 5, "Blade");
            Assert.IsEmpty(TrialBook.MetBy(loss));
        }

        [Test]
        public void AStoryWinMeetsOnlyFirstBloodEvenWhenFlawless()
        {
            var met = TrialBook.MetBy(Win(Difficulty.Story, hits: 0, heals: 0, adaptations: 0, deflects: 20, seconds: 60f));

            Assert.AreEqual(1, met.Count);
            Assert.AreEqual("first_blood", met[0].Id);
        }

        [Test]
        public void AFlawlessQuickNightmareWinMeetsItsTrials()
        {
            var met = TrialBook.MetBy(Win(Difficulty.Nightmare, hits: 0, heals: 0, adaptations: 1, deflects: 12, seconds: 150f));
            var ids = met.ConvertAll(t => t.Id);

            CollectionAssert.IsSubsetOf(
                new[] { "first_blood", "knight", "nightmare", "untouched", "abstinent", "unread", "duellist", "swift" }, ids);
            CollectionAssert.DoesNotContain(ids, "ascended");
        }

        [Test]
        public void EveryTrialHasADistinctIdAndEveryRewardIsARealTint()
        {
            var seen = new System.Collections.Generic.HashSet<string>();

            foreach (Trial trial in TrialBook.Trials)
            {
                Assert.IsTrue(seen.Add(trial.Id), trial.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(trial.Description), trial.Id);

                if (trial.RewardTintId != null)
                {
                    Assert.AreEqual(trial.RewardTintId, TrialBook.TintById(trial.RewardTintId).Id, trial.Id);
                }
            }
        }

        [Test]
        public void OnlyEarnedTintsCanBeWorn()
        {
            var records = new RecordsData();

            Assert.IsTrue(TrialBook.IsTintUnlocked(TrialBook.DefaultTintId, records));
            Assert.IsFalse(TrialBook.IsTintUnlocked("gilded", records));
            Assert.AreEqual(TrialBook.DefaultTintId, TrialBook.NextUnlockedTint(TrialBook.DefaultTintId, records));

            records.CompleteTrial("untouched");

            Assert.IsTrue(TrialBook.IsTintUnlocked("gilded", records));
            Assert.AreEqual("gilded", TrialBook.NextUnlockedTint(TrialBook.DefaultTintId, records));
            Assert.AreEqual(TrialBook.DefaultTintId, TrialBook.NextUnlockedTint("gilded", records));
        }

        [Test]
        public void ATrialCompletesOnlyOnce()
        {
            var records = new RecordsData();

            Assert.IsTrue(records.CompleteTrial("first_blood"));
            Assert.IsFalse(records.CompleteTrial("first_blood"));
        }

        [Test]
        public void TheRecordsPanelNamesEveryModeAndCountsTrials()
        {
            var records = new RecordsData();
            records.RecordAttempt(new AttemptSummary(true, Difficulty.Story, false, 125f, 0.5f, 1, 0, 0, 0, 0, "Blade"));
            records.CompleteTrial("first_blood");

            string panel = RunModifierMenu.DescribeRecords(records);

            StringAssert.Contains("Story", panel);
            StringAssert.Contains("2:05", panel);
            StringAssert.Contains("FLAWLESS", panel);
            StringAssert.Contains("Nightmare", panel);
            StringAssert.Contains("not yet fought", panel);
            StringAssert.Contains($"Trials 1 / {TrialBook.Trials.Count}", panel);
        }
    }
}
