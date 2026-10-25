using AdaptiveBossArena.AI;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.UI;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Plays the fight's set pieces on a timetable for an unattended capture: every phase change, the Last Stand's
    /// lightning, the kneel and the finisher.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A capture with an idle knight never takes the boss out of his first phase, so the moments that matter most
    /// were never photographed. This walks his health down in the knight's name, as a fight would, and leaves
    /// everything else to the game: the boss still fights, the camera, the titles, the fires and the storm all run
    /// on their own wiring, which is the thing under test.
    /// </para>
    /// <para>
    /// Only ever present when a capture asks for it with <c>-perfShowcase</c>: the probe adds it, the scene never
    /// does, so no ordinary fight can contain it. The run is a Training run, so the knight cannot die and nothing is
    /// recorded. It sits in Game, which may see both fighters, and touches neither's decisions.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ShowcaseDirector : MonoBehaviour
    {
        /// <summary>A step that brings the boss down from standing to his knee.</summary>
        private const float Lethal = -1f;

        /// <summary>The last blow on the kneeling boss.</summary>
        private const float Final = -2f;

        /// <summary>How long a step may wait for the boss to be hittable before it is skipped.</summary>
        private const float LongestWaitSeconds = 3f;

        /// <summary>
        /// When each blow falls, in seconds of the fight, and what share of his health it leaves him: each phase in
        /// turn, time in the Last Stand for the lightning, then the kneel and the last blow.
        /// </summary>
        private static readonly (float At, float Leaves)[] Timetable =
        {
            (4f, 0.64f), (12f, 0.43f), (20f, 0.23f), (34f, Lethal), (35.5f, Final)
        };

        private BossController _boss;
        private RoundIntro _intro;
        private float _elapsed;
        private float _waiting;
        private int _next;

        private void Awake() => RunSettings.Current = new RunModifiers { TrainingMode = true };

        private void Update()
        {
            if (_next >= Timetable.Length || !Resolve())
            {
                return;
            }

            // The fight's own clock, from the moment the intro releases it.
            if (_intro != null && _intro.IsRunning)
            {
                return;
            }

            // Nobody is at the mouse: lock on, as a player keeping the boss in view would.
            if (_camera == null)
            {
                _camera = FindAnyObjectByType<ArenaCameraRig>();
                _camera?.SetLockedOn(true);
            }

            // Training is only borrowed for the knight's immortality: its learning panel would cover the frames and
            // cost frame time no ordinary fight pays, so a measurement of the fight would measure the panel.
            if (_overlay == null)
            {
                _overlay = FindAnyObjectByType<AdaptationDebugOverlay>();
            }

            _overlay?.SetForcedVisible(false);

            _elapsed += Time.deltaTime;
            Report();

            if (_elapsed < Timetable[_next].At)
            {
                return;
            }

            if (TryStrike(Timetable[_next].Leaves) || (_waiting += Time.unscaledDeltaTime) > LongestWaitSeconds)
            {
                Debug.Log($"[Showcase] step {_next} at {_elapsed:0.0}s, boss health {_boss.Health.Current:0}.");
                _next++;
                _waiting = 0f;
            }
        }

        /// <summary>Seconds of real time between lines of the showcase's log.</summary>
        private const float ReportEverySeconds = 1f;

        private float _sinceReport;
        private Player.PlayerController _knight;
        private AdaptationDebugOverlay _overlay;
        private ArenaCameraRig _camera;

        /// <summary>
        /// Logs where the two fighters and the camera are, once a second, so a frame that shows the wrong thing can
        /// be read against what the game believed at the time.
        /// </summary>
        private void Report()
        {
            _sinceReport += Time.unscaledDeltaTime;

            if (_sinceReport < ReportEverySeconds)
            {
                return;
            }

            _sinceReport = 0f;

            if (_knight == null)
            {
                _knight = FindAnyObjectByType<Player.PlayerController>();
            }

            if (_camera == null)
            {
                _camera = FindAnyObjectByType<ArenaCameraRig>();
            }

            Camera lens = Camera.main;
            Debug.Log(
                $"[Showcase] t={_elapsed:0.0} phase={_boss.PhaseIndex} boss={_boss.transform.position} " +
                $"knight={(_knight != null ? _knight.transform.position.ToString() : "none")} " +
                $"cam={(lens != null ? lens.transform.position.ToString() : "none")} " +
                $"look={(lens != null ? lens.transform.forward.ToString() : "none")} " +
                $"shot={(_camera != null && _camera.IsPlayingPhaseShot)} timescale={Time.timeScale:0.00}");
        }

        private bool Resolve()
        {
            if (_boss == null)
            {
                _boss = FindAnyObjectByType<BossController>();
            }

            if (_intro == null)
            {
                _intro = FindAnyObjectByType<RoundIntro>(FindObjectsInactive.Include);
            }

            return _boss != null && _boss.IsInitialised;
        }

        /// <summary>Lands one step's blow, once the boss can take it.</summary>
        /// <returns>True when the step is done.</returns>
        private bool TryStrike(float leaves)
        {
            if (!_boss.IsAlive)
            {
                return true;
            }

            // Not through his transformation: the blow would be swallowed, and the next phase's beat would not play.
            if (_boss.PhaseTransitionInvulnRemaining > 0f)
            {
                return false;
            }

            float amount;

            if (leaves == Final)
            {
                if (!_boss.AwaitingFinalBlow)
                {
                    return false;
                }

                amount = _boss.Health.Current + 1f;
            }
            else if (leaves == Lethal)
            {
                amount = _boss.Health.Current + 100f;
            }
            else
            {
                amount = _boss.Health.Current - _boss.Health.Maximum * leaves;

                if (amount <= 0f)
                {
                    return true;
                }
            }

            return _boss.TakeDamage(Blow(amount)).Outcome == DamageOutcome.Applied;
        }

        /// <summary>A blow from the knight that no parry turns aside, so the timetable does not depend on a guess.</summary>
        private static DamageInfo Blow(float amount)
        {
            DamageInfo basis = DamageInfo.Create(amount, DamageType.Heavy, CombatantTeam.Player, 0);

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
    }
}
