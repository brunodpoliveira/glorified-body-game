using System.Collections;
using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Combat
{
    /// <summary>
    /// TELEKINESIS, first form: the victim rises, hangs, and slams into the floor. The slam is the hit.
    /// Bruno, 2026-10-20: while it hangs, the next attack press launches it (Launch). It flies into another enemy,
    /// and both take the damage. With nobody to hit it flies straight and drops. While it hangs it turns with her:
    /// it keeps its offset from her, turned by her yaw change, so she aims the throw with the tank turn. An enemy
    /// with this component on it does nothing until it ends (see DummyShooter). Props and ragdolls come later.
    /// </summary>
    public sealed class TelekinesisLift : MonoBehaviour
    {
        private const float LaunchSpeed = 14f;      // m/s, PROPOSED
        private const float FreeFlightMetres = 8f;  // with nobody to hit
        private const float HitRadius = 0.7f;

        public bool IsLifted { get; private set; }
        /// <summary>True while it rises or hangs: a launch is possible.</summary>
        public bool CanLaunch => IsLifted && !launched && !slamming;
        public bool Launched => launched;
        public Health LaunchVictim { get; private set; }

        private GameObject attacker;
        private float slamDamage;
        private Vector3 floor;
        private Vector3 holdOffset;   // from her to the victim at the lift start, flat
        private float holdYaw;        // her yaw at the lift start
        private float holdHeight;
        private bool launched;
        private bool slamming;
        private Vector3 launchDestination;
        private Health launchTarget;

        public static TelekinesisLift Apply(GameObject victim, GameObject attacker, float liftHeight, float liftSeconds, float holdSeconds, float slamDamage)
        {
            if (victim == null) return null;
            TelekinesisLift lift = victim.GetComponent<TelekinesisLift>();
            if (lift != null && lift.IsLifted) return lift;
            if (lift == null) lift = victim.AddComponent<TelekinesisLift>();
            lift.attacker = attacker;
            lift.slamDamage = slamDamage;
            lift.StartCoroutine(lift.Run(liftHeight, liftSeconds, holdSeconds));
            return lift;
        }

        /// <summary>
        /// Throws the hanging victim at a destination. With a target, the flight ends on contact and both take
        /// the damage. Without one, it flies FreeFlightMetres and drops with the slam damage.
        /// </summary>
        public bool Launch(Vector3 destination, Health target)
        {
            if (!CanLaunch) return false;
            launched = true;
            launchTarget = target;
            launchDestination = destination;
            RunTelemetry.Event("tk_launch", target != null ? target.name : "free");
            return true;
        }

        private IEnumerator Run(float liftHeight, float liftSeconds, float holdSeconds)
        {
            IsLifted = true;
            floor = transform.position;
            Transform holder = attacker != null ? attacker.transform : null;
            if (holder != null)
            {
                holdOffset = Vector3.ProjectOnPlane(floor - holder.position, Vector3.up);
                holdYaw = holder.eulerAngles.y;
            }
            float t = 0f;
            while (t < liftSeconds && !launched)
            {
                t += Time.deltaTime;
                holdHeight = Mathf.SmoothStep(0f, liftHeight, t / liftSeconds);
                FollowHolder(holder);
                yield return null;
            }

            t = 0f;
            while (t < holdSeconds && !launched)
            {
                t += Time.deltaTime;
                FollowHolder(holder);
                yield return null;
            }

            if (launched)
            {
                yield return Fly();
            }
            else
            {
                slamming = true;
                Vector3 from = transform.position;
                t = 0f;
                const float slamSeconds = 0.15f;
                while (t < slamSeconds)
                {
                    t += Time.deltaTime;
                    transform.position = Vector3.Lerp(from, floor, t / slamSeconds);
                    yield return null;
                }
                transform.position = floor;
                Hit(GetComponent<Health>() ?? GetComponentInParent<Health>(), slamDamage, floor, Vector3.up);
            }

            IsLifted = false;
            Destroy(this);
        }

        /// <summary>The hang point: her position plus the start offset, turned by her yaw change since the lift.</summary>
        private void FollowHolder(Transform holder)
        {
            if (holder == null)
            {
                transform.position = new Vector3(floor.x, floor.y + holdHeight, floor.z);
                return;
            }
            Vector3 offset = Quaternion.Euler(0f, holder.eulerAngles.y - holdYaw, 0f) * holdOffset;
            Vector3 flat = holder.position + offset;
            transform.position = new Vector3(flat.x, floor.y + holdHeight, flat.z);
            floor = new Vector3(flat.x, floor.y, flat.z); // the slam lands where it hangs
        }

        private IEnumerator Fly()
        {
            Health self = GetComponent<Health>() ?? GetComponentInParent<Health>();
            Vector3 start = transform.position;
            Vector3 flat = launchDestination - start;
            flat.y = 0f;
            Vector3 direction = flat.sqrMagnitude > 0.001f ? flat.normalized : transform.forward;
            float maxMetres = launchTarget != null ? flat.magnitude + 1f : FreeFlightMetres;
            float flown = 0f;
            Health hit = null;
            Vector3 hitPoint = start;

            while (flown < maxMetres && hit == null)
            {
                float step = LaunchSpeed * Time.deltaTime;
                Vector3 next = transform.position + direction * step;
                // A wall stops it.
                if (Physics.Raycast(transform.position, direction, out RaycastHit wall, step + HitRadius, ~0, QueryTriggerInteraction.Ignore)
                    && wall.collider.GetComponentInParent<Health>() == null && wall.collider.transform != transform && !wall.collider.transform.IsChildOf(transform))
                {
                    next = wall.point - direction * HitRadius;
                    flown = maxMetres;
                }
                transform.position = next;
                flown += step;

                foreach (Collider col in Physics.OverlapSphere(transform.position, HitRadius, ~0, QueryTriggerInteraction.Ignore))
                {
                    Health other = col.GetComponentInParent<Health>();
                    if (other == null || other == self || other.IsDead || other.gameObject.CompareTag("Player")) continue;
                    hit = other;
                    hitPoint = other.transform.position + Vector3.up;
                    break;
                }
                yield return null;
            }

            if (hit != null)
            {
                Vector3 normal = (hit.transform.position - transform.position).normalized;
                Hit(hit, slamDamage, hitPoint, -normal);
                hit.GetComponentInParent<IStaggerable>()?.Stagger(0.8f);
                LaunchVictim = hit;
                RunTelemetry.Event("tk_launch_hit", $"{name}>{hit.name}");
            }

            // The drop to the floor, the flyer's own damage.
            Vector3 from = transform.position;
            Vector3 landing = new Vector3(from.x, floor.y, from.z);
            float t = 0f;
            const float dropSeconds = 0.15f;
            while (t < dropSeconds)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(from, landing, t / dropSeconds);
                yield return null;
            }
            transform.position = landing;
            Hit(self, slamDamage, landing, Vector3.up);
        }

        private void Hit(Health health, float damage, Vector3 point, Vector3 normal)
        {
            if (health == null || health.IsDead) return;
            health.TakeDamage(damage);
            HitEventBus.Raise(new HitInfo
            {
                Attacker = attacker,
                Victim = health.gameObject,
                HitTransform = health.transform,
                HitPoint = point,
                HitNormal = normal,
                DamageAmount = damage,
                DamageType = DamageType.PsychicTelekinesis,
                WasFatal = health.IsDead
            });
        }
    }
}
