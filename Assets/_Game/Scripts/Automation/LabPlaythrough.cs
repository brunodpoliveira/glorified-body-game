using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The lab bot (2026-10-27, the design sweep's rank 6): in the LAB TEST room it shows the knife lab worker's run and
    /// strike, the gunman's shaking two-hand aim, his retreat at a fear of 3, the card that drops from the carrier,
    /// and the door it opens. Shots: shots/lab_&lt;n&gt;_&lt;moment&gt;.png. In a record run
    /// (tools/unity.sh record LabTest Main_Menu lab 300) each moment is a clip. Active only when BotArgs.Bot == "lab".
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class LabPlaythrough : MonoBehaviour
    {
        public const string SceneName = "LabTest";
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
        private static Camera closeCamera;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "lab", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<LabPlaythrough>() != null) return;
            var go = new GameObject("[LabPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<LabPlaythrough>();
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
            string line = $"[LabBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
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
            GameObject knifeObj = GameObject.Find("Lab_Knife"), carrierObj = GameObject.Find("Lab_Gun1"), gunmanObj = GameObject.Find("Lab_Gun2");
            Record("loaded", lucy != null && knifeObj != null && carrierObj != null && gunmanObj != null, $"spawned={EnemySpawn.Spawned} missing={EnemySpawn.Missing}");
            if (lucy == null || knifeObj == null || carrierObj == null || gunmanObj == null) { Finish(); yield break; }
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            // One figure at a time: the others stand still out of the fight.
            carrierObj.GetComponent<DummyShooter>().enabled = false;
            gunmanObj.GetComponent<DummyShooter>().enabled = false;
            carrierObj.GetComponent<SoldierBrain>().enabled = false;
            gunmanObj.GetComponent<SoldierBrain>().enabled = false;
            yield return BotClock.Wait(0.5f);

            // 1. The knife: it runs at her across the hall and strikes (the wind-up, the stab, the vendor attack).
            SoldierBrain knife = knifeObj.GetComponent<SoldierBrain>();
            DummyShooter blade = knifeObj.GetComponent<DummyShooter>();
            Clip("lab_01_knife", knifeObj.transform, () => Side(knifeObj.transform), 4.5f, 1.2f);
            knife.Alert("bot", callOthers: false);
            t0 = BotClock.Now;
            bool shotTaken = false;
            while (blade.MeleeStrikes < 2 && BotClock.Now - t0 < 20f)
            {
                if (!shotTaken && blade.Current == DummyShooter.State.Telegraph && blade.TelegraphProgress > 0.6f)
                {
                    shotTaken = true;
                    Shot("knife_windup", knifeObj.transform, Side(knifeObj.transform), 3.2f, 1.3f);
                }
                yield return null;
            }
            yield return BotClock.Wait(1f);
            EndClip();
            Record("knife", blade.MeleeStrikes >= 1 && blade.RoundsFired == 0, $"strikes={blade.MeleeStrikes} rounds={blade.RoundsFired} apart={Flat(knifeObj.transform.position - lucy.transform.position).magnitude:0.0} m");
            Kill(knifeObj, DamageType.PsychicBlast, 0.6f);
            yield return BotClock.Wait(2f);

            // 2. The gunman: both hands on the pistol, the gun shakes, the lock lands off her chest.
            DummyShooter pistol = gunmanObj.GetComponent<DummyShooter>();
            EnemyGun hold = gunmanObj.GetComponent<EnemyGun>();
            pistol.enabled = true;
            gunmanObj.GetComponent<SoldierBrain>().enabled = true;
            gunmanObj.GetComponent<SoldierBrain>().Alert("bot", callOthers: false);
            Clip("lab_02_pistol", gunmanObj.transform, () => Side(gunmanObj.transform), 2.6f, 1.4f);
            t0 = BotClock.Now;
            shotTaken = false;
            float maxShake = 0f, maxMiss = 0f;
            int rounds = 0;
            while (pistol.RoundsFired < 3 && BotClock.Now - t0 < 30f)
            {
                maxShake = Mathf.Max(maxShake, hold.LastShake);
                if (pistol.RoundsFired > rounds) { rounds = pistol.RoundsFired; maxMiss = Mathf.Max(maxMiss, pistol.LastMissDistance); }
                if (!shotTaken && pistol.Current == DummyShooter.State.Telegraph)
                {
                    shotTaken = true;
                    Shot("pistol_aim", gunmanObj.transform, Side(gunmanObj.transform), 2.2f, 1.4f);
                }
                yield return null;
            }
            yield return BotClock.Wait(0.8f);
            EndClip();
            Record("pistol", pistol.RoundsFired >= 1 && maxShake > 0.5f && hold.TwoHanded, $"rounds={pistol.RoundsFired} shake={maxShake:0.0} deg worst miss={maxMiss:0.00} m");

            // 3. Fear of 3: he runs, cowers and shakes.
            SoldierBrain gunman = gunmanObj.GetComponent<SoldierBrain>();
            EnemyMorale morale = gunmanObj.GetComponent<EnemyMorale>();
            Clip("lab_03_retreat", gunmanObj.transform, () => Side(gunmanObj.transform), 4f, 1.1f);
            for (int i = 0; i < 3; i++) morale.See(null, gruesome: false);
            t0 = BotClock.Now;
            while (gunman.Current != SoldierBrain.Phase.Cower && BotClock.Now - t0 < 10f) yield return null;
            yield return BotClock.Wait(2.5f);
            Shot("cower", gunmanObj.transform, Side(gunmanObj.transform), 2.5f, 0.9f);
            EndClip();
            Record("retreat", gunman.Current == SoldierBrain.Phase.Cower, $"phase={gunman.Current} tremble={gunmanObj.GetComponent<Tremble>().LastDegrees:0.0} deg");

            // 4. The carrier dies: the card drops beside his body.
            ItemCarrier carrier = carrierObj.GetComponent<ItemCarrier>();
            Transform hips = carrierObj.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
            // A high fixed camera: from the side at eye height the body hid the card (the first record).
            Vector3 overBody = hips.position + Side(carrierObj.transform).normalized * 1.6f + Vector3.up * 2.2f;
            Vector3 bodyFloor = new Vector3(hips.position.x, 0f, hips.position.z);
            ClipFixed("lab_04_card", overBody, bodyFloor);
            Kill(carrierObj, DamageType.PsychicBlast, 0.6f);
            yield return BotClock.Wait(ItemCarrier.FallSeconds + 1.5f);
            ItemPickup card = carrier.Drop;
            if (card != null) ShotFixed("card", card.transform.position + Side(carrierObj.transform).normalized * 1.0f + Vector3.up * 1.4f, card.transform.position);
            EndClip();
            bool dropped = card != null && card.gameObject.activeInHierarchy;
            Record("card_drop", dropped, dropped ? $"card {Flat(card.transform.position - hips.position).magnitude:0.00} m from his hips" : "no card");
            if (!dropped) { Finish(); yield break; }

            // 5. She takes the card and opens the store room.
            Interactor hands = lucy.GetComponent<Interactor>();
            TankController tank = lucy.GetComponent<TankController>();
            Vector3 toCard = Flat(card.transform.position - lucy.transform.position);
            tank.Teleport(card.transform.position - toCard.normalized * 0.7f, Quaternion.LookRotation(toCard.normalized).eulerAngles.y);
            yield return BotClock.Wait(0.3f);
            string taken = card.Confirm(hands);
            LockedDoor door = GameObject.Find("Door_Store").GetComponent<LockedDoor>();
            tank.Teleport(door.Point + Vector3.right * 1.0f - Vector3.up * door.Point.y, 270f);
            yield return BotClock.Wait(0.5f);
            // From inside the hall, south-east of the doorway: the leaf slides away and the store room shows.
            Vector3 hallView = new Vector3(door.Point.x + 4.5f, 2.3f, door.Point.z - 3.2f), doorway = new Vector3(door.Point.x - 0.8f, 1.0f, door.Point.z);
            ClipFixed("lab_05_door", hallView, doorway);
            yield return BotClock.Wait(0.8f);
            string opened = door.KindFor(hands) == InteractKind.Ask ? door.Confirm(hands) : "locked";
            yield return BotClock.Wait(1.5f);
            ShotFixed("door_open", hallView, doorway);
            EndClip();
            Record("card_opens_door", taken == "taken:lab_card" && door.IsOpen, $"{taken}, {opened}");
            Finish();
        }

        /// <summary>A kill by her: the damage, and the hit on the bus (the death push, the morale, the exp).</summary>
        private void Kill(GameObject figure, DamageType type, float charge)
        {
            Health h = figure.GetComponent<Health>();
            Vector3 chest = figure.transform.position + Vector3.up * 1.2f;
            h.TakeDamage(h.MaxHealth * 10f);
            HitEventBus.Raise(new HitInfo { Attacker = lucy, Victim = figure, HitTransform = figure.transform, HitPoint = chest,
                HitNormal = (lucy.transform.position - chest).normalized, DamageAmount = 999f, DamageType = type, Charge = charge, WasFatal = true });
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
                var go = new GameObject("[LabCamera]");
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

        /// <summary>The camera on the wanted side, turned around the target until no wall, cover or other figure blocks it (as the reactions bot).</summary>
        private static void Place(Camera cam, Transform target, Vector3 side, float distance, float height)
        {
            side.y = 0f;
            if (side.sqrMagnitude < 0.001f) side = Vector3.back;
            Vector3 point = target.position + Vector3.up * height;
            int mask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player", "BodyPart", "Ignore Raycast", "Debris");
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

        /// <summary>A clip through a camera that stays at one place (record runs only).</summary>
        private static void ClipFixed(string name, Vector3 at, Vector3 lookAt)
        {
            if (!FrameRecorder.Enabled) return;
            Camera cam = CloseCamera();
            FrameRecorder.CameraProvider = () =>
            {
                cam.transform.position = at;
                cam.transform.LookAt(lookAt);
                return cam;
            };
            FrameRecorder.Ensure().Begin(name);
        }

        private void ShotFixed(string moment, Vector3 at, Vector3 lookAt)
        {
            shotIndex++;
            Camera cam = CloseCamera();
            cam.transform.position = at;
            cam.transform.LookAt(lookAt);
            ShotRenderer.Render(cam, Path.Combine(shots, $"lab_{shotIndex:00}_{moment}.png"), ShotWidth, ShotHeight, useTargetTexture: true);
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
            placeTurn = 0f;
            Camera cam = CloseCamera();
            Place(cam, target, side, distance, height);
            ShotRenderer.Render(cam, Path.Combine(shots, $"lab_{shotIndex:00}_{moment}.png"), ShotWidth, ShotHeight, useTargetTexture: true);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private void Finish()
        {
            if (finished) return;
            finished = true;
            EndClip();
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"lab\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[LabBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("lab_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
