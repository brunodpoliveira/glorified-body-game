using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.DebugTools
{
    /// <summary>
    /// Game events for automated runs (bots, critics). Writes through RunLogSink when it is active.
    /// Call sites stay cheap when no run is active: Event() returns at once.
    /// Event names are lower_snake_case. See docs/CONTRACT.md for the list.
    /// </summary>
    public static class RunTelemetry
    {
        public static event Action<string, string> OnEvent;

        /// <summary>Events seen in this session. The structural bot reads this list.</summary>
        public static readonly List<KeyValuePair<string, string>> Recent = new List<KeyValuePair<string, string>>();

        public static bool Enabled => RunLogSink.IsActive || OnEvent != null;

        public static void Event(string name, string detail = null)
        {
            if (Recent.Count > 2000) Recent.RemoveRange(0, 1000);
            Recent.Add(new KeyValuePair<string, string>(name, detail));
            OnEvent?.Invoke(name, detail);
            if (RunLogSink.IsActive) RunLogSink.WriteEvent(name, detail);
        }

        public static void Clear() => Recent.Clear();
    }
}
