using System;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// Reads bot settings from the command line (player builds, batch editor) or, in the editor,
    /// from SessionState (set by GlorifiedBody.Editor.Automation.PlaythroughRunner before Play mode).
    /// </summary>
    public static class BotArgs
    {
        public const string KeyBot = "GlorifiedBody.Bot.Kind";
        public const string KeyLevel = "GlorifiedBody.Bot.Level";
        public const string KeyRunDir = "GlorifiedBody.RunDir";
        public const string KeyTimeout = "GlorifiedBody.Bot.Timeout";

        public static string Bot => Read("-gbBot", KeyBot);
        public static string Level => Read("-gbLevel", KeyLevel);
        public static string RunDir => Read("-gbRunDir", KeyRunDir);

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
#if UNITY_EDITOR
            string s = UnityEditor.SessionState.GetString(sessionKey, string.Empty);
            if (!string.IsNullOrEmpty(s)) return s;
#endif
            return null;
        }
    }
}
