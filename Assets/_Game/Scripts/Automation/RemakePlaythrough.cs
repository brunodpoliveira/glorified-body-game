using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.Player;
using GlorifiedBody.Rendering;
using GlorifiedBody.Systems;
using Debug = UnityEngine.Debug;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The T26 bot (bot "remake", restart of 2026-11-01): the black recovery room (W1, act 1) of RemakeLook under profile A
    /// (the current look) and profile B (the remake look), in one run with one staging: Lucy and the guard in the pool of
    /// the ceiling light for each of the three cameras (Cam_Recovery, Cam_Recovery_Bed, Cam_Recovery_Long), the other
    /// figures hidden. Shots at 1920 x 1080: remake_A_&lt;camera&gt;.png, remake_B_&lt;camera&gt;.png, each switch off alone
    /// (remake_B_off&lt;n&gt;_&lt;camera&gt;.png), all off (remake_B_alloff_&lt;camera&gt;.png), one 2560 x 1440 shot, and a mask
    /// of each figure for the contrast check (mask_&lt;A|B&gt;_&lt;lucy|guard&gt;_&lt;camera&gt;.png: the figure white on black).
    /// The frame time: the camera rendered 30 times into a target of the shot size, each frame waited for on the GPU (a
    /// one-pixel read), three interleaved rounds. Then a walk through the room under the zone cameras in A, then in B
    /// (recorded with tools/unity.sh record).
    /// Batch: tools/unity.sh play RemakeLook Main_Menu remake 1200   (record for the clips)
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class RemakePlaythrough : MonoBehaviour
    {
        public const int ShotWidth = 1920, ShotHeight = 1080;
        public static readonly string[] CameraNames = { "Cam_Recovery", "Cam_Recovery_Bed", "Cam_Recovery_Long" };

        [Serializable]
        private class Step { public string name; public bool ok; public string detail; public float t; }

        private readonly List<Step> steps = new List<Step>();
        private VirtualPad pad;
        private float startTime;
        private bool finished;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "remake", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested || FindFirstObjectByType<RemakePlaythrough>() != null) return;
            var go = new GameObject("[RemakePlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<RemakePlaythrough>();
        }

        private void Start()
        {
            startTime = Time.unscaledTime;
            StartCoroutine(Run());
            StartCoroutine(Watchdog());
        }

        private void Update() => pad?.Commit();

        private IEnumerator Watchdog()
        {
            float limit = BotArgs.TimeoutSeconds;
            while (!finished && Time.unscaledTime - startTime < limit) yield return null;
            if (!finished) { Record("timeout", false, $"{limit:0}s"); Finish(); }
        }

        private void Record(string name, bool ok, string detail = "")
        {
            steps.Add(new Step { name = name, ok = ok, detail = detail, t = Time.unscaledTime - startTime });
            string line = $"[RemakeBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        /// <summary>Where Lucy and the guard stand for each camera: in the pool of the one ceiling light (T22's rule).</summary>
        private static void Staging(string camera, out Vector3 lucy, out Vector3 guard)
        {
            switch (camera)
            {
                // The light (5.1, 5.4) sits between the camera and the figures, so their lit side faces the lens; the two
                // figures stand on separate lines of sight (the first frames had the guard behind Lucy).
                case "Cam_Recovery": lucy = new Vector3(4.45f, 0f, 5.0f); guard = new Vector3(5.75f, 0f, 4.05f); break;
                case "Cam_Recovery_Bed": lucy = new Vector3(4.6f, 0f, 6.4f); guard = new Vector3(5.6f, 0f, 5.5f); break;
                default: lucy = new Vector3(5.4f, 0f, 6.0f); guard = new Vector3(5.0f, 0f, 6.9f); break;
            }
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1f);
            string level = string.IsNullOrEmpty(BotArgs.Level) ? "RemakeLook" : BotArgs.Level;
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.LoadLevel(level); else SceneManager.LoadScene(level);
            float t0 = Time.realtimeSinceStartup;
            while (SceneManager.GetActiveScene().name != level && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSeconds(0.3f);

            RemakeDirector remake = RemakeDirector.Instance;
            FixedCameraDirector director = FixedCameraDirector.Instance;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            TankController tank = player != null ? player.GetComponent<TankController>() : null;
            GameObject guard = GameObject.Find("Figure_Guard");
            foreach (string hide in new[] { "Figure_LabWorker", "Figure_Patient" })
            {
                GameObject h = GameObject.Find(hide);
                if (h != null) h.SetActive(false);
            }
            Record("loaded", director != null && tank != null && guard != null && remake != null,
                $"scene={SceneManager.GetActiveScene().name} remake={remake != null} lightmaps={LightmapSettings.lightmaps.Length} swaps={(remake != null ? remake.bodySwaps.Length : 0)}");
            if (director == null || tank == null || guard == null || remake == null) { Finish(); yield break; }
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            string shots = Path.Combine(BotArgs.RunDir, "shots");

            var cameras = new List<FixedCamera>();
            foreach (string name in CameraNames)
            {
                FixedCamera fc = FindObjectsByType<FixedCamera>(FindObjectsSortMode.None).FirstOrDefault(c => c.cameraName == name);
                if (fc != null) cameras.Add(fc);
            }
            Record("cameras", cameras.Count == 3, string.Join(",", cameras.Select(c => $"{c.cameraName}:{c.fieldOfView:0}")));

            LightFlicker flicker = FindFirstObjectByType<LightFlicker>();
            bool first = true;
            foreach (FixedCamera fc in cameras)
            {
                Staging(fc.cameraName, out Vector3 lucyAt, out Vector3 guardAt);
                Vector3 camPos = fc.transform.position;
                tank.Teleport(lucyAt, Yaw(lucyAt, Vector3.Lerp(camPos, guardAt, 0.35f)));
                guard.transform.SetPositionAndRotation(guardAt, Quaternion.Euler(0f, Yaw(guardAt, Vector3.Lerp(camPos, lucyAt, 0.5f)), 0f));
                director.Override(fc);
                yield return new WaitForSeconds(first ? 2.5f : 0.8f);
                first = false;
                string cam = fc.cameraName.ToLowerInvariant();

                remake.SetProfileA(true);
                yield return null;
                yield return Shot(flicker, Path.Combine(shots, $"remake_A_{cam}.png"), ShotWidth, ShotHeight, $"A_{cam}");
                yield return Masks(shots, "A", cam, player, guard);
                remake.SetProfileA(false);
                yield return null;
                yield return Shot(flicker, Path.Combine(shots, $"remake_B_{cam}.png"), ShotWidth, ShotHeight, $"B_{cam}");
                yield return Masks(shots, "B", cam, player, guard);
                for (int s = 1; s <= RemakeDirector.SwitchCount + 1; s++)
                {
                    remake.Cycle(s);
                    string tag = s <= RemakeDirector.SwitchCount ? $"off{s}" : "alloff";
                    yield return Shot(flicker, Path.Combine(shots, $"remake_B_{tag}_{cam}.png"), ShotWidth, ShotHeight, $"B_{tag}_{cam} {remake.Describe()}");
                }
                remake.Cycle(0);
                yield return null;
            }

            // The 1440p sample and the frame times, on the bed camera.
            FixedCamera bed = cameras.FirstOrDefault(c => c.cameraName == "Cam_Recovery_Bed") ?? cameras[0];
            Staging(bed.cameraName, out Vector3 bl, out Vector3 bg);
            tank.Teleport(bl, Yaw(bl, Vector3.Lerp(bed.transform.position, bg, 0.35f)));
            guard.transform.SetPositionAndRotation(bg, Quaternion.Euler(0f, Yaw(bg, Vector3.Lerp(bed.transform.position, bl, 0.5f)), 0f));
            director.Override(bed);
            yield return new WaitForSeconds(0.8f);
            yield return Shot(flicker, Path.Combine(shots, "remake_B_1440p_cam_recovery_bed.png"), 2560, 1440, "B_1440p");
            Camera main = Camera.main;
            // Frame times. The desktop and the browser share the GPU, so every setting is measured in three rounds,
            // interleaved, and the report gives the lowest (the least interference) and the median.
            // -2 = profile A, 0 = B all on, 1..10 = that switch off, 11 = all off, -1 = B with the mid shadow maps.
            var settings = new List<int> { -2, 0 };
            for (int s = 1; s <= RemakeDirector.SwitchCount + 1; s++) settings.Add(s);
            settings.Add(-1);
            var times = new Dictionary<int, List<float>>();
            var times1440 = new Dictionary<int, List<float>> { { -2, new List<float>() }, { 0, new List<float>() } };
            FrameMs(main, 1920, 1080, 20); // warm-up
            for (int round = 0; round < 3; round++)
            {
                foreach (int setting in settings)
                {
                    remake.midShadows = setting == -1;
                    if (setting == -2) remake.SetProfileA(true); else remake.Cycle(Mathf.Max(0, setting));
                    yield return null;
                    yield return null;
                    FrameMs(main, 1920, 1080, 5); // settle after a change
                    if (!times.ContainsKey(setting)) times[setting] = new List<float>();
                    times[setting].Add(FrameMs(main, 1920, 1080, 30));
                    if (times1440.ContainsKey(setting)) times1440[setting].Add(FrameMs(main, 2560, 1440, 20));
                }
            }
            foreach (int setting in settings)
            {
                List<float> t = times[setting].OrderBy(x => x).ToList();
                string tag = setting == -2 ? "A" : setting == 0 ? "B" : setting == -1 ? "B_midshadows" : setting <= RemakeDirector.SwitchCount ? $"B_off{setting}" : "B_alloff";
                string extra = times1440.ContainsKey(setting) ? $"  1440p min {times1440[setting].Min():0.00} ms" : setting == -1 ? "  (all on, the light 2048 a face, 4096 atlas)" : "";
                Record($"frametime_{tag}", t[0] > 0f, $"1080p min {t[0]:0.00} ms median {t[1]:0.00} ms (rounds {string.Join("/", times[setting].Select(x => x.ToString("0.0")))}){extra}");
            }
            remake.midShadows = false;
            remake.Cycle(0);
            yield return null;

            // The walk, once in A and once in B: from the south-west corner toward the bed under the zone cameras (one cut at z 5.5).
            foreach (string profile in new[] { "A", "B" })
            {
                remake.SetProfileA(profile == "A");
                director.Override(null);
                Vector3 start = new Vector3(3.0f, 0f, 3.7f);
                tank.Teleport(start, 25f);
                Vector3 gAt = new Vector3(5.9f, 0f, 4.3f);
                guard.transform.SetPositionAndRotation(gAt, Quaternion.Euler(0f, Yaw(gAt, new Vector3(4.5f, 0f, 6f)), 0f));
                yield return new WaitForSeconds(0.6f);
                if (FrameRecorder.Enabled) FrameRecorder.Ensure().Begin($"remake_{profile}_walk");
                string firstCam = director.CurrentName;
                yield return new WaitForSeconds(0.8f);
                pad.SetHeld(InputManager.GameAction.Forward, true);
                yield return new WaitForSeconds(2.8f); // 4.2 s at 15 degrees walked her onto the cabinet's base
                pad.ReleaseAll();
                yield return new WaitForSeconds(1.4f);
                if (FrameRecorder.Instance != null) FrameRecorder.Instance.End();
                float walked = Vector3.Distance(start, player.transform.position);
                Record($"walk_{profile}", walked > 2f && firstCam != director.CurrentName, $"walked={walked:0.00} m cameras {firstCam} -> {director.CurrentName} end={player.transform.position}");
            }
            Record("air_pass", VolumetricLightFeature.Runs > 0 && VolumetricLightFeature.LastFound > 0, $"runs={VolumetricLightFeature.Runs} lights={VolumetricLightFeature.LastFound} marked={VolumetricLight.All.Count}");

            GameInput.ClearBotSource(pad);
            Finish();
        }

        private static Material maskMaterial;

        /// <summary>The contrast masks: each figure alone, unlit white on black, from the same camera and the same frame.</summary>
        private IEnumerator Masks(string folder, string profile, string cam, GameObject lucy, GameObject guard)
        {
            yield return null;
            Camera main = Camera.main;
            if (maskMaterial == null) maskMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };
            var data = main.GetUniversalAdditionalCameraData();
            int mask = main.cullingMask;
            CameraClearFlags clear = main.clearFlags;
            Color background = main.backgroundColor;
            bool post = data.renderPostProcessing;
            var aa = data.antialiasing;
            foreach ((string who, GameObject root) in new[] { ("lucy", lucy), ("guard", guard) })
            {
                var swapped = new List<(Renderer r, Material m)>();
                int layer = -1;
                foreach (SkinnedMeshRenderer r in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                {
                    if (!r.enabled) continue;
                    swapped.Add((r, r.sharedMaterial));
                    r.sharedMaterial = maskMaterial;
                    layer = r.gameObject.layer;
                }
                if (layer < 0) { Record($"mask_{profile}_{who}_{cam}", false, "no visible renderer"); continue; }
                main.cullingMask = 1 << layer;
                main.clearFlags = CameraClearFlags.SolidColor;
                main.backgroundColor = Color.black;
                data.renderPostProcessing = false;
                data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                data.SetRenderer(0);
                bool saved = ShotRenderer.Render(main, Path.Combine(folder, $"mask_{profile}_{who}_{cam}.png"), ShotWidth, ShotHeight, useTargetTexture: true);
                foreach ((Renderer r, Material m) in swapped) r.sharedMaterial = m;
                main.cullingMask = mask;
                main.clearFlags = clear;
                main.backgroundColor = background;
                data.renderPostProcessing = post;
                data.antialiasing = aa;
                Record($"mask_{profile}_{who}_{cam}", saved, $"layer={LayerMask.LayerToName(layer)} renderers={swapped.Count}");
            }
            RemakeDirector.Instance.ApplyAll(); // the renderer index of the profile
            yield return null;
        }

        private IEnumerator Shot(LightFlicker flicker, string file, int width, int height, string label)
        {
            yield return null;
            yield return null;
            float wait = 0f;
            while (flicker != null && flicker.Stuttering && wait < 8f) { wait += Time.deltaTime; yield return null; }
            bool saved = ShotRenderer.Render(Camera.main, file, width, height, useTargetTexture: true);
            Record("shot_" + Path.GetFileNameWithoutExtension(file), saved, label);
        }

        /// <summary>The cost of a frame: the camera rendered n times into a target of this size, each one waited for (a one-pixel read stalls until the GPU is done).</summary>
        private static float FrameMs(Camera cam, int width, int height, int n)
        {
            if (cam == null) return 0f;
            var urp = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset;
            int samples = cam.allowMSAA && urp != null ? Mathf.Max(1, urp.msaaSampleCount) : 1;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = samples };
            rt.Create();
            var px = new Texture2D(1, 1, TextureFormat.RGB24, false);
            RenderTexture prevTarget = cam.targetTexture, prevActive = RenderTexture.active;
            var sw = new Stopwatch();
            try
            {
                cam.targetTexture = rt;
                for (int i = 0; i < n + 3; i++)
                {
                    if (i == 3) sw.Start();
                    cam.Render();
                    RenderTexture.active = rt;
                    px.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                }
                sw.Stop();
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                Destroy(rt);
                Destroy(px);
            }
            return (float)sw.Elapsed.TotalMilliseconds / n;
        }

        private static float Yaw(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return d.sqrMagnitude < 1e-6f ? 0f : Quaternion.LookRotation(d).eulerAngles.y;
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (FrameRecorder.Instance != null) FrameRecorder.Instance.End();
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"remake\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
            for (int i = 0; i < steps.Count; i++)
            {
                Step s = steps[i];
                sb.Append("    {\"name\": \"").Append(Esc(s.name)).Append("\", \"ok\": ").Append(s.ok ? "true" : "false")
                  .Append(", \"t\": ").Append(s.t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(", \"detail\": \"").Append(Esc(s.detail)).Append("\"}").Append(i < steps.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            if (!string.IsNullOrEmpty(BotArgs.RunDir))
            {
                Directory.CreateDirectory(BotArgs.RunDir);
                File.WriteAllText(Path.Combine(BotArgs.RunDir, "playthrough.json"), sb.ToString());
            }
            Debug.Log($"[RemakeBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
