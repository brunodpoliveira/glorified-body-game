using UnityEngine;
using UnityEngine.AI;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// A living enemy tends a dead colleague (Bruno, 2026-10-24: "cpr animation (living npc does it to dead npc)",
    /// "dragging the body anim (same rule)"). When a colleague lies dead within careRange and the enemy has not seen
    /// Lucy for unseenSeconds (she hides), the nearest free colleague goes to the body, drags it to a spot away from
    /// her (crouched, walking backward, the body pulled by the shoulders), then kneels at its chest and does CPR
    /// (a crouch held, the upper body pumps at 110 a minute). The CPR never brings the colleague back (PROPOSED).
    /// When it sees her, or anything hurts it, it drops the body and fights. One carer per body.
    /// The unarmed civilians tend bodies too (Bruno, 2026-10-25): a lab worker by the same rule, a nurse even with
    /// her in plain sight (ICarer.CareIgnoresHer). Only a hurt stops the nurse.
    /// A body with a ragdoll goes limp for the drag (EnemyRagdoll.BeginDrag): the carer holds it by the shoulders
    /// and the legs trail. At the CPR it lets go, and the body settles and freezes again (Bruno, 2026-10-25).
    /// The clips are placeholders: no CPR or drag clip exists in the vendor set or in RFab's motion presets (RFAB
    /// gap 13). Numbers PROPOSED.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class CorpseCare : MonoBehaviour
    {
        public enum Step { None, GoToBody, Drag, Cpr }

        [SerializeField, Min(1f)] private float careRange = 15f;
        [SerializeField, Min(0f)] private float unseenSeconds = 3f;
        [SerializeField, Min(0.5f)] private float dragDistance = 4f;
        [SerializeField, Min(0.5f)] private float dragSpeed = 1.0f;     // metres per second, for the time limit only
        [SerializeField, Min(1f)] private float cprSeconds = 12f;
        [SerializeField, Min(0.5f)] private float compressionsPerMinute = 110f;
        [SerializeField, Range(0f, 40f)] private float compressionDegrees = 14f;
        [Tooltip("The kneel of the CPR: a vendor clip (Pick_Down), held at KneelFrom. The factory sets it (the prefab is not in git).")]
        [SerializeField] private AnimationClip kneelClip;

        /// <summary>The kneel of the vendor's Pick_Down: one knee on the floor, the hips 0.44 m up (ClipProbe, 2026-10-31). CprLegs puts the other knee down.</summary>
        public const float KneelFrom = 0.42f, KneelLength = 0.03f;

        /// <summary>
        /// The CPR's arms (2026-10-31, Bruno: the CPR pose "sometimes comes extremely uncanny"; "do them now"): both arms
        /// straight down to the chest, the head down, as humanoid muscles, so one set fits every rig (the old code turned
        /// the bones along +y, which only some rigs have). Over the vendor kneel. PROPOSED (picked from the PoseProbe sheet
        /// over Pick_Down, set B: ~/MEGA/glorified-body-renders/game/animation/shots/cpr_probe_sheet.png).
        /// </summary>
        public static readonly (string muscle, float value)[] CprArms =
        {
            ("Shoulder Down-Up", -0.3f),
            ("Shoulder Front-Back", 0.5f),
            ("Arm Down-Up", -0.45f),
            ("Arm Front-Back", -0.8f),
            ("Arm Twist In-Out", 0f),
            ("Forearm Stretch", 1f),
            ("Forearm Twist In-Out", 0f),
            ("Hand Down-Up", 0.6f),
            ("Hand In-Out", 0f),
            ("Head Nod Down-Up", -0.5f),
            ("Neck Nod Down-Up", -0.3f),
        };

        /// <summary>
        /// The CPR's legs (2026-11-02, Bruno: "both knees"): the vendor kneel has one knee up. Both thighs straight down,
        /// the shins flat on the floor behind. PoseProbe over Pick_Down at 0.43 s: the knees 0.07 to 0.09 m over the
        /// floor, the ankles about 0.09 m (~/MEGA/glorified-body-renders/game/animation/shots/cpr_knees_probe_sheet2.png).
        /// </summary>
        public static readonly (string muscle, float value)[] CprLegs =
        {
            ("Upper Leg Front-Back", 0f),
            ("Upper Leg In-Out", 0f),
            ("Upper Leg Twist In-Out", 0f),
            ("Lower Leg Stretch", -0.25f),
        };
        // The spine and chest bend (negative leans forward: the PoseProbe sheet of 2026-10-31, set B), and the bob of a
        // compression (more forward at the push).
        private const float CprBend = -0.5f, CprPump = -0.15f;

        public AnimationClip KneelClip => kneelClip;
        public void SetKneelClip(AnimationClip clip) => kneelClip = clip;

        private HumanPoseHandler poseHandler;
        private HumanPose humanPose;
        private int[] cprIndices;
        private float[] cprValues;
        private int spineBend = -1, chestBend = -1;
        private Transform hips;
        private Transform leftHand, rightHand;
        private GlorifiedBody.Characters.PoseOverride kneel;

        private ICarer brain;
        private Health health;
        private Transform player;
        private float lastSeen;
        private Transform patient;
        private Vector3 dragTarget;
        private Quaternion patientTurn;
        private EnemyRagdoll patientRagdoll; // set while the body hangs limp from his hands
        private Vector3 lastGrip;
        private float stepTimer;
        private Transform spine, chest, leftArm, rightArm, leftFore, rightFore;
        private static readonly System.Collections.Generic.HashSet<Transform> tended = new System.Collections.Generic.HashSet<Transform>();

        public Step Current { get; private set; } = Step.None;
        public Transform Patient => patient;
        public float Dragged { get; private set; }
        /// <summary>True while the body hangs limp from his hands (a ragdoll drag), false for the rigid drag.</summary>
        public bool LimpDrag => patientRagdoll != null;
        public int Compressions { get; private set; }
        public bool Active => Current != Step.None;
        /// <summary>The sight rule allows a start now: she is unseen long enough, or the carer does not mind her.</summary>
        private bool MayStart => brain.CareIgnoresHer || Time.time - lastSeen >= unseenSeconds;

        private void Awake()
        {
            brain = GetComponent<ICarer>();
            health = GetComponent<Health>();
            Animator animator = GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.UpperChest);
                leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                leftFore = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rightFore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (animator.avatar != null) poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
                var set = new System.Collections.Generic.List<(string, float)>(CprArms) { ("Spine Front-Back", 0f), ("Chest Front-Back", 0f) };
                set.AddRange(CprLegs);
                GlorifiedBody.Characters.PleaHands.Resolve(set.ToArray(), out cprIndices, out cprValues);
                spineBend = System.Array.IndexOf(cprIndices, System.Array.IndexOf(HumanTrait.MuscleName, "Spine Front-Back"));
                chestBend = System.Array.IndexOf(cprIndices, System.Array.IndexOf(HumanTrait.MuscleName, "Chest Front-Back"));
            }
            lastSeen = Time.time;
        }

        private void OnDestroy() => poseHandler?.Dispose();

        private void OnEnable()
        {
            health.OnDamaged ??= new UnityEngine.Events.UnityEvent<float>();
            health.OnDamaged.AddListener(OnHurt);
        }

        private void OnDisable()
        {
            health.OnDamaged?.RemoveListener(OnHurt);
            Stop("disabled");
        }

        private void OnHurt(float amount) => Stop("hurt");

        /// <summary>A body that a load puts back: no one tends it again (PROPOSED).</summary>
        public static void MarkTended(Transform body)
        {
            if (body != null) tended.Add(body);
        }

        /// <summary>Starts the care of this body now (the rule calls it; tests call it directly).</summary>
        public bool Begin(Transform body)
        {
            if (body == null || health.IsDead || Active || tended.Contains(body)) return false;
            if (!brain.TryBeginCare()) return false;
            patient = body;
            patientChest = null;
            tended.Add(body);
            Current = Step.GoToBody;
            Dragged = 0f;
            Compressions = 0;
            stepTimer = 8f;
            brain.Body.MoveTo(BodySide(), MoveSpeed.Run);
            RunTelemetry.Event("care_start", $"{name}>{body.name}");
            if (brain.CareStartLine != null) Bark.Say(gameObject, brain.CareStartLine);
            return true;
        }

        /// <summary>Drops the body and goes back to the fight.</summary>
        public void Stop(string why)
        {
            if (!Active) return;
            RunTelemetry.Event("care_stop", $"{name}:{Current}:{why}");
            LetGo();
            Current = Step.None;
            if (patient != null) tended.Remove(patient);
            patient = null;
            if (kneel != null && kneel.Current == kneelClip) kneel.Stop();
            brain.Body.Crouch(false);
            brain.EndCare();
        }

        private void Update()
        {
            if (health.IsDead) { Stop("dead"); return; }
            if (player == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p != null) player = p.transform;
            }
            bool sees = SeesHer();
            if (sees) lastSeen = Time.time;

            if (!Active)
            {
                if (MayStart && brain.CanCare) TryFindPatient();
                return;
            }
            if (sees && !brain.CareIgnoresHer) { Stop("saw her"); return; }
            if (patient == null) { Stop("no body"); return; }

            float dt = Time.deltaTime;
            stepTimer -= dt;
            switch (Current)
            {
                case Step.GoToBody:
                {
                    float gap = Flat(transform.position, BodySide());
                    if (gap <= 0.8f) StartDrag();
                    // No way to the body in time: the care ends. A drag from far off pulled the body across the
                    // room (found 2026-10-25: a desk on the path, the nurse 5 m short).
                    else if (stepTimer <= 0f) { if (gap <= ReachGap) StartDrag(); else Stop($"no way to the body: {gap:0.0} m short at {transform.position.x:0.0},{transform.position.z:0.0}, the spot {BodySide().x:0.0},{BodySide().z:0.0}"); }
                    break;
                }
                case Step.Drag:
                    DragTick(dt);
                    break;
                case Step.Cpr:
                    if (stepTimer <= 0f) Stop("cpr over");
                    break;
            }
        }

        private void LateUpdate()
        {
            if (Current == Step.Drag) { DragHands(); return; }
            if (Current != Step.Cpr) return;
            // The compressions: the upper body pumps down over the chest, the arms straight down.
            float phase = Time.time * compressionsPerMinute / 60f;
            float pump = Mathf.Max(0f, Mathf.Sin(phase * Mathf.PI * 2f));
            int count = Mathf.FloorToInt(phase);
            if (count != lastCount) { Compressions++; lastCount = count; }
            if (poseHandler != null && kneel != null && kneel.Playing)
            {
                // The kneel holds; the arms and the bob are muscles (every rig the same).
                if (spineBend >= 0) cprValues[spineBend] = CprBend + CprPump * pump;
                if (chestBend >= 0) cprValues[chestBend] = CprBend * 0.8f + CprPump * 0.5f * pump;
                GlorifiedBody.Characters.PleaHands.Apply(poseHandler, ref humanPose, cprIndices, cprValues, 1f, hips);
                return;
            }
            if (spine != null) spine.localRotation *= Quaternion.Euler(20f + compressionDegrees * pump, 0f, 0f);
            if (chest != null) chest.localRotation *= Quaternion.Euler(10f, 0f, 0f);
            // The arms: straight from the shoulders to the chest of the body (the bone's own axis is +y on this rig).
            Vector3 target = PatientChest() + Vector3.up * (0.05f + 0.06f * (1f - pump));
            PointBone(leftArm, leftFore, target);
            PointBone(rightArm, rightFore, target);
        }
        private int lastCount = -1;

        private static void PointBone(Transform upper, Transform fore, Vector3 target)
        {
            if (upper == null) return;
            Vector3 want = target - upper.position;
            if (want.sqrMagnitude < 0.0001f) return;
            upper.rotation = Quaternion.FromToRotation(upper.rotation * Vector3.up, want.normalized) * upper.rotation;
            if (fore != null) fore.rotation = Quaternion.FromToRotation(fore.rotation * Vector3.up, want.normalized) * fore.rotation;
        }
        private float restrafeAt;

        private void TryFindPatient()
        {
            Transform best = null;
            float bestDistance = careRange;
            foreach (EnemyMorale other in FindObjectsByType<EnemyMorale>(FindObjectsSortMode.None))
            {
                if (other == null || other.gameObject == gameObject) continue;
                Health h = other.GetComponent<Health>();
                if (h == null || !h.IsDead || tended.Contains(other.transform)) continue;
                float d = Flat(transform.position, other.transform.position);
                if (d >= bestDistance || !GlorifiedBody.Rooms.Room.Same(transform.position, other.transform.position)) continue; // a body in its own room (2026-11-02)
                // The nearest free colleague takes it: another live one nearer to the body goes instead.
                if (NearerCarer(other.transform, d)) continue;
                best = other.transform;
                bestDistance = d;
            }
            if (best != null) Begin(best);
        }

        private bool NearerCarer(Transform body, float myDistance)
        {
            foreach (CorpseCare other in FindObjectsByType<CorpseCare>(FindObjectsSortMode.None))
            {
                if (other == this || other.health.IsDead || other.Active || !other.brain.CanCare || !other.MayStart) continue;
                if (Flat(other.transform.position, body.position) < myDistance - 0.01f) return true;
            }
            return false;
        }

        /// <summary>The kneeling spot beside the body's chest, on the near side.</summary>
        private Vector3 BodySide()
        {
            Vector3 chestPoint = PatientChest();
            Vector3 to = transform.position - chestPoint;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) to = Vector3.forward;
            Vector3 spot = chestPoint + to.normalized * 0.65f;
            spot.y = patient.position.y;
            // On the walkable floor: a body against a wall or a desk has its near side off the NavMesh.
            if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 0.8f, NavMesh.AllAreas)) spot = hit.position;
            return spot;
        }

        /// <summary>The chest of the body (its chest bone), else its root.</summary>
        private Vector3 PatientChest()
        {
            if (patient == null) return transform.position;
            if (patientChest == null)
            {
                Animator a = patient.GetComponent<Animator>();
                patientChest = a != null && a.isHuman ? (a.GetBoneTransform(HumanBodyBones.Chest) ?? a.GetBoneTransform(HumanBodyBones.Spine)) : null;
            }
            return patientChest != null ? patientChest.position : patient.position;
        }
        private Transform patientChest;

        private void StartDrag()
        {
            Current = Step.Drag;
            brain.Body.Stop();
            brain.Body.Crouch(true);
            // The drag goes away from her: the farthest of eight spots dragDistance away, on the NavMesh.
            Vector3 away = player != null ? (patient.position - player.position) : -transform.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            dragTarget = patient.position + away.normalized * dragDistance;
            float best = -1f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 candidate = patient.position + Quaternion.Euler(0f, i * 45f, 0f) * away.normalized * dragDistance;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
                float score = player != null ? Flat(hit.position, player.position) : 0f;
                if (score > best) { best = score; dragTarget = hit.position; }
            }
            // A ragdoll body goes limp and hangs from his hands by the shoulders. Else he holds it rigid by its
            // chest: it keeps its turn from the grab, its chest follows his hands.
            EnemyRagdoll ragdoll = patient.GetComponent<EnemyRagdoll>();
            patientRagdoll = ragdoll != null && ragdoll.BeginDrag() ? ragdoll : null;
            if (patientRagdoll != null) lastGrip = patientRagdoll.GripPoint;
            patientTurn = Quaternion.Inverse(transform.rotation) * patient.rotation;
            stepTimer = dragDistance / dragSpeed + 3f;
            restrafeAt = 0f;
            RunTelemetry.Event("care_drag", $"{name}>{patient.name} to {dragTarget.x:0.0},{dragTarget.z:0.0}");
            if (brain.DragLine != null) Bark.Say(gameObject, brain.DragLine);
        }

        private void DragTick(float dt)
        {
            // He faces the body and walks backward to the target; the body follows his hands.
            Vector3 back = dragTarget - transform.position;
            back.y = 0f;
            Vector3 facing = patient.position - transform.position;
            facing.y = 0f;
            if (back.magnitude <= 0.3f || stepTimer <= 0f)
            {
                StartCpr();
                return;
            }
            // The vendor crouch strafe: he walks backward, facing the body (its root motion moves him).
            if (Time.time >= restrafeAt)
            {
                restrafeAt = Time.time + 0.3f;
                brain.Body.StrafeTo(dragTarget, facing.sqrMagnitude > 0.01f ? facing.normalized : transform.forward);
            }
            Vector3 grip = transform.position + transform.forward * 0.6f;
            if (patientRagdoll != null)
            {
                // His hands at the crouch: the shoulders come up off the floor, the hips and legs drag.
                patientRagdoll.DragTo(grip + Vector3.up * LimpGripHeight);
                Vector3 now = patientRagdoll.GripPoint;
                Dragged += Flat(lastGrip, now);
                lastGrip = now;
                return;
            }
            Vector3 before = patient.position;
            patient.rotation = transform.rotation * patientTurn;
            Vector3 pull = grip - PatientChest();
            pull.y = 0f;
            patient.position = before + pull;
            Dragged += Flat(before, patient.position);
        }

        private const float LimpGripHeight = 0.5f; // metres over his feet: the hands of a crouched carer

        /// <summary>
        /// The drag (Bruno, 2026-10-27: "the body and the dragger's arm" need refinement): both hands on the body's
        /// shoulders, by the two-bone reach of the gun hold, the elbows down and out. Over the vendor crouch walk.
        /// </summary>
        private void DragHands()
        {
            if (patient == null || leftArm == null || rightArm == null) return;
            Animator body = patient.GetComponent<Animator>();
            Transform left = body != null && body.isHuman ? body.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
            Transform right = body != null && body.isHuman ? body.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
            if (left == null || right == null) return;
            Vector3 side = transform.right;
            // His left hand takes the shoulder on his left: the body lies with its head to him.
            Transform toLeft = Vector3.Dot(left.position - right.position, side) < 0f ? left : right;
            Transform toRight = toLeft == left ? right : left;
            EnemyGun.Reach(leftArm, leftFore, leftHand, null, toLeft.position, -Vector3.up - side * 0.6f, Vector3.zero);
            EnemyGun.Reach(rightArm, rightFore, rightHand, null, toRight.position, -Vector3.up + side * 0.6f, Vector3.zero);
        }
        private const float ReachGap = 1.5f;       // metres from the kneeling spot: near enough to take the body

        /// <summary>A limp body falls from his hands; it settles and freezes again (EnemyRagdoll).</summary>
        private void LetGo()
        {
            if (patientRagdoll != null) patientRagdoll.EndDrag();
            patientRagdoll = null;
        }

        private void StartCpr()
        {
            LetGo();
            Current = Step.Cpr;
            stepTimer = cprSeconds;
            brain.Body.Stop();
            brain.Body.Crouch(true);
            brain.Body.Face(PatientChest() - transform.position);
            // The kneel: the vendor's Pick_Down held at its low point (one knee down), the arms and the legs over it in LateUpdate (CprLegs: both knees down).
            Animator animator = GetComponent<Animator>();
            if (kneelClip != null && animator != null)
            {
                kneel = animator.GetComponent<GlorifiedBody.Characters.PoseOverride>() ?? animator.gameObject.AddComponent<GlorifiedBody.Characters.PoseOverride>();
                kneel.Play(kneelClip, loop: true, fade: 0f, from: KneelFrom, length: KneelLength);
            }
            RunTelemetry.Event("care_cpr", $"{name}>{patient.name} dragged {Dragged:0.0} m");
            if (brain.CprLine != null) Bark.Say(gameObject, brain.CprLine);
        }

        /// <summary>Debug (F12): the care counts her as unseen, so a colleague tends a body in an open room (Bruno, 2026-10-24: "B9 is really hard to test").</summary>
        public static bool DebugHidden;

        // Off again in each room (2026-10-26: the debug flag followed her into the next room).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDebugHidden()
        {
            DebugHidden = false;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnRoomLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnRoomLoaded;
        }

        private static void OnRoomLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) DebugHidden = false;
        }

        private bool SeesHer()
        {
            if (player == null || DebugHidden) return false;
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 her = player.position + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight;
            if (Vector3.Distance(eye, her) > 20f) return false;
            return !Physics.Linecast(eye, her, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => tended.Clear();
    }
}
