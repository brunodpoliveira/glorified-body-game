using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Rooms;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The team bot (2026-11-06): the world scene with a Lucy who takes no damage, and the moments Bruno asked for: the MK bars on
    /// the HUD, the soldiers firing at the place where she hid, a pair in the search, the second fireteam after the first one
    /// died, and the death screen of a short.
    /// Active only when BotArgs.Bot == "team". Writes playthrough.json and shots/team_&lt;n&gt;_&lt;moment&gt;.png.
    /// Batch: tools/unity.sh play Demo Main_Menu team 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class TeamPlaythrough : MonoBehaviour
    {
        private const string SceneName = "Demo";
        private const int ShotWidth = 1920, ShotHeight = 1080;
        private const string Released = "sector_c_released"; // DemoPublic.Released (editor code)
        private const string Reinforced = "demo_g5_reinforced"; // DemoPublic.Reinforced

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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "team", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<TeamPlaythrough>() != null) return;
            var go = new GameObject("[TeamPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<TeamPlaythrough>();
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
            string line = $"[TeamBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private static IEnumerator Until(Func<bool> condition, float seconds)
        {
            float t0 = BotClock.Now;
            while (!condition() && BotClock.Now - t0 < seconds) yield return null;
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.LoadLevel(SceneName); else SceneManager.LoadScene(SceneName);
            yield return Until(() => SceneManager.GetActiveScene().name == SceneName && CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 30f);
            CutscenePlayer.Skip();
            yield return Until(() => StoryFlags.Has("demo_w1_wake"), 10f);
            string shots = Path.Combine(BotArgs.RunDir, "shots");
            Directory.CreateDirectory(shots);

            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            Health health = lucy.GetComponentInChildren<Health>();
            health.SetInvulnerable(true);
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            DrugInventory drugs = lucy.GetComponent<DrugInventory>();
            TankController tank = lucy.GetComponent<TankController>();
            stats.DebugSet(100f, 30f);
            stats.DebugSetMk(PowerKind.Fire, 35f);
            stats.DebugSetMk(PowerKind.Telekinesis, 15f);
            drugs.DebugSet(DrugType.Nalcon, 1);
            drugs.DebugSet(DrugType.NalconFire, 2);
            drugs.DebugSet(DrugType.Red, 1);
            tank.Teleport(Room.Of("G2").transform.position + new Vector3(16f, 0f, 1f), 90f);
            yield return Until(() => RoomTracker.CurrentId == "G2", 3f);
            SoldierBrain[] four = FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Where(b => b.name.StartsWith("Soldier_Pair")).ToArray();
            yield return Until(() => four.All(b => b.Team != null), 10f);
            Record("team_alerted", four.Length == 4 && four.All(b => b.Team != null), $"soldiers={four.Length}");
            if (four.Length != 4 || four[0].Team == null) { Finish(); yield break; }
            FireTeam fire = four[0].Team;
            Canvas hud = GameObject.Find("PlayerHudCanvas")?.GetComponent<Canvas>();
            yield return BotClock.Wait(0.5f);
            if (hud != null) yield return OverlayShot.Capture(hud, "team_01_hud_bars.png", 0.1f);
            Record("hud_bars", hud != null, $"mk {stats.Mk(PowerKind.Blast):0} {stats.Mk(PowerKind.Fire):0} {stats.Mk(PowerKind.Telekinesis):0}");

            yield return BotClock.Wait(2.5f);
            SoldierBrain[] team = fire.Members.ToArray();
            tank.Teleport(Room.Of("W6").Centre + new Vector3(0.5f, 0f, -1.5f), 0f); // the other floor: nobody finds her
            yield return Until(() => fire.HerHidden && team.Any(b => b.Gun.Suppressing && b.Gun.Current == DummyShooter.State.Telegraph), 8f);
            SoldierBrain firing = team.FirstOrDefault(b => b.Gun.Suppressing);
            Record("suppression", firing != null, firing != null ? $"{firing.name} aims at {firing.Gun.AimPoint}, last seen {fire.LastSeen}" : "no gun aimed at the place");
            if (firing != null)
            {
                Draw("G2");
                Vector3 from = firing.transform.position, to = firing.Gun.AimPoint;
                Vector3 middle = (from + to) * 0.5f + Vector3.up * 0.4f;
                CloseShot(middle, Vector3.Cross(Vector3.up, to - from), Path.Combine(shots, "team_02_suppression.png"), Mathf.Max(5f, Vector3.Distance(from, to) * 0.8f), 1.2f);
            }

            yield return Until(() => fire.Lost && team.Count(b => b.Current == SoldierBrain.Phase.Search) >= 2, FireTeam.LostAfter + 6f);
            yield return BotClock.Wait(3f);
            SoldierBrain walker = team.FirstOrDefault(b => b.Current == SoldierBrain.Phase.Search && b.Body.IsMoving && fire.PartnerOf(b) != null);
            Record("search", walker != null, $"route {fire.Route.Count} points, {team.Count(b => b.Current == SoldierBrain.Phase.Search)} of {team.Length} search");
            if (walker != null)
            {
                SoldierBrain partner = fire.PartnerOf(walker);
                Draw("G2");
                Draw("G5");
                Vector3 both = (walker.transform.position + partner.transform.position) * 0.5f + Vector3.up;
                CloseShot(both, Vector3.Cross(Vector3.up, walker.transform.position - partner.transform.position), Path.Combine(shots, "team_03_search_pair.png"), Mathf.Max(5f, Vector3.Distance(walker.transform.position, partner.transform.position) * 1.2f), 1.5f);
                Record("pair_one_walks", !partner.Body.IsMoving || !walker.Body.IsMoving, $"{walker.name} walks, {partner.name} {(partner.Body.IsMoving ? "walks too" : "covers")}");
            }

            // The first team dies, sector C is released: the second fireteam comes, after the silence.
            foreach (SoldierBrain b in four) b.GetComponent<Health>().TakeDamage(9999f);
            tank.Teleport(Room.Of("G9").Centre + Vector3.right * 2f, 270f);
            StoryFlags.Raise(Released);
            yield return Until(() => StoryFlags.Has(Reinforced), TeamReinforcement.SilenceSeconds + 6f);
            yield return BotClock.Wait(1f);
            SoldierBrain[] second = FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Where(b => b.name.StartsWith("Reinforcement_")).ToArray();
            Record("second_team", second.Length == 4 && second.All(b => b.Alerted), $"{second.Length} soldiers");
            if (second.Length > 0)
            {
                Draw("G2");
                CloseShot(second[0].transform.position + Vector3.up, Vector3.back + Vector3.right * 0.4f, Path.Combine(shots, "team_04_second_team.png"), 5f, 1.5f);
            }

            // The short kills her: the death screen has its own row.
            foreach (SoldierBrain b in second) b.GetComponent<Health>().TakeDamage(9999f);
            foreach (SoldierBrain b in FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None)) b.gameObject.SetActive(false); // no bullet takes the credit
            health.SetInvulnerable(false);
            health.SetRuntimeInvulnerable(false);
            health.SetSecondWindEnabled(false);
            health.SetHealth(4f);
            stats.DebugSet(45f, PsychicStats.MaxShort);
            lucy.GetComponent<ShortController>().Begin();
            yield return Until(() => gm != null && gm.IsGameOver, ShortController.StartFreeze + 10f);
            GameObject deathCanvas = null;
            yield return Until(() => (deathCanvas = GameObject.Find("DeathMenuUICanvas")) != null && deathCanvas.activeSelf, 4f);
            DeathMenuUI screen = FindAnyObjectByType<DeathMenuUI>();
            bool shown = deathCanvas != null && deathCanvas.activeSelf && screen != null && screen.Shown != null && screen.Shown.killerId == ShortController.KillerId;
            Record("short_death_screen", shown, screen != null && screen.Shown != null ? $"{screen.Shown.killerId}: {screen.Shown.voiceText} / {screen.Shown.hint}" : "no screen");
            if (deathCanvas != null) yield return OverlayShot.Capture(deathCanvas.GetComponent<Canvas>(), "team_05_short_death_ui.png", 0.5f);
            Finish();
        }

        /// <summary>A shot of a room she is not in: the far rooms are culled (RoomTracker).</summary>
        private static void Draw(string roomId)
        {
            Room room = Room.Of(roomId);
            if (room == null) return;
            foreach (Renderer r in room.GetComponentsInChildren<Renderer>()) r.enabled = true;
        }

        /// <summary>A close shot from a temporary camera: it stands back from the point and looks at it.</summary>
        private static void CloseShot(Vector3 point, Vector3 facing, string path, float distance, float height)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
            var go = new GameObject("[CloseShotCamera]");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.05f;
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
            sb.Append("{\n  \"bot\": \"team\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[TeamBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("team_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
