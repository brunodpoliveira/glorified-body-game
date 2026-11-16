using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Characters;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The model check of Lucy and the round 178 figures (2026-11-16, the T49 models, rounds 175 and 178): in a level (default the Tutorial, a dressed
    /// room) she stands, walks and runs at the play camera, and the bot measures what a model swap can break: her
    /// height (the baked mesh, the Crown included), the lowest vertex against the floor (feet in the floor or in the
    /// air), the facing (the toes against the forward of her root), and the slip of the planted foot while the
    /// controller moves her (the clips play in place, the controller sets the speed). Shots:
    /// shots/lucy_&lt;n&gt;_&lt;moment&gt;.png, at the play camera and at two close cameras (front, side).
    /// Active only when BotArgs.Bot == "lucy": tools/unity.sh play Tutorial Main_Menu lucy 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class LucyModelPlaythrough : MonoBehaviour
    {
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
        private int shotIndex;
        private VirtualPad pad;
        private GameObject lucy;
        private SkinnedMeshRenderer skin;
        private GameObject subject; // the figure Measure, Slip, FreeSide and CloseShot look at: Lucy, then each class
        private Mesh baked;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "lucy", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<LucyModelPlaythrough>() != null) return;
            var go = new GameObject("[LucyModelPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<LucyModelPlaythrough>();
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
            if (!finished)
            {
                Record("timeout", false, $"bot did not finish in {limit:0}s");
                Finish();
            }
        }

        private void Record(string name, bool ok, string detail = "")
        {
            steps.Add(new Step { name = name, ok = ok, detail = detail, t = Time.unscaledTime - startTime });
            string line = $"[LucyBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            Directory.CreateDirectory(Path.Combine(BotArgs.RunDirOrData, "shots"));
            string level = string.IsNullOrEmpty(BotArgs.Level) ? "Tutorial" : BotArgs.Level;
            GameManager gm = GameManager.Instance;
            if (SceneManager.GetActiveScene().name != level)
            {
                if (gm != null) gm.LoadLevel(level); else SceneManager.LoadScene(level);
            }
            float t0 = BotClock.Now;
            while (SceneManager.GetActiveScene().name != level && BotClock.Now - t0 < 60f) yield return null;
            yield return BotClock.Wait(1.5f);
            if (GlorifiedBody.Story.CutscenePlayer.IsPlaying) GlorifiedBody.Story.CutscenePlayer.Skip();
            yield return BotClock.Wait(0.5f);
            GlorifiedBody.Interaction.UprightRestraint.FreeAll(); // the Demo opens with her in the frame of H1
            yield return BotClock.Wait(0.5f);

            lucy = GameObject.FindGameObjectWithTag("Player");
            skin = null;
            if (lucy != null)
                foreach (SkinnedMeshRenderer s in lucy.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if (skin == null || s.sharedMesh.triangles.Length > skin.sharedMesh.triangles.Length) skin = s; // the body, not the Crown
            Record("lucy_in_the_level", lucy != null && skin != null,
                $"scene={SceneManager.GetActiveScene().name} model={GlorifiedBody.Characters.LucyModel.Current(lucy)} mesh={(skin != null ? skin.sharedMesh.name : "none")} tris={(skin != null ? skin.sharedMesh.triangles.Length / 3 : 0)} meshes={(lucy != null ? string.Join(", ", lucy.GetComponentsInChildren<SkinnedMeshRenderer>().Select(k => k.name)) : "")}");
            if (lucy == null || skin == null) { Finish(); yield break; }
            lucy.GetComponentInChildren<Health>()?.SetInvulnerable(true);
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            baked = new Mesh();
            subject = lucy;
            TankController tank = lucy.GetComponent<TankController>();

            // 1. She stands: the play camera, then turned to the camera, then two close cameras.
            yield return BotClock.Wait(1.0f);
            yield return Shot("idle_play_camera");
            Camera main = Camera.main;
            Vector3 across = lucy.transform.right; // the camera's flat right: she walks across the frame
            if (main != null && tank != null)
            {
                // Into the middle of the frame: the floor 4 m out along the camera's flat forward, when it is free.
                Vector3 flat = main.transform.forward; flat.y = 0f; flat.Normalize();
                across = Vector3.Cross(Vector3.up, flat).normalized;
                Vector3 spot = main.transform.position + flat * 4f;
                Vector3 place = lucy.transform.position;
                if (Physics.Raycast(spot + Vector3.up * 1.5f, Vector3.down, out RaycastHit floorHit, 4f, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore)
                    && floorHit.normal.y > 0.9f && Mathf.Abs(floorHit.point.y - place.y) < 0.2f
                    && !Physics.CheckCapsule(floorHit.point + Vector3.up * 0.4f, floorHit.point + Vector3.up * 1.4f, 0.3f, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
                    place = floorHit.point;
                Vector3 to = main.transform.position - place; to.y = 0f;
                tank.Teleport(place, Quaternion.LookRotation(to).eulerAngles.y);
                Debug.Log($"[LucyBot] placed at {place} (spot {spot}, moved {place != lucy.transform.position})");
                yield return BotClock.Wait(1.0f);
                yield return Shot("idle_facing_the_play_camera");
            }
            Measure(out float height, out float lowest, out float facing);
            Record("height", height > 1.6f && height < 1.8f, $"{height:0.000} m (DEMO_PLAN §3.2: 1.70)");
            Record("feet_on_the_floor", Mathf.Abs(lowest) < 0.03f, $"lowest vertex {lowest * 100f:0.0} cm from the floor (- = in the floor)");
            Record("faces_forward", facing > 0.8f, $"toes . root forward = {facing:0.00}");
            yield return CloseShot("idle_close_front", 0f, 2.4f);
            yield return CloseShot("idle_close_side", FreeSide(), 2.4f);

            // 2. She walks: across the frame when the floor is free that way, else towards the camera. The shot, the
            // slip of the planted foot, a close shot from a free side.
            Vector3 start = lucy.transform.position;
            float startYaw = lucy.transform.eulerAngles.y;
            Vector3 toCamera = main != null ? Vector3.ProjectOnPlane(main.transform.position - start, Vector3.up).normalized : -lucy.transform.forward;
            Vector3 walkDir = across;
            float bestFree = -1f;
            foreach (Vector3 d in new[] { across, -across, toCamera })
            {
                float free = FreeRun(start - d * 1.2f, d, 5f);
                if (free > bestFree + 0.5f) { bestFree = free; walkDir = d; }
            }
            float acrossYaw = Quaternion.LookRotation(walkDir).eulerAngles.y;
            Debug.Log($"[LucyBot] the walk goes {walkDir} ({bestFree:0.0} m free)");
            tank.Teleport(start - walkDir * 1.2f, acrossYaw);
            yield return BotClock.Wait(0.5f);
            Vector3 walkFrom = lucy.transform.position;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return BotClock.Wait(0.6f);
            yield return Shot("walk_play_camera");
            float walkSlip = 0f;
            yield return Slip(1.0f, v => walkSlip = v);
            yield return CloseShot("walk_close_side", FreeSide(), 2.6f);
            pad.SetHeld(InputManager.GameAction.Forward, false);
            float walked = Vector3.Distance(walkFrom, lucy.transform.position);
            Record("walks", walked > 1f, $"{walked:0.00} m moved, planted foot slip {walkSlip:0.00} m/s (controller walk {tank.WalkSpeed:0.00} m/s)");
            yield return BotClock.Wait(0.6f);

            // 3. She runs the same free line (a teleport past it once dropped her out of the Tutorial room).
            tank.Teleport(walkFrom, acrossYaw);
            yield return BotClock.Wait(0.5f);
            pad.SetHeld(InputManager.GameAction.Run, true);
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return BotClock.Wait(0.35f);
            yield return Shot("run_play_camera");
            float runSlip = 0f;
            yield return Slip(0.6f, v => runSlip = v);
            Record("runs", tank.IsRunning || runSlip >= 0f, $"planted foot slip {runSlip:0.00} m/s (controller run {tank.RunSpeed:0.00} m/s)");
            pad.SetHeld(InputManager.GameAction.Forward, false);
            pad.SetHeld(InputManager.GameAction.Run, false);
            yield return BotClock.Wait(0.6f);
            Measure(out _, out float lowestEnd, out _);
            Debug.Log($"[LucyBot] the run ends at {lucy.transform.position} (root y {lucy.transform.position.y:0.000}), lowest {lowestEnd * 100f:0.0} cm");
            // Back to the free spot in the middle of the frame: the rest of the checks there.
            tank.Teleport(start, startYaw);
            // After the run: the feet over 3 s, with the animator state (the stop blends run, walk, idle).
            float lowestAfter = 0f;
            Animator anim = lucy.GetComponentInChildren<Animator>();
            foreach (float wait in new[] { 0.8f, 0.8f, 1.4f })
            {
                yield return BotClock.Wait(wait);
                Measure(out _, out lowestAfter, out _);
                AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
                Debug.Log($"[LucyBot] after the run: lowest {lowestAfter * 100f:0.0} cm, Speed {anim.GetFloat("Speed"):0.00}, state {info.shortNameHash} t {info.normalizedTime:0.00}, moving {tank.IsRunning}");
            }
            yield return Shot("after_the_run_play_camera");
            Record("feet_on_the_floor_after", Mathf.Abs(lowestAfter) < 0.03f, $"lowest vertex {lowestAfter * 100f:0.0} cm, 3 s after the run");

            // 4. The pain clip of the short (lucy_pain.fbx, frustrated_02, made on the T19 rig): a Humanoid clip, so it
            // plays through the new avatar. The hand must reach the head and the feet stay on the floor.
            PoseClips clips = PoseClips.Load();
            if (clips != null && clips.lucyPain != null)
            {
                PoseOverride pose = anim.GetComponent<PoseOverride>() ?? anim.gameObject.AddComponent<PoseOverride>();
                pose.Play(clips.lucyPain, loop: false, unscaledTime: true, from: PoseClips.PainFrom, length: 3f);
                yield return BotClock.Wait(1.0f);
                Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
                float hand = Mathf.Min(Vector3.Distance(anim.GetBoneTransform(HumanBodyBones.LeftHand).position, head.position),
                                       Vector3.Distance(anim.GetBoneTransform(HumanBodyBones.RightHand).position, head.position));
                Measure(out _, out float lowestPain, out _);
                yield return Shot("pain_play_camera");
                yield return CloseShot("pain_close_front", 0f, 2.4f);
                yield return CloseShot("pain_close_side", FreeSide(), 2.4f);
                Record("pain_clip_plays", pose.Playing && hand < 0.3f && Mathf.Abs(lowestPain) < 0.05f,
                    $"clip {clips.lucyPain.name} human={clips.lucyPain.humanMotion}, nearest hand to the head {hand:0.00} m, lowest vertex {lowestPain * 100f:0.0} cm");
                yield return BotClock.Wait(2.2f);
            }
            else Record("pain_clip_plays", false, "no PoseClips.lucyPain");

            // 5. The round 178 figures (the guard, the patient, the nurse): each from the catalog on the free spot, idle
            // at the play camera, then the walk. Their walk clip travels, and the import gives the travel to the root
            // motion (not applied), so the bot first reads the ground speed of the clip (the planted foot, the root
            // still), then moves the root at that speed for the walk shots.
            foreach (Renderer r in lucy.GetComponentsInChildren<Renderer>()) r.enabled = false;
            CharacterCatalog catalog = CharacterCatalog.Load();
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            foreach (string id in new[] { "guard", "patient", "nurse" })
            {
                CharacterEntry entry = catalog != null ? catalog.Find(id) : null;
                if (entry == null || entry.prefab == null) { Record($"{id}_in_the_level", false, "no catalog prefab"); continue; }
                Vector3 toCam = main != null ? Vector3.ProjectOnPlane(main.transform.position - start, Vector3.up).normalized : Vector3.forward;
                GameObject figure = Instantiate(entry.prefab, start, Quaternion.LookRotation(toCam));
                foreach (Transform t in figure.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = enemyLayer;
                subject = figure;
                Animator fa = figure.GetComponentInChildren<Animator>();
                yield return BotClock.Wait(1.0f);
                Measure(out float fh, out float fl, out float ff);
                Record($"{id}_height", Mathf.Abs(fh - entry.height) < 0.1f, $"{fh:0.000} m (catalog {entry.height:0.00})");
                Record($"{id}_feet_on_the_floor", Mathf.Abs(fl) < 0.03f, $"lowest vertex {fl * 100f:0.0} cm from the floor");
                Record($"{id}_faces_forward", ff > 0.8f, $"toes . root forward = {ff:0.00}");
                yield return Shot($"{id}_idle_play_camera");
                yield return CloseShot($"{id}_idle_close_front", 0f, 2.6f);

                figure.transform.SetPositionAndRotation(start - walkDir * 1.0f, Quaternion.LookRotation(walkDir));
                fa.SetFloat("Speed", 0.5f);
                yield return BotClock.Wait(0.6f);
                float ground = 0f;
                yield return Slip(1.2f, v => ground = v);
                float walkStart = BotClock.Now;
                bool shot = false;
                while (BotClock.Now - walkStart < 1.4f)
                {
                    figure.transform.position += walkDir * ground * Time.deltaTime;
                    if (!shot && BotClock.Now - walkStart > 0.7f)
                    {
                        shot = true;
                        yield return Shot($"{id}_walk_play_camera");
                        yield return CloseShot($"{id}_walk_close_side", FreeSide(), 2.8f);
                    }
                    yield return null;
                }
                Record($"{id}_walks", ground > 0.5f, $"the walk clip's ground speed {ground:0.00} m/s (the speed the root needs for no foot slide)");
                Destroy(figure);
                yield return null;
            }
            subject = lucy;
            foreach (Renderer r in lucy.GetComponentsInChildren<Renderer>()) r.enabled = true;
            Finish();
        }

        /// <summary>The baked mesh in the world: height, the lowest point against her root (the floor), the facing.</summary>
        private void Measure(out float height, out float lowest, out float facing)
        {
            // BakeMesh(useScale: false) gives world-size vertices about the renderer's pivot and axes, so
            // TRS(position, rotation, 1) places them (measured 2026-11-16, Unity 6000.3; the bounds read 4 cm high).
            // Every skinned mesh of the figure counts (Lucy: the body and the Crown).
            float min = float.MaxValue, max = float.MinValue;
            foreach (SkinnedMeshRenderer s in subject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                s.BakeMesh(baked, false);
                Matrix4x4 m = Matrix4x4.TRS(s.transform.position, s.transform.rotation, Vector3.one);
                foreach (Vector3 v in baked.vertices)
                {
                    float y = m.MultiplyPoint3x4(v).y;
                    if (y < min) min = y;
                    if (y > max) max = y;
                }
            }
            height = max - min;
            Transform root = subject.transform;
            float floor = root.position.y;
            int mask = ~LayerMask.GetMask("Player", "Enemy");
            if (Physics.Raycast(root.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.5f, mask, QueryTriggerInteraction.Ignore))
                floor = hit.point.y;
            lowest = min - floor;
            Animator animator = subject.GetComponentInChildren<Animator>();
            facing = 0f;
            if (animator != null && animator.isHuman)
            {
                Vector3 feet = (animator.GetBoneTransform(HumanBodyBones.LeftFoot).position + animator.GetBoneTransform(HumanBodyBones.RightFoot).position) * 0.5f;
                Transform lt = animator.GetBoneTransform(HumanBodyBones.LeftToes), rt = animator.GetBoneTransform(HumanBodyBones.RightToes);
                if (lt != null && rt != null)
                {
                    Vector3 toes = (lt.position + rt.position) * 0.5f - feet; toes.y = 0f;
                    facing = Vector3.Dot(toes.normalized, root.forward);
                }
            }
            Debug.Log($"[LucyBot] measure {subject.name}: height {height:0.000} lowest {lowest:0.000} floor {floor:0.000} root y {root.position.y:0.000} facing {facing:0.00}");
        }

        /// <summary>
        /// The slip of the planted foot: each frame the lower foot (the toes), while it is within 3 cm of its lowest
        /// height, gives its speed over the floor. The median of those speeds: 0 when the stride fits the controller.
        /// </summary>
        private IEnumerator Slip(float seconds, Action<float> result)
        {
            Animator animator = subject.GetComponentInChildren<Animator>();
            Transform lt = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftToes) : null;
            Transform rt = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightToes) : null;
            if (lt == null || rt == null) { result(-1f); yield break; }
            var speeds = new List<float>();
            float lowL = float.MaxValue, lowR = float.MaxValue;
            Vector3 pl = lt.position, pr = rt.position;
            float end = BotClock.Now + seconds;
            while (BotClock.Now < end)
            {
                yield return null;
                float dt = Time.deltaTime;
                if (dt <= 0f) continue;
                lowL = Mathf.Min(lowL, lt.position.y); lowR = Mathf.Min(lowR, rt.position.y);
                Transform planted = lt.position.y < rt.position.y ? lt : rt;
                Vector3 before = planted == lt ? pl : pr;
                float low = planted == lt ? lowL : lowR;
                if (planted.position.y - low < 0.03f)
                {
                    Vector3 d = planted.position - before; d.y = 0f;
                    speeds.Add(d.magnitude / dt);
                }
                pl = lt.position; pr = rt.position;
            }
            speeds.Sort();
            float median = speeds.Count > 0 ? speeds[speeds.Count / 2] : -1f;
            Debug.Log($"[LucyBot] slip: {speeds.Count} samples, median {median:0.00} m/s, p25 {(speeds.Count > 0 ? speeds[speeds.Count / 4] : -1f):0.00}");
            result(median);
        }

        /// <summary>How far a body (a 0.3 m capsule) can go from a point along a direction, up to a limit.</summary>
        private static float FreeRun(Vector3 from, Vector3 dir, float limit)
        {
            int mask = ~LayerMask.GetMask("Player");
            if (Physics.CheckCapsule(from + Vector3.up * 0.4f, from + Vector3.up * 1.4f, 0.3f, mask, QueryTriggerInteraction.Ignore)) return 0f;
            return Physics.CapsuleCast(from + Vector3.up * 0.4f, from + Vector3.up * 1.4f, 0.3f, dir, out RaycastHit hit, limit, mask, QueryTriggerInteraction.Ignore) ? hit.distance : limit;
        }

        /// <summary>The side (90 or -90 from her forward) with the free line for a close camera, or the front.</summary>
        private float FreeSide()
        {
            Vector3 chest = subject.transform.position + Vector3.up * 1.0f;
            int mask = ~LayerMask.GetMask("Player", "Enemy");
            foreach (float a in new[] { 90f, -90f, 0f, 180f })
            {
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * subject.transform.forward;
                if (!Physics.SphereCast(chest, 0.2f, dir, out _, 2.8f, mask, QueryTriggerInteraction.Ignore)) return a;
            }
            return 90f;
        }

        private IEnumerator CloseShot(string moment, float aroundDegrees, float distance)
        {
            Transform root = subject.transform;
            Vector3 dir = Quaternion.Euler(0f, aroundDegrees, 0f) * root.forward;
            Vector3 at = root.position + Vector3.up * 0.95f;
            Vector3 from = at + dir * distance + Vector3.up * 0.15f;
            Camera main = Camera.main;
            var obj = new GameObject("[LucyShotCamera]");
            Camera cam = obj.AddComponent<Camera>();
            if (main != null) cam.CopyFrom(main);
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            obj.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
            cam.enabled = false;
            yield return Shot(moment, cam);
            Destroy(obj);
        }

        private IEnumerator Shot(string moment, Camera own = null)
        {
            shotIndex++;
            yield return null; // not WaitForEndOfFrame: it never comes in a batch run
            Camera cam = own != null ? own : Camera.main;
            if (cam == null) yield break;
            ShotRenderer.Render(cam, Path.Combine(BotArgs.RunDirOrData, "shots", $"lucy_{shotIndex:00}_{moment}.png"), ShotWidth, ShotHeight, useTargetTexture: true);
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (pad != null) GameInput.ClearBotSource(pad);
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"lucy\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[LucyBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("lucy_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
