using System.Collections;
using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Destruction;

namespace GlorifiedBody.Combat
{
    /// <summary>
    /// TELEKINESIS: the victim rises and hangs, and it stays up (Bruno, 2026-10-22: "once target grabbed and
    /// floating, telekinesis lasts until tgt thrown, slammed (full charge on target when it is floating) or damaged
    /// by another enemy (in this case, the floater is released with no damage)"). A short press of the attack key
    /// throws it (Launch): it flies into another enemy and both take the damage, or flies straight and drops. A full
    /// charge slams it into the floor (Slam). A hit from someone other than her drops it, unhurt (Release). While it
    /// hangs it turns with her, so she aims the throw with the tank turn. An enemy with this component does nothing
    /// until it ends (see DummyShooter). A prop (a crate) is single use (Bruno, 2026-10-23: "crate/props thrown are
    /// single-use only"): it breaks where a throw or a slam lands it. A dropped prop (a release) stays whole.
    /// A body with a ragdoll (IRagdollBody, Bruno, 2026-10-24: "orzabal should ragdoll the floater, and cause the
    /// people hit by it to ragdoll as well") hangs limp from the hips, falls limp at every ending and stands up
    /// after the down time; the enemy a thrown body hits falls limp too.
    /// </summary>
    public sealed class TelekinesisLift : MonoBehaviour
    {
        private const float LaunchSpeed = 14f;      // m/s, PROPOSED
        private const float FreeFlightMetres = 8f;  // with nobody to hit
        private const float HitRadius = 0.7f;
        private const float SteerDegrees = 180f;     // a second, toward the target
        private const float MaxFlightMetres = 20f;
        public const float ThrownDownSeconds = 2.5f;  // the thrown body and the one it hits, PROPOSED
        public const float SlamDownSeconds = 2.5f;    // PROPOSED
        public const float ReleaseDownSeconds = 1.2f; // dropped by another enemy's hit, PROPOSED
        // The room (the breakable cover, 2026-10-24), PROPOSED.
        private const float ThroughShare = 2f;         // a thrown body hits a breakable with twice its damage
        private const float LandingSplashRadius = 1f;
        private const float SlamSplashRadius = 1.3f;
        private const float ThrowLowRoot = 0.3f;      // m: the root's height at the target (the hips near his chest)
        private const float BodySweepLow = 0.25f, BodySweepHigh = 1.4f, BodySweepRadius = 0.25f;
        private static int DebrisMask => Breakables.DebrisLayer >= 0 ? 1 << Breakables.DebrisLayer : 0;

        public bool IsLifted { get; private set; }
        /// <summary>True while it rises or hangs: a throw, a slam or a release is possible.</summary>
        public bool CanLaunch => IsLifted && !launched && !slamming && !released;
        /// <summary>How it ended: "throw", "slam", "released", or "" while it hangs.</summary>
        public string Ending { get; private set; } = "";
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
        private bool slamRequested;
        private bool released;
        private Health self;
        private IRagdollBody ragdoll; // null: the body moves as one piece (a crate, a capsule dummy)

