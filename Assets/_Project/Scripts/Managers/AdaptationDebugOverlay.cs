using System;
using System.Text;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.Learning;
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
        private const float PanelWidth = 360f;
        private const float PanelMargin = 12f;
        private const int BarSegments = 20;

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
            // Stripped from release builds unless Training explicitly forces it on. The developer
            // toggle exists to tune the encounter, not to ship with it; the Training overlay is a
            // deliberate player-facing practice aid.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            const bool developerBuild = true;
#else
            const bool developerBuild = false;
#endif
            if (!developerBuild && !_forced)
            {
                return;
            }

            if ((!_isVisible && !_forced) || _boss == null)
            {
                return;
            }

            AdaptationManager adaptation = _boss.GetAdaptation();
            BossTuning tuning = _boss.Tuning;

            if (adaptation == null || tuning == null)
            {
                return;
            }

            EnsureStyle();

            GUILayout.BeginArea(
                new Rect(PanelMargin, PanelMargin, PanelWidth, Screen.height - PanelMargin * 2f),
                GUI.skin.box);

            GUILayout.Label(BuildReport(adaptation, tuning), _style);
            GUILayout.EndArea();
        }

        /// <summary>Composes the whole panel into one string, rebuilt each frame without allocating.</summary>
        private string BuildReport(AdaptationManager adaptation, BossTuning tuning)
        {
            _builder.Clear();

            _builder.AppendLine($"<b>BOSS</b>   phase {_boss.PhaseIndex + 1}   {_boss.CurrentStateName}");
            _builder.AppendLine($"health {Percent(_boss.Health.Normalized)}   poise {Percent(_boss.Poise.Normalized)}");
            _builder.AppendLine();

            AppendProfile(adaptation.Profile);
            AppendTuning(tuning);
            AppendAdopted(adaptation);

            return _builder.ToString();
        }

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
