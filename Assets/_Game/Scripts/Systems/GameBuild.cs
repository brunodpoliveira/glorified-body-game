using System;
using UnityEngine;

namespace GlorifiedBody.Systems
{
    /// <summary>
    /// Dev or release (2026-10-31, the design sweep's rank 16: "a build script, a dev flag for the debug keys and the
    /// test-room buttons"). Dev: the editor, a development build, or a player started with -gbDev. A release build hides
    /// the test rooms of the main menu and turns off the debug keys (F1, F2, F4 to F9, F11, F12).
    /// </summary>
    public static class GameBuild
    {
        public const string DevArg = "-gbDev";

        /// <summary>Tests: null follows the build; true or false forces it.</summary>
        public static bool? Force { get; set; }

        public static bool Dev => Force ?? (Application.isEditor || Debug.isDebugBuild || HasArg(DevArg));

        private static bool HasArg(string arg)
        {
            foreach (string a in Environment.GetCommandLineArgs())
                if (string.Equals(a, arg, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Force = null;
    }
}
