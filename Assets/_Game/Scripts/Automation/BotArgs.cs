using System;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// Reads bot settings from the command line (player builds, batch editor), from the page URL (the web build), or, in the editor,
    /// from SessionState (set by GlorifiedBody.Editor.Automation.PlaythroughRunner before Play mode).
    /// </summary>
    public static class BotArgs
    {
        public const string KeyBot = "GlorifiedBody.Bot.Kind";
        public const string KeyLevel = "GlorifiedBody.Bot.Level";
        public const string KeyRunDir = "GlorifiedBody.RunDir";
        public const string KeyTimeout = "GlorifiedBody.Bot.Timeout";
        public const string KeyRecord = "GlorifiedBody.Bot.Record";

        public static string Bot => Read("-gbBot", KeyBot);
        public static string Level => Read("-gbLevel", KeyLevel);
        public static string RunDir => Read("-gbRunDir", KeyRunDir);

        /// <summary>The run folder of a bot, or a folder of the player's data when no run folder is given (the web build: its files are lost, the console carries the steps).</summary>
        public static string RunDirOrData => RunDir ?? System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "bot_run");

        /// <summary>-gbShotWait s: the bot holds each shot this long (real time), so a browser driver can take the screen.</summary>
        public static float ShotWait =>
            float.TryParse(Read("-gbShotWait", null), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;

        /// <summary>-gbRecord 1: the bot saves video frames of its steps (FrameRecorder).</summary>
        public static bool Record => Read("-gbRecord", KeyRecord) == "1";

        public static float TimeoutSeconds
        {
            get
            {
                string s = Read("-gbTimeout", KeyTimeout);
                return float.TryParse(s, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v) && v > 0f ? v : 900f;
            }
        }

        private static string Read(string arg, string sessionKey)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == arg) return args[i + 1];
            }
            // The web build has no command line: the page URL carries them (index.html?gbBot=demo&gbShotWait=1.5).
            string url = UnityEngine.Application.absoluteURL;
            int query = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (query >= 0)
            {
                foreach (string pair in url.Substring(query + 1).Split('&'))
                {
                    int eq = pair.IndexOf('=');
                    if (eq > 0 && "-" + pair.Substring(0, eq) == arg) return Uri.UnescapeDataString(pair.Substring(eq + 1));
                }
            }
#if UNITY_EDITOR
            if (sessionKey == null) return null;
            string s = UnityEditor.SessionState.GetString(sessionKey, string.Empty);
            if (!string.IsNullOrEmpty(s)) return s;
#endif
            return null;
        }
    }
}
