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
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The gun holds bot (2026-10-31, the animation check): close shots of each armed figure's hands while it aims (the
    /// pistol in front of the fingers, the long-gun arms). AI TEST by default, LAB TEST with that level. Shots:
    /// shots/holds_&lt;n&gt;_&lt;figure&gt;_hands.png. Active only when BotArgs.Bot == "holds": tools/unity.sh play AiTest Main_Menu holds 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class HoldsPlaythrough : MonoBehaviour
    {
        [Serializable]
        private class Step
        {
            public string name;
            public bool ok;
            public string detail;
            public float t;
        }

        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad Pad;
            private void Update() => Pad?.Commit();
        }

        private readonly List<Step> steps = new List<Step>();
        private float startTime;
        private bool finished;
        private int shotIndex;
        private VirtualPad pad;
        private PadDriver padDriver;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "holds", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<HoldsPlaythrough>() != null) return;
            var go = new GameObject("[HoldsPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<HoldsPlaythrough>();
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
            string line = $"[HoldsBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            Directory.CreateDirectory(Path.Combine(BotArgs.RunDir, "shots"));
            GameManager gm = GameManager.Instance;
            string room = BotArgs.Level == "LabTest" ? "LabTest" : "AiTest";
            gm.LoadLevel(room);
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != room || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(1f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            if (room == "AiTest")
            {
                yield return Lineup(lucy);
                Finish();
                yield break;
            }
            if (room == "LabTest")
            {
                // The armed lab worker at rest, then the room reduced to him, with her in his sight.
                GameObject gunman = GameObject.Find("Lab_Gun2");
                Vector3 restChest = gunman.transform.position + Vector3.up * 1.2f;
                yield return Shot("Lab_Gun2_rest_side", restChest + gunman.transform.right * 2.1f, restChest);
                yield return Shot("Lab_Gun2_rest_front", restChest + gunman.transform.forward * 2.2f + gunman.transform.right * 0.9f, restChest);
                foreach (string other in new[] { "Lab_Knife", "Lab_Gun1" }) GameObject.Find(other)?.SetActive(false);
                lucy.GetComponent<TankController>().Teleport(gunman.transform.position + gunman.transform.forward * 6f, 0f);
                gunman.GetComponent<Enemy.SoldierBrain>().Alert("bot", callOthers: false);
            }
            int seen = 0;
            var done = new HashSet<Enemy.EnemyGun>();
            t0 = BotClock.Now;
            // Each armed figure, close at its hands while it aims at her (the telegraph): the grip of the gun.
            while (BotClock.Now - t0 < 40f)
            {
                foreach (Enemy.EnemyGun gun in FindObjectsByType<Enemy.EnemyGun>(FindObjectsSortMode.None))
                {
                    if (done.Contains(gun) || gun.Model == null) continue;
                    Enemy.DummyShooter shooter = gun.GetComponent<Enemy.DummyShooter>();
                    if (shooter == null || !shooter.HoldsSlot || shooter.CurrentAttack == Enemy.DummyShooter.Attack.Melee) continue;
                    Animator body = gun.GetComponent<Animator>();
                    Transform hand = body != null ? body.GetBoneTransform(HumanBodyBones.RightHand) : null;
                    if (hand == null) continue;
                    done.Add(gun);
                    yield return BotClock.Wait(0.4f); // the raise
                    Vector3 side = gun.transform.right;
                    yield return Shot($"{gun.name}_hands", hand.position + side * 0.7f + gun.transform.forward * 0.15f + Vector3.up * 0.1f, hand.position);
                    Vector3 chest = gun.transform.position + Vector3.up * 1.3f;
                    if (gun.name != "Guard_B") Debug.Log($"[HoldsBot] {gun.name} along the barrel from the grip point: {Along(gun, body)}");
                    if (gun.name != "Guard_B") Record($"{gun.name}_long_gun_hold", gun.RightGap < 0.03f && gun.LeftGap < 0.05f && gun.RightElbowLift < 0f, $"right gap {gun.RightGap:0.000} left gap {gun.LeftGap:0.000} elbow {gun.RightElbowLift:0.00} m; left arm {gun.LeftArm:0.000} m, shoulder to the wrist's target {gun.LeftReach:0.000} m, the grip {gun.LeftGripAlong:0.000} m ahead of the butt, the left shoulder forward {gun.LeftShoulderTurn:0} deg");
                    // The others hide for the two body shots (two soldiers overlapped).
                    var hidden = new List<Renderer>();
                    foreach (Enemy.EnemyGun other in FindObjectsByType<Enemy.EnemyGun>(FindObjectsSortMode.None))
                        if (other != gun) foreach (Renderer r in other.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
                    yield return Shot($"{gun.name}_side", chest + side * 2.1f + Vector3.up * 0.1f, chest);
                    yield return Shot($"{gun.name}_front", chest + gun.transform.forward * 2.2f + side * 0.9f + Vector3.up * 0.2f, chest);
                    foreach (Renderer r in hidden) if (r != null) r.enabled = true;
                    seen++;
                    if (gun.Model != null && gun.name.Contains("Guard"))
                        Record($"{gun.name}_pistol_grip_in_the_palm", gun.RightGap < 0.03f, $"gap {gun.RightGap:0.000}");
                }
                yield return null;
            }
            Record("hands_seen", seen > 0, $"guns={seen}");
            Finish();
        }

        /// <summary>Every armed kind (InvectorEnemyFactory.Kinds), the rifle soldier and the pistol guard first.</summary>
        public static readonly string[] LineupKinds =
        {
            "soldier_rifle", "guard_pistol", "guard_shield", "soldier_gas", "labworker_pistol", "power_armour_guns", "power_armour",
            "toll_guard_pistol", "labworker_knife", "soldier_pistol", "soldier_shotgun", "soldier_grenades", "soldier_shield",
        };

        /// <summary>
        /// The lineup (option B, 2026-11-17): the room's figures go, then every armed kind stands alone at the same place.
        /// Shots at the same angles from the figure: its first pose (rest), then with her 6 m ahead, while it aims (or
        /// winds up a strike): the hands, the side, the front. The hold is measured against Invector's grip (HoldOffsets,
        /// HoldCalibration): the turn of each hand from that grip, the gaps, the barrel against the line to its aim point.
        /// </summary>
        private IEnumerator Lineup(GameObject lucy)
        {
            Enemy.EnemyGun first = FindObjectsByType<Enemy.EnemyGun>(FindObjectsSortMode.InstanceID).FirstOrDefault();
            Vector3 spot = first != null ? first.transform.position : Vector3.zero;
            Quaternion facing = first != null ? Quaternion.Euler(0f, first.transform.eulerAngles.y, 0f) : Quaternion.identity;
            foreach (Enemy.DummyShooter figure in FindObjectsByType<Enemy.DummyShooter>(FindObjectsSortMode.None)) figure.gameObject.SetActive(false);
            foreach (Enemy.CivilianBrain figure in FindObjectsByType<Enemy.CivilianBrain>(FindObjectsSortMode.None)) figure.gameObject.SetActive(false);
            var tank = lucy.GetComponent<TankController>();
            int seen = 0;
            foreach (string kind in LineupKinds)
            {
                if (Resources.Load<GameObject>(Enemy.EnemySpawn.Folder + kind) == null) { Record($"{kind}_prefab", false, "no prefab (InvectorEnemyFactory.BuildEnemyPrefabs)"); continue; }
                tank.Teleport(spot - facing * Vector3.forward * 7f, 0f);
                string label = "Hold_" + kind;
                var marker = new GameObject(label);
                marker.SetActive(false);
                marker.transform.SetPositionAndRotation(spot, facing);
                marker.AddComponent<Enemy.EnemySpawn>().kind = kind;
                marker.SetActive(true); // the figure takes the marker's place and name
                yield return null;
                GameObject body = GameObject.Find(label);
                Enemy.EnemyGun gun = body != null ? body.GetComponent<Enemy.EnemyGun>() : null;
                Enemy.DummyShooter shooter = body != null ? body.GetComponent<Enemy.DummyShooter>() : null;
                Animator animator = body != null ? body.GetComponent<Animator>() : null;
                if (gun == null || shooter == null || animator == null) { Record($"{kind}_spawn", false, "no armed figure"); if (body != null) Destroy(body); continue; }
                yield return BotClock.Wait(0.8f);
                bool moves = MotionKinds.Contains(kind);
                yield return Views(kind, "rest", animator);
                if (moves) Clear(kind, "rest", animator, gun);
                if (moves)
                {
                    // The walk: she stands far ahead, he comes for her.
                    tank.Teleport(body.transform.position + body.transform.forward * 14f, 0f);
                    body.GetComponent<Enemy.SoldierBrain>()?.Alert("bot", callOthers: false);
                    Vector3 from = body.transform.position;
                    float w0 = BotClock.Now;
                    while (BotClock.Now - w0 < 8f && Vector3.Distance(from, body.transform.position) < 0.8f) yield return null;
                    yield return BotClock.Wait(0.3f);
                    yield return Views(kind, "walk", animator);
                    Clear(kind, "walk", animator, gun);
                }
                bool melee = shooter.WeaponKind == Enemy.DummyShooter.Weapon.Knife;
                // A gun that comes for her from afar aims on the way (the shield guard keeps his distance): wait for it first.
                float a0 = BotClock.Now;
                while (moves && !melee && BotClock.Now - a0 < 4f && !(shooter.HoldsSlot && shooter.CurrentAttack != Enemy.DummyShooter.Attack.Melee)) yield return null;
                if (!(shooter.HoldsSlot && (shooter.CurrentAttack == Enemy.DummyShooter.Attack.Melee) == melee))
                {
                    tank.Teleport(body.transform.position + body.transform.forward * 6f, 0f);
                    body.GetComponent<Enemy.SoldierBrain>()?.Alert("bot", callOthers: false);
                }
                float t0 = BotClock.Now, again = BotClock.Now;
                while (BotClock.Now - t0 < 30f && !(shooter.HoldsSlot && (shooter.CurrentAttack == Enemy.DummyShooter.Attack.Melee) == melee))
                {
                    if (!melee && BotClock.Now - again > 4f)
                    {
                        // He walked off to a firing spot: she stands in front of him again.
                        again = BotClock.Now;
                        tank.Teleport(body.transform.position + body.transform.forward * 6f, 0f);
                    }
                    yield return null;
                }
                if (!shooter.HoldsSlot) { Record($"{kind}_aims", false, "no aim in 30 s"); Destroy(body); yield return null; continue; }
                yield return BotClock.Wait(melee ? 0.25f : 0.6f);
                bool ok = MeasureHold(kind, gun, shooter, animator, melee, out string detail);
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                float scale = Scale(animator);
                yield return Shot($"{kind}_aim_hands", hand.position + body.transform.right * 0.7f * scale + body.transform.forward * 0.15f * scale + Vector3.up * 0.1f * scale, hand.position);
                yield return Views(kind, "aim", animator);
                if (moves) Clear(kind, "aim", animator, gun);
                Record($"{kind}_hold", ok, detail);
                if (moves && shooter.CanMelee)
                {
                    // The strike: she stands in his reach.
                    int swings = gun.MeleeSwings;
                    tank.Teleport(body.transform.position + body.transform.forward * 1.3f, 0f);
                    float s0 = BotClock.Now;
                    while (BotClock.Now - s0 < 12f && gun.MeleeSwings == swings) yield return null;
                    if (gun.MeleeSwings == swings) Record($"{kind}_strikes", false, "no strike in 12 s");
                    else
                    {
                        yield return BotClock.Wait(0.12f);
                        yield return Views(kind, "strike", animator);
                        Clear(kind, "strike", animator, gun);
                    }
                }
                seen++;
                Destroy(body);
                yield return null;
            }
            Record("hands_seen", seen == LineupKinds.Length, $"figures={seen} of {LineupKinds.Length}");
        }

        /// <summary>The kinds the lineup also shows walking and striking (the shield and the knife, Bruno 2026-11-17: "Yes, fix them first").</summary>
        public static readonly HashSet<string> MotionKinds = new HashSet<string> { "guard_shield", "soldier_shield", "labworker_knife" };

        /// <summary>
        /// Nothing cuts through the body: no point of his head, chest or right arm (and the gun) inside the shield's box
        /// (1 cm margin), and no point of the right arm or the knife across his face (a box ahead of the head, 0.12 m
        /// each side, 0.15 m up and down, 0.45 m forward, in a 1.8 m man).
        /// </summary>
        private void Clear(string kind, string moment, Animator animator, Enemy.EnemyGun gun)
        {
            float s = Scale(animator);
            Transform B(HumanBodyBones b) => animator.GetBoneTransform(b);
            var body = new List<(string, Vector3)>();
            foreach (var b in new[] { HumanBodyBones.Head, HumanBodyBones.Neck, HumanBodyBones.UpperChest, HumanBodyBones.Chest, HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm })
                if (B(b) != null) body.Add((b.ToString(), B(b).position));
            Transform ru = B(HumanBodyBones.RightUpperArm), rl = B(HumanBodyBones.RightLowerArm), rh = B(HumanBodyBones.RightHand);
            var arm = new List<(string, Vector3)>();
            for (int i = 0; i <= 4; i++) arm.Add(($"right upper arm {i}", Vector3.Lerp(ru.position, rl.position, i / 4f)));
            for (int i = 0; i <= 4; i++) arm.Add(($"right forearm {i}", Vector3.Lerp(rl.position, rh.position, i / 4f)));
            if (gun.Model != null) arm.Add(("gun", gun.Model.position));
            if (gun.Muzzle != null) arm.Add(("muzzle", gun.Muzzle.position));
            if (gun.Model != null && gun.Muzzle != null) arm.Add(("blade middle", Vector3.Lerp(gun.Model.position, gun.Muzzle.position, 0.5f)));
            var cuts = new List<string>();
            Enemy.EnemyShield shield = animator.GetComponent<Enemy.EnemyShield>();
            MeshFilter face = shield != null && shield.Model != null && shield.Model.gameObject.activeInHierarchy ? shield.Model.GetComponentInChildren<MeshFilter>() : null;
            if (face != null && face.sharedMesh != null)
            {
                Bounds box = face.sharedMesh.bounds;
                foreach (var (name, point) in body.Concat(arm))
                {
                    Vector3 local = face.transform.InverseTransformPoint(point);
                    Vector3 margin = new Vector3(0.01f / Mathf.Max(1e-4f, face.transform.lossyScale.x), 0.01f / Mathf.Max(1e-4f, face.transform.lossyScale.y), 0.01f / Mathf.Max(1e-4f, face.transform.lossyScale.z));
                    Bounds grown = box;
                    grown.Expand(margin * 2f);
                    if (grown.Contains(local)) cuts.Add("shield/" + name);
                }
            }
            Transform head = B(HumanBodyBones.Head);
            if (gun.UpperBody == 0 && head != null) // the knife
            {
                Vector3 forward = Vector3.ProjectOnPlane(animator.transform.forward, Vector3.up).normalized, right = Vector3.Cross(Vector3.up, forward);
                foreach (var (name, point) in arm)
                {
                    Vector3 d = point - head.position;
                    if (Mathf.Abs(Vector3.Dot(d, right)) < 0.12f * s && Vector3.Dot(d, forward) > 0f && Vector3.Dot(d, forward) < 0.45f * s && Mathf.Abs(d.y) < 0.15f * s)
                        cuts.Add("face/" + name);
                }
            }
            string armNote = "";
            bool held = true;
            if (shield != null && shield.HasPoints && shield.IsUp)
            {
                // The shield on the forearm: the left wrist on the handle, the forearm through the strap.
                Transform lh = B(HumanBodyBones.LeftHand), ll = B(HumanBodyBones.LeftLowerArm);
                float grip = Vector3.Distance(lh.position, shield.Grip);
                Vector3 along = lh.position - ll.position;
                float t = Mathf.Clamp01(Vector3.Dot(shield.Forearm - ll.position, along) / Mathf.Max(1e-6f, along.sqrMagnitude));
                float strap = Vector3.Distance(shield.Forearm, ll.position + along * t);
                held = grip < 0.03f * s && strap < 0.08f * s;
                armNote = $"; left wrist {grip:0.000} m from the handle, forearm {strap:0.000} m from the strap";
            }
            Record($"{kind}_{moment}_clear", cuts.Count == 0 && held, (cuts.Count == 0 ? "nothing through the body or across the face" : string.Join(", ", cuts)) + armNote);
        }

        /// <summary>The figure's height against a 1.8 m man (the power armour stands 2.9 m).</summary>
        private static float Scale(Animator animator)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            return head != null ? Mathf.Max(0.5f, (head.position.y - animator.transform.position.y) / 1.62f) : 1f;
        }

        private IEnumerator Views(string kind, string moment, Animator animator)
        {
            Transform t = animator.transform;
            float s = Scale(animator);
            Vector3 chest = t.position + Vector3.up * 1.3f * s;
            yield return Shot($"{kind}_{moment}_side", chest + t.right * 2.1f * s + Vector3.up * 0.1f * s, chest);
            yield return Shot($"{kind}_{moment}_front", chest + t.forward * 2.2f * s + t.right * 0.9f * s + Vector3.up * 0.2f * s, chest);
            Enemy.EnemyShield shield = animator.GetComponent<Enemy.EnemyShield>();
            if (shield != null && shield.HasPoints && shield.IsUp) // the hand on the handle, from over his left shoulder
                yield return Shot($"{kind}_{moment}_shieldarm", shield.Grip - t.right * 0.9f * s - t.forward * 0.5f * s + Vector3.up * 0.55f * s, shield.Grip);
        }

        /// <summary>
        /// The hold against Invector's grip on the same gun (HoldOffsets): the right hand's turn from the grip and the gun's
        /// gap from it, the left hand's turn and gap on leftHandIK (two hands), the barrel against the line to the aim point.
        /// The knife: the blade against the forearm's line, and the handle against the palm.
        /// </summary>
        private static bool MeasureHold(string kind, Enemy.EnemyGun gun, Enemy.DummyShooter shooter, Animator animator, bool melee, out string detail)
        {
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand), fore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform model = gun.Model, muzzle = gun.Muzzle != null ? gun.Muzzle : gun.Model;
            var text = new StringBuilder();
            float palmAngle = gun.TryHandFrame(true, out Vector3 fingers, out Vector3 palm) ? Vector3.Angle(palm, model.position - right.position) : -1f;
            string classId = Enemy.HoldOffsets.ClassOf(animator);
            Enemy.HoldOffsets set = Enemy.HoldOffsets.Load();
            if (melee)
            {
                // The knife against Invector's sword grip (HoldOffsets, upper body 0): the grip point and the blade's line.
                Enemy.HoldOffsets.Entry sword = set != null ? set.Find(classId, 0) : null;
                Transform gripPoint = FindDeep(model, "grip") ?? model;
                float forearm = Vector3.Angle(muzzle.position - gripPoint.position, right.position - fore.position);
                text.Append($"blade to forearm {forearm:0.0} deg, handle {Vector3.Distance(gripPoint.position, right.position):0.000} m from the wrist");
                if (sword == null) { detail = text.Append("; no Invector sword grip measured").ToString(); return false; }
                float length = Vector3.Distance(right.position, fore.position);
                Vector3 wantGrip = right.position + right.rotation * sword.gripOffset * length;
                Vector3 wantBlade = right.rotation * sword.GripRotation * Vector3.forward;
                float bladeTurn = Vector3.Angle(muzzle.position - gripPoint.position, wantBlade), gripGap = Vector3.Distance(gripPoint.position, wantGrip);
                text.Append($"; blade {bladeTurn:0.0} deg from Invector's sword, grip {gripGap:0.000} m off it, blade {Vector3.Distance(gripPoint.position, muzzle.position):0.00} m");
                detail = text.ToString();
                return bladeTurn < 5f && gripGap < 0.02f;
            }
            Enemy.HoldOffsets.Entry entry = set != null ? set.Find(classId, gun.UpperBody) : null;
            float aim = Vector3.Angle(muzzle.forward, shooter.AimPoint - muzzle.position);
            text.Append($"{classId} set {gun.UpperBody}, clip hold {gun.ClipHold}; barrel to the aim {aim:0.0} deg, the chest turned {gun.AimTurn:0.0} deg to it; palm to the grip {palmAngle:0.0} deg");
            if (entry == null) { detail = text.Append("; no Invector grip measured (HoldCalibration)").ToString(); return false; }
            float foreLength = Vector3.Distance(right.position, fore.position);
            Vector3 grip = right.position + right.rotation * entry.gripOffset * foreLength + model.rotation * entry.tuneGripOffset;
            float turnRight = Quaternion.Angle(right.rotation, model.rotation * Quaternion.Inverse(entry.GripRotation));
            float gapRight = Vector3.Distance(model.position, grip);
            text.Append($"; right hand {turnRight:0.0} deg from Invector's grip, gun {gapRight:0.000} m off it");
            bool ok = turnRight < 10f && gapRight < 0.03f && aim < 6f;
            Transform leftIK = FindDeep(model, "leftHandIK");
            if (leftIK != null && gun.FreeHandTo == null)
            {
                float turnLeft = Quaternion.Angle(left.rotation, entry.LeftHand(leftIK.rotation));
                float gapLeft = Vector3.Distance(left.position, leftIK.position + model.rotation * entry.tuneLeftOffset);
                text.Append($"; left hand {turnLeft:0.0} deg from the grip on leftHandIK, wrist {gapLeft:0.000} m off it");
                ok &= turnLeft < 30f && gapLeft < 0.03f;
            }
            else text.Append("; one hand");
            detail = text.ToString();
            return ok;
        }

        /// <summary>
        /// Where the support hand and the forend (the vendor shotgun's "pump" mesh) lie along the barrel, in metres from the
        /// gun's grip point (leftHandIK), for the log: the pump's two ends, the left wrist and the left hand's middle.
        /// </summary>
        private static string Along(Enemy.EnemyGun gun, Animator body)
        {
            Transform grip = FindDeep(gun.Model, "leftHandIK"), muzzle = FindDeep(gun.Model, "muzzle"), pump = FindDeep(gun.Model, "pump");
            Transform wrist = body.GetBoneTransform(HumanBodyBones.LeftHand);
            if (grip == null || wrist == null) return "no grip point";
            Vector3 axis = muzzle != null ? (muzzle.position - gun.Model.position).normalized : gun.Model.forward;
            var text = new StringBuilder();
            MeshFilter mesh = pump != null ? pump.GetComponent<MeshFilter>() : null;
            if (mesh != null && mesh.sharedMesh != null)
            {
                Bounds b = mesh.sharedMesh.bounds;
                float lo = float.MaxValue, hi = float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    float a = Vector3.Dot(pump.TransformPoint(corner) - grip.position, axis);
                    lo = Mathf.Min(lo, a); hi = Mathf.Max(hi, a);
                }
                text.Append($"pump {lo:0.000} to {hi:0.000} m, ");
            }
            Transform knuckle = body.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            text.Append($"left wrist {Vector3.Dot(wrist.position - grip.position, axis):0.000} m");
            if (knuckle != null) text.Append($", left knuckles {Vector3.Dot(knuckle.position - grip.position, axis):0.000} m");
            return text.ToString();
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>The same through a camera of its own at the place, looking at the point (a close look at a screen).</summary>
        private IEnumerator Shot(string moment, Vector3 from, Vector3 at)
        {
            Camera main = Camera.main;
            var obj = new GameObject("[ShotCamera]");
            Camera cam = obj.AddComponent<Camera>();
            if (main != null) cam.CopyFrom(main);
            cam.fieldOfView = 50f;
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
            var canvases = new List<Canvas>();
            if (SubtitleUI.Instance != null) canvases.Add(SubtitleUI.Instance.GetComponent<Canvas>());
            GlorifiedBody.UI.PlayerHud hud = FindFirstObjectByType<GlorifiedBody.UI.PlayerHud>();
            Canvas hudCanvas = hud != null ? hud.GetComponentInChildren<Canvas>() : null;
            if (hudCanvas != null) canvases.Add(hudCanvas);
            var old = canvases.Select(c => (c.renderMode, c.worldCamera, c.planeDistance)).ToList();
            foreach (Canvas c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.05f;
            }
            Canvas.ForceUpdateCanvases();
            try
            {
                ShotRenderer.Render(cam, Path.Combine(BotArgs.RunDir, "shots", $"holds_{shotIndex:00}_{moment}.png"), useTargetTexture: true);
            }
            finally
            {
                for (int i = 0; i < canvases.Count; i++)
                {
                    canvases[i].renderMode = old[i].renderMode;
                    canvases[i].worldCamera = old[i].worldCamera;
                    canvases[i].planeDistance = old[i].planeDistance;
                }
            }
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (padDriver != null) { GameInput.ClearBotSource(padDriver.Pad); Destroy(padDriver.gameObject); }
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"screens\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[HoldsBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("holds_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
