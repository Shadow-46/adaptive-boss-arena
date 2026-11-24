using System.Collections;
using System.IO;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Drives the generated title screen: the buttons the run modes added must be wired, and must not overlap.
    /// </summary>
    /// <remarks>
    /// Every piece of the title's new logic was tested as a pure function, and nothing had loaded the scene. A
    /// persistent listener that failed to serialise, or a column laid over the buttons, would pass all of them.
    /// Each test starts from a first launch, so none depends on which ran before it.
    /// </remarks>
    [TestFixture]
    public sealed class TitleScreenTests
    {
        private const int WarmUpFrames = 5;

        private RunModifiers _saved;

        [UnitySetUp]
        public IEnumerator LoadTheTitle()
        {
            _saved = RunSettings.Current;

            foreach (string key in new[] { HowToFightPanel.SaveKey, "records", "run-modifiers" })
            {
                // The backup too: the save service falls back to it when the main file is missing.
                foreach (string suffix in new[] { ".json", ".json.bak", ".json.tmp" })
                {
                    string path = Path.Combine(TestSaveIsolation.Folder, key + suffix);

                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }

            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }
        }

        [TearDown]
        public void RestoreSettings() => RunSettings.Current = _saved ?? new RunModifiers();

        private static Button FindButton(string name)
        {
            foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button.name == name)
                {
                    return button;
                }
            }

            return null;
        }

        [UnityTest]
        public IEnumerator TheDifficultyButtonCyclesTheRunAndSaysSo()
        {
            Button difficulty = FindButton("Difficulty");
            Assert.IsNotNull(difficulty, "No difficulty button on the title.");

            Difficulty before = RunSettings.Current.Difficulty;
            difficulty.onClick.Invoke();
            yield return null;

            Assert.AreEqual(DifficultyProfile.Next(before), RunSettings.Current.Difficulty, "The click did not change the run.");
            StringAssert.Contains(
                DifficultyProfile.Label(RunSettings.Current.Difficulty), difficulty.GetComponentInChildren<Text>().text);
        }

        [UnityTest]
        public IEnumerator AFirstLaunchOpensTheGuideAndItCloses()
        {
            GameObject guide = GameObject.Find("HowToFightPanel");
            Assert.IsNotNull(guide, "The guide did not open by itself on a first launch.");

            Button ready = FindButton("Close");
            Assert.IsNotNull(ready);
            ready.onClick.Invoke();
            yield return null;

            Assert.IsFalse(guide.activeSelf, "Ready did not close the guide.");

            FindButton("HowToFight").onClick.Invoke();
            yield return null;
            Assert.IsTrue(guide.activeSelf, "How to Fight did not reopen the guide.");
        }

        [UnityTest]
        public IEnumerator AscendedIsLockedAndTheArmourIsPlainOnAFreshSave()
        {
            yield return null;

            Toggle ascended = null;

            foreach (Toggle toggle in Object.FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (toggle.name == "Ascended Toggle")
                {
                    ascended = toggle;
                }
            }

            Assert.IsNotNull(ascended, "No Ascended toggle on the title.");
            Assert.IsFalse(ascended.interactable, "Ascended is open without a win.");

            FindButton("Armour").onClick.Invoke();
            yield return null;
            Assert.AreEqual(TrialBook.DefaultTintId, RunSettings.Current.KnightTintId, "An unearned tint was handed out.");
        }

        [UnityTest]
        public IEnumerator ClearRecordsAsksTwiceThenErases()
        {
            // A win written straight into the save, then the title reloaded to read it.
            var records = new RecordsData();
            records.RecordAttempt(new AttemptSummary(true, Difficulty.Knight, false, 200f, 0.5f, 1, 2, 1, 3, 1, "Blade"));
            ServiceRegistry.Current.Get<ISaveService>().Save("records", records);
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            Text panel = GameObject.Find("Records").GetComponent<Text>();
            StringAssert.Contains("wins 1", panel.text, "The written win did not show.");

            Button clear = FindButton("ClearRecords");
            Assert.IsNotNull(clear, "No clear button on the title.");

            clear.onClick.Invoke();
            yield return null;
            StringAssert.Contains("wins 1", panel.text, "One click erased the records.");
            Assert.AreEqual(RunModifierMenu.ConfirmClearCaption, clear.GetComponentInChildren<Text>().text);

            clear.onClick.Invoke();
            yield return null;
            StringAssert.DoesNotContain("wins 1", panel.text, "The second click did not erase the records.");
            Assert.AreEqual(RunModifierMenu.ClearCaption, clear.GetComponentInChildren<Text>().text);
        }

        [UnityTest]
        public IEnumerator FightIsSelectedForAGamepad()
        {
            yield return null;

            Assert.IsNotNull(EventSystem.current);
            Assert.AreEqual(FindButton("Start").gameObject, EventSystem.current.currentSelectedGameObject);
        }

        [UnityTest]
        public IEnumerator TheRecordsColumnDoesNotCoverTheButtons()
        {
            yield return null;

            Rect records = ScreenRect(GameObject.Find("Records").GetComponent<RectTransform>());

            foreach (string name in new[] { "Difficulty", "Start", "HowToFight", "Settings", "Quit", "Armour" })
            {
                Button button = FindButton(name);

                if (button == null || !button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Assert.IsFalse(records.Overlaps(ScreenRect(button.GetComponent<RectTransform>())),
                    $"The records column covers {name}.");
            }
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
