using System;
using System.Text;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Learning;
using AdaptiveBossArena.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Shows what the boss currently believes about the player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Effectively mandatory for tuning this encounter. Adaptation is invisible by construction —
    /// the boss changes gradually and never states why — which makes it impossible to tell a
    /// threshold that is set too high from one that is set too low by playing alone. This panel is
    /// the only way to see whether the boss has drawn the conclusion you were trying to provoke.
    /// </para>
    /// <para>
    /// Drawn with immediate-mode GUI on purpose. It needs no prefabs, no canvas and no layout work,
    /// and it is compiled out of release builds entirely, so it can afford to be ugly.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class AdaptationDebugOverlay : MonoBehaviour
    {
        private const float PanelWidth = 420f;
        private const float PanelMargin = 12f;
        private const int BarSegments = 20;

        /// <summary>How many recent combat occurrences the tail shows.</summary>
        /// <remarks>
        /// Enough to cover a full combo and the answer to it, short enough to read at a glance while
        /// the fight is running.
        /// </remarks>
        private const int EventTailLength = 10;

        [SerializeField]
        [Tooltip("Key that shows and hides the panel.")]
        private Key _toggleKey = Key.F1;

        [SerializeField]
        [Tooltip("Whether the panel starts visible.")]
        private bool _visibleOnStart;

        [SerializeField]
        [Tooltip("Boss to inspect. Found automatically when left empty.")]
        private BossController _boss;

        private readonly StringBuilder _builder = new StringBuilder(1024);
        private bool _isVisible;
        private bool _forced;
        private GUIStyle _style;

        private PlayerController _player;
        private ITimeService _time;
        private ICombatEventBus _events;

        /// <summary>The most recent occurrences, oldest first, for the tail.</summary>
        private readonly CombatEvent[] _recentEvents = new CombatEvent[EventTailLength];
        private int _recentCount;

        /// <summary>
        /// Forces the panel on regardless of the toggle key, for Training mode.
        /// </summary>
        /// <remarks>
        /// Training is a practice mode the player opts into, and seeing what the boss is working out is
        /// the whole point of practising against it — so a forced panel renders even in a player build,
        /// unlike the developer toggle, which stays stripped from release.
        /// </remarks>
        /// <param name="on">Whether to force the panel visible.</param>
        public void SetForcedVisible(bool on) => _forced = on;

        private void Start()
        {
            _isVisible = _visibleOnStart;

            if (_boss == null)
            {
                _boss = FindAnyObjectByType<BossController>();
            }

            _player = FindAnyObjectByType<PlayerController>();

            ServiceRegistry.Current.TryGet(out _time);

            // The event tail is the decisive instrument: it distinguishes "no attack was ever thrown"
            // from "attacks were thrown and all missed" from "attacks landed but the interface did not
            // move", which look identical from the outside and cannot be told apart by reading code.
            if (ServiceRegistry.Current.TryGet(out _events))
            {
                _events.EventRecorded += OnCombatEvent;
            }
        }

        private void OnDestroy()
        {
            if (_events != null)
            {
                _events.EventRecorded -= OnCombatEvent;
            }
        }

        /// <summary>Keeps the most recent occurrences, shuffling the oldest out.</summary>
        private void OnCombatEvent(CombatEvent combatEvent)
        {
            if (_recentCount < _recentEvents.Length)
            {
                _recentEvents[_recentCount++] = combatEvent;
                return;
            }

            Array.Copy(_recentEvents, 1, _recentEvents, 0, _recentEvents.Length - 1);
            _recentEvents[_recentEvents.Length - 1] = combatEvent;
        }

        /// <summary>
        /// Polls the toggle key through the Input System.
        /// </summary>
        /// <remarks>
        /// Deliberately not <c>UnityEngine.Input</c>. With the project set to use the Input System
        /// package exclusively, the legacy class throws on first access, and a debug tool that
        /// crashes the game it is meant to help diagnose is worse than no debug tool.
        /// </remarks>
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard[_toggleKey].wasPressedThisFrame)
            {
                _isVisible = !_isVisible;
            }
        }

        private void OnGUI()
        {
            // Available in player builds too, behind the toggle key. The panel began as a tuning aid,
            // but it is also the only way to diagnose a fight that has gone quiet — and the fights
            // worth diagnosing are the ones a player reports from a shipped build, not the ones
            // reproduced in the editor.
            if (!_isVisible && !_forced)
            {
                return;
            }

            EnsureStyle();

            GUILayout.BeginArea(
                new Rect(PanelMargin, PanelMargin, PanelWidth, Screen.height - PanelMargin * 2f),
                GUI.skin.box);

            GUILayout.Label(BuildReport(), _style);
            GUILayout.EndArea();
        }

        /// <summary>
        /// Composes the whole panel into one string, rebuilt each frame without allocating.
        /// </summary>
        /// <remarks>
        /// The combat diagnostics come first and are drawn unconditionally. Everything below them
        /// needs a fully-constructed boss, and a boss that failed to construct is precisely the
        /// failure this panel has to be able to show — so the adaptation sections degrade to a line
        /// of explanation rather than blanking the whole overlay.
        /// </remarks>
        private string BuildReport()
        {
            _builder.Clear();

            AppendClock();
            AppendCombatants();
            AppendEventTail();

            AdaptationManager adaptation = _boss != null ? _boss.GetAdaptation() : null;
            BossTuning tuning = _boss != null ? _boss.Tuning : null;

            if (adaptation == null || tuning == null)
            {
                _builder.AppendLine("<b>LEARNING</b>");
                _builder.AppendLine(_boss == null
                    ? "no boss in the scene."
                    : "boss has not finished initialising.");

                return _builder.ToString();
            }

            AppendProfile(adaptation.Profile);
            AppendTuning(tuning);
            AppendAdopted(adaptation);

            return _builder.ToString();
        }

        /// <summary>
        /// Reports the clock, which gates almost every other system.
        /// </summary>
        /// <remarks>
        /// A paused or zero-scaled clock stops both controllers' <c>Update</c> before anything runs,
        /// which freezes every bar, prevents death and prevents the outcome screen — while logging
        /// nothing at all. It is the cheapest thing to rule out, so it goes first.
        /// </remarks>
        private void AppendClock()
        {
            if (_time == null)
            {
                _builder.AppendLine("<b>CLOCK</b>   no time service");
                _builder.AppendLine();
                return;
            }

            _builder.AppendLine(
                $"<b>CLOCK</b>   {(_time.IsPaused ? "<b>PAUSED</b>" : "running")}   " +
                $"scale {_time.TimeScale:0.000}   dt {_time.DeltaTime:0.0000}");
            _builder.AppendLine($"combat time {_time.CombatTime:0.0}s");
            _builder.AppendLine();
        }

        /// <summary>
        /// Reports both combatants as raw values, not percentages.
        /// </summary>
        /// <remarks>
        /// Raw current-over-maximum on purpose: a bar pinned at 100% and a bar that is not being
        /// driven at all look identical, and the numbers separate them immediately. The flags beside
        /// them cover every guard that can silently swallow a hit.
        /// </remarks>
        private void AppendCombatants()
        {
            if (_player == null)
            {
                _builder.AppendLine("<b>PLAYER</b>   not found in scene");
            }
            else
            {
                _builder.AppendLine(
                    $"<b>PLAYER</b>   {_player.CurrentStateName}" +
                    (_player.IsInitialised ? string.Empty : "   <b>UNINITIALISED</b>"));
                _builder.AppendLine(
                    $"hp {Amount(_player.Health)}   sta {Amount(_player.Stamina)}   " +
                    $"posture {Amount(_player.Posture)}");
                _builder.AppendLine(
                    $"invuln {Flag(_player.IsInvulnerable)}   guard {Flag(_player.IsGuarding)}   " +
                    $"immortal {Flag(_player.IsImmortal)}   alive {Flag(_player.IsAlive)}");
            }

            _builder.AppendLine();

            if (_boss == null)
            {
                _builder.AppendLine("<b>BOSS</b>   not found in scene");
                _builder.AppendLine();
                return;
            }

            _builder.AppendLine(
                $"<b>BOSS</b>   phase {_boss.PhaseIndex + 1}   {_boss.CurrentStateName}" +
                (_boss.IsInitialised ? string.Empty : "   <b>UNINITIALISED</b>"));
            _builder.AppendLine($"hp {Amount(_boss.Health)}   poise {Amount(_boss.Poise)}");

            BossContext context = _boss.GetContext();

            if (context != null)
            {
                _builder.AppendLine(
                    $"cooldown {context.AttackCooldownRemaining:0.00}   " +
                    $"sees player {Flag(context.HasPerceivedPlayer)}   " +
                    $"overbalanced {Flag(context.IsOverbalanced)}");
            }

            _builder.AppendLine($"phase invuln {_boss.PhaseTransitionInvulnRemaining:0.00}");
            _builder.AppendLine();
        }

        /// <summary>
        /// Lists the most recent combat occurrences.
        /// </summary>
        /// <remarks>
        /// This is the diagnostic the rest of the panel exists to support. Nothing at all means no
        /// attack was thrown; a run of whiffs means the swings are missing; evasions mean an
        /// invulnerability guard is eating them; and landings while the bars sit still mean the
        /// damage is real and the interface is at fault. One caveat: a deflect or a late block
        /// resolves to an outcome the executor does not publish, so guarded hits leave no trace here.
        /// </remarks>
        private void AppendEventTail()
        {
            _builder.AppendLine($"<b>LAST {EventTailLength} COMBAT EVENTS</b>");

            if (_events == null)
            {
                _builder.AppendLine("no combat event bus.");
                _builder.AppendLine();
                return;
            }

            if (_recentCount == 0)
            {
                _builder.AppendLine("<b>nothing has happened yet.</b>");
                _builder.AppendLine();
                return;
            }

            for (int i = _recentCount - 1; i >= 0; i--)
            {
                CombatEvent recorded = _recentEvents[i];
                string actor = recorded.Actor == CombatantTeam.Player ? "you " : "boss";

                _builder.AppendLine(
                    $"{recorded.Timestamp,6:0.0}  {actor}  {recorded.Kind}" +
                    (recorded.Magnitude > 0f ? $"  {recorded.Magnitude:0.#}" : string.Empty));
            }

            _builder.AppendLine();
        }

        /// <summary>Renders a pool as raw current-over-maximum, so a still bar can be told from a full one.</summary>
        private static string Amount(IResourcePool pool) =>
            pool == null ? "n/a" : $"{pool.Current:0}/{pool.Maximum:0}";

        /// <summary>Renders a flag compactly, emphasising the state that would explain a swallowed hit.</summary>
        private static string Flag(bool value) => value ? "<b>YES</b>" : "no";

        /// <summary>Lists every habit with its strength and how much evidence stands behind it.</summary>
        private void AppendProfile(BehaviorProfile profile)
        {
            _builder.AppendLine("<b>WHAT IT THINKS IT KNOWS</b>");
            _builder.AppendLine("<i>habit                 value  confidence</i>");

            foreach (BehaviorFeature feature in Enum.GetValues(typeof(BehaviorFeature)))
            {
                FeatureReading reading = profile.Get(feature);

                _builder.Append(feature.ToString().PadRight(22));
                _builder.Append(Bar(reading.Value));
                _builder.Append(' ');
                _builder.AppendLine(Percent(reading.Confidence));
            }

            _builder.AppendLine($"strongest read: {profile.StrongestFeature()}");
            _builder.AppendLine();
        }

        /// <summary>Lists only the behaviours that have actually moved away from their baseline.</summary>
        private void AppendTuning(BossTuning tuning)
        {
            _builder.AppendLine("<b>HOW IT HAS CHANGED</b>");

            bool anyChanged = false;

            foreach (BossTuningParameter parameter in Enum.GetValues(typeof(BossTuningParameter)))
            {
                if (tuning.DeviationFromBaseline(parameter) < 0.01f)
                {
                    continue;
                }

                anyChanged = true;

                _builder.Append(parameter.ToString().PadRight(24));
                _builder.Append(tuning.Get(parameter).ToString("0.00"));
                _builder.Append("  -> ");
                _builder.AppendLine(tuning.GetTarget(parameter).ToString("0.00"));
            }

            if (!anyChanged)
            {
                _builder.AppendLine("(nothing yet - fighting from its baseline)");
            }

            _builder.AppendLine();
        }

        /// <summary>Lists the answers the boss has developed, in the order it developed them.</summary>
        private void AppendAdopted(AdaptationManager adaptation)
        {
            _builder.AppendLine("<b>ANSWERS DEVELOPED</b>");

            if (adaptation.AdoptedStrategies.Count == 0)
            {
                _builder.AppendLine("(none)");
                return;
            }

            foreach (CounterStrategy strategy in adaptation.AdoptedStrategies)
            {
                if (strategy == null)
                {
                    continue;
                }

                _builder.AppendLine($"- {strategy.name}");

                if (!string.IsNullOrWhiteSpace(strategy.TellMessage))
                {
                    _builder.AppendLine($"    \"{strategy.TellMessage}\"");
                }
            }
        }

        /// <summary>Renders a zero-to-one value as a fixed-width text bar.</summary>
        private static string Bar(float normalized)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(normalized * BarSegments), 0, BarSegments);
            return new string('#', filled).PadRight(BarSegments, '.');
        }

        private static string Percent(float normalized) => $"{Mathf.RoundToInt(normalized * 100f),3}%";

        /// <summary>Builds the panel style lazily, since GUI styles are unavailable before first draw.</summary>
        private void EnsureStyle()
        {
            if (_style != null)
            {
                return;
            }

            _style = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 11,
                alignment = TextAnchor.UpperLeft,
                font = Font.CreateDynamicFontFromOSFont("Consolas", 11)
            };
        }
    }
}