        public static TelekinesisLift Apply(GameObject victim, GameObject attacker, float liftHeight, float liftSeconds, float holdSeconds, float slamDamage)
        {
            if (victim == null) return null;
            TelekinesisLift lift = victim.GetComponent<TelekinesisLift>();
            if (lift != null && lift.IsLifted) return lift;
            if (lift == null) lift = victim.AddComponent<TelekinesisLift>();
            lift.attacker = attacker;
            lift.slamDamage = slamDamage;
            lift.self = victim.GetComponent<Health>() ?? victim.GetComponentInParent<Health>();
            IRagdollBody ragdoll = victim.GetComponent<IRagdollBody>();
            lift.ragdoll = ragdoll != null && ragdoll.BeginHold() ? ragdoll : null;
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

        /// <summary>A full charge on the floater (Bruno, 2026-10-22): it slams into the floor with this damage.</summary>
        public bool Slam(float damage)
        {
            if (!CanLaunch) return false;
            slamDamage = damage;
            slamRequested = true;
            RunTelemetry.Event("tk_slam", name);
            return true;
        }

        /// <summary>Another enemy hit the floater (Bruno, 2026-10-22): it drops, with no damage.</summary>
        public bool Release(GameObject by)
        {
            if (!CanLaunch) return false;
            released = true;
            RunTelemetry.Event("tk_released", $"{name} by {(by != null ? by.name : "?")}");
            return true;
        }

        private void OnEnable() => HitEventBus.OnHit += OnHit;

        private void OnDisable() => HitEventBus.OnHit -= OnHit;

        /// <summary>A hit on the floater from someone other than the holder releases it.</summary>
        private void OnHit(HitInfo hit)
        {
            if (!CanLaunch || hit.Victim == null || hit.Attacker == attacker) return;
            if (hit.Victim != gameObject && (self == null || hit.Victim != self.gameObject)) return;
            Release(hit.Attacker);
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
            while (t < liftSeconds && !launched && !slamRequested && !released)
            {
                t += Time.deltaTime;
                holdHeight = Mathf.SmoothStep(0f, liftHeight, t / liftSeconds);
                FollowHolder(holder);
                yield return null;
            }

            // It hangs until a throw, a slam or a release: no timer (holdSeconds is no longer a limit).
            while (!launched && !slamRequested && !released)
            {
                if (self != null && self.IsDead) released = true; // a burn or a hit killed it: it falls
                if (attacker == null) released = true;
                FollowHolder(holder);
                yield return null;
            }

            if (launched)
            {
                Ending = "throw";
                yield return Fly();
            }
            else if (released)
            {
                Ending = "released";
                if (ragdoll != null)
                {
                    ragdoll.EndHold(Vector3.zero, ReleaseDownSeconds);
                    IsLifted = false;
                    Destroy(this);
                    yield break;
                }
                Vector3 from = transform.position;
                t = 0f;
                const float dropSeconds = 0.25f;
                while (t < dropSeconds)
                {
                    t += Time.deltaTime;
                    transform.position = Vector3.Lerp(from, floor, t / dropSeconds);
                    yield return null;
                }
                transform.position = floor;
            }
            else
            {
                Ending = "slam";
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
                ragdoll?.EndHold(Vector3.down * 4f, SlamDownSeconds);
                Hit(GetComponent<Health>() ?? GetComponentInParent<Health>(), slamDamage, floor, Vector3.up);
                Breakables.Splash(floor, SlamSplashRadius, slamDamage, DamageType.PsychicTelekinesis);
            }

            IsLifted = false;
            LockOnTargetable prop = GetComponent<LockOnTargetable>();
            if (prop != null && prop.IsProp && Ending != "released")
            {
                Break();
                yield break;
            }
            Destroy(this);
        }

        /// <summary>The prop breaks: a few pieces with its material fly apart and vanish, the prop is gone.</summary>
        private void Break()
        {
            RunTelemetry.Event("prop_broken", name);
            Renderer renderer = GetComponent<Renderer>();
            Material material = renderer != null ? renderer.sharedMaterial : null;
            float size = transform.lossyScale.x;
            for (int i = 0; i < 6; i++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "PropPiece";
                Destroy(piece.GetComponent<Collider>());
                piece.transform.position = transform.position + Random.insideUnitSphere * size * 0.4f;
                piece.transform.rotation = Random.rotation;
                piece.transform.localScale = new Vector3(size * 0.5f, size * 0.12f, size * 0.25f);
                if (material != null) piece.GetComponent<Renderer>().sharedMaterial = material;
                piece.AddComponent<PropPiece>().Velocity = (Random.insideUnitSphere + Vector3.up) * 2.5f;
            }
            Destroy(gameObject);
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
            float startY = start.y;
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
                // It steers to the target as it flies (a running soldier does not step out of the throw), PROPOSED.
                if (launchTarget != null && !launchTarget.IsDead)
                {
                    Vector3 to = launchTarget.transform.position - transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.01f)
                    {
                        direction = Vector3.RotateTowards(direction, to.normalized, SteerDegrees * Mathf.Deg2Rad * Time.deltaTime, 0f);
                        maxMetres = Mathf.Min(MaxFlightMetres, flown + to.magnitude + 1f);
                    }
                }
                Vector3 next = transform.position + direction * step;
                // A thrown body arcs down to the target's chest as it flies, so its legs meet the cover in front of
                // him (the breakable cover, 2026-10-24), PROPOSED. A throw at nobody flies level.
                if (launchTarget != null) next.y = Mathf.Lerp(startY, floor.y + ThrowLowRoot, Mathf.Clamp01((flown + step) / Mathf.Max(0.1f, maxMetres)));
                // A wall stops it, at any height of the body. A breakable in the way takes the hit; when it breaks,
                // the body flies on through it.
                if (Obstacle(transform.position, next - transform.position, HitRadius, out RaycastHit wall))
                {
                    Breakable piece = wall.collider.GetComponentInParent<Breakable>();
                    // A thrown body smashes through any breakable (Bruno, 2026-10-24: a part-charge lift stopped at
                    // the barricade, and the soldier behind it never fell). A thrown prop does its damage.
                    bool body = self != null && (GetComponent<LockOnTargetable>() == null || !GetComponent<LockOnTargetable>().IsProp);
                    bool through = piece != null && !piece.IsBroken
                        && (body ? BreakPiece(piece, wall.point, direction) : piece.Damage(slamDamage * ThroughShare, DamageType.PsychicTelekinesis, wall.point, direction));
                    if (!through)
                    {
                        next = transform.position + (next - transform.position).normalized * Mathf.Max(0f, wall.distance - 0.05f);
                        flown = maxMetres;
                        Breakables.Splash(wall.point, LandingSplashRadius, slamDamage * 0.5f, DamageType.PsychicTelekinesis);
                    }
                }
                transform.position = next;
                flown += step;

                foreach (Collider col in Physics.OverlapSphere(transform.position, HitRadius, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (col.gameObject.layer == Breakables.DebrisLayer && !col.transform.IsChildOf(transform))
                    {
                        Breakables.HitCollider(col, 0f, DamageType.PsychicTelekinesis, col.transform.position, direction); // an item on the way flies
                        continue;
                    }
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
                // The one it hits falls limp too, pushed along the flight (not a heavy: it only staggers).
                LockOnTargetable struck = hit.GetComponent<LockOnTargetable>();
                if (struck == null || !struck.IsHeavy) hit.GetComponentInParent<IRagdollBody>()?.KnockDown(ThrownDownSeconds, direction * 4f + Vector3.up * 1.5f);
                LaunchVictim = hit;
                RunTelemetry.Event("tk_launch_hit", $"{name}>{hit.name}");
            }

            Breakables.Splash(transform.position, LandingSplashRadius, slamDamage * 0.5f, DamageType.PsychicTelekinesis);
            // A limp body tumbles on with the flight (the ragdoll lands it). Its own damage at once.
            if (ragdoll != null)
            {
                ragdoll.EndHold(direction * LaunchSpeed * 0.4f + Vector3.up, ThrownDownSeconds);
                Hit(self, slamDamage, transform.position, Vector3.up);
                yield break;
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

        private static bool BreakPiece(Breakable piece, Vector3 point, Vector3 direction)
        {
            piece.Break(point, direction);
            return true;
        }

        /// <summary>The first wall or breakable on the move, swept with the body (a capsule from its feet to its chest). Bodies do not count.</summary>
        private bool Obstacle(Vector3 from, Vector3 move, float reach, out RaycastHit wall)
        {
            wall = default;
            float length = move.magnitude;
            if (length < 0.0001f) return false;
            Vector3 direction = move / length;
            Vector3 low = from + Vector3.up * BodySweepLow, high = from + Vector3.up * BodySweepHigh;
            float best = float.MaxValue;
            foreach (RaycastHit hit in Physics.CapsuleCastAll(low, high, BodySweepRadius, direction, length + reach, ~DebrisMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance <= 0f || hit.distance >= best) continue; // a collider it starts in (its own bones, the floor under it) does not stop it
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<Health>() != null) continue;
                best = hit.distance;
                wall = hit;
            }
            return best < float.MaxValue;
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
