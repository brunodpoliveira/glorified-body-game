using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The ride (round 118, CANON: she "surfs" across a gap on an object she moves with mk_orzabal). Bruno, 2026-11-14:
    /// "the player needs to ride the gurney themselves; i need to test if it's possible to manipulate objects to get her
    /// to surf on them". So the ride is no question any more, it is the power (DEMO_PLAN §2.1, PROPOSED there and built
    /// so): on it, the defend key held with mk_orzabal lifts the thing with her on it, the walk keys steer it (forward and
    /// back along her facing, the turn keys turn it), and a release sets it down. Over a gap it does not land: the ride goes
    /// on until a floor is under it. PsychicPowers starts and ends the ride. Every box of the physics puzzles is rideable,
    /// and the steel gurney of the Hold.
    /// No key climbs her on (Bruno, 2026-11-16: "the interact to go on top of it must go. the player must manipulate the
    /// object successfully so they can step on it and float to the other side"). She steps onto a top low enough for her
    /// step, or she carries the thing over a gap, lowers it until its top is level with the floor (TelekinesisLift), and
    /// walks onto it: the carry becomes the ride (PsychicPowers.TryBoard). A ride ends on its top; she walks off the edge.
    /// The cost (PROPOSED): the gurney 40 MK of the mk_orzabal bar (a full bar crosses twice; with less she rides on
    /// what is left), a box the cost of any lift. Walls and bodies stop it.
    /// </summary>
    public sealed class Rideable : Interactable
    {
        public const float RideHeight = 0.35f;  // over its rest height
        public const float Speed = 2.2f;        // m/s
        public const float TurnSpeed = 90f;     // degrees a second
        public const float RiderHeight = 1.6f;  // m over the top: her head, a little low so the 2.9 m lintel of the tube passes
        public const float ClimbHeight = 1.2f;  // m: she climbs onto a top this high or lower

        [Tooltip("MK of the mk_orzabal bar for one ride. 0: the cost of any lift (PsychicPowers.LiftCost).")]
        public float cost;
        public string emptyText = "It does not rise. The mk_orzabal bar is empty.";
        public string gapText = "Not over the gap.";

        public bool Riding { get; private set; }
        public int Rides { get; private set; }
        public float Travelled { get; private set; }
        public float LastCost { get; private set; }
        public float Cost => cost > 0f ? cost : PsychicPowers.LiftCost;

        private GameObject her;
        private TankController tank;
        private Collider body;
        private Rigidbody rb;
        private float lift, riseTo, herYaw, gapSaidAt = -9f;
        private bool wasKinematic;
        private RigidbodyInterpolation wasInterpolation;
        private int mask;

        public override bool Available => false; // no key climbs on or off (2026-11-16)

        private void Awake()
        {
            body = GetComponent<Collider>();
            rb = GetComponent<Rigidbody>();
            mask = (Physics.DefaultRaycastLayers | (1 << 2)) & ~LayerMask.GetMask("Player", "Debris");
        }

        private bool Find()
        {
            if (her == null) her = GameObject.FindWithTag("Player");
            if (her == null) return false;
            tank = tank != null ? tank : her.GetComponent<TankController>();
            return tank != null;
        }

        private float Top => body.bounds.max.y;
        private float Bottom => body.bounds.min.y;

        /// <summary>Her feet on its top: within its outline, a short way over it.</summary>
        public bool OnTop(Transform who)
        {
            Vector3 local = transform.InverseTransformPoint(who.position);
            Vector3 half = Half();
            return Mathf.Abs(local.x) * transform.lossyScale.x <= half.x + 0.1f && Mathf.Abs(local.z) * transform.lossyScale.z <= half.z + 0.1f
                   && who.position.y > Top - 0.15f && who.position.y < Top + 0.5f;
        }

        /// <summary>The thing she stands on, if it is rideable.</summary>
        public static Rideable Under(Transform who)
        {
            foreach (RaycastHit hit in Physics.RaycastAll(who.position + Vector3.up * 0.3f, Vector3.down, 0.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                Rideable ride = hit.collider.GetComponentInParent<Rideable>();
                if (ride != null && ride.isActiveAndEnabled && ride.OnTop(who)) return ride;
            }
            return null;
        }

        /// <summary>Half its size in metres, x across and z along (the collider in its own frame).</summary>
        private Vector3 Half()
        {
            if (body is BoxCollider box) return Vector3.Scale(box.size, transform.lossyScale) * 0.5f;
            return body.bounds.extents;
        }

        public override InteractKind KindFor(Interactor who) => InteractKind.Act;

        public override string TextFor(Interactor who) => "";

        public override string Confirm(Interactor who)
        {
            if (!Find()) return "ride:none";
            if (OnTop(her.transform)) return ClimbDown() ? "ride:down" : "ride:stay";
            return ClimbOn() ? "ride:up" : "ride:far";
        }

        /// <summary>
        /// The step from the floor onto a carried thing (PsychicPowers.TryBoard): its top level with her feet, its near edge a
        /// short step in front of her.
        /// </summary>
        public bool CanBoardFrom(Transform who)
        {
            if (Riding || body == null) return false;
            if (Mathf.Abs(Top - who.position.y) > BoardLevel) return false;
            Vector3 feet = who.position + Vector3.up * 0.05f;
            Vector3 edge = body.ClosestPoint(new Vector3(feet.x, Top - 0.05f, feet.z));
            Vector3 to = Vector3.ProjectOnPlane(edge - feet, Vector3.up);
            return to.magnitude <= BoardReach && (to.magnitude < 0.05f || Vector3.Dot(to.normalized, who.forward) > 0.3f);
        }

        public const float BoardLevel = 0.25f; // m: its top this close to her feet
        public const float BoardReach = 0.9f;  // m: its near edge this close in front of her

        /// <summary>Tests and bots: she stands on the top at once, as after a step onto it. No cost. The gurney: she faces along it. A box: she keeps her facing.</summary>
        public bool ClimbOn()
        {
            if (!Find() || Riding) return false;
            float yaw = her.transform.eulerAngles.y;
            if (cost > 0f) yaw = transform.eulerAngles.y + (Vector3.Dot(her.transform.forward, transform.forward) >= 0f ? 0f : 180f);
            herYaw = Mathf.DeltaAngle(transform.eulerAngles.y, yaw);
            Stand();
            Sfx.Play(Sfx.StepLucy, transform.position, 0.6f);
            RunTelemetry.Event("ride_climb", name);
            return true;
        }

        /// <summary>She climbs down beside it, on the side she faces, or on the first side with a floor.</summary>
        public bool ClimbDown()
        {
            if (!Find() || Riding) return false;
            Vector3 half = Half();
            Vector3 f = Vector3.ProjectOnPlane(her.transform.forward, Vector3.up).normalized;
            foreach (Vector3 dir in new[] { f, transform.right, -transform.right, transform.forward, -transform.forward })
            {
                float reach = Mathf.Abs(Vector3.Dot(dir, transform.right)) * half.x + Mathf.Abs(Vector3.Dot(dir, transform.forward)) * half.z + 0.45f;
                Vector3 at = new Vector3(transform.position.x, Top + 0.2f, transform.position.z) + dir * reach;
                if (!Physics.Raycast(at, Vector3.down, out RaycastHit floor, Top - Bottom + 0.8f, mask, QueryTriggerInteraction.Ignore)) continue;
                if (floor.collider.GetComponentInParent<Rideable>() == this) continue;
                if (Physics.CheckCapsule(floor.point + Vector3.up * 0.35f, floor.point + Vector3.up * 1.4f, 0.3f, mask & ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                tank.Teleport(floor.point, her.transform.eulerAngles.y);
                RunTelemetry.Event("ride_down", name);
                return true;
            }
            return false;
        }

        /// <summary>
        /// The defend key on its top, or her step onto it from a hold (PsychicPowers): the cost is spent, it rises with her. It
        /// rises RideHeight over where it starts, and at least RideHeight over floorY: a box lowered into a gap comes up over the
        /// floor she stepped from, or the edge of the far floor would stop it. False: the bar is empty.
        /// </summary>
        public bool StartRide(PsychicStats stats, float floorY = float.NegativeInfinity)
        {
            if (Riding || !Find() || stats == null) return false;
            float bar = stats.Mk(PowerKind.Telekinesis);
            if (bar <= 0.01f)
            {
                Say(emptyText, "ride:empty");
                return false;
            }
            float amount = Cost;
            if (!stats.CanSpendMk(PowerKind.Telekinesis, amount)) amount = bar * Cost / PsychicStats.CostOf(Cost) - 0.001f; // what is left
            stats.TrySpendMk(PowerKind.Telekinesis, Mathf.Max(0.01f, amount));
            LastCost = bar - stats.Mk(PowerKind.Telekinesis);
            herYaw = Mathf.DeltaAngle(transform.eulerAngles.y, her.transform.eulerAngles.y);
            if (rb == null) rb = GetComponent<Rigidbody>(); // the first carry gives a prop its Rigidbody (PsychicPowers.EnsurePropBody)
            if (rb != null)
            {
                wasKinematic = rb.isKinematic;
                wasInterpolation = rb.interpolation;
                rb.interpolation = RigidbodyInterpolation.None; // an interpolated body writes its old pose back over the ride's moves
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Riding = true;
            Rides++;
            lift = 0f;
            riseTo = Mathf.Max(Bottom + RideHeight, floorY + RideHeight);
            tank.LockMovement(true);
            Sfx.Play(Sfx.Lift, transform.position, 0.8f);
            RunTelemetry.Event("ride_on", $"{name}:cost={LastCost:0}");
            Stand();
            return true;
        }

        /// <summary>
        /// The release of the defend key (PsychicPowers). Over a floor it sets down and she stands on it. Over a gap: false, the
        /// ride goes on. Quiet: no "Not over the gap." (a ride from the left-press lift has no key to release).
        /// </summary>
        public bool EndRide(bool quiet = false)
        {
            if (!Riding) return true;
            if (!OverFloor())
            {
                if (!quiet && Time.time - gapSaidAt > 2f && Say(gapText, "ride:gap")) gapSaidAt = Time.time;
                return false;
            }
            Riding = false;
            Vector3 p = transform.position;
            float drop = Bottom - p.y;
            if (Physics.Raycast(new Vector3(p.x, Bottom + 0.05f, p.z), Vector3.down, out RaycastHit floor, lift + 1f, mask, QueryTriggerInteraction.Ignore)) p.y = floor.point.y - drop;
            transform.position = p;
            Physics.SyncTransforms();
            if (rb != null)
            {
                rb.position = transform.position;
                rb.isKinematic = wasKinematic;
                rb.interpolation = wasInterpolation;
            }
            Stand();
            tank.LockMovement(false);
            RunTelemetry.Event("ride_off", $"{name}:{transform.position.x:0.0},{transform.position.z:0.0}");
            return true;
        }

        /// <summary>A floor under its bottom, within a short drop (not the dark of the gap).</summary>
        public bool OverFloor()
        {
            float down = (Riding ? lift : 0f) + 0.6f;
            float along = Half().z * 0.7f;
            foreach (float z in new[] { -along, 0f, along })
            {
                Vector3 from = new Vector3(transform.position.x, Bottom + 0.05f, transform.position.z) + transform.forward * z;
                if (!Floor(from, down)) return false;
            }
            return true;
        }

        private bool Floor(Vector3 from, float down)
        {
            foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, down + 0.05f, mask, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<Rideable>() != this) return true;
            return false;
        }

        private void Update()
        {
            if (!Riding || !Find()) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            GameManager gm = GameManager.Instance;
            bool live = gm == null || gm.IsPlaying;
            if (Bottom < riseTo - 0.001f)
            {
                float up = Mathf.Min(riseTo - Bottom, dt * 1.2f);
                lift += up;
                transform.position += Vector3.up * up;
            }
            if (live)
            {
                float turn = (GameInput.GetAction(InputManager.GameAction.TurnRight, KeyCode.D) ? 1f : 0f) - (GameInput.GetAction(InputManager.GameAction.TurnLeft, KeyCode.A) ? 1f : 0f);
                float move = (GameInput.GetAction(InputManager.GameAction.Forward, KeyCode.W) ? 1f : 0f) - (GameInput.GetAction(InputManager.GameAction.Back, KeyCode.S) ? 0.6f : 0f);
                Steer(move, turn, dt);
            }
            Stand();
        }

        /// <summary>One step of the ride: a turn, then a move along her facing, both stopped by walls and bodies.</summary>
        public void Steer(float move, float turn, float dt)
        {
            if (!Riding) return;
            if (Mathf.Abs(turn) > 0.01f)
            {
                Quaternion was = transform.rotation;
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + turn * TurnSpeed * dt, 0f);
                if (Blocked(transform.position)) transform.rotation = was;
            }
            if (Mathf.Abs(move) > 0.01f)
            {
                Vector3 step = Quaternion.Euler(0f, transform.eulerAngles.y + herYaw, 0f) * Vector3.forward * (move * Speed * dt);
                if (!Blocked(transform.position + step))
                {
                    transform.position += step;
                    Travelled += step.magnitude;
                }
            }
        }

        private bool Blocked(Vector3 at)
        {
            Vector3 half = Half();
            float bottom = Bottom - transform.position.y;
            float tall = Top - Bottom - 0.25f + RiderHeight; // from a step over the floor to her head
            Vector3 centre = at + Vector3.up * (bottom + 0.25f + tall * 0.5f);
            Vector3 box = new Vector3(half.x, tall * 0.5f, half.z);
            foreach (Collider c in Physics.OverlapBox(centre, box, transform.rotation, mask, QueryTriggerInteraction.Ignore))
                if (c.GetComponentInParent<Rideable>() != this) return true;
            return false;
        }

        /// <summary>She stands on the top, at its centre.</summary>
        private void Stand()
        {
            Physics.SyncTransforms();
            tank.Teleport(new Vector3(transform.position.x, Top + 0.05f, transform.position.z), transform.eulerAngles.y + herYaw);
        }

        private bool Say(string text, string what)
        {
            Interactor hand = her != null ? her.GetComponent<Interactor>() : null;
            return hand != null && hand.ShowMessage(text, Interactor.MessageSeconds, what);
        }
    }
}
