using System.IO;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Game;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests for persistence.
    /// </summary>
    /// <remarks>
    /// The interesting cases are the failures rather than the round trip. A save system that works
    /// when everything is fine but loses a player's settings the one time a file is truncated is
    /// worse than no save system, because the player has come to rely on it.
    /// </remarks>
    [TestFixture]
    public sealed class JsonSaveServiceTests
    {
        private string _folder;
        private JsonSaveService _service;

        [SetUp]
        public void CreateIsolatedFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "aba-save-tests", Path.GetRandomFileName());
            _service = new JsonSaveService(_folder);
        }

        [TearDown]
        public void RemoveIsolatedFolder()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }

        [Test]
        public void SaveThenLoad_RoundTripsTheRecord()
        {
            var settings = new SettingsData
            {
                MasterVolume = 0.42f,
                ScreenShakeIntensity = 0.25f,
                ReducedFlashing = true
            };

            _service.Save(SaveKeys.Settings, settings);

            Assert.IsTrue(_service.TryLoad(SaveKeys.Settings, out SettingsData loaded));
            Assert.AreEqual(0.42f, loaded.MasterVolume, 0.0001f);
            Assert.AreEqual(0.25f, loaded.ScreenShakeIntensity, 0.0001f);
            Assert.IsTrue(loaded.ReducedFlashing);
        }

        [Test]
        public void LoadingAnAbsentRecord_ReportsFailureWithoutThrowing()
        {
            Assert.IsFalse(_service.TryLoad("nothing-here", out SettingsData loaded));
            Assert.IsNull(loaded);
        }

        [Test]
        public void LoadingACorruptRecord_FallsBackRatherThanThrowing()
        {
            File.WriteAllText(Path.Combine(_folder, "broken.json"), "{ this is not valid json ][");

            // The player with a damaged file must still reach the game, with defaults.
            Assert.DoesNotThrow(() => _service.TryLoad("broken", out SettingsData _));
        }

        [Test]
        public void LoadingAnEmptyFile_ReportsFailure()
        {
            File.WriteAllText(Path.Combine(_folder, "empty.json"), string.Empty);

            Assert.IsFalse(_service.TryLoad("empty", out SettingsData _));
        }

        [Test]
        public void OverwritingARecord_KeepsTheNewValues()
        {
            _service.Save(SaveKeys.Settings, new SettingsData { MasterVolume = 0.1f });
            _service.Save(SaveKeys.Settings, new SettingsData { MasterVolume = 0.9f });

            Assert.IsTrue(_service.TryLoad(SaveKeys.Settings, out SettingsData loaded));
            Assert.AreEqual(0.9f, loaded.MasterVolume, 0.0001f);
        }

        [Test]
        public void ACorruptPrimaryFile_IsRecoveredFromItsBackup()
        {
            _service.Save(SaveKeys.Settings, new SettingsData { MasterVolume = 0.33f });

            // The second write leaves the first behind as a backup.
            _service.Save(SaveKeys.Settings, new SettingsData { MasterVolume = 0.77f });

            string primary = Path.Combine(_folder, SaveKeys.Settings + ".json");
            File.WriteAllText(primary, "corrupted beyond parsing");

            Assert.IsTrue(_service.TryLoad(SaveKeys.Settings, out SettingsData loaded));
            Assert.AreEqual(0.33f, loaded.MasterVolume, 0.0001f,
                "The backup written by the previous save should have been used.");
        }

        [Test]
        public void Exists_TracksTheRecord()
        {
            Assert.IsFalse(_service.Exists(SaveKeys.Settings));

            _service.Save(SaveKeys.Settings, new SettingsData());
            Assert.IsTrue(_service.Exists(SaveKeys.Settings));

            _service.Delete(SaveKeys.Settings);
            Assert.IsFalse(_service.Exists(SaveKeys.Settings));
        }
    }

    /// <summary>Tests for the personal-best bookkeeping.</summary>
    [TestFixture]
    public sealed class RecordsDataTests
    {
        [Test]
        public void ALostAttempt_CountsButSetsNoRecord()
        {
            var records = new RecordsData();

            records.RecordAttempt(won: false, durationSeconds: 30f, remainingHealth: 0f, adaptationsAllowed: 4);

            Assert.AreEqual(1, records.TotalAttempts);
            Assert.IsFalse(records.HasWon);
            Assert.AreEqual(0f, records.FastestVictorySeconds, 0.0001f);
        }

        [Test]
        public void TheFirstVictory_SetsEveryRecord()
        {
            var records = new RecordsData();

            records.RecordAttempt(won: true, durationSeconds: 95f, remainingHealth: 0.4f, adaptationsAllowed: 3);

            Assert.IsTrue(records.HasWon);
            Assert.AreEqual(95f, records.FastestVictorySeconds, 0.0001f);
            Assert.AreEqual(3, records.FewestAdaptationsAllowed);
        }

        [Test]
        public void ASlowerVictory_DoesNotOverwriteTheBest()
        {
            var records = new RecordsData();
            records.RecordAttempt(true, 60f, 0.5f, 2);

            records.RecordAttempt(true, 120f, 0.1f, 5);

            Assert.AreEqual(60f, records.FastestVictorySeconds, 0.0001f);
            Assert.AreEqual(2, records.FewestAdaptationsAllowed);
            Assert.AreEqual(0.5f, records.BestRemainingHealth, 0.0001f);
        }

        [Test]
        public void WinningWithFewerAdaptations_SetsARecordEvenIfSlower()
        {
            var records = new RecordsData();
            records.RecordAttempt(true, 60f, 0.5f, 4);

            // Slower, but the boss learned less. That is the harder achievement and must register.
            records.RecordAttempt(true, 90f, 0.2f, 1);

            Assert.AreEqual(1, records.FewestAdaptationsAllowed);
            Assert.AreEqual(60f, records.FastestVictorySeconds, 0.0001f);
        }
    }
}
