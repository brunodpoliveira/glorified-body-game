using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Destruction
{
    /// <summary>
    /// An item that rests on a surface (Bruno, 2026-10-24: "greebles on tables"): a binder, a monitor, a tray, a
    /// mug. It sleeps (kinematic) until a hit, a splash or the break of the table under it wakes it. It flies,
    /// settles, and stays where it fell for the visit and the next one (RoomDamageLedger). Large items only: the
    /// fixed camera is 5 to 12 m away, so a small one is a few pixels (the study). On the Debris layer.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Greeble : MonoBehaviour
    {
        private const float SettleSpeed = 0.1f;
        private const float SettleSeconds = 0.4f;
        private const float MinFlightSeconds = 0.4f;
        private const float MaxFlightSeconds = 6f;

        [SerializeField] private string id;

        private static readonly List<Greeble> all = new List<Greeble>();
        public static IReadOnlyList<Greeble> All => all;
        public static int TotalDisturbed { get; private set; }

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public bool Moving { get; private set; }
        public int Disturbs { get; private set; }

        private Rigidbody body;
        private Collider[] colliders;
        private float born, still;

        public void Bind(string stableId) => id = stableId;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
            body.isKinematic = true;
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        private void Start()
        {
            if (!RoomDamageLedger.FindGreeble(this, out Pose pose)) return;
            body.interpolation = RigidbodyInterpolation.None; // an interpolated body would put its old pose back
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            body.position = pose.position;
            body.rotation = pose.rotation;
        }

        /// <summary>Wakes it with a push away from the source, up a little, and a spin.</summary>
        public void Disturb(Vector3 from, float speed)
        {
            if (body == null) return;
            Vector3 away = transform.position - from;
            away.y = Mathf.Max(0f, away.y);
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Random.onUnitSphere;
            foreach (Collider c in colliders) c.enabled = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.isKinematic = false;
            body.linearVelocity = away * speed + Vector3.up * speed * 0.5f;
            body.angularVelocity = Random.insideUnitSphere * speed * 2f;
            body.WakeUp();
            Moving = true;
            still = 0f;
            born = Time.time;
            Disturbs++;
            TotalDisturbed++;
            RunTelemetry.Event("greeble_disturbed", $"{Id}:{speed:0.0}");
        }

        private void FixedUpdate()
        {
            if (!Moving || body.isKinematic) return;
            float age = Time.time - born;
            if (age < MinFlightSeconds) return;
            still = body.linearVelocity.magnitude < SettleSpeed ? still + Time.fixedDeltaTime : 0f;
            if (still < SettleSeconds && age < MaxFlightSeconds) return;
            Moving = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            RoomDamageLedger.NoteGreeble(this, new Pose(transform.position, transform.rotation));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            all.Clear();
            TotalDisturbed = 0;
        }
    }
}
