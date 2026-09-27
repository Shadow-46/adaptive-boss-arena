using System.Collections.Generic;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Chooses the graphics quality on first run, applies it to the renderer, and in the browser checks the choice
    /// against the frame rate once the fight is running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The quality levels are named by platform - "Browser Medium", "Desktop High" - and each build keeps only its
    /// own three, so a quality is found by the end of its name rather than by a fixed index.
    /// </para>
    /// <para>
    /// On Windows the graphics card can be read, so a dedicated card starts on High and integrated graphics on Low.
    /// A browser hides the card, so it starts on Medium and measures instead: the first ten seconds of fighting
    /// decide whether to step up or down, once, and only while the player has not chosen a quality themselves.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class QualityDirector : MonoBehaviour
    {
        /// <summary>How long the browser measures before deciding.</summary>
        private const float SampleSeconds = 10f;

        /// <summary>Frame time the 95th percentile must stay under to step up: room to spare at sixty.</summary>
        public const float StepUpMilliseconds = 12f;

        /// <summary>Frame time the 95th percentile must exceed to step down: a visible stutter.</summary>
        public const float StepDownMilliseconds = 20f;

        /// <summary>Video memory from which a Windows graphics card counts as dedicated.</summary>
        private const int DedicatedMemoryMegabytes = 3000;

        private readonly List<float> _frames = new List<float>(1024);
        private float _sampled;
        private bool _decided;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ChooseOnStartup()
        {
            GraphicsTier.Changed -= ApplyLevel;
            GraphicsTier.Changed += ApplyLevel;

            GraphicsTier.SetAutomatic(StartingQuality(
                Application.platform == RuntimePlatform.WebGLPlayer,
                SystemInfo.graphicsMemorySize,
                SystemInfo.graphicsDeviceName));

            ApplyLevel(GraphicsTier.Current);
        }

        /// <summary>The quality a machine starts on before anything has been measured or chosen.</summary>
        /// <param name="browser">Whether this is the browser build.</param>
        /// <param name="graphicsMemoryMegabytes">The graphics card's memory.</param>
        /// <param name="deviceName">The graphics card's name.</param>
        /// <returns>The starting quality.</returns>
        public static GraphicsQuality StartingQuality(bool browser, int graphicsMemoryMegabytes, string deviceName)
        {
            if (browser)
            {
                return GraphicsQuality.Medium;
            }

            string device = (deviceName ?? string.Empty).ToLowerInvariant();
            bool integrated = device.Contains("intel") || device.Contains("uhd") || device.Contains("iris") ||
                              device.Contains("radeon(tm) graphics") || device.Contains("vega");

            return !integrated && graphicsMemoryMegabytes >= DedicatedMemoryMegabytes
                ? GraphicsQuality.High
                : GraphicsQuality.Low;
        }

        /// <summary>
        /// The quality to move to after measuring, or the current one when no move is warranted.
        /// </summary>
        /// <param name="current">The quality measured.</param>
        /// <param name="p95Milliseconds">The 95th-percentile frame time over the sample.</param>
        /// <returns>The quality to use.</returns>
        public static GraphicsQuality AdjustedQuality(GraphicsQuality current, float p95Milliseconds)
        {
            if (p95Milliseconds > StepDownMilliseconds && current > GraphicsQuality.Low)
            {
                return current - 1;
            }

            if (p95Milliseconds < StepUpMilliseconds && current < GraphicsQuality.High)
            {
                return current + 1;
            }

            return current;
        }

        /// <summary>
        /// The index, among the quality levels a build carries, of the level for a quality.
        /// </summary>
        /// <param name="levelNames">The build's quality level names, lowest first.</param>
        /// <param name="quality">The quality wanted.</param>
        /// <returns>The level's index, or the nearest by position when no name matches.</returns>
        public static int LevelIndex(IReadOnlyList<string> levelNames, GraphicsQuality quality)
        {
            string suffix = " " + quality;

            for (int i = 0; i < levelNames.Count; i++)
            {
                if (levelNames[i] != null && levelNames[i].EndsWith(suffix))
                {
                    return i;
                }
            }

            return Mathf.Clamp((int)quality, 0, Mathf.Max(0, levelNames.Count - 1));
        }

        private static void ApplyLevel(GraphicsQuality quality)
        {
            int level = LevelIndex(QualitySettings.names, quality);

            if (QualitySettings.GetQualityLevel() != level)
            {
                QualitySettings.SetQualityLevel(level, applyExpensiveChanges: true);
            }
        }

        private void Update()
        {
            // Only in the browser, only once, only while the player has not chosen, and only during the fight -
            // the frozen intro and the menus say nothing about what the fight costs.
            if (_decided || !GraphicsTier.IsAutomatic || Application.platform != RuntimePlatform.WebGLPlayer ||
                Time.timeScale <= 0f)
            {
                return;
            }

            float frame = Time.unscaledDeltaTime;
            _frames.Add(frame * 1000f);
            _sampled += frame;

            if (_sampled < SampleSeconds)
            {
                return;
            }

            _decided = true;
            _frames.Sort();
            float p95 = _frames[Mathf.Min(_frames.Count - 1, (int)(_frames.Count * 0.95f))];

            GraphicsQuality adjusted = AdjustedQuality(GraphicsTier.Current, p95);
            Debug.Log($"[Adaptive Boss Arena] Graphics: p95 {p95:F1} ms on {GraphicsTier.Current}; using {adjusted}.");
            GraphicsTier.SetAutomatic(adjusted);
        }
    }
}
