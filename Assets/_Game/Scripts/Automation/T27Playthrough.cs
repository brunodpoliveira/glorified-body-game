using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.Player;
using GlorifiedBody.Rendering;
using GlorifiedBody.Systems;
using Debug = UnityEngine.Debug;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The T27 bot (bot "t27", 2026-11-02): the new baked game characters (Resources/T27: the guard, Lucy with the new helmet)
    /// beside the old ones in a T26 room under B-game (2048 maps; the air on in RemakeLook, off in RemakeLookBright, as T26).
    /// For each of the three cameras (Cam_Recovery, Cam_Recovery_Bed, Cam_Recovery_Long), at T26's staging spots in the
    /// light: the old pair alone (t27_old_&lt;cam&gt;.png), the new pair alone on the same spots (t27_new_&lt;cam&gt;.png), and all
    /// four (t27_all_&lt;cam&gt;.png: each new figure 0.6 m to the right of its old one, seen from the camera). Then the frame
    /// time of the first camera with the new pair and with the old pair (30 renders each, three rounds).
    /// The scene is not saved: the T26 rooms stay as they are.
    /// Batch: tools/unity.sh play RemakeLookBright Main_Menu t27 600   and   tools/unity.sh play RemakeLook Main_Menu t27 600
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class T27Playthrough : MonoBehaviour
    {
        public const int ShotWidth = 1920, ShotHeight = 1080, Tier = 2048;
        private static readonly string[] CameraNames = { "Cam_Recovery", "Cam_Recovery_Bed", "Cam_Recovery_Long" };

        [Serializable] private class Step { public string name; public bool ok; public string detail; public float t; }
        private readonly List<Step> steps = new List<Step>();
        private float startTime;
        private bool finished;
        private VirtualPad pad;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "t27", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested || FindFirstObjectByType<T27Playthrough>() != null) return;
            var go = new GameObject("[T27Playthrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<T27Playthrough>();
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
            string line = $"[T27Bot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        /// <summary>T26's spots (RemakePlaythrough.Staging): Lucy and the guard in the pool of the room's light.</summary>
        private static void Staging(string camera, out Vector3 lucy, out Vector3 guard)
        {
            switch (camera)
            {
                case "Cam_Recovery": lucy = new Vector3(4.45f, 0f, 5.0f); guard = new Vector3(5.75f, 0f, 4.05f); break;
                case "Cam_Recovery_Bed": lucy = new Vector3(4.6f, 0f, 6.4f); guard = new Vector3(5.6f, 0f, 5.5f); break;
                default: lucy = new Vector3(5.4f, 0f, 6.0f); guard = new Vector3(5.0f, 0f, 6.9f); break;
            }
        }

        private static readonly Vector3 Away = new Vector3(0f, -50f, 0f);

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
            var newGuardPrefab = Resources.Load<GameObject>("T27/t27_guard");
            var newLucyPrefab = Resources.Load<GameObject>("T27/t27_lucy");
            Record("loaded", director != null && tank != null && guard != null && remake != null && newGuardPrefab != null && newLucyPrefab != null,
                $"scene={SceneManager.GetActiveScene().name} remake={remake != null} prefabs={newGuardPrefab != null},{newLucyPrefab != null}");
            if (director == null || tank == null || guard == null || remake == null || newGuardPrefab == null || newLucyPrefab == null) { Finish(); yield break; }
            bool roomAir = level != "RemakeLookBright";
            remake.SetProfileGame(roomAir, Tier);
            Record("profile", true, remake.Describe());
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);

            GameObject newGuard = Instantiate(newGuardPrefab);
            GameObject newLucy = Instantiate(newLucyPrefab);
            newGuard.name = "T27_Guard"; newLucy.name = "T27_Lucy";
            // the same layers as the old figures (the stencil mark and the light layers of the room)
            SetLayer(newGuard, LayerOf(guard));
            SetLayer(newLucy, LayerOf(player));
            int tris(GameObject g) => g.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(r => r.sharedMesh != null ? r.sharedMesh.triangles.Length / 3 : 0)
                                    + g.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh != null ? f.sharedMesh.triangles.Length / 3 : 0);
            Transform helmet = newLucy.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Helmet_T27");
            Record("figures", true, $"old guard {tris(guard)} tris, new guard {tris(newGuard)}, old Lucy {tris(player)}, new Lucy {tris(newLucy)}");
            yield return null;
            Record("helmet", HelmetCheck(newLucy, out string helmetDetail), helmetDetail);

            string shots = Path.Combine(BotArgs.RunDir, "shots");
            Directory.CreateDirectory(shots);
            var cameras = CameraNames.Select(n => FindObjectsByType<FixedCamera>(FindObjectsSortMode.None).FirstOrDefault(c => c.cameraName == n)).Where(c => c != null).ToList();
            Record("cameras", cameras.Count == 3, string.Join(",", cameras.Select(c => c.cameraName)));
            LightFlicker flicker = FindFirstObjectByType<LightFlicker>();
            bool first = true;
            foreach (FixedCamera fc in cameras)
            {
                Staging(fc.cameraName, out Vector3 lucyAt, out Vector3 guardAt);
                Vector3 camPos = fc.transform.position;
                Vector3 right = Vector3.Cross(Vector3.up, (Vector3.Lerp(lucyAt, guardAt, 0.5f) - camPos).WithY0()).normalized;
                float lucyYaw = Yaw(lucyAt, Vector3.Lerp(camPos, guardAt, 0.35f));
                float guardYaw = Yaw(guardAt, Vector3.Lerp(camPos, lucyAt, 0.5f));
                director.Override(fc);
                string cam = fc.cameraName.ToLowerInvariant();
                for (int pass = 0; pass < 3; pass++)
                {
                    // 0: old pair, 1: new pair on the same spots, 2: all four (the new one 0.6 m to the right of the old)
                    Vector3 oldL = pass == 1 ? Away : (pass == 2 ? lucyAt - right * 0.3f : lucyAt);
                    Vector3 oldG = pass == 1 ? Away + Vector3.right * 3f : (pass == 2 ? guardAt - right * 0.3f : guardAt);
                    Vector3 newL = pass == 0 ? Away + Vector3.right * 6f : (pass == 2 ? lucyAt + right * 0.3f : lucyAt);
                    Vector3 newG = pass == 0 ? Away + Vector3.right * 9f : (pass == 2 ? guardAt + right * 0.3f : guardAt);
                    tank.Teleport(oldL, lucyYaw);
                    guard.transform.SetPositionAndRotation(oldG, Quaternion.Euler(0f, guardYaw, 0f));
                    newLucy.transform.SetPositionAndRotation(newL, Quaternion.Euler(0f, lucyYaw, 0f));
                    newGuard.transform.SetPositionAndRotation(newG, Quaternion.Euler(0f, guardYaw, 0f));
                    yield return new WaitForSeconds(first ? 2.5f : 0.8f);
                    first = false;
                    remake.SetProfileGame(roomAir, Tier);
                    yield return null;
                    yield return Shot(flicker, Path.Combine(shots, $"t27_{(pass == 0 ? "old" : pass == 1 ? "new" : "all")}_{cam}.png"), $"{cam} {remake.Describe()}");
                }
            }
            // the frame time of the first camera: the new pair against the old pair, on the same spots
            if (cameras.Count > 0)
            {
                FixedCamera fc = cameras[0];
                Staging(fc.cameraName, out Vector3 lucyAt, out Vector3 guardAt);
                director.Override(fc);
                var ms = new Dictionary<string, List<float>> { { "old", new List<float>() }, { "new", new List<float>() } };
                for (int round = 0; round < 3; round++)
                {
                    foreach (string who in new[] { "old", "new" })
                    {
                        bool isNew = who == "new";
                        tank.Teleport(isNew ? Away : lucyAt, 0f);
                        guard.transform.position = isNew ? Away + Vector3.right * 3f : guardAt;
                        newLucy.transform.position = isNew ? lucyAt : Away + Vector3.right * 6f;
                        newGuard.transform.position = isNew ? guardAt : Away + Vector3.right * 9f;
                        yield return new WaitForSeconds(0.5f);
                        ms[who].Add(FrameMs(Camera.main, ShotWidth, ShotHeight, 30));
                    }
                }
                Record("frame_ms", true, $"{fc.cameraName} 1080p: old pair {ms["old"].Min():0.00} ms, new pair {ms["new"].Min():0.00} ms (lowest of 3 rounds; rounds old {string.Join("/", ms["old"].Select(x => x.ToString("0.0")))}, new {string.Join("/", ms["new"].Select(x => x.ToString("0.0")))})");
            }
            Finish();
        }

        /// <summary>The helmet on the new Lucy: a skinned mesh whose every vertex follows mixamorig:Head only, its front
        /// (the shark mouth) on her front, and its material with the 2048 maps (colour, metal-smoothness, normal).</summary>
        private static bool HelmetCheck(GameObject lucy, out string detail)
        {
            var hr = lucy.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name.StartsWith("Helmet_T27"));
            if (hr == null || hr.sharedMesh == null) { detail = "no helmet renderer"; return false; }
            Mesh mesh = hr.sharedMesh;
            int headIndex = Array.FindIndex(hr.bones, b => b != null && b.name == "mixamorig:Head");
            var weights = mesh.boneWeights;
            int other = weights.Count(w => (w.weight0 > 0.001f && w.boneIndex0 != headIndex) || w.weight1 > 0.001f);
            // its front: the mean of the helmet's 5 % most forward vertices (along her forward) against the head bone
            var baked = new Mesh(); hr.BakeMesh(baked, true);
            Vector3[] v = baked.vertices; Transform t = hr.transform;
            Transform model = lucy.transform;
            Vector3 head = hr.bones[headIndex].position;
            var world = v.Select(p => t.TransformPoint(p)).ToArray();
            float[] f = world.Select(p => Vector3.Dot(p - head, model.forward)).OrderByDescending(x => x).ToArray();
            float front = f.Take(Mathf.Max(1, f.Length / 20)).Average(), back = f.Reverse().Take(Mathf.Max(1, f.Length / 20)).Average();
            Vector3 c = world.Aggregate(Vector3.zero, (a, b) => a + b) / world.Length;
            Material m = hr.sharedMaterial;
            Texture bm = m != null ? m.GetTexture("_BaseMap") : null, nm = m != null ? m.GetTexture("_BumpMap") : null, ms = m != null ? m.GetTexture("_MetallicGlossMap") : null;
            bool maps = bm != null && nm != null && ms != null && bm.width == 2048 && nm.width == 2048 && ms.width == 2048 && m.IsKeywordEnabled("_NORMALMAP");
            bool ok = headIndex >= 0 && other == 0 && front > 0.05f && front > -back && maps;
            detail = $"skinned to {(headIndex >= 0 ? "mixamorig:Head" : "NO HEAD")} ({weights.Length} vertices, {other} on another bone); front {front:0.000} m ahead of the head, back {-back:0.000} m behind; centre {(c - head).ToString("0.000")} from the head; " +
                     $"maps {bm?.name} {bm?.width}, {nm?.name} {nm?.width} ({(nm is Texture2D n2 ? n2.format.ToString() : "-")}), {ms?.name} {ms?.width}, _NORMALMAP {(m != null && m.IsKeywordEnabled("_NORMALMAP"))}";
            Destroy(baked);
            return ok;
        }

        private static int LayerOf(GameObject g)
        {
            Renderer r = g.GetComponentsInChildren<Renderer>().FirstOrDefault(x => x.enabled);
            return r != null ? r.gameObject.layer : g.layer;
        }

        private static void SetLayer(GameObject g, int layer)
        {
            foreach (Transform t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        private IEnumerator Shot(LightFlicker flicker, string file, string label)
        {
            yield return null;
            yield return null;
            float wait = 0f;
            while (flicker != null && flicker.Stuttering && wait < 8f) { wait += Time.deltaTime; yield return null; }
            bool saved = ShotRenderer.Render(Camera.main, file, ShotWidth, ShotHeight, useTargetTexture: true);
            Record("shot_" + Path.GetFileNameWithoutExtension(file), saved, label);
        }

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
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"t27\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[T27Bot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }

    internal static class T27VectorExt
    {
        public static Vector3 WithY0(this Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
