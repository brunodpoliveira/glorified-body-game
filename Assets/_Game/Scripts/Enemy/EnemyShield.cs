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

        private void Awake()
        {
            health = GetComponent<Health>();
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
            model.position = transform.position + transform.rotation * (offset * Mathf.Max(1f, scale / 1.8f));
            model.rotation = transform.rotation * modelRotation;
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
