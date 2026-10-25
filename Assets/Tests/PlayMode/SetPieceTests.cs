using System.Collections;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Game;
using AdaptiveBossArena.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Plays the fight's set pieces in the shipped scene and checks each one actually happens: the phase change's
    /// cut, title, slow motion and flare; the Last Stand's lightning; and the finisher that ends the fight.
    /// </summary>
    /// <remarks>
    /// The logic behind each was already tested in isolation; what was never seen was the wiring. A capture with
    /// an idle knight never took the boss out of his first phase, so these drive his health down directly.
    /// </remarks>
    [TestFixture]
    public sealed class SetPieceTests
    {
        private const int WarmUpFrames = 10;

        private BossController _boss;
        private ArenaCameraRig _camera;
        private RoundIntro _intro;
        private EncounterDirector _director;

        [UnitySetUp]
        public IEnumerator LoadTheArenaAndReleaseTheFight()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            _boss = Object.FindAnyObjectByType<BossController>();
            _camera = Object.FindAnyObjectByType<ArenaCameraRig>();
            _intro = Object.FindAnyObjectByType<RoundIntro>(FindObjectsInactive.Include);
            _director = Object.FindAnyObjectByType<EncounterDirector>();

            Assert.IsNotNull(_boss, "No boss in the arena scene.");
            Assert.IsNotNull(_camera, "No camera rig in the arena scene.");
            Assert.IsNotNull(_intro, "No round intro in the arena scene.");

            // The entrance holds the fight paused; skipping it releases the fight exactly as the director does for a
            // key press: the entrance shot is ended first, or it would go on holding the camera.
            _camera.EndEntrance();
            _intro.Skip();

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }
        }

        /// <summary>A blow from the knight that no parry turns aside, so the test does not depend on a guess.</summary>
        private static DamageInfo Blow(float amount)
        {
            DamageInfo basis = DamageInfo.Create(amount, DamageType.Heavy, CombatantTeam.Player, 1);

            return new DamageInfo
            {
                Amount = basis.Amount,
                Type = basis.Type,
                SourceTeam = basis.SourceTeam,
                SourceInstanceId = basis.SourceInstanceId,
                HitDirection = basis.HitDirection,
                Stagger = basis.Stagger,
                Unparryable = true
            };
        }

        /// <summary>Takes the boss down to a share of his health in one blow.</summary>
        private void DropTo(float share)
        {
            float target = _boss.Health.Maximum * share;
            float amount = _boss.Health.Current - target;
            Assert.Greater(amount, 0f, "The boss is already below that share.");

            DamageResult result = _boss.TakeDamage(Blow(amount));
            Assert.AreEqual(DamageOutcome.Applied, result.Outcome, "The blow did not land.");
        }

        /// <summary>Waits real time until a condition holds, or the time runs out.</summary>
        private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;

            while (!condition() && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ATurnOfTheFight_CutsToTheBoss_NamesThePhase_SlowsTheWorld_AndFlaresTheFires()
        {
            DropTo(0.64f);

            yield return WaitUntil(() => _camera.IsPlayingPhaseShot, 1f);

            Assert.IsTrue(_camera.IsPlayingPhaseShot, "The camera did not cut to the boss.");

            // The time service publishes its scale on its own tick, a frame after the request.
            yield return null;

            Assert.IsTrue(_intro.IsFlashing, "No phase title came up.");
            StringAssert.StartsWith("II", _intro.BannerText, "The title is not the second phase's.");
            Assert.Less(Time.timeScale, 1f, "The world did not slow.");

            FireFlicker[] fires = Object.FindObjectsByType<FireFlicker>(FindObjectsSortMode.None);

            if (fires.Length > 0)
            {
                Assert.IsTrue(System.Array.Exists(fires, fire => fire.IsFlaring), "No brazier flared.");
            }
        }

        [UnityTest]
        public IEnumerator TheCut_EndsWhileTheBossStillCannotStrike()
        {
            DropTo(0.64f);

            yield return WaitUntil(() => _camera.IsPlayingPhaseShot, 1f);
            Assert.IsTrue(_camera.IsPlayingPhaseShot, "The camera did not cut to the boss.");

            yield return WaitUntil(() => !_camera.IsPlayingPhaseShot, 3f);

            Assert.IsFalse(_camera.IsPlayingPhaseShot, "The cut never ended.");
            Assert.Greater(
                _boss.PhaseTransitionInvulnRemaining, 0f,
                "The camera was still on the boss after his transformation ended, when he can strike again.");
        }

        [UnityTest]
        public IEnumerator TheBars_StepAsideForTheCut_AndComeBackForTheFight()
        {
            CinematicHud hud = Object.FindAnyObjectByType<CinematicHud>();
            Assert.IsNotNull(hud, "Nothing fades the bars for the cut.");

            yield return WaitUntil(() => hud.Alpha >= 1f, 2f);
            Assert.AreEqual(1f, hud.Alpha, 0.01f, "The bars were not on screen during the fight.");

            DropTo(0.64f);
            yield return WaitUntil(() => hud.Alpha <= 0.05f, 1f);
            Assert.LessOrEqual(hud.Alpha, 0.05f, "The bars stayed over the phase's cut.");

            yield return WaitUntil(() => hud.Alpha >= 1f, 3f);
            Assert.AreEqual(1f, hud.Alpha, 0.01f, "The bars did not come back for the fight.");
        }

        [UnityTest]
        public IEnumerator TheLastStand_BringsLightning()
        {
            ArenaAtmosphere atmosphere = Object.FindAnyObjectByType<ArenaAtmosphere>();
            Assert.IsNotNull(atmosphere, "No atmosphere in the arena scene.");

            int before = atmosphere.StrikeCount;
            DropTo(0.2f);

            yield return WaitUntil(() => atmosphere.StrikeCount > before, 9f);

            Assert.Greater(atmosphere.StrikeCount, before, "No lightning struck in the Last Stand.");
        }

        [UnityTest]
        public IEnumerator TheLastBlow_EndsTheFightOnTheFinisher()
        {
            EndScreen end = Object.FindAnyObjectByType<EndScreen>(FindObjectsInactive.Include);
            Assert.IsNotNull(end, "No end screen in the arena scene.");

            _boss.TakeDamage(Blow(_boss.Health.Current + 100f));
            Assert.IsTrue(_boss.AwaitingFinalBlow, "The lethal blow did not bring him to his knee.");

            yield return null;

            DamageResult last = _boss.TakeDamage(Blow(10f));
            Assert.IsTrue(last.WasLethal, "The last blow did not end him.");

            yield return null;

            Assert.IsTrue(_camera.IsPlayingFinisher, "The finisher's two-shot did not play.");
            Assert.IsTrue(end.IsShown, "The fight did not end on the victory screen.");
        }

        [UnityTest]
        public IEnumerator AKneelingBoss_OffersTheKnightAnExecution_ByName()
        {
            var knight = Object.FindAnyObjectByType<Player.PlayerController>();
            Assert.IsNotNull(knight, "No knight in the arena scene.");

            _boss.TakeDamage(Blow(_boss.Health.Current + 100f));
            Assert.IsTrue(_boss.AwaitingFinalBlow, "The lethal blow did not bring him to his knee.");

            // Two metres in front of the knight's own facing, so he stands within reach and looking at the boss.
            Vector3 facing = knight.transform.forward;
            facing.y = 0f;
            facing.Normalize();
            knight.ResetForNewAttempt(_boss.transform.position - facing * 2f);

            yield return WaitUntil(() => knight.RiposteOffered, 2f);
            Assert.IsTrue(knight.RiposteOffered, "The kneeling boss offered no riposte to a knight standing over him.");

            yield return null;
            yield return null;

            Assert.IsTrue(_director.IsPromptShowing, "The prompt is not on screen.");
            Assert.AreEqual("EXECUTE", _director.PromptWords, "The prompt does not name the last blow.");
        }

        [UnityTest]
        public IEnumerator AfterARetry_TheNextTurnIsStagedAgain()
        {
            Assert.IsNotNull(_director, "No encounter director in the arena scene.");

            DropTo(0.64f);
            yield return WaitUntil(() => _camera.IsPlayingPhaseShot, 1f);
            yield return WaitUntil(() => !_camera.IsPlayingPhaseShot, 3f);

            // A retry drops the boss back to his opening phase, which is not an escalation and plays nothing.
            _director.RestartAttempt();
            yield return null;

            Assert.IsFalse(_camera.IsPlayingPhaseShot, "Resetting to the opening phase played a phase beat.");

            // The retry opens on its own READY, which holds the fight paused until it is skipped or runs out.
            _camera.EndEntrance();
            _intro.Skip();
            yield return null;

            yield return WaitUntil(() => _boss.PhaseTransitionInvulnRemaining <= 0f, 3f);

            DropTo(0.64f);
            yield return WaitUntil(() => _camera.IsPlayingPhaseShot, 1f);

            Assert.IsTrue(_camera.IsPlayingPhaseShot, "The second fight's turn into phase II was not staged.");
        }
    }
}
