using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
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
    /// The T38 bots (the Arkham Knight look), in ArkhamLook (dark) or ArkhamLookBright, with the T26 staging: Lucy and the
    /// guard in the light for each of the three cameras.
    /// - "arkham": per camera, B-game (the control), AK all on, AK with each switch off (the ablation), AK with the LUT out
    ///   (the input of tools/ak_lut.py), and a mask of each figure for B-game and AK (the contrast). Then the walk in
    ///   B-game and in AK (record mode makes the clips). Shots: ak_BG_&lt;cam&gt;, ak_AK_&lt;cam&gt;, ak_AKoff&lt;n&gt;_&lt;cam&gt;,
    ///   ak_AKprelut_&lt;cam&gt;, mask_&lt;BG|AK&gt;_&lt;who&gt;_&lt;cam&gt;.
    /// - "arkham_time": the frame times only, first thing after the load (T26: shots before the measure pushed the 2 GB
    ///   card into VRAM thrash): B-game, AK all on, B-game with each switch alone, three interleaved rounds, the bed camera,
    ///   1920 x 1080, each frame waited for on the GPU, the planar mirror rendered before each frame. The graphics driver
    ///   memory after each setting.
    /// Batch: tools/unity.sh record ArkhamLook Main_Menu arkham 1500; tools/unity.sh play ArkhamLook Main_Menu arkham_time 600
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class ArkhamPlaythrough : MonoBehaviour
    {
        public const int ShotWidth = 1920, ShotHeight = 1080;
        public static readonly string[] CameraNames = { "Cam_Recovery", "Cam_Recovery_Bed", "Cam_Recovery_Long" };

        [Serializable]
        private class Step { public string name; public bool ok; public string detail; public float t; }

        private readonly List<Step> steps = new List<Step>();
        private VirtualPad pad;
        private float startTime;
        private bool finished;

        public static bool IsShots => string.Equals(BotArgs.Bot, "arkham", StringComparison.OrdinalIgnoreCase);
        public static bool IsTime => string.Equals(BotArgs.Bot, "arkham_time", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!(IsShots || IsTime) || FindFirstObjectByType<ArkhamPlaythrough>() != null) return;
            var go = new GameObject("[ArkhamPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<ArkhamPlaythrough>();
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
            string line = $"[ArkhamBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        /// <summary>The T26 staging (RemakePlaythrough): both figures in the light, on separate lines of sight.</summary>
        private static void Staging(string camera, out Vector3 lucy, out Vector3 guard)
        {
            switch (camera)
            {
                case "Cam_Recovery": lucy = new Vector3(4.45f, 0f, 5.0f); guard = new Vector3(5.75f, 0f, 4.05f); break;
                case "Cam_Recovery_Bed": lucy = new Vector3(4.6f, 0f, 6.4f); guard = new Vector3(5.6f, 0f, 5.5f); break;
                default: lucy = new Vector3(5.4f, 0f, 6.0f); guard = new Vector3(5.0f, 0f, 6.9f); break;
            }
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1f);
            string level = string.IsNullOrEmpty(BotArgs.Level) ? "ArkhamLook" : BotArgs.Level;
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.LoadLevel(level); else SceneManager.LoadScene(level);
            float t0 = Time.realtimeSinceStartup;
            while (SceneManager.GetActiveScene().name != level && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSeconds(0.3f);

            ArkhamDirector ak = ArkhamDirector.Instance;
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
            bool ok = director != null && tank != null && guard != null && remake != null && ak != null;
            Record("loaded", ok, $"scene={SceneManager.GetActiveScene().name} ak={ak != null} remake={remake != null} lightmaps={LightmapSettings.lightmaps.Length} wet={(ak != null ? ak.wetRenderers.Length : 0)} flares={(ak != null ? ak.flares.Length : 0)} leak={(ak != null ? ak.leakObjects.Length : 0)}");
            if (!ok) { Finish(); yield break; }
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            var cameras = CameraNames.Select(n => FindObjectsByType<FixedCamera>(FindObjectsSortMode.None).FirstOrDefault(c => c.cameraName == n)).Where(c => c != null).ToList();
            Record("cameras", cameras.Count == 3, string.Join(",", cameras.Select(c => $"{c.cameraName}:{c.fieldOfView:0}")));
            LightFlicker flicker = FindFirstObjectByType<LightFlicker>();

            if (IsTime)
            {
                yield return Times(ak, cameras, tank, guard, director);
                GameInput.ClearBotSource(pad);
                Finish();
                yield break;
            }

            string shots = Path.Combine(BotArgs.RunDir, "shots");
            ColorLookup lut = null;
            if (ak.gradeVolume != null) ak.gradeVolume.profile.TryGet(out lut); // .profile: a run-time copy, the asset stays
            bool first = true;
            foreach (FixedCamera fc in cameras)
            {
                Stage(fc, tank, guard, director);
                yield return new WaitForSeconds(first ? 2.5f : 0.8f);
                first = false;
                string cam = fc.cameraName.ToLowerInvariant();

                ak.SetProfileGame();
                yield return Shot(ak, flicker, Path.Combine(shots, $"ak_BG_{cam}.png"), $"BG_{cam} {ak.Describe()}");
                yield return Masks(shots, "BG", cam, player, guard);
                ak.SetProfileAK();
                yield return Shot(ak, flicker, Path.Combine(shots, $"ak_AK_{cam}.png"), $"AK_{cam} {ak.Describe()}");
                yield return Masks(shots, "AK", cam, player, guard);
                for (int s = 1; s <= ArkhamDirector.SwitchCount; s++)
                {
                    ak.Cycle(s);
                    yield return Shot(ak, flicker, Path.Combine(shots, $"ak_AKoff{s}_{cam}.png"), $"AKoff{s}_{cam} {ak.Describe()}");
                }
                ak.SetProfileAK();
                if (lut != null)
                {
                    float c = lut.contribution.value;
                    lut.contribution.value = 0f;
                    yield return Shot(ak, flicker, Path.Combine(shots, $"ak_AKprelut_{cam}.png"), $"AKprelut_{cam} lut {lut.texture.value?.name}");
                    lut.contribution.value = c;
                }
            }

            // The planar mirror and the rims ran (the counts), and the switch states the director reports.
            Record("planar_pass", ak.planar != null && ak.planar.Renders > 0, $"mirror renders={(ak.planar != null ? ak.planar.Renders : 0)}");

            // The walk: B-game, then AK, from the south-west corner toward the bed under the zone cameras (T26's walk).
            foreach (string profile in new[] { "BG", "AK" })
            {
                if (profile == "BG") ak.SetProfileGame(); else ak.SetProfileAK();
                director.Override(null);
                Vector3 start = new Vector3(3.0f, 0f, 3.7f);
                tank.Teleport(start, 25f);
                Vector3 gAt = new Vector3(5.9f, 0f, 4.3f);
                guard.transform.SetPositionAndRotation(gAt, Quaternion.Euler(0f, Yaw(gAt, new Vector3(4.5f, 0f, 6f)), 0f));
                yield return new WaitForSeconds(0.6f);
                if (FrameRecorder.Enabled) FrameRecorder.Ensure().Begin($"ak_{profile}_walk");
                string firstCam = director.CurrentName;
                yield return new WaitForSeconds(0.8f);
                pad.SetHeld(InputManager.GameAction.Forward, true);
                yield return new WaitForSeconds(2.8f);
                pad.ReleaseAll();
                yield return new WaitForSeconds(1.4f);
                if (FrameRecorder.Instance != null) FrameRecorder.Instance.End();
                float walked = Vector3.Distance(start, player.transform.position);
                Record($"walk_{profile}", walked > 2f && firstCam != director.CurrentName, $"walked={walked:0.00} m cameras {firstCam} -> {director.CurrentName}");
            }
            Record("air_pass", !ak.roomAir || (VolumetricLightFeature.Runs > 0 && VolumetricLightFeature.LastFound > 0), $"room air={ak.roomAir} runs={VolumetricLightFeature.Runs} lights={VolumetricLightFeature.LastFound} marked={VolumetricLight.All.Count}");
            GameInput.ClearBotSource(pad);
            Finish();
        }

        private void Stage(FixedCamera fc, TankController tank, GameObject guard, FixedCameraDirector director)
        {
            Staging(fc.cameraName, out Vector3 lucyAt, out Vector3 guardAt);
            Vector3 camPos = fc.transform.position;
            tank.Teleport(lucyAt, Yaw(lucyAt, Vector3.Lerp(camPos, guardAt, 0.35f)));
            guard.transform.SetPositionAndRotation(guardAt, Quaternion.Euler(0f, Yaw(guardAt, Vector3.Lerp(camPos, lucyAt, 0.5f)), 0f));
            director.Override(fc);
        }

        /// <summary>The frame times: -1 B-game, 0 AK all on, 1..8 B-game with that switch alone.</summary>
        private IEnumerator Times(ArkhamDirector ak, List<FixedCamera> cameras, TankController tank, GameObject guard, FixedCameraDirector director)
        {
            FixedCamera bed = cameras.FirstOrDefault(c => c.cameraName == "Cam_Recovery_Bed") ?? cameras[0];
            Stage(bed, tank, guard, director);
            yield return new WaitForSeconds(2.5f);
            Camera main = Camera.main;
            var settings = new List<int> { -1, 0 };
            for (int s = 1; s <= ArkhamDirector.SwitchCount; s++) settings.Add(s);
            var times = settings.ToDictionary(s => s, s => new List<float>());
            var vram = settings.ToDictionary(s => s, s => 0L);
            ak.SetProfileGame();
            yield return null;
            FrameMs(ak, main, 20);
            for (int round = 0; round < 3; round++)
            {
                foreach (int setting in settings)
                {
                    Apply(ak, setting);
                    yield return null;
                    yield return null;
                    FrameMs(ak, main, 5);
                    times[setting].Add(FrameMs(ak, main, 30));
                    vram[setting] = Math.Max(vram[setting], Profiler.GetAllocatedMemoryForGraphicsDriver());
                }
            }
            foreach (int setting in settings)
            {
                List<float> t = times[setting].OrderBy(x => x).ToList();
                string tag = setting == -1 ? "BG" : setting == 0 ? "AK" : $"alone{setting}_{ArkhamDirector.Names[setting - 1].Replace(' ', '_')}";
                Record($"frametime_{tag}", t[0] > 0f, $"1080p min {t[0]:0.00} ms median {t[1]:0.00} ms (rounds {string.Join("/", times[setting].Select(x => x.ToString("0.0")))}) driver memory {vram[setting] / (1024f * 1024f):0} MB");
            }
            ak.SetProfileAK();
        }

        private static void Apply(ArkhamDirector ak, int setting)
        {
            if (setting == -1) ak.SetProfileGame();
            else if (setting == 0) ak.SetProfileAK();
            else ak.SetAlone((ArkhamSwitch)(setting - 1));
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
                data.antialiasing = AntialiasingMode.None;
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
            ArkhamDirector.Instance.ApplyAll();
            yield return null;
        }

        private IEnumerator Shot(ArkhamDirector ak, LightFlicker flicker, string file, string label)
        {
            yield return null;
            yield return null;
            float wait = 0f;
            while (flicker != null && flicker.Stuttering && wait < 8f) { wait += Time.deltaTime; yield return null; }
            ak.PrepareRender(Camera.main, ShotWidth, ShotHeight);
            bool saved = ShotRenderer.Render(Camera.main, file, ShotWidth, ShotHeight, useTargetTexture: true);
            Record("shot_" + Path.GetFileNameWithoutExtension(file), saved, label);
        }

        /// <summary>The cost of a frame (T26's method): the camera rendered n times into a 1080p target, each frame waited for
        /// on the GPU (a one-pixel read), with the planar mirror rendered first when the switch is on.</summary>
        private static float FrameMs(ArkhamDirector ak, Camera cam, int n)
        {
            if (cam == null) return 0f;
            var urp = UniversalRenderPipeline.asset;
            int samples = cam.allowMSAA && urp != null ? Mathf.Max(1, urp.msaaSampleCount) : 1;
            var rt = new RenderTexture(ShotWidth, ShotHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = samples };
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
                    ak.PrepareRender(cam, ShotWidth, ShotHeight);
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
            sb.Append("{\n  \"bot\": \"").Append(BotArgs.Bot).Append("\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[ArkhamBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
