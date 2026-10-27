using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// Lucy's death plays out physically too (Bruno, 2026-10-27: "every death plays out physically"). At her death the
    /// ragdoll is built on her bones (HumanoidRagdoll, the same parts as the soldiers'), the animator and the tank stop,
    /// the body keeps her speed and takes the push of the hit that killed her, and her muscles hold the pose and fade,
    /// the legs first. PlayerDeathFlowController waits FallSeconds before the death screen, so she lies on the floor
    /// when it shows. Numbers PROPOSED.
    /// </summary>
    public sealed class PlayerDeathFall : MonoBehaviour
    {
        public const float FallSeconds = 1.6f;      // from the death to the death screen (the fall and a moment on the floor)
        private const float Mass = 55f;             // kg, PROPOSED
        private const float MuscleStrength = 380f;
        private const float MuscleFadeSeconds = 1.0f;
        private const float LegFadeShare = 0.55f;

        public bool Fallen { get; private set; }
        public int Bones => parts.Count;
        public Transform Hips { get; private set; }
        public float LastPush { get; private set; }

        private static HitInfo lastHit;
        private static int lastHitFrame = -10;
        private readonly List<Rigidbody> parts = new List<Rigidbody>();
        private int[] parentIndex;
        private bool[] legPart;
        private Quaternion[] target;
        private float fallStart;
        private Vector3 lastPosition, velocity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            lastHit = null;
            lastHitFrame = -10;
            HitEventBus.OnHit -= Track;
            HitEventBus.OnHit += Track;
        }

        /// <summary>The last hit on her (the death push needs it).</summary>
        private static void Track(HitInfo hit)
        {
            if (hit == null || hit.Victim == null) return;
            if (!hit.Victim.CompareTag("Player") && !hit.Victim.transform.root.CompareTag("Player")) return;
            lastHit = hit;
            lastHitFrame = Time.frameCount;
        }

        /// <summary>Her death: the fall component on her root does it (it is added the first time).</summary>
        public static PlayerDeathFall Begin(Health health)
        {
            if (health == null) return null;
            GameObject root = health.CompareTag("Player") ? health.gameObject : health.transform.root.gameObject;
            PlayerDeathFall fall = root.GetComponent<PlayerDeathFall>() ?? root.AddComponent<PlayerDeathFall>();
            fall.Fall();
            return fall;
        }

        private void Awake() => lastPosition = transform.position;

        private void LateUpdate()
        {
            if (Fallen || Time.deltaTime <= 0f) return;
            velocity = (transform.position - lastPosition) / Time.deltaTime;
            lastPosition = transform.position;
        }

        private void Fall()
        {
            if (Fallen) return;
            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;
            // The pose of this frame, for the muscles.
            HumanoidRagdoll.Result built = HumanoidRagdoll.Build(animator, Mass);
            if (built.Error != "")
            {
                Debug.LogWarning($"[PlayerDeathFall] no ragdoll: {built.Error}");
                return;
            }
            Fallen = true;
            fallStart = Time.time;
            Hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            parts.Clear();
            parts.AddRange(built.Bodies);
            BuildMuscles(animator);

            if (TryGetComponent(out PoseOverride pose)) pose.Halt();
            if (animator.TryGetComponent(out PoseOverride modelPose)) modelPose.Halt();
            animator.enabled = false;
            foreach (MonoBehaviour b in GetComponents<MonoBehaviour>())
                if (b is TankController || b is LockOnController) b.enabled = false;
            if (TryGetComponent(out CharacterController controller)) controller.enabled = false;
            int layer = LayerMask.NameToLayer("BodyPart");
            Vector3 carried = new Vector3(velocity.x, 0f, velocity.z);
            carried = Vector3.ClampMagnitude(carried, 6f);
            foreach (Rigidbody part in parts)
            {
                if (layer >= 0) part.gameObject.layer = layer;
                part.isKinematic = false;
                part.interpolation = RigidbodyInterpolation.Interpolate;
                part.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                part.linearVelocity = carried;
                if (part.TryGetComponent(out CharacterJoint joint)) joint.enableProjection = true;
            }

            HitInfo hit = Time.frameCount - lastHitFrame <= 1 ? lastHit : null;
            Vector3 dir = Vector3.zero;
            if (hit != null && hit.Attacker != null) dir = Flat(transform.position - hit.Attacker.transform.position);
            if (dir.sqrMagnitude < 0.01f && hit != null) dir = Flat(-hit.HitNormal);
            if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;
            dir.Normalize();
            float speed = hit == null ? 1f
                : hit.DamageType == DamageType.Explosion || hit.DamageType == DamageType.EnemyRocket || hit.DamageType == DamageType.Grenade ? 6f
                : hit.DamageType == DamageType.EnemyMelee ? 2.5f : 1.8f;
            LastPush = speed;
            Rigidbody hitPart = null;
            if (hit != null)
            {
                float best = float.MaxValue;
                foreach (Rigidbody p in parts)
                {
                    float d = (p.worldCenterOfMass - hit.HitPoint).sqrMagnitude;
                    if (d < best) { best = d; hitPart = p; }
                }
            }
            foreach (Rigidbody p in parts) p.linearVelocity += dir * (speed * 0.3f);
            if (hitPart != null) hitPart.AddForceAtPosition(dir * (speed * 0.7f * hitPart.mass * 2f), hit.HitPoint, ForceMode.Impulse);
            RunTelemetry.Event("player_death_fall", $"{parts.Count} bones, push {speed:0.0} m/s");
        }

        private void BuildMuscles(Animator animator)
        {
            parentIndex = new int[parts.Count];
            legPart = new bool[parts.Count];
            target = new Quaternion[parts.Count];
            var legs = new HashSet<Transform>();
            foreach (HumanBodyBones b in new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg })
            {
                Transform t = animator.GetBoneTransform(b);
                if (t != null) legs.Add(t);
            }
            for (int i = 0; i < parts.Count; i++)
            {
                parentIndex[i] = -1;
                legPart[i] = legs.Contains(parts[i].transform);
                for (Transform t = parts[i].transform.parent; t != null && parentIndex[i] < 0; t = t.parent)
                    for (int j = 0; j < i; j++) if (parts[j].transform == t) { parentIndex[i] = j; break; }
                int p = parentIndex[i];
                target[i] = p >= 0 ? Quaternion.Inverse(parts[p].rotation) * parts[i].rotation : parts[i].rotation;
            }
        }

        private void FixedUpdate()
        {
            if (!Fallen || target == null) return;
            float t = Time.time - fallStart;
            if (t > MuscleFadeSeconds) return;
            for (int i = 1; i < parts.Count; i++)
            {
                int p = parentIndex[i];
                if (p < 0) continue;
                Rigidbody body = parts[i], parent = parts[p];
                float fade = Mathf.Clamp01(1f - t / (MuscleFadeSeconds * (legPart[i] ? LegFadeShare : 1f)));
                float strength = fade * fade * MuscleStrength;
                if (strength < 0.5f) continue;
                ((parent.rotation * target[i]) * Quaternion.Inverse(body.rotation)).ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                Vector3 torque = Vector3.zero;
                if (Mathf.Abs(angle) > 0.01f && axis.sqrMagnitude > 0.5f) torque += axis.normalized * (angle * Mathf.Deg2Rad * strength);
                torque -= (body.angularVelocity - parent.angularVelocity) * (1.6f * Mathf.Sqrt(strength));
                body.AddTorque(torque, ForceMode.Acceleration);
            }
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
