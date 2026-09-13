using System.Globalization;

namespace AdaptiveBossArena.Utilities.Statistics
{
    /// <summary>
    /// Whether this run was launched to capture a performance measurement, and how.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pure and in Utilities because two assemblies must agree on it and neither may see the other.
    /// The probe that measures lives in Game; the title screen that must skip straight into the fight
    /// lives in UI, which Game references and not the reverse. A capture that stopped at the title
    /// screen measured a menu and never wrote its line, which is exactly how the first baseline
    /// attempt failed.
    /// </para>
    /// <para>
    /// Desktop players take <c>-perfCapture &lt;seconds&gt; [-perfLog &lt;path&gt;] [-perfQuit]</c>.
    /// A WebGL page has no command line, so it takes <c>?perf=&lt;seconds&gt;</c> instead.
    /// </para>
    /// </remarks>
    public readonly struct PerfCaptureRequest
    {
        private const string CaptureFlag = "-perfCapture";
        private const string LogFlag = "-perfLog";
        private const string QuitFlag = "-perfQuit";
        private const string CaptureQueryKey = "perf";

        /// <summary>How long to sample, in seconds. Zero when no capture was requested.</summary>
        public float Seconds { get; init; }

        /// <summary>File the report line is appended to, or null.</summary>
        public string LogPath { get; init; }

        /// <summary>Whether the application should quit once the capture is written.</summary>
        public bool QuitWhenDone { get; init; }

        /// <summary>Whether a capture was requested at all.</summary>
        public bool IsRequested => Seconds > 0f;

        /// <summary>Reads a capture request from the command line, falling back to the page URL.</summary>
        /// <param name="arguments">Command-line arguments, as from <c>Environment.GetCommandLineArgs</c>.</param>
        /// <param name="url">The page URL, which is empty outside a browser.</param>
        /// <returns>The request, which may be empty.</returns>
        public static PerfCaptureRequest Read(string[] arguments, string url)
        {
            float seconds = 0f;
            string logPath = null;
            bool quit = false;

            if (arguments != null)
            {
                for (int i = 0; i < arguments.Length; i++)
                {
                    string next = i + 1 < arguments.Length ? arguments[i + 1] : null;

                    switch (arguments[i])
                    {
                        case CaptureFlag when TryParseSeconds(next, out float parsed):
                            seconds = parsed;
                            break;

                        case LogFlag when !string.IsNullOrEmpty(next):
                            logPath = next;
                            break;

                        case QuitFlag:
                            quit = true;
                            break;
                    }
                }
            }

            if (seconds <= 0f && TryReadQuerySeconds(url, out float querySeconds))
            {
                seconds = querySeconds;
            }

            return new PerfCaptureRequest { Seconds = seconds, LogPath = logPath, QuitWhenDone = quit };
        }

        /// <summary>Reads the capture length from a page URL's query string.</summary>
        /// <remarks>
        /// Parsed by hand because a WebGL build has no <c>System.Web</c>, and only one key is needed.
        /// </remarks>
        /// <param name="url">The page URL.</param>
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
    }
}
