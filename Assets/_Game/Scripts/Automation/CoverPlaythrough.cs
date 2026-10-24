using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Destruction;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The cover bot (Bruno, 2026-10-24: breakable cover "for more kinetic scenarios"): loads the cover test room
    /// from the menu with a Lucy who takes no damage, and shoots the moments: the lit room, a lamp, the items on a
    /// table, a soldier behind a barricade, her blast stopped on the barricade, the barricade breaking and its
    /// pieces on the floor, a table and its items flying, a grenade bringing the plaster down to the bare wall, the
    /// short's surge putting the lamps out, and the room still broken when she comes back.
    /// Active only when BotArgs.Bot == "cover". Writes playthrough.json and shots/cover_&lt;n&gt;_&lt;moment&gt;.png.
    /// Batch: tools/unity.sh play CoverTest Main_Menu cover 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class CoverPlaythrough : MonoBehaviour
    {
        public const string SceneName = "CoverTest";
        public const int ShotWidth = 1920, ShotHeight = 1080;

        [Serializable]
        private class Step
        {
            public string name;
            public bool ok;
            public string detail;
            public float t;
        }

        private readonly List<Step> steps = new List<Step>();
        private float startTime;
        private bool finished;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "cover", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<CoverPlaythrough>() != null) return;
            var go = new GameObject("[CoverPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<CoverPlaythrough>();
        }

        private void Start()
        {
            startTime = Time.unscaledTime;
            StartCoroutine(Run());
            StartCoroutine(Watchdog());
        }

        private IEnumerator Watchdog()
        {
            float limit = BotArgs.TimeoutSeconds;
            while (!finished && Time.unscaledTime - startTime < limit) yield return null;
            if (!finished)
            {
                Record("timeout", false, $"bot did not finish in {limit:0}s");
                Finish();
            }
        }

        private void Record(string name, bool ok, string detail = "")
        {
            steps.Add(new Step { name = name, ok = ok, detail = detail, t = Time.unscaledTime - startTime });
            string line = $"[CoverBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private static Breakable Piece(string id) => Breakable.All.FirstOrDefault(b => b.Id == id);

        private IEnumerator Load()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.LoadLevel(SceneName); else SceneManager.LoadScene(SceneName);
            float t0 = BotClock.Now;
            while (SceneManager.GetActiveScene().name != SceneName && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(0.5f);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            yield return Load();
            string shots = Path.Combine(BotArgs.RunDir, "shots");
            Directory.CreateDirectory(shots);

            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            SoldierBrain[] soldiers = FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
            int barricades = Breakable.All.Count(b => b.IsCover), lamps = Breakable.All.OfType<BreakableLight>().Count();
            Record("cover_loaded", SceneManager.GetActiveScene().name == SceneName && lucy != null && soldiers.Length == 2 && barricades == 3 && lamps == 6,
                $"soldiers={soldiers.Length} breakables={Breakable.All.Count} barricades={barricades} lamps={lamps} items={Greeble.All.Count}");
            if (lucy == null || soldiers.Length == 0) { Finish(); yield break; }
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            stats.RegenPaused = true;
            stats.DebugSet(45f, 0f);
            Shot(Path.Combine(shots, "cover_01_room.png"));
            BreakableLight lamp = Breakable.All.OfType<BreakableLight>().First();
            CloseShot(lamp.Bounds.center, Vector3.back + Vector3.right * 0.3f, Path.Combine(shots, "cover_02_lamp.png"), 1.6f, -0.9f);
            Breakable table = Piece("Table_2");
            CloseShot(table.Bounds.center + Vector3.up * 0.2f, ClearSide(table.Bounds.center, 2.2f), Path.Combine(shots, "cover_03_table_items.png"), 2.2f, 0.9f);

            // A soldier takes cover behind a barricade.
            float t0 = BotClock.Now;
            SoldierBrain covered = null;
            while (BotClock.Now - t0 < 20f)
            {
                covered = soldiers.FirstOrDefault(s => s.Cover != null && Flat(s.transform.position - s.Spot).magnitude < 1.1f);
                if (covered != null) break;
                yield return null;
            }
            Record("soldier_in_cover", covered != null, covered != null ? $"{covered.name} behind {covered.Cover.Id}" : "nobody in cover in 20 s");
            if (covered == null) { Finish(); yield break; }
            yield return BotClock.Wait(0.5f);
            Breakable cover = covered.Cover;
            covered.Hold(999f); // he stays for the pictures
            Shot(Path.Combine(shots, "cover_04_soldier_in_cover.png"));
            Vector3 her = lucy.transform.position;
            Vector3 side = Flat(cover.Bounds.center - her).normalized;
            CloseShot(cover.Bounds.center + Vector3.up * 0.3f, Quaternion.Euler(0f, 70f, 0f) * -side, Path.Combine(shots, "cover_05_behind_the_barricade.png"), 3.4f, 0.8f);

            // Her blast stops on the barricade, then it breaks.
            LockOnTargetable target = covered.GetComponent<LockOnTargetable>();
            Health hp = covered.GetComponent<Health>();
            float before = hp.CurrentHealth;
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 1f, target, 1f);
            Breakable took = Breakable.All.FirstOrDefault(b => b.IsCover && b.Health < b.MaxHealth);
            Record("blast_on_cover", powers.LastResult == "cover" && hp.CurrentHealth == before,
                $"result={powers.LastResult} on {(took != null ? $"{took.Id} {took.Health:0}/{took.MaxHealth:0}" : "-")}, {covered.name} hp {hp.CurrentHealth:0}");
            int blasts = 1;
            while (!cover.IsBroken && blasts < 6)
            {
                yield return BotClock.Wait(0.4f);
                powers.FireAt(powers.Selected, 1f, target, 1f);
                blasts++;
            }
            yield return BotClock.Wait(0.12f);
            CloseShot(cover.Bounds.center + Vector3.up * 0.2f, Quaternion.Euler(0f, 70f, 0f) * -side, Path.Combine(shots, "cover_06_barricade_breaks.png"), 3.6f, 0.6f);
            float wait = BotClock.Now;
            while (cover.Pieces.Any(p => !p.Settled) && BotClock.Now - wait < 8f) yield return null;
            CloseShot(cover.transform.position + Vector3.up * 0.2f, Quaternion.Euler(0f, 70f, 0f) * -side, Path.Combine(shots, "cover_07_barricade_pieces.png"), 3.2f, 1.6f);
            powers.FireAt(powers.Selected, 1f, target, 1f);
            Record("barricade_breaks", cover.IsBroken && cover.Pieces.All(p => p.Settled) && hp.CurrentHealth < before,
                $"{cover.Id} broken after {blasts} blasts (every barricade on her line takes them), pieces settled={cover.Pieces.Count(p => p.Settled)}/{cover.Pieces.Count}, then hp {hp.CurrentHealth:0}");

            // A full blast with the MK boost beside a table: it breaks and the items fly.
            SoldierBrain other = soldiers.First(s => s != covered);
            table = Piece("Table_2");
            Vector3 beside = table.transform.position + new Vector3(0f, 0f, 1f);
            NavMeshAgent agent = other.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(beside); else other.transform.position = beside;
            other.Hold(999f);
            yield return BotClock.Wait(0.5f);
            stats.DebugSet(100f, 0f);
            Vector3 tableAt = table.Bounds.center;
            powers.FireAt(powers.Selected, 1f, other.GetComponent<LockOnTargetable>());
            yield return BotClock.Wait(0.15f);
            CloseShot(tableAt + Vector3.up * 0.3f, ClearSide(tableAt, 3.4f), Path.Combine(shots, "cover_08_table_breaks.png"), 3.4f, 1.0f);
            wait = BotClock.Now;
            while ((table.Pieces.Any(p => !p.Settled) || table.Resting.Any(g => g.Moving)) && BotClock.Now - wait < 8f) yield return null;
            CloseShot(table.transform.position + Vector3.up * 0.2f, ClearSide(table.transform.position + Vector3.up * 0.3f, 3f), Path.Combine(shots, "cover_09_table_pieces.png"), 3f, 1.8f);
            int moved = table.Resting.Count(g => g.Disturbs > 0);
            Record("table_breaks", table.IsBroken && moved == table.Resting.Count, $"{table.Id} broken={table.IsBroken}, items moved {moved}/{table.Resting.Count}");
            stats.DebugSet(45f, 0f);

            // A grenade at the east wall: the plaster comes down to the bare concrete.
            Breakable plaster = Piece("Plaster_E1");
            Vector3 floor = new Vector3(plaster.Bounds.center.x - 1f, 0f, plaster.Bounds.center.z);
            EnemyGrenade.Launch(floor + Vector3.up * 2f, floor, 0.3f, 0.2f, 30f, 3f, null, "bot");
            wait = BotClock.Now;
            while (!plaster.IsBroken && BotClock.Now - wait < 3f) yield return null;
            yield return BotClock.Wait(0.15f);
            CloseShot(plaster.Bounds.center, plaster.transform.forward + plaster.transform.right * 0.5f, Path.Combine(shots, "cover_10_plaster_falls.png"), 3.2f, 0f);
            wait = BotClock.Now;
            while (plaster.Pieces.Any(p => !p.Settled) && BotClock.Now - wait < 8f) yield return null;
            yield return BotClock.Wait(3f); // the grenade's smoke clears
            CloseShot(plaster.Bounds.center + Vector3.down * 0.6f, plaster.transform.forward + plaster.transform.right * 0.5f, Path.Combine(shots, "cover_11_bare_wall.png"), 3.2f, 0.2f);
            Record("plaster_falls", plaster.IsBroken && plaster.BrokenRoot.activeInHierarchy, $"{plaster.Id} broken={plaster.IsBroken}");

            // The short's surge: the lamps near her go out.
            ShortController shortController = lucy.GetComponent<ShortController>();
            shortController.Begin();
            yield return BotClock.Wait(0.08f);
            CloseShot(lamp.Bounds.center, Vector3.back + Vector3.right * 0.3f, Path.Combine(shots, "cover_12_lamp_sparks.png"), 1.6f, -0.9f);
            yield return BotClock.Wait(0.6f);
            int dark = Breakable.All.OfType<BreakableLight>().Count(l => l.IsDark);
            Shot(Path.Combine(shots, "cover_13_lamps_out.png"));
            Record("lamps_out", dark > 0 && Breakable.All.OfType<BreakableLight>().Where(l => l.IsDark).All(l => !l.Lamp.enabled && !l.Bounce.enabled), $"dark {dark}/{lamps}");
            shortController.End(true);

            // She leaves and comes back: the room is still broken.
            int broken = Breakable.All.Count(b => b.IsBroken);
            GameManager.Instance.LoadLevel("TrainingRoom");
            wait = BotClock.Now;
            while (SceneManager.GetActiveScene().name != "TrainingRoom" && BotClock.Now - wait < 20f) yield return null;
            yield return BotClock.Wait(1f);
            yield return Load();
            yield return BotClock.Wait(0.5f);
            int again = Breakable.All.Count(b => b.IsBroken);
            GameObject back = GameObject.FindGameObjectWithTag("Player");
            if (back != null) back.GetComponentInChildren<Health>().SetInvulnerable(true);
            Shot(Path.Combine(shots, "cover_14_she_comes_back.png"));
            Record("room_stays_broken", again == broken && broken >= 3, $"broken {broken} when she left, {again} when she came back");
            Finish();
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static Vector3 ClearSide(Vector3 point, float distance)
        {
            Vector3 preferred = Camera.main != null ? Camera.main.transform.position - point : Vector3.back;
            preferred.y = 0f;
            if (preferred.sqrMagnitude < 0.001f) preferred = Vector3.back;
            int mask = ~LayerMask.GetMask("BodyPart", "Debris");
            for (int i = 0; i < 16; i++)
            {
                Vector3 side = Quaternion.Euler(0f, (i % 2 == 0 ? 1 : -1) * (i + 1) / 2 * 22.5f, 0f) * preferred.normalized;
                Vector3 from = point + side * distance + Vector3.up * 0.5f;
                if (!Physics.Linecast(from, point, out RaycastHit hit, mask, QueryTriggerInteraction.Ignore) || Vector3.Distance(hit.point, point) < 0.45f) return side;
            }
            return preferred;
        }

        /// <summary>A 1080p shot of the room camera with the HUD drawn through it.</summary>
        private static void Shot(string path)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            PlayerHud hud = FindFirstObjectByType<PlayerHud>();
            Canvas canvas = hud != null ? hud.Canvas : null;
            RenderMode mode = RenderMode.ScreenSpaceOverlay;
            Camera worldCamera = null;
            float plane = 1f;
            if (canvas != null)
            {
                mode = canvas.renderMode;
                worldCamera = canvas.worldCamera;
                plane = canvas.planeDistance;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.05f;
                Canvas.ForceUpdateCanvases();
            }
            try
            {
                ShotRenderer.Render(cam, path, ShotWidth, ShotHeight, useTargetTexture: true);
            }
            finally
            {
                if (canvas != null)
                {
                    canvas.renderMode = mode;
                    canvas.worldCamera = worldCamera;
                    canvas.planeDistance = plane;
                }
            }
        }

        /// <summary>A close shot from a temporary camera: it stands back from the point and looks at it.</summary>
        private static void CloseShot(Vector3 point, Vector3 facing, string path, float distance = 1.4f, float height = 0.25f)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
            var go = new GameObject("[CloseShotCamera]");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            Camera main = Camera.main;
            if (main != null)
            {
                cam.clearFlags = main.clearFlags;
                cam.backgroundColor = main.backgroundColor;
            }
            go.transform.position = point + facing.normalized * distance + Vector3.up * height;
            go.transform.LookAt(point);
            try
            {
                ShotRenderer.Render(cam, path, ShotWidth, ShotHeight, useTargetTexture: true);
            }
            finally
            {
                Destroy(go);
            }
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"cover\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
            for (int i = 0; i < steps.Count; i++)
            {
                Step s = steps[i];
                sb.Append("    {\"name\": \"").Append(Esc(s.name)).Append("\", \"ok\": ").Append(s.ok ? "true" : "false")
                  .Append(", \"t\": ").Append(s.t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(", \"detail\": \"").Append(Esc(s.detail)).Append("\"}").Append(i < steps.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            string dir = BotArgs.RunDir;
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "playthrough.json"), sb.ToString());
            }
            Debug.Log($"[CoverBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("cover_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
