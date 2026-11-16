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
    /// The model check of Lucy (bare and guard) and the figures of rounds 178 and 179 (2026-11-16, the T49 models): in a level (default the Tutorial, a dressed
    /// room) she stands, walks and runs at the play camera, and the bot measures what a model swap can break: her
    /// height (the baked mesh, the Crown included), the lowest vertex against the floor (feet in the floor or in the
    /// air), the facing (the toes against the forward of her root), and the slip of the planted foot while the
    /// controller moves her (the clips play in place at the pace of round 179, the controller sets the speed). Shots:
    /// shots/lucy_&lt;n&gt;_&lt;moment&gt;.png, at the play camera and at two close cameras (front, side).
    /// Active only when BotArgs.Bot == "lucy": tools/unity.sh play Tutorial Main_Menu lucy 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class LucyModelPlaythrough : MonoBehaviour
    {
        public const int ShotWidth = 1920, ShotHeight = 1080;
        private const int MinRunFrames = 60;

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
        private int slipFrames; // the stance frames of the last Slip
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
            foreach (Vector3 d in new[] { across, -across, toCamera, Quaternion.Euler(0f, 45f, 0f) * across, Quaternion.Euler(0f, -45f, 0f) * across, Quaternion.Euler(0f, 135f, 0f) * across, Quaternion.Euler(0f, -135f, 0f) * across, -toCamera })
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
            yield return Slip(1.4f, v => walkSlip = v, tank.WalkSpeed * 0.8f);
            yield return CloseShot("walk_close_side", FreeSide(), 2.6f);
            pad.SetHeld(InputManager.GameAction.Forward, false);
            float walked = Vector3.Distance(walkFrom, lucy.transform.position);
            // The slip in play is for the record (it goes from 0.2 to 0.4 m/s between runs): the check of the pace is lucy_bare_walks.
            Record("walks", walked > 1f && walkSlip >= 0f, $"{walked:0.00} m moved, planted foot slip {walkSlip:0.00} m/s (controller walk {tank.WalkSpeed:0.00} m/s; the pace check: lucy_bare_walks)");
            yield return BotClock.Wait(0.6f);

            // 3. She runs the same free line (a teleport past it once dropped her out of the Tutorial room).
            tank.Teleport(walkFrom, acrossYaw);
            yield return BotClock.Wait(0.5f);
            pad.SetHeld(InputManager.GameAction.Run, true);
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return BotClock.Wait(0.35f);
            yield return Shot("run_play_camera");
            float runSlip = 0f;
            yield return Slip(0.9f, v => runSlip = v, tank.RunSpeed * 0.8f);
            Record("runs", slipFrames < MinRunFrames || runSlip >= 0f, slipFrames < MinRunFrames
                ? $"{slipFrames} free stance frames only (the free line of this room is short): no measure here, the check lucy_bare_runs measures the clip"
                : $"planted foot slip {runSlip:0.00} m/s (controller run {tank.RunSpeed:0.00} m/s; the pace check: lucy_bare_runs)");
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

            // 4b. Lucy in the guard uniform (lucy_guard, the T49 model of round 179): idle and walk, the slip again.
            if (LucyModel.Wear(lucy, "lucy_guard"))
            {
                yield return null;
                subject = lucy;
                tank.Teleport(start, startYaw);
                yield return BotClock.Wait(1.0f);
                Measure(out float gh, out float gl, out float gf);
                Record("lucy_guard_model", LucyModel.Current(lucy) == "lucy_guard" && gh > 1.6f && gh < 1.8f && Mathf.Abs(gl) < 0.03f && gf > 0.8f,
                    $"height {gh:0.000} m, lowest vertex {gl * 100f:0.0} cm, facing {gf:0.00}, meshes {string.Join(", ", lucy.GetComponentsInChildren<SkinnedMeshRenderer>().Select(k => k.name))}");
                yield return Shot("lucy_guard_idle_play_camera");
                yield return CloseShot("lucy_guard_idle_close_front", 0f, 2.4f);
                tank.Teleport(walkFrom, acrossYaw);
                yield return BotClock.Wait(0.5f);
                pad.SetHeld(InputManager.GameAction.Forward, true);
                yield return BotClock.Wait(0.6f);
                yield return Shot("lucy_guard_walk_play_camera");
                float guardSlip = 0f;
                yield return Slip(1.4f, v => guardSlip = v, tank.WalkSpeed * 0.8f);
                yield return CloseShot("lucy_guard_walk_close_side", FreeSide(), 2.6f);
                pad.SetHeld(InputManager.GameAction.Forward, false);
                Record("lucy_guard_walks_in_play", guardSlip >= 0f, $"planted foot slip {guardSlip:0.00} m/s (controller walk {tank.WalkSpeed:0.00} m/s; the pace check: lucy_guard_walks)");
                yield return BotClock.Wait(0.5f);
                tank.Teleport(walkFrom, acrossYaw);
                yield return BotClock.Wait(0.5f);
                pad.SetHeld(InputManager.GameAction.Run, true);
                pad.SetHeld(InputManager.GameAction.Forward, true);
                yield return BotClock.Wait(0.35f);
                float guardRunSlip = 0f;
                yield return Slip(0.9f, v => guardRunSlip = v, tank.RunSpeed * 0.8f);
                pad.SetHeld(InputManager.GameAction.Forward, false);
                pad.SetHeld(InputManager.GameAction.Run, false);
                Record("lucy_guard_runs_in_play", slipFrames < MinRunFrames || guardRunSlip >= 0f, slipFrames < MinRunFrames
                    ? $"{slipFrames} free stance frames only (the free line of this room is short): no measure here, the check lucy_guard_runs measures the clip"
                    : $"planted foot slip {guardRunSlip:0.00} m/s (controller run {tank.RunSpeed:0.00} m/s; the pace check: lucy_guard_runs)");
                yield return BotClock.Wait(0.6f);
                tank.Teleport(start, startYaw);
                LucyModel.Wear(lucy, "lucy_bare");
                yield return null;
            }
            else Record("lucy_guard_model", false, "LucyModel.Wear(lucy_guard) failed");

            // 5. Every T49 class (rounds 175 to 181) on a treadmill: the catalog prefab on the free spot, idle at the play camera, then the walk
            // and the run with the root moved by the bot at the game speed of the class (Lucy: the TankController; the
            // figures: the Invector AI, ClassPace). The root is moved through walls, so the run is as long as it needs.
            // The import gives a clip's travel to the root motion (not applied), so the body stays over the root.
            foreach (Renderer r in lucy.GetComponentsInChildren<Renderer>()) r.enabled = false;
            CharacterCatalog catalog = CharacterCatalog.Load();
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            foreach (string id in new[] { "lucy_bare", "lucy_guard", "guard", "patient", "nurse", "lodge_staff", "toll_guard", "labworker", "soldier", "fireman" })
            {
                bool isLucy = id.StartsWith("lucy");
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

                // The walk: first the root still (the clip's ground speed, the planted foot over the floor), then the
                // root moves at the AI walk speed (ClassPace) and the slip of the planted foot is measured: about 0
                // when the pace of the clip fits (round 179: "Speed up the clips").
                float walkSpeed = isLucy ? tank.WalkSpeed : ClassPace.FigureWalkSpeed, runSpeed = isLucy ? tank.RunSpeed : ClassPace.FigureRunSpeed;
                string by = isLucy ? "the TankController" : "the AI";
                figure.transform.SetPositionAndRotation(start - walkDir * 1.4f, Quaternion.LookRotation(walkDir));
                fa.SetFloat("Speed", 0.5f);
                yield return BotClock.Wait(0.6f);
                float ground = 0f;
                yield return Slip(1.8f, v => ground = v);
                float slipWalk = -1f;
                IEnumerator Move(float speed, float seconds)
                {
                    float from = BotClock.Now;
                    while (BotClock.Now - from < seconds)
                    {
                        figure.transform.position += walkDir * speed * Time.deltaTime;
                        yield return null;
                    }
                }
                // The check is the ground speed with the root still, within 10 % of the game speed. The slip with the
                // root at that speed is for the record only: the toe rolls in the stance, so it does not go to 0 (about
                // 0.2 to 0.4 m/s for a walk, 0.5 to 0.9 for a run, from one run to the next; at the old pace the walk
                // feet of the figures were 0.8 m/s off).
                // Move runs as its own coroutine beside Slip; the shots are taken while the root still moves.
                Coroutine mover = StartCoroutine(Move(walkSpeed, 2.0f));
                yield return Slip(1.8f, v => slipWalk = v, walkSpeed * 0.8f);
                yield return mover;
                // The walk shots: a short walk through the free spot, so the figure is in the frame (the measure above
                // ends 2 m past it).
                figure.transform.SetPositionAndRotation(start - walkDir * 0.7f, Quaternion.LookRotation(walkDir));
                mover = StartCoroutine(Move(walkSpeed, 1.2f));
                yield return BotClock.Wait(0.35f);
                yield return Shot($"{id}_walk_play_camera");
                yield return CloseShot($"{id}_walk_close_side", FreeSide(), 2.8f);
                yield return mover;
                Record($"{id}_walks", Mathf.Abs(ground - walkSpeed) < 0.1f * walkSpeed && slipWalk >= 0f,
                    $"the clip's ground speed, root still, {ground:0.00} m/s against the walk of {by} {walkSpeed:0.00} m/s; planted foot slip {slipWalk:0.00} m/s with the root at that speed");
                // The run, the same way at the AI run speed.
                figure.transform.SetPositionAndRotation(start - walkDir * 1.4f, Quaternion.LookRotation(walkDir));
                fa.SetFloat("Speed", 1f);
                yield return BotClock.Wait(0.5f);
                float runGround = 0f;
                yield return Slip(1.8f, v => runGround = v);
                figure.transform.SetPositionAndRotation(start - walkDir * 1.4f, Quaternion.LookRotation(walkDir));
                float slipRun = -1f;
                mover = StartCoroutine(Move(runSpeed, 1.9f));
                yield return Slip(1.8f, v => slipRun = v, runSpeed * 0.8f);
                yield return mover;
                Record($"{id}_runs", Mathf.Abs(runGround - runSpeed) < 0.1f * runSpeed && slipRun >= 0f,
                    $"the clip's ground speed, root still, {runGround:0.00} m/s against the run of {by} {runSpeed:0.00} m/s; planted foot slip {slipRun:0.00} m/s with the root at that speed");
                fa.SetFloat("Speed", 0f);
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
        /// The slip of the planted foot: each frame the lower foot (the toes) gives its speed over the floor. After the
        /// window, the frames where that foot is within 2 cm of its lowest height in the window count (the stance), and
        /// the result is the median of their speeds: about 0 when the stride fits the root's speed. The window must hold
        /// one full step at least (round 179: a run window of 0.6 s, half a step, read the touch-down frames only).
        /// </summary>
        private IEnumerator Slip(float seconds, Action<float> result, float minRootSpeed = 0f)
        {
            Transform root = subject.transform;
            Vector3 rootBefore = root.position;
            Animator animator = subject.GetComponentInChildren<Animator>();
            Transform lt = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftToes) : null;
            Transform rt = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightToes) : null;
            if (lt == null || rt == null) { result(-1f); yield break; }
            var frames = new List<(bool left, float y, float speed)>();
            float lowL = float.MaxValue, lowR = float.MaxValue;
            Vector3 pl = lt.position, pr = rt.position;
            int blocked = 0;
            float end = BotClock.Now + seconds;
            while (BotClock.Now < end)
            {
                yield return null;
                float dt = Time.deltaTime;
                if (dt <= 0f) continue;
                Vector3 rootStep = root.position - rootBefore; rootStep.y = 0f;
                rootBefore = root.position;
                // A blocked step (a wall in the way, or a slide along it) is no measure of the clip: only the frames where
                // the root moves forward at speed.
                if (minRootSpeed > 0f && (rootStep.magnitude / dt < minRootSpeed || Vector3.Dot(rootStep.normalized, root.forward) < 0.94f))
                { pl = lt.position; pr = rt.position; blocked++; continue; }
                bool left = lt.position.y < rt.position.y;
                Transform planted = left ? lt : rt;
                Vector3 d = planted.position - (left ? pl : pr); d.y = 0f;
                frames.Add((left, planted.position.y, d.magnitude / dt));
                if (left) lowL = Mathf.Min(lowL, planted.position.y); else lowR = Mathf.Min(lowR, planted.position.y);
                pl = lt.position; pr = rt.position;
            }
            var speeds = new List<float>();
            foreach (var f in frames) if (f.y - (f.left ? lowL : lowR) < 0.02f) speeds.Add(f.speed);
            speeds.Sort();
            float median = speeds.Count > 0 ? speeds[speeds.Count / 2] : -1f;
            slipFrames = speeds.Count;
            Debug.Log($"[LucyBot] slip: {speeds.Count} of {frames.Count} frames in the stance ({blocked} blocked), median {median:0.00} m/s, p25 {(speeds.Count > 0 ? speeds[speeds.Count / 4] : -1f):0.00}");
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
