using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The shield of the shield + pistol soldier (Bruno, 2026-10-24). From the front (guardArc each side of his
    /// facing) it stops mk_warspite, mk_erebus and returned bullets: no damage, no stagger, a spark and a clang.
    /// The side and the back are open. mk_orzabal still lifts him, and the lift tears the shield away: it falls to
    /// the floor and he fights on without it. The model is the vendor's great shield with its scripts removed
    /// (InvectorEnemyFactory). It does not follow a hand bone: it stands in front of his left side, upright, so the
    /// guard reads the same in every animation. Numbers PROPOSED.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class EnemyShield : MonoBehaviour, IFrontGuard
    {
        [SerializeField] private Transform model;
        [SerializeField] private Quaternion modelRotation = Quaternion.identity; // the model's face to +z, its height to +y
        [SerializeField, Range(10f, 120f)] private float guardArc = 75f;
        [SerializeField] private Vector3 offset = new Vector3(-0.18f, 1.05f, 0.42f); // metres: left, up, forward

        private Health health;
        private TelekinesisLift lift;

        public bool IsUp => isActiveAndEnabled && model != null && !Dropped && health != null && !health.IsDead && !(lift != null && lift.IsLifted);
        public bool Dropped { get; private set; }
        public int BlockCount { get; private set; }
        public string LastBlocked { get; private set; } = "";
        public Transform Model => model;

        public void Bind(Transform shieldModel, Quaternion faceRotation)
        {
            model = shieldModel;
            modelRotation = faceRotation;
        }

        // The shield on the forearm (2026-11-17, Bruno: "Yes, fix them first"): the left hand holds a grip on its back, the
        // forearm lies along its back toward its left edge. In the aim it moves left and in, so the pistol arm fires past
        // its right edge and its face still covers the chest. Numbers PROPOSED, in metres for a 1.8 m man.
        [SerializeField] private Vector3 aimOffset = new Vector3(-0.26f, 1.02f, 0.44f);
        [SerializeField, Range(-30f, 30f)] private float aimYaw = 8f; // degrees: the face turns this much to his right in the aim
        private Vector3 gripLocal, forearmLocal; // in the shield's frame (x his right, y up, z forward), from the model's place
        private bool hasPoints;
        private DummyShooter shooter;
        private float aimBlend;

        /// <summary>The shield's frame now: x his right, y up (the long side), z out of the face (the thin side), from its mesh.</summary>
        public Quaternion Frame => model != null ? model.rotation * frameInModel : transform.rotation;
        private Quaternion frameInModel = Quaternion.identity; // that frame in the model's own axes
        /// <summary>The handle on the shield's back, where the left hand holds it.</summary>
        public Vector3 Grip => model != null ? model.position + Frame * gripLocal : transform.position;
        /// <summary>The cuff behind the handle, toward his left: the left forearm lies through it near the wrist.</summary>
        public Vector3 Forearm => model != null ? model.position + Frame * forearmLocal : transform.position;
        public bool HasPoints => hasPoints;

        private void Awake()
        {
            health = GetComponent<Health>();
            shooter = GetComponent<DummyShooter>();
            FindPoints();
        }

        /// <summary>
        /// The handle and the strap from the shield's mesh: its box in the shield's frame. The handle sits on the back at
        /// the middle, a little over the half height; the cuff 7 cm left of it and 6 cm farther back. A model with
        /// the children "grip" and "forearm" gives its own.
        /// </summary>
        private void FindPoints()
        {
            if (model == null) return;
            FindFrame();
            Quaternion frame = Frame;
            Transform grip = Find(model, "grip"), strap = Find(model, "forearm");
            if (grip != null && strap != null)
            {
                gripLocal = Quaternion.Inverse(frame) * (grip.position - model.position);
                forearmLocal = Quaternion.Inverse(frame) * (strap.position - model.position);
                hasPoints = true;
                return;
            }
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 local = Quaternion.Inverse(frame) * (mf.transform.TransformPoint(corner) - model.position);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
            if (min.x > max.x) return;
            Vector3 middle = (min + max) * 0.5f, size = max - min;
            gripLocal = new Vector3(middle.x, middle.y + size.y * 0.06f, min.z - GripBehind);
            forearmLocal = gripLocal + new Vector3(-StrapAside, 0f, -StrapBehind);
            hasPoints = true;
        }

        // The left arm that carries it (EnemyGun.SupportHandOff): the wrist on the handle and the elbow, in his frame, in
        // metres for a 1.8 m man. At rest the shield covers his chest; in the aim the arm goes left, so the pistol arm fires
        // past the shield's right edge. PROPOSED.
        public static readonly Vector3 RestHand = new Vector3(-0.18f, 1.11f, 0.34f), RestElbow = new Vector3(-0.36f, 1.02f, 0.10f);
        public static readonly Vector3 AimHand = new Vector3(-0.26f, 1.1f, 0.35f), AimElbow = new Vector3(-0.44f, 1.0f, 0.10f);
        private const float MaxStrapTurn = 25f; // degrees the shield may lean off his front to lay its cuff on the forearm

        /// <summary>How far the shield is in its aim pose now (0 at rest, 1 in the aim).</summary>
        public float AimBlend => Mathf.SmoothStep(0f, 1f, aimBlend);

        /// <summary>
        /// The shield on the left forearm: its handle at the wrist, its face to his front (and aimYaw to his right in the
        /// aim), then leaned at most MaxStrapTurn so the cuff behind the handle lies on the forearm. EnemyGun calls it after
        /// the arm's IK, so the shield follows the arm in every pose (rest, walk, aim, strike).
        /// </summary>
        public void HoldOn(Vector3 wrist, Vector3 elbow, Vector3 forward)
        {
            if (Dropped || model == null || !hasPoints) return;
            Vector3 face = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (face.sqrMagnitude < 1e-6f || (elbow - wrist).sqrMagnitude < 1e-6f) return;
            Quaternion frame = Quaternion.LookRotation(Quaternion.AngleAxis(aimYaw * AimBlend, Vector3.up) * face.normalized, Vector3.up);
            Vector3 cuff = (forearmLocal - gripLocal).normalized;
            Quaternion lean = Quaternion.FromToRotation(frame * cuff, (elbow - wrist).normalized);
            float angle = Quaternion.Angle(Quaternion.identity, lean);
            if (angle > MaxStrapTurn) lean = Quaternion.Slerp(Quaternion.identity, lean, MaxStrapTurn / angle);
            frame = lean * frame;
            model.rotation = frame * Quaternion.Inverse(frameInModel);
            model.position = wrist - frame * gripLocal;
        }

        /// <summary>
        /// The shield's own frame from its mesh, in the model's axes: the face out of the thinnest side (toward his front
        /// as built), up along the longest side.
        /// </summary>
        private void FindFrame()
        {
            // The mesh's own box (a child may be turned and scaled unevenly: the model's box axes lie, so take the mesh's).
            MeshFilter mesh = null;
            float best = -1f;
            foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Vector3 s = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
                float volume = Mathf.Abs(s.x * s.y * s.z);
                if (volume > best) { best = volume; mesh = mf; }
            }
            if (mesh == null) return;
            Vector3 size = Vector3.Scale(mesh.sharedMesh.bounds.size, mesh.transform.lossyScale);
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            int thin = 0, tall = 0;
            for (int i = 1; i < 3; i++)
            {
                if (Mathf.Abs(size[i]) < Mathf.Abs(size[thin])) thin = i;
                if (Mathf.Abs(size[i]) > Mathf.Abs(size[tall])) tall = i;
            }
            if (thin == tall) return;
            Quaternion toModel = Quaternion.Inverse(model.rotation) * mesh.transform.rotation;
            Vector3 face = toModel * axes[thin], up = toModel * axes[tall];
            // The signs from the build's turn (modelRotation: the face to his +z, the height to his +y).
            if ((modelRotation * face).z < 0f) face = -face;
            if ((modelRotation * up).y < 0f) up = -up;
            frameInModel = Quaternion.LookRotation(face, up);
        }

        private const float GripBehind = 0.035f, StrapAside = 0.07f, StrapBehind = 0.06f; // metres: the handle behind the back, the cuff behind and left of it

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void OnEnable()
        {
            health.OnDeath ??= new UnityEngine.Events.UnityEvent();
            health.OnDeath.AddListener(Drop);
            if (model != null && !Dropped) model.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            health.OnDeath?.RemoveListener(Drop);
            // Off (the tutorial dummy before its step): the shield is not in the hands.
            if (model != null && !Dropped) model.gameObject.SetActive(false);
        }

        public bool Blocks(Vector3 from)
        {
            if (!IsUp) return false;
            Vector3 to = from - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return true;
            return Vector3.Angle(transform.forward, to) <= guardArc;
        }

        public void Blocked(Vector3 point, string what)
        {
            BlockCount++;
            LastBlocked = what;
            Vector3 face = model != null ? model.position : point;
            Vector3 normal = (point - transform.position);
            normal.y = 0f;
            ShotEffects.Impact(face, normal.sqrMagnitude > 0.001f ? normal.normalized : transform.forward, withSound: false);
            ShotEffects fx = ShotEffects.Load();
            if (fx != null) ShotEffects.PlayAt(fx.blockClips, face);
            GetComponent<SoldierBrain>()?.Alert("blocked", callOthers: true);
            RunTelemetry.Event("shield_blocked", $"{name}:{what}");
        }

        private void LateUpdate()
        {
            if (lift == null) lift = GetComponent<TelekinesisLift>();
            if (!Dropped && ((lift != null && lift.IsLifted) || RagdollBodies.IsDown(this))) Drop(); // a lift or a fall
            if (Dropped || model == null) return;
            float scale = transform.lossyScale.y;
            bool aiming = shooter != null && shooter.HoldsSlot && shooter.CurrentAttack != DummyShooter.Attack.Melee;
            aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1f : 0f, Time.deltaTime * 5f);
            float blend = Mathf.SmoothStep(0f, 1f, aimBlend);
            Vector3 place = Vector3.Lerp(offset, aimOffset, blend);
            model.position = transform.position + transform.rotation * (place * Mathf.Max(1f, scale / 1.8f));
            model.rotation = transform.rotation * Quaternion.AngleAxis(aimYaw * blend, Vector3.up) * modelRotation;
        }

        /// <summary>The shield falls away (a lift or his death) and stays on the floor.</summary>
        public void Drop()
        {
            if (Dropped || model == null) return;
            Dropped = true;
            model.SetParent(null, true);
            var box = model.gameObject.AddComponent<BoxCollider>();
            Renderer r = model.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                box.center = model.InverseTransformPoint(r.bounds.center);
                Vector3 size = model.InverseTransformVector(r.bounds.size);
                box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            }
            var body = model.gameObject.AddComponent<Rigidbody>();
            body.mass = 6f;
            body.AddForce((transform.forward + Vector3.up * 0.5f) * 1.5f, ForceMode.VelocityChange);
            int defaultLayer = LayerMask.NameToLayer("Default");
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = defaultLayer;
            RunTelemetry.Event("shield_dropped", name);
        }
    }
}
