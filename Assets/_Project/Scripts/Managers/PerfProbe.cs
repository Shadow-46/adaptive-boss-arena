using System;
using System.Globalization;
using System.IO;
using AdaptiveBossArena.Utilities.Statistics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Measures frame cost against the project's performance budgets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The physics and graphics overhaul adds skinned characters, ragdolls, debris, fog and a second
    /// render tier. Every one of those can quietly cost a frame budget, and "it felt smooth on this
    /// machine" is not a measurement. This reports the same numbers the budgets are written in, from
    /// the same scenario every time, so a stage that regresses shows it.
    /// </para>
    /// <para>
    /// Two ways in. F3 shows a live panel while playing. A capture runs unattended: the command-line
    /// flag <c>-perfCapture &lt;seconds&gt;</c> on a desktop player, or the query string
    /// <c>?perf=&lt;seconds&gt;</c> on the WebGL page. A capture waits for the fight to start, lets it
    /// settle, samples for the requested time, and writes one <c>[PERF]</c> line to the log — which in
    /// a browser is the developer console, the only place a shipped build can be read from remotely.
    /// </para>
    /// <para>
    /// Draw-call and triangle counts come from the profiler, which only reports them in development
    /// builds and the editor. A release build prints <c>n/a</c> for those rather than a zero that
    /// would read as a passing budget.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PerfProbe : MonoBehaviour
    {
        private const string CaptureFlag = "-perfCapture";
        private const string LogFlag = "-perfLog";
        private const string QuitFlag = "-perfQuit";
        private const string CaptureQueryKey = "perf";

        /// <summary>Upper bound on captured frames: ten minutes at 240 Hz.</summary>
        private const int SampleCapacity = 144000;

        /// <summary>Frames discarded after the fight starts, so loading hitches do not skew the capture.</summary>
        private const float SettleSeconds = 2f;

        private const float PanelWidth = 260f;
        private const float PanelMargin = 12f;

        [SerializeField]
        [Tooltip("Key that shows and hides the live panel.")]
        private Key _toggleKey = Key.F3;

        private readonly FrameTimeSampler _sampler = new FrameTimeSampler(SampleCapacity);

        private ProfilerRecorder _drawCalls;
        private ProfilerRecorder _triangles;
        private long _drawCallTotal;
        private long _triangleTotal;
        private int _countedFrames;

        private bool _isVisible;
        private GUIStyle _style;

        private float _captureSeconds;
        private string _logPath;
        private bool _quitWhenDone;
        private float _settleRemaining = SettleSeconds;
        private float _captured;
        private bool _captureFinished;

        /// <summary>Whether an unattended capture was requested for this run and is still running.</summary>
        public bool IsCapturing => _captureSeconds > 0f && !_captureFinished;

        private void Awake()
        {
            ReadCaptureRequest();
        }

        private void OnEnable()
        {
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        private void OnDisable()
        {
            _drawCalls.Dispose();
            _triangles.Dispose();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard[_toggleKey].wasPressedThisFrame)
            {
                _isVisible = !_isVisible;
            }

            // Measured whenever the fight is running, whether or not a capture was asked for, so the
            // live panel shows numbers for the fight rather than for the frozen intro. Reading the
            // time scale is fine; only TimeService may write it.
            if (Time.timeScale <= 0f)
            {
                return;
            }

            if (_settleRemaining > 0f)
            {
                _settleRemaining -= Time.unscaledDeltaTime;
                return;
            }

            Sample();

            if (!IsCapturing)
            {
                return;
            }

            _captured += Time.unscaledDeltaTime;

            if (_captured >= _captureSeconds)
            {
                FinishCapture();
            }
        }

        private void Sample()
        {
            _sampler.Add(Time.unscaledDeltaTime);

            if (_drawCalls.Valid && _drawCalls.LastValue > 0)
            {
                _drawCallTotal += _drawCalls.LastValue;
                _triangleTotal += _triangles.Valid ? _triangles.LastValue : 0;
                _countedFrames++;
            }
        }

        private void FinishCapture()
        {
            _captureFinished = true;

            string line = FormatReport();
            Debug.Log(line);

            if (!string.IsNullOrEmpty(_logPath))
            {
                try
                {
                    File.AppendAllText(_logPath, line + Environment.NewLine);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[Adaptive Boss Arena] Could not write perf log '{_logPath}': {exception.Message}");
                }
            }

            if (_quitWhenDone)
            {
                Application.Quit();
            }
        }

        /// <summary>Formats the current measurement as the single line budgets are checked against.</summary>
        /// <returns>The report line.</returns>
        public string FormatReport()
        {
            CultureInfo invariant = CultureInfo.InvariantCulture;

            string drawCalls = _countedFrames > 0
                ? (_drawCallTotal / _countedFrames).ToString(invariant)
                : "n/a";

            string triangles = _countedFrames > 0
                ? (_triangleTotal / _countedFrames).ToString(invariant)
                : "n/a";

            return string.Format(
                invariant,
                "[PERF] platform={0} p50={1:F2}ms p95={2:F2}ms max={3:F2}ms frames={4} drawcalls={5} tris={6} seconds={7:F1}",
                Application.platform,
                _sampler.PercentileMilliseconds(0.5f),
                _sampler.PercentileMilliseconds(0.95f),
                _sampler.MaxMilliseconds,
                _sampler.Count,
                drawCalls,
                triangles,
                _captured);
        }

        private void ReadCaptureRequest()
        {
            string[] arguments = Environment.GetCommandLineArgs();

            for (int i = 0; i < arguments.Length; i++)
            {
                string argument = arguments[i];
                string next = i + 1 < arguments.Length ? arguments[i + 1] : null;

                if (argument == CaptureFlag && TryParseSeconds(next, out float seconds))
                {
                    _captureSeconds = seconds;
                }
                else if (argument == LogFlag && !string.IsNullOrEmpty(next))
                {
                    _logPath = next;
                }
                else if (argument == QuitFlag)
                {
                    _quitWhenDone = true;
                }
            }

            if (_captureSeconds <= 0f && TryReadQuerySeconds(Application.absoluteURL, out float querySeconds))
            {
                _captureSeconds = querySeconds;
            }
        }

        /// <summary>Reads the capture length from a page URL's query string.</summary>
        /// <remarks>
        /// Parsed by hand because a WebGL build has no <c>System.Web</c>, and only one key is needed.
        /// </remarks>
        /// <param name="url">The page URL, which is empty outside a browser.</param>
        /// <param name="seconds">The requested capture length.</param>
        /// <returns>True when the query asked for a capture.</returns>
        public static bool TryReadQuerySeconds(string url, out float seconds)
        {
            seconds = 0f;

            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            int queryStart = url.IndexOf('?');

            if (queryStart < 0)
            {
                return false;
            }

            string query = url.Substring(queryStart + 1);
            int fragment = query.IndexOf('#');

            if (fragment >= 0)
            {
                query = query.Substring(0, fragment);
            }

            foreach (string pair in query.Split('&'))
            {
                int equals = pair.IndexOf('=');

                if (equals <= 0 || pair.Substring(0, equals) != CaptureQueryKey)
                {
                    continue;
                }

                return TryParseSeconds(pair.Substring(equals + 1), out seconds);
            }

            return false;
        }

        private static bool TryParseSeconds(string text, out float seconds)
        {
            seconds = 0f;

            return !string.IsNullOrEmpty(text) &&
                   float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) &&
                   seconds > 0f;
        }

        private void OnGUI()
        {
            if (!_isVisible)
            {
                return;
            }

            _style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                wordWrap = true
            };

            var panel = new Rect(Screen.width - PanelWidth - PanelMargin, PanelMargin, PanelWidth, 170f);
            GUI.Box(panel, FormatReport().Replace(" ", "\n"), _style);
        }
    }
}
