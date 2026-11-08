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
    /// by another enemy (in this case, the floater is released with no damage)"). A release of the attack key
    /// throws it (Launch): it flies into another enemy and both take the damage, or flies straight and drops. A full
    /// charge slams it into the floor (Slam). A hit from someone other than her drops it, unhurt (Release). While it
    /// hangs it turns with her, so she aims the throw with the tank turn. An enemy with this component does nothing
    /// until it ends (see DummyShooter). A prop (a crate) is single use (Bruno, 2026-10-23: "crate/props thrown are
    /// single-use only"): it breaks where a throw or a slam lands it. A dropped prop (a release) stays whole.
    /// A body with a ragdoll (IRagdollBody, Bruno, 2026-10-24: "orzabal should ragdoll the floater, and cause the
    /// people hit by it to ragdoll as well") hangs limp from the hips, falls limp at every ending and stands up
    /// after the down time; the enemy a thrown body hits falls limp too.
    /// The mouse moves what she holds (Bruno, 2026-11-06: "the tk'd object should be able to be moved with a mouse. this will
    /// allow us to do physics puzzles with boxes"): SteerBy. A prop with a Rigidbody goes by velocity, so a wall or another box
    /// stops it and it pushes a loose box, and a release leaves it to gravity. The defend press releases a box too (Bruno, 2026-11-07:
    /// "right clicking should cancel the lift and drop the object gently on the ground; left click still does the usual").
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
        private const float BodyCoreRadius = 0.02f;   // its middle line: a wall inside the body's width at the start still stops it
        private const float HangSkin = 0.03f;         // m: the gap a hanging body keeps from a wall
        public const float MinHeight = 0.1f, MaxHeight = 2.6f; // m over where it lay, the wheel's limits, PROPOSED
        private const float NearestToHer = 1f;        // m: the mouse brings it no nearer
        private const float MaxLead = 0.6f;           // m: the hold point leads the thing by this at most
        private const float CarrySpeed = 7f;          // m/s: a carried box follows the mouse this fast at most, PROPOSED
        private static int DebrisMask => Breakables.DebrisLayer >= 0 ? 1 << Breakables.DebrisLayer : 0;

        public bool IsLifted { get; private set; }
        /// <summary>True while it rises or hangs: a throw, a slam or a release is possible.</summary>
        public bool CanLaunch => IsLifted && !launched && !slamming && !released;
        /// <summary>How it ended: "throw", "slam", "released", or "" while it hangs.</summary>
        public string Ending { get; private set; } = "";
        public bool Launched => launched;
        /// <summary>A box or a crate: a prop that goes by physics. Not an enemy, and not the slab of the floor shield.</summary>
        public bool IsBox => box != null;
        /// <summary>The speed of the throw, m/s.</summary>
        public float ThrowSpeed { get; private set; } = LaunchSpeed;
        private float FlightScale = 1f;
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
        private Rigidbody box;        // a physics prop: it goes by velocity while it hangs
        private Vector3 boxTarget;
        private Collider boxCollider;
        private PhysicsMaterial boxMaterial;
        private static PhysicsMaterial slippery;
        private static PhysicsMaterial Slippery => slippery != null ? slippery : slippery = new PhysicsMaterial("TK slippery")
        {
            staticFriction = 0f,
            dynamicFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f,
        };

        public static TelekinesisLift Apply(GameObject victim, GameObject attacker, float liftHeight, float liftSeconds, float holdSeconds, float slamDamage)
        {
            if (victim == null) return null;
            TelekinesisLift lift = victim.GetComponent<TelekinesisLift>();
            if (lift != null && lift.IsLifted) return lift;
            if (lift == null) lift = victim.AddComponent<TelekinesisLift>();
            lift.attacker = attacker;
            lift.slamDamage = slamDamage;
            lift.self = victim.GetComponent<Health>() ?? victim.GetComponentInParent<Health>();
            lift.box = victim.GetComponent<Rigidbody>();
            if (lift.box != null && victim.TryGetComponent(out LockOnTargetable lockable) && lockable.IsProp)
            {
                lift.box.isKinematic = false;
                lift.box.useGravity = false;
                lift.box.constraints = RigidbodyConstraints.FreezeRotation;
                lift.boxTarget = victim.transform.position;
                // Friction held a box still while the mouse pressed it against a wall: it could not rise or slide along it.
                lift.boxCollider = victim.GetComponent<Collider>();
                if (lift.boxCollider != null)
                {
                    lift.boxMaterial = lift.boxCollider.sharedMaterial;
                    lift.boxCollider.sharedMaterial = Slippery;
                }
            }
            else lift.box = null;
            IRagdollBody ragdoll = victim.GetComponent<IRagdollBody>();
            lift.ragdoll = ragdoll != null && ragdoll.BeginHold() ? ragdoll : null;
            lift.StartCoroutine(lift.Run(liftHeight, liftSeconds, holdSeconds));
            return lift;
        }

        /// <summary>
        /// Throws the hanging victim at a destination. With a target, the flight ends on contact and both take
        /// the damage. Without one, it flies FreeFlightMetres and drops with the slam damage. The damage and the speed
        /// come from the charge of the throw (PsychicPowers.TryLaunch).
        /// </summary>
        public bool Launch(Vector3 destination, Health target, float damage = -1f, float speedScale = 1f)
        {
            if (!CanLaunch) return false;
            if (damage >= 0f) slamDamage = damage;
            ThrowSpeed = LaunchSpeed * speedScale;
            FlightScale = speedScale;
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

        /// <summary>The mouse moves it: flat metres in the world, height metres, inside her reach. A wall stops it on the way.</summary>
        public void SteerBy(Vector3 flat, float height, float reach)
        {
            if (!CanLaunch || attacker == null) return;
            Quaternion toWorld = Quaternion.Euler(0f, HoldYawNow(attacker.transform), 0f);
            Vector3 her = attacker.transform.position;
            Vector3 point = her + toWorld * holdOffset + flat;
            // The hold point stays close to the thing: a wall stops the thing, and the mouse must not wind up behind that wall.
            Vector3 lead = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (lead.magnitude > MaxLead) point = transform.position + lead.normalized * MaxLead;
            Vector3 offset = Vector3.ProjectOnPlane(point - her, Vector3.up);
            float distance = offset.magnitude;
            if (distance > 0.001f) offset *= Mathf.Clamp(distance, NearestToHer, reach) / distance;
            holdOffset = Quaternion.Inverse(toWorld) * offset;
            float now = transform.position.y - floor.y;
            holdHeight = Mathf.Clamp(Mathf.Clamp(holdHeight + height, now - MaxLead, now + MaxLead), MinHeight, MaxHeight);
        }

        private void FixedUpdate()
        {
            if (box == null || !CanLaunch) return;
            Vector3 velocity = (boxTarget - box.position) / Time.fixedDeltaTime;
            box.linearVelocity = Vector3.ClampMagnitude(velocity, CarrySpeed);
            box.angularVelocity = Vector3.zero;
        }

        /// <summary>A box leaves the physics while it flies or slams (the lift moves it), and it breaks at the end.</summary>
        private void StopBoxPhysics()
        {
            if (box == null) return;
            box.linearVelocity = Vector3.zero;
            box.isKinematic = true;
        }

        private void OnEnable() => HitEventBus.OnHit += OnHit;

        private void OnDisable() => HitEventBus.OnHit -= OnHit;

        /// <summary>
        /// A hit on the floater from someone other than the holder releases it. So does a hit on the holder (Bruno,
        /// 2026-11-07: "she won't drop the guy even after being hit multiple times"): the hold breaks, and the thing drops.
        /// </summary>
        private void OnHit(HitInfo hit)
        {
            if (!CanLaunch || hit.Victim == null || hit.Attacker == attacker) return;
            bool onIt = hit.Victim == gameObject || (self != null && hit.Victim == self.gameObject);
            bool onHer = attacker != null && (hit.Victim == attacker || hit.Victim.transform.IsChildOf(attacker.transform) || attacker.transform.IsChildOf(hit.Victim.transform));
            if (!onIt && !onHer) return;
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
                StopBoxPhysics();
                yield return Fly();
            }
            else if (released)
            {
                Ending = "released";
                if (box != null)
                {
                    // It falls by itself, and it stays where it lands: the puzzle needs that.
                    box.linearVelocity = Vector3.zero; // a drop, not a throw: it falls straight down
                    box.useGravity = true;
                    box.constraints = RigidbodyConstraints.None;
                    if (boxCollider != null) boxCollider.sharedMaterial = boxMaterial;
                    IsLifted = false;
                    Destroy(this);
                    yield break;
                }
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
                StopBoxPhysics();
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

        /// <summary>
        /// The hang point: her position plus the start offset, turned by her yaw change since the lift. A wall stops the body
        /// on the way there and it slides along it (Bruno, 2026-11-05: "the soldier that floats can phase through walls"). It
        /// goes back to the hang point when the way is free. Only a throw, a slam or a hit ends the hold.
        /// </summary>
        private void FollowHolder(Transform holder)
        {
            if (holder == null)
            {
                transform.position = new Vector3(floor.x, floor.y + holdHeight, floor.z);
                return;
            }
            Vector3 offset = Quaternion.Euler(0f, HoldYawNow(holder), 0f) * holdOffset;
            Vector3 flat = holder.position + offset;
            float y = floor.y + holdHeight;
            if (box != null)
            {
                boxTarget = new Vector3(flat.x, y, flat.z);
                floor = new Vector3(transform.position.x, floor.y, transform.position.z);
                return;
            }
            Vector3 settled = HangMove(new Vector3(transform.position.x, y, transform.position.z), new Vector3(flat.x, y, flat.z));
            transform.position = settled;
            floor = new Vector3(settled.x, floor.y, settled.z); // the slam lands where it hangs
        }

        /// <summary>
        /// A floater turns with her, so she aims the throw with the tank turn. A carried box stays where the mouse puts it: she
        /// turns to face it, and if it turned with her, each turn would move it again.
        /// </summary>
        public bool StaysWhenSheTurns
        {
            get => stays;
            set
            {
                if (stays && !value && attacker != null) holdYaw = attacker.transform.eulerAngles.y; // no jump when it starts to turn with her
                stays = value;
            }
        }
        private bool stays;

        private float HoldYawNow(Transform holder) => StaysWhenSheTurns ? 0f : holder.eulerAngles.y - holdYaw;

        /// <summary>The move to the hang point, as far as the walls let it: to the first wall, then along that wall.</summary>
        private Vector3 HangMove(Vector3 from, Vector3 to)
        {
            Vector3 move = to - from;
            if (!Obstacle(from, move, HangSkin, out RaycastHit wall)) return to;
            Vector3 direction = move.normalized;
            Vector3 stop = from + direction * Mathf.Max(0f, wall.distance - HangSkin);
            Vector3 slide = Vector3.ProjectOnPlane(to - stop, wall.normal);
            slide.y = 0f;
            if (!Obstacle(stop, slide, HangSkin, out RaycastHit corner)) return stop + slide;
            return stop + slide.normalized * Mathf.Max(0f, corner.distance - HangSkin);
        }

        private IEnumerator Fly()
        {
            Health self = GetComponent<Health>() ?? GetComponentInParent<Health>();
            Vector3 start = transform.position;
            float startY = start.y;
            Vector3 flat = launchDestination - start;
            flat.y = 0f;
            Vector3 direction = flat.sqrMagnitude > 0.001f ? flat.normalized : transform.forward;
            float maxMetres = launchTarget != null ? flat.magnitude + 1f : FreeFlightMetres * FlightScale;
            float flown = 0f;
            Health hit = null;
            Vector3 hitPoint = start;

            while (flown < maxMetres && hit == null)
            {
                float step = ThrowSpeed * Time.deltaTime;
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
                if (struck == null || !struck.IsHeavy) hit.GetComponentInParent<IRagdollBody>()?.KnockDown(ThrownDownSeconds, direction * 4f * FlightScale + Vector3.up * 1.5f);
                LaunchVictim = hit;
                RunTelemetry.Event("tk_launch_hit", $"{name}>{hit.name}");
            }

            Breakables.Splash(transform.position, LandingSplashRadius, slamDamage * 0.5f, DamageType.PsychicTelekinesis);
            // A limp body tumbles on with the flight (the ragdoll lands it). Its own damage at once.
            if (ragdoll != null)
            {
                ragdoll.EndHold(direction * ThrowSpeed * 0.4f + Vector3.up, ThrownDownSeconds);
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

        /// <summary>
        /// The first wall or breakable on the move, swept with the body (a capsule from its feet to its chest) and with its
        /// middle line (the wide sweep skips what it starts in). Bodies do not count.
        /// </summary>
        private bool Obstacle(Vector3 from, Vector3 move, float reach, out RaycastHit wall)
        {
            wall = default;
            float length = move.magnitude;
            if (length < 0.0001f) return false;
            Vector3 direction = move / length;
            Vector3 low = from + Vector3.up * BodySweepLow, high = from + Vector3.up * BodySweepHigh;
            float best = float.MaxValue;
            foreach (float radius in new[] { BodySweepRadius, BodyCoreRadius })
                foreach (RaycastHit hit in Physics.CapsuleCastAll(low, high, radius, direction, length + reach, ~DebrisMask, QueryTriggerInteraction.Ignore))
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
