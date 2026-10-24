using UnityEngine;

namespace GlorifiedBody.Destruction
{
    /// <summary>
    /// One piece of a broken Breakable: it flies, settles, then freezes where it lies (kinematic, no collider, no
    /// shadow when it is small), for the rest of the visit and for the next one (RoomDamageLedger). Warspite let
    /// its debris shrink away at 6 s, which reads "gamey" in a slow horror game (the study, 2026-10-24). The pieces
    /// are on the Debris layer: they hit the room, the other pieces and the ragdolls, not her or the capsules.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DebrisPiece : MonoBehaviour
    {
        private const float SettleSpeed = 0.12f;
        private const float SettleSeconds = 0.4f;
        private const float MinFlightSeconds = 0.5f;
        private const float MaxFlightSeconds = 6f;
        private const float SmallSize = 0.3f;

        public Breakable Owner { get; private set; }
        public int Index { get; private set; }
        public bool Settled { get; private set; }

        private Rigidbody body;
        private Collider[] colliders;
        private float born, still;

        public void Bind(Breakable owner, int index)
        {
            Owner = owner;
            Index = index;
            Cache();
        }

        private void Cache()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (colliders == null) colliders = GetComponentsInChildren<Collider>(true);
        }

        public void Launch(Vector3 velocity, Vector3 spin)
        {
            Cache();
            Settled = false;
            still = 0f;
            born = Time.time;
            foreach (Collider c in colliders) c.enabled = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.isKinematic = false;
            body.linearVelocity = velocity;
            body.angularVelocity = spin;
            body.WakeUp();
        }

        /// <summary>The pose from the ledger, frozen at once.</summary>
        public void Rest(Pose pose)
        {
            Cache();
            // An interpolated body puts its own old pose back on the transform: set both, no interpolation.
            body.interpolation = RigidbodyInterpolation.None;
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            body.position = pose.position;
            body.rotation = pose.rotation;
            Freeze(false);
        }

        private void FixedUpdate()
        {
            if (Settled || body == null || body.isKinematic) return;
            float age = Time.time - born;
            if (age < MinFlightSeconds) return;
            still = body.linearVelocity.magnitude < SettleSpeed ? still + Time.fixedDeltaTime : 0f;
            if (still >= SettleSeconds || age >= MaxFlightSeconds) Freeze(true);
        }

        private void Freeze(bool note)
        {
            Settled = true;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            foreach (Collider c in colliders) c.enabled = false;
            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null && r.bounds.size.magnitude < SmallSize) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (note && Owner != null) RoomDamageLedger.NotePiece(Owner, Index, new Pose(transform.position, transform.rotation));
        }
    }
}
