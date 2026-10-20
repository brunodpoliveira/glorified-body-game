using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GlorifiedBody.DebugTools
{
    /// <summary>
    /// Writes every Unity log message and every RunTelemetry event to runtime.jsonl.
    /// Active only when a run folder is given: command line "-gbRunDir PATH",
    /// or the GB_RUN_DIR environment variable, or Begin(path) from an editor tool.
    /// Also records frame time once per second (event "frame_stats").
    /// </summary>
    public static class RunLogSink
    {
        private static StreamWriter writer;
        private static readonly object Gate = new object();
        private static float statsWindowStart;
        private static int statsFrames;
        private static float statsMaxDt;

        public const string EditorRunDirKey = "GlorifiedBody.RunDir";

        public static bool IsActive => writer != null;
        public static string RunDir { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoStart()
        {
            string dir = ReadArg("-gbRunDir") ?? Environment.GetEnvironmentVariable("GB_RUN_DIR");
#if UNITY_EDITOR
            // Editor tools (gb_play) pass the run folder through SessionState, because
            // entering Play mode can reload the domain and clear static fields.
            if (string.IsNullOrEmpty(dir)) dir = UnityEditor.SessionState.GetString(EditorRunDirKey, string.Empty);
#endif
            if (!string.IsNullOrEmpty(dir)) Begin(dir);
        }

        public static void Begin(string runDir)
        {
            End();
            Directory.CreateDirectory(runDir);
            RunDir = runDir;
            writer = new StreamWriter(Path.Combine(runDir, "runtime.jsonl"), append: true, new UTF8Encoding(false)) { AutoFlush = true };
            Application.logMessageReceivedThreaded += OnLog;
            Application.quitting += End;
            EnsureFrameProbe();
            WriteEvent("sink_begin", Application.unityVersion);
        }

        public static void End()
        {
            if (writer == null) return;
            Application.logMessageReceivedThreaded -= OnLog;
            lock (Gate)
            {
                writer.Flush();
                writer.Dispose();
                writer = null;
            }
        }

        public static void WriteEvent(string name, string detail)
        {
            Write("event", name, detail ?? string.Empty);
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            Write(type.ToString(), condition, stackTrace);
        }

        private static void Write(string type, string message, string stack)
        {
            if (writer == null) return;
            string scene = string.Empty;
            int frame = -1;
            float time = -1f;
            // Unity API calls are main-thread only. Logs from other threads skip these fields.
            if (System.Threading.Thread.CurrentThread.ManagedThreadId == MainThreadId)
            {
                scene = SceneManager.GetActiveScene().name;
                frame = Time.frameCount;
                time = Time.unscaledTime;
            }
            var sb = new StringBuilder(256);
            sb.Append("{\"type\":").Append(Quote(type))
              .Append(",\"frame\":").Append(frame)
              .Append(",\"t\":").Append(time.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
              .Append(",\"scene\":").Append(Quote(scene))
              .Append(",\"message\":").Append(Quote(message))
              .Append(",\"stack\":").Append(Quote(stack))
              .Append('}');
            lock (Gate)
            {
                writer?.WriteLine(sb.ToString());
            }
        }

        private static int MainThreadId = -1;

        private static void EnsureFrameProbe()
        {
            MainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            if (!Application.isPlaying) return;
            var go = new GameObject("[RunLogSink.FrameProbe]");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<FrameProbe>();
        }

        private sealed class FrameProbe : MonoBehaviour
        {
            private void Update()
            {
                float dt = Time.unscaledDeltaTime;
                statsFrames++;
                if (dt > statsMaxDt) statsMaxDt = dt;
                float now = Time.unscaledTime;
                if (now - statsWindowStart < 1f) return;
                float avgMs = (now - statsWindowStart) * 1000f / Mathf.Max(1, statsFrames);
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                WriteEvent("frame_stats", string.Format(inv, "avg_ms={0:0.00};max_ms={1:0.00};frames={2};scene={3}", avgMs, statsMaxDt * 1000f, statsFrames, SceneManager.GetActiveScene().name));
                statsWindowStart = now;
                statsFrames = 0;
                statsMaxDt = 0f;
            }
        }

        private static string ReadArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }

        private static string Quote(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
