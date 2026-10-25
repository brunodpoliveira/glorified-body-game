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
    /// The clips are placeholders: no CPR or drag clip exists in the vendor set or in RFab's motion presets (RFAB
    /// gap 13). Numbers PROPOSED.
    /// </summary>
    [RequireComponent(typeof(SoldierBrain), typeof(Health))]
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

        private SoldierBrain brain;
        private Health health;
        private Transform player;
        private float lastSeen;
        private Transform patient;
        private Vector3 dragTarget;
        private Quaternion patientTurn;
        private float stepTimer;
        private Transform spine, chest, leftArm, rightArm, leftFore, rightFore;
        private static readonly System.Collections.Generic.HashSet<Transform> tended = new System.Collections.Generic.HashSet<Transform>();

        public Step Current { get; private set; } = Step.None;
        public Transform Patient => patient;
        public float Dragged { get; private set; }
        public int Compressions { get; private set; }
        public bool Active => Current != Step.None;

        private void Awake()
        {
            brain = GetComponent<SoldierBrain>();
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
            }
            lastSeen = Time.time;
        }

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
            Bark.Say(gameObject, "MAN DOWN!"); // PLACEHOLDER line (the writer owns the barks)
            return true;
        }

        /// <summary>Drops the body and goes back to the fight.</summary>
        public void Stop(string why)
        {
            if (!Active) return;
            RunTelemetry.Event("care_stop", $"{name}:{Current}:{why}");
            Current = Step.None;
            if (patient != null) tended.Remove(patient);
            patient = null;
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
                if (Time.time - lastSeen >= unseenSeconds && brain.CanCare) TryFindPatient();
                return;
            }
            if (sees) { Stop("saw her"); return; }
            if (patient == null) { Stop("no body"); return; }

            float dt = Time.deltaTime;
            stepTimer -= dt;
            switch (Current)
            {
                case Step.GoToBody:
                    if (Flat(transform.position, BodySide()) <= 0.8f || stepTimer <= 0f) StartDrag();
                    break;
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
            if (Current != Step.Cpr) return;
            // The compressions: the upper body pumps down over the chest, the arms straight down.
            float phase = Time.time * compressionsPerMinute / 60f;
            float pump = Mathf.Max(0f, Mathf.Sin(phase * Mathf.PI * 2f));
            int count = Mathf.FloorToInt(phase);
            if (count != lastCount) { Compressions++; lastCount = count; }
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
                if (d >= bestDistance) continue;
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
                if (other == this || other.health.IsDead || other.Active || !other.brain.CanCare) continue;
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
            // He holds the body by its chest: it keeps its turn from the grab, its chest follows his hands.
            patientTurn = Quaternion.Inverse(transform.rotation) * patient.rotation;
            stepTimer = dragDistance / dragSpeed + 3f;
            restrafeAt = 0f;
            RunTelemetry.Event("care_drag", $"{name}>{patient.name} to {dragTarget.x:0.0},{dragTarget.z:0.0}");
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
            Vector3 before = patient.position;
            patient.rotation = transform.rotation * patientTurn;
            Vector3 pull = grip - PatientChest();
            pull.y = 0f;
            patient.position = before + pull;
            Dragged += Flat(before, patient.position);
        }

        private void StartCpr()
        {
            Current = Step.Cpr;
            stepTimer = cprSeconds;
            brain.Body.Stop();
            brain.Body.Crouch(true);
            brain.Body.Face(PatientChest() - transform.position);
            RunTelemetry.Event("care_cpr", $"{name}>{patient.name} dragged {Dragged:0.0} m");
            Bark.Say(gameObject, "STAY WITH ME!"); // PLACEHOLDER line
        }

        /// <summary>Debug (F12): the care counts her as unseen, so a colleague tends a body in an open room (Bruno, 2026-10-24: "B9 is really hard to test").</summary>
        public static bool DebugHidden;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDebugHidden() => DebugHidden = false;

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
