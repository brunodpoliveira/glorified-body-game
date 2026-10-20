using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Gore.Warmup
{
    /// <summary>
    /// Diagnostic for first-use stalls. Off unless the environment variable GB_STALL_PROBE is set.
    /// Watches profiler markers (shader, instantiate, GC, loading, animator, physics) and, for each frame over
    /// 150 ms, writes a "stall" telemetry event with the heaviest markers of that frame.
    /// Needs no Profiler window: ProfilerRecorder works in the editor and in development players.
    /// </summary>
    public sealed class FirstUseStallProbe : MonoBehaviour
    {
        public const string EnvVar = "GB_STALL_PROBE";
        private const float StallSeconds = 0.15f;
        private static readonly string[] Keywords =
        {
            "Shader", "GPUProgram", "Compile", "Instantiate", "GC.Collect", "Loading", "Awake", "Animator",
            "NavMesh", "Physics", "Particle", "Font", "Canvas", "Texture", "Mesh", "Skinned", "PSO", "Pipeline",
            "BehaviourUpdate", "Coroutine", "Gfx.", "Camera.Render", "Inl_", "Start",
            // Top-level and render-side markers: they name stalls that the narrow markers above miss
            // (for example the second frame after a scene load).
            "PlayerLoop", "EditorLoop", "Application.", "Render", "Present", "WaitFor", "Culling", "Batch",
            "UIR", "Semaphore", "Script", "PostLate", "Initialization", "PreLate",
        };

        private readonly List<(string name, ProfilerRecorder recorder)> recorders = new();
        private long[] previous = Array.Empty<long>();
        private readonly List<(string name, double ms)> scratch = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvVar)))
            {
                return;
            }

            var go = new GameObject("[FirstUseStallProbe]");
            DontDestroyOnLoad(go);
            go.AddComponent<FirstUseStallProbe>();
        }

        private void Awake()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (ProfilerRecorderHandle handle in handles)
            {
                ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(handle);
                if (d.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds) continue;
                foreach (string k in Keywords)
                {
                    if (d.Name.IndexOf(k, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    recorders.Add((d.Name, ProfilerRecorder.StartNew(d.Category, d.Name, 1)));
                    break;
                }
            }

            previous = new long[recorders.Count];
            Debug.Log($"[StallProbe] watching {recorders.Count} markers.");
        }

        private void OnDestroy()
        {
            foreach (var r in recorders) r.recorder.Dispose();
            recorders.Clear();
        }

        private void Update()
        {
            // LastValue holds the previous (finished) frame; that is the frame unscaledDeltaTime measured.
            // A marker with no sample in a frame keeps its old LastValue: count only values that changed.
            float dt = Time.unscaledDeltaTime;
            bool stall = dt >= StallSeconds;
            if (stall) scratch.Clear();
            for (int i = 0; i < recorders.Count; i++)
            {
                long value = recorders[i].recorder.LastValue;
                if (stall && value != previous[i] && value >= 5_000_000) scratch.Add((recorders[i].name, value * 1e-6));
                previous[i] = value;
            }

            if (!stall)
            {
                return;
            }

            scratch.Sort((a, b) => b.ms.CompareTo(a.ms));
            var sb = new StringBuilder();
            sb.Append("frame_ms=").Append((dt * 1000f).ToString("0"));
            for (int i = 0; i < scratch.Count && i < 12; i++)
            {
                sb.Append(';').Append(scratch[i].name).Append('=').Append(scratch[i].ms.ToString("0"));
            }

            RunTelemetry.Event("stall", sb.ToString());
        }
    }
}
