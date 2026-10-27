using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The reactions bot (Bruno, 2026-10-27: option D and "every death plays out physically"): in the AI test room,
    /// with the guns off, it shows each answer to a hit and each kind of death, close up. Lucy's own powers make the
    /// blasts and the fire. The bullets and the shock come from the hit bus (a bullet or a shock at that bone). Shots:
    /// shots/react_&lt;n&gt;_&lt;moment&gt;.png. In a record run (tools/unity.sh record AiTest Main_Menu reactions 400)
    /// each moment is a clip. Active only when BotArgs.Bot == "reactions".
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class ReactionsPlaythrough : MonoBehaviour
    {
        public const string SceneName = "AiTest";
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
        private string shots;
        private int shotIndex;
        private GameObject lucy;
        private PsychicPowers powers;
        private static Camera closeCamera;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "reactions", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<ReactionsPlaythrough>() != null) return;
            var go = new GameObject("[ReactionsPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<ReactionsPlaythrough>();
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
            string line = $"[ReactionsBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.LoadLevel(SceneName); else SceneManager.LoadScene(SceneName);
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != SceneName || gm == null || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(0.5f);
            shots = Path.Combine(BotArgs.RunDir, "shots");
            Directory.CreateDirectory(shots);

            lucy = GameObject.FindGameObjectWithTag("Player");
            SoldierBrain[] soldiers = FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
            Record("loaded", lucy != null && soldiers.Length >= 4, $"soldiers={soldiers.Length}");
            if (lucy == null || soldiers.Length < 4) { Finish(); yield break; }
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            powers = lucy.GetComponent<PsychicPowers>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            stats.RegenPaused = true;
            foreach (DummyShooter gun in FindObjectsByType<DummyShooter>(FindObjectsSortMode.None)) gun.enabled = false;
            SoldierBrain a = soldiers.First(s => s.name == "Soldier_A");
            SoldierBrain guard = soldiers.First(s => s.name == "Guard_B");
            SoldierBrain armour = soldiers.First(s => s.name == "Armour_C");
            SoldierBrain gasser = soldiers.First(s => s.name == "Gasser_D");
            yield return BotClock.Wait(1f);

            // 1. Two bullets in the chest, from two sides: the vendor's small hit (the upper body), and a step.
            stats.DebugSet(45f, 0f);
            HitReactions ra = a.GetComponent<HitReactions>();
            Vector3 start = a.transform.position;
            Transform chest = a.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Chest);
            Clip("react_01_small", a.transform, () => Side(a.transform), 3.2f, 1.1f);
            HitAt(a, chest.position, 6f, DamageType.EnemyProjectile, lucy);
            string first = ra.LastClip;
            yield return BotClock.Wait(0.3f);
            Shot("small_hit", a.transform, Side(a.transform), 3f, 1.1f);
            yield return BotClock.Wait(1.2f);
            HitAt(a, chest.position, 6f, DamageType.EnemyProjectile, gasser.gameObject);
            string second = ra.LastClip;
            yield return BotClock.Wait(1.5f);
            EndClip();
            Record("small_hits", first.StartsWith("small_") && second.StartsWith("small_") && Flat(a.transform.position - start).magnitude > 0.05f,
                $"{first}, {second}, step={Flat(a.transform.position - start).magnitude:0.00} m");

            // 2. Half a mk_warspite: the vendor's big hit (the whole body).
            Clip("react_02_big", a.transform, () => Side(a.transform), 3.4f, 1.1f);
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 0.5f, a.GetComponent<LockOnTargetable>(), 0.05f);
            yield return BotClock.Wait(0.35f);
            Shot("big_hit", a.transform, Side(a.transform), 3f, 1.1f);
            yield return BotClock.Wait(1.6f);
            EndClip();
            Record("big_hit", ra.LastBig, $"clip={ra.LastClip}");

            // 3. A leg hit while he runs: the limp.
            a.Alert("bot", callOthers: false);
            yield return BotClock.Wait(0.6f);
            Clip("react_03_limp", a.transform, () => Side(a.transform), 4.5f, 1f);
            yield return BotClock.Wait(0.8f);
            HitAt(a, a.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftLowerLeg).position, 6f, DamageType.EnemyProjectile, gasser.gameObject);
            yield return BotClock.Wait(0.2f);
            bool limping = ra.Limping;
            yield return BotClock.Wait(1.0f);
            Shot("limp", a.transform, Side(a.transform), 4f, 1f);
            yield return BotClock.Wait(1.8f);
            EndClip();
            Record("leg_limp", ra.LastZone == HitReactions.Zone.Leg && limping, $"zone={ra.LastZone} limping={limping}");

            // 4. A shock that does not kill: the spasm (the body locks and shakes).
            HitReactions rg = guard.GetComponent<HitReactions>();
            Clip("react_04_shock", guard.transform, () => Side(guard.transform), 3f, 1.1f);
            yield return BotClock.Wait(0.4f);
            HitAt(guard, guard.transform.position + Vector3.up * 1.2f, 4f, DamageType.ShortShock, lucy);
            bool spasm = rg.Spasming;
            yield return BotClock.Wait(0.3f);
            Shot("shock_spasm", guard.transform, Side(guard.transform), 2.8f, 1.1f);
            yield return BotClock.Wait(1f);
            EndClip();
            Record("shock_spasm", spasm && rg.LastClip == "spasm", $"clip={rg.LastClip}");

            // 5. A full mk_erebus: the fire panic (big hits from changing sides, the torso writhes, the arms beat at
            // the flames), and he does not attack.
            HitReactions rd = gasser.GetComponent<HitReactions>();
            Health gasserHealth = gasser.GetComponent<Health>();
            gasserHealth.SetHealth(gasserHealth.MaxHealth);
            stats.DebugSet(100f, 0f);
            powers.Select(PowerKind.Fire);
            Clip("react_05_fire", gasser.transform, () => Side(gasser.transform), 3.6f, 1.1f);
            powers.FireAt(powers.Selected, 1f, gasser.GetComponent<LockOnTargetable>());
            yield return BotClock.Wait(0.5f);
            bool writhing = rd.Writhing;
            Shot("fire_panic", gasser.transform, Side(gasser.transform), 3f, 1.1f);
            yield return BotClock.Wait(3f);
            EndClip();
            Record("fire_panic", writhing, $"writhing={writhing} result={powers.LastResult} hp={gasserHealth.CurrentHealth:0}");
            yield return BotClock.Wait(1f);

            // 5b. Lucy: the small vendor hit on her upper body, from the side of the shot (one from the front, one from
            // her left).
            LucyAnimator lucyAnimator = lucy.GetComponent<LucyAnimator>();
            Health her = lucy.GetComponentInChildren<Health>();
            Clip("react_10_lucy_hit", lucy.transform, () => lucy.transform.forward + lucy.transform.right * 0.6f, 2.6f, 1.2f);
            yield return BotClock.Wait(0.3f);
            int hits0 = lucyAnimator.Hits;
            HitEventBus.Raise(new HitInfo { Attacker = null, Victim = her.gameObject, HitTransform = lucy.transform, HitPoint = lucy.transform.position + Vector3.up * 1.2f + lucy.transform.forward * 0.2f,
                HitNormal = lucy.transform.forward, DamageAmount = 5f, DamageType = DamageType.EnemyProjectile });
            yield return BotClock.Wait(0.25f);
            Shot("lucy_hit_front", lucy.transform, lucy.transform.forward + lucy.transform.right * 0.6f, 2.4f, 1.2f);
            yield return BotClock.Wait(1.0f);
            HitEventBus.Raise(new HitInfo { Attacker = null, Victim = her.gameObject, HitTransform = lucy.transform, HitPoint = lucy.transform.position + Vector3.up * 1.2f - lucy.transform.right * 0.2f,
                HitNormal = -lucy.transform.right, DamageAmount = 5f, DamageType = DamageType.EnemyProjectile });
            yield return BotClock.Wait(1.3f);
            EndClip();
            Record("lucy_hit", lucyAnimator.Hits - hits0 == 2 && lucyAnimator.LastHitDirection == -90, $"hits={lucyAnimator.Hits - hits0} last={lucyAnimator.LastHitDirection}");

            // 6. Deaths. A part mk_warspite kill: the knees buckle, he falls the way the blast pushed.
            stats.DebugSet(45f, 0f);
            powers.Select(PowerKind.Blast);
            EnemyRagdoll da = a.GetComponent<EnemyRagdoll>();
            a.GetComponent<Health>().SetHealth(1f);
            Clip("react_06_death_part_blast", da.Hips, () => Side(a.transform), 4f, 0.6f);
            powers.FireAt(powers.Selected, 0.5f, a.GetComponent<LockOnTargetable>());
            yield return BotClock.Wait(0.35f);
            Shot("death_part_blast_mid", a.transform, Side(a.transform), 3.4f, 0.9f);
            yield return BotClock.Wait(2.6f);
            Shot("death_part_blast_lies", da.Hips, Side(a.transform), 3f, 0.3f);
            EndClip();
            Record("death_part_blast", da.DiedFalling && da.LastDeath == EnemyRagdoll.DeathKind.Plain, $"fall={da.DiedFalling} kind={da.LastDeath} push={da.LastDeathPush:0.0} bone={da.LastDeathBone}");

            // 7. A full mk_warspite kill: the body flies.
            EnemyRagdoll dg = gasser.GetComponent<EnemyRagdoll>();
            gasser.GetComponent<Health>().SetHealth(1f);
            stats.DebugSet(45f, 0f);
            Vector3 hips0 = dg.Hips.position;
            Clip("react_07_death_full_blast", dg.Hips, () => Side(gasser.transform), 6f, 0.6f);
            powers.FireAt(powers.Selected, 1f, gasser.GetComponent<LockOnTargetable>());
            yield return BotClock.Wait(0.3f);
            Shot("death_full_blast_air", dg.Hips, Side(gasser.transform), 5f, 0.6f);
            yield return BotClock.Wait(2.7f);
            EndClip();
            float flew = Flat(dg.Hips.position - hips0).magnitude;
            Record("death_full_blast", dg.DiedFalling && dg.LastDeath == EnemyRagdoll.DeathKind.Blast && flew > 1f, $"kind={dg.LastDeath} flew={flew:0.0} m");

            // 8. A shock kill: the body convulses on the floor.
            EnemyRagdoll dgu = guard.GetComponent<EnemyRagdoll>();
            Clip("react_08_death_shock", dgu.Hips, () => Side(guard.transform), 3.4f, 0.6f);
            Health gh = guard.GetComponent<Health>();
            gh.TakeDamage(gh.MaxHealth * 10f);
            HitAt(guard, guard.transform.position + Vector3.up * 1.2f, 999f, DamageType.ShortShock, lucy, fatal: true);
            yield return BotClock.Wait(0.8f);
            Shot("death_shock", dgu.Hips, Side(guard.transform), 3f, 0.5f);
            yield return BotClock.Wait(1.6f);
            EndClip();
            Record("death_shock", dgu.DiedFalling && dgu.LastDeath == EnemyRagdoll.DeathKind.Shock, $"kind={dgu.LastDeath}");

            // 9. The power armour: a fire kill, the 2.6 m body falls and writhes.
            EnemyRagdoll dar = armour.GetComponent<EnemyRagdoll>();
            Health ah = armour.GetComponent<Health>();
            Clip("react_09_death_armour", dar.Hips, () => Side(armour.transform), 6f, 1f);
            ah.TakeDamage(ah.MaxHealth * 10f);
            HitAt(armour, armour.transform.position + Vector3.up * 1.8f, 999f, DamageType.PsychicFire, lucy, fatal: true);
            yield return BotClock.Wait(1.2f);
            Shot("death_armour", dar.Hips, Side(armour.transform), 5f, 0.8f);
            yield return BotClock.Wait(2.5f);
            EndClip();
            Record("death_armour", dar.DiedFalling && dar.LastDeath == EnemyRagdoll.DeathKind.Burn, $"kind={dar.LastDeath}");

            yield return BotClock.Wait(3f);
            int corpses = new[] { da, dg, dgu, dar }.Count(r => r.Current == EnemyRagdoll.State.Corpse);
            Shot("all_dead", lucy.transform, Vector3.back, 9f, 3f);
            Record("all_still", corpses == 4, $"corpses={corpses}/4");
            Finish();
        }

        private void HitAt(SoldierBrain figure, Vector3 point, float damage, DamageType type, GameObject attacker, bool fatal = false)
        {
            Health h = figure.GetComponent<Health>();
            if (!fatal) h.TakeDamage(damage);
            HitEventBus.Raise(new HitInfo { Attacker = attacker, Victim = figure.gameObject, HitTransform = figure.transform, HitPoint = point,
                HitNormal = attacker != null ? (attacker.transform.position - point).normalized : Vector3.forward, DamageAmount = damage, DamageType = type, WasFatal = h.IsDead });
        }

        /// <summary>The camera side: to the right of the line from her to the figure.</summary>
        private Vector3 Side(Transform figure)
        {
            Vector3 toFigure = Flat(figure.position - lucy.transform.position);
            if (toFigure.sqrMagnitude < 0.01f) return Vector3.right;
            return Vector3.Cross(Vector3.up, toFigure.normalized) - toFigure.normalized * 0.4f;
        }

        private static Camera CloseCamera()
        {
            if (closeCamera == null)
            {
                var go = new GameObject("[ReactionsCamera]");
                DontDestroyOnLoad(go);
                closeCamera = go.AddComponent<Camera>();
                closeCamera.fieldOfView = 40f;
                closeCamera.nearClipPlane = 0.05f;
                closeCamera.enabled = false;
            }
            Camera main = Camera.main;
            if (main != null)
            {
                closeCamera.clearFlags = main.clearFlags;
                closeCamera.backgroundColor = main.backgroundColor;
            }
            return closeCamera;
        }

        private static float placeTurn; // the last clear turn from the wanted side: the camera keeps it while it sees

        /// <summary>
        /// The camera on the wanted side, turned around the target until no wall, pillar or other figure stands between
        /// them (the first record hid the part-blast death behind a wall, the second behind the walking armour).
        /// </summary>
        private static void Place(Camera cam, Transform target, Vector3 side, float distance, float height)
        {
            side.y = 0f;
            if (side.sqrMagnitude < 0.001f) side = Vector3.back;
            Vector3 point = target.position + Vector3.up * height;
            // Walls, cover and the other figures block; the target's own body and the limp bodies do not.
            int mask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player", "BodyPart", "Ignore Raycast", "Debris");
            // The figure itself (the room puts all figures under one parent, so not target.root).
            Health owner = target.GetComponentInParent<Health>();
            Transform own = owner != null ? owner.transform : target;
            Vector3 At(float turn) => point + Quaternion.Euler(0f, turn, 0f) * side.normalized * distance + Vector3.up * 0.4f;
            bool Clear(Vector3 at)
            {
                foreach (RaycastHit h in Physics.SphereCastAll(point, 0.15f, at - point, Vector3.Distance(point, at), mask, QueryTriggerInteraction.Ignore))
                    if (!h.collider.transform.IsChildOf(own)) return false;
                return true;
            }
            if (!Clear(At(placeTurn)))
            {
                foreach (float turn in new[] { 0f, 40f, -40f, 80f, -80f, 120f, -120f, 160f, -160f })
                    if (Clear(At(turn))) { placeTurn = turn; break; }
            }
            cam.transform.position = At(placeTurn);
            cam.transform.LookAt(point);
        }

        /// <summary>A clip through a close camera that follows the target (record runs only).</summary>
        private static void Clip(string name, Transform target, Func<Vector3> side, float distance, float height)
        {
            if (!FrameRecorder.Enabled) return;
            placeTurn = 0f;
            Camera cam = CloseCamera();
            FrameRecorder.CameraProvider = () =>
            {
                if (target == null) return null;
                Place(cam, target, side(), distance, height);
                return cam;
            };
            FrameRecorder.Ensure().Begin(name);
        }

        private static void EndClip()
        {
            if (FrameRecorder.Instance != null) FrameRecorder.Instance.End();
            FrameRecorder.CameraProvider = null;
        }

        private void Shot(string moment, Transform target, Vector3 side, float distance, float height)
        {
            if (target == null) return;
            shotIndex++;
            Camera cam = CloseCamera();
            Place(cam, target, side, distance, height);
            ShotRenderer.Render(cam, Path.Combine(shots, $"react_{shotIndex:00}_{moment}.png"), ShotWidth, ShotHeight, useTargetTexture: true);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private void Finish()
        {
            if (finished) return;
            finished = true;
            EndClip();
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"reactions\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[ReactionsBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("reactions_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
