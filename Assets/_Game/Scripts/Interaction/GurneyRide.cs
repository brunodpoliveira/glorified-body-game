using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The ride (round 118, CANON: she "surfs" across a gap on an object she moves with mk_orzabal; the object cannot
    /// break, and it looks it). In the demo the one thing to ride is the steel transfer gurney of the airlock H5
    /// (DEMO_PLAN §2.1, §10.6). The controls are the game agent's (PROPOSED): the Interact key at the gurney asks "Ride
    /// the gurney?"; a yes puts her on it and lifts it with mk_orzabal; the walk keys steer it (forward, back, turn);
    /// the Interact key over a floor steps her off and sets it down. Over the gap it does not land, so she cannot drop
    /// into it. The cost (PROPOSED, §10.6): 40 MK of the mk_orzabal bar, so a full bar crosses twice; with less she
    /// rides on what is left, and with an empty bar the gurney does not rise. Walls and bodies stop it. It has no
    /// health and no lock point: nothing breaks it, and the mouse carry and the throw do not take it.
    /// </summary>
    public sealed class GurneyRide : Interactable
    {
        public const float Cost = 40f;          // PROPOSED: a full bar crosses twice
        public const float RideHeight = 0.35f;  // over its rest height
        public const float Speed = 2.2f;        // m/s
        public const float TurnSpeed = 90f;     // degrees a second
        public const float RiderHeight = 1.8f; // the box that walls stop: the doorways of the tube are 3 m high

        [Tooltip("The top of the deck over the gurney's root, where she stands.")]
        public float deck = 0.75f;
        [Tooltip("The deck's size, x across, z along.")]
        public Vector2 footprint = new Vector2(0.75f, 2.0f);
        public string emptyText = "The gurney does not rise. The mk_orzabal bar is empty.";
        public string gapText = "Not over the gap.";

        public bool Riding { get; private set; }
        public int Rides { get; private set; }
        public float Travelled { get; private set; }
        public float LastCost { get; private set; }

        private GameObject her;
        private TankController tank;
        private PsychicStats stats;
        private float restY, lift;
        private int mask;

        public override int Priority => Riding ? 20 : 2;
        public override bool Available => base.Available;
        public override string Hint => Riding ? "step off" : "ride the gurney";

        private void Awake()
        {
            restY = transform.position.y;
            mask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player", "Debris") & ~(1 << gameObject.layer);
        }

        private bool Find()
        {
            if (her == null) her = GameObject.FindWithTag("Player");
            if (her == null) return false;
            tank = tank != null ? tank : her.GetComponent<TankController>();
            stats = stats != null ? stats : her.GetComponent<PsychicStats>();
            return tank != null && stats != null;
        }

        private bool Empty => Find() && stats.Mk(PowerKind.Telekinesis) <= 0.01f;

        public override InteractKind KindFor(Interactor who)
        {
            if (Riding) return OverFloor() ? InteractKind.Act : InteractKind.Message;
            return Empty ? InteractKind.Message : InteractKind.Ask;
        }

        public override string TextFor(Interactor who)
        {
            if (Riding) return OverFloor() ? "" : gapText;
            return Empty ? emptyText : "Ride the gurney with mk_orzabal?";
        }

        public override string Shown(Interactor who) => Riding ? "gurney:gap" : "gurney:empty";

        public override string Confirm(Interactor who)
        {
            if (Riding) return StepOff() ? "gurney:off" : "gurney:gap";
            return Mount() ? "gurney:on" : "gurney:empty";
        }

        /// <summary>She steps on, the cost is spent, the gurney rises.</summary>
        public bool Mount()
        {
            if (Riding || !Find() || Empty) return false;
            float bar = stats.Mk(PowerKind.Telekinesis);
            float amount = Cost;
            if (!stats.CanSpendMk(PowerKind.Telekinesis, amount)) amount = bar * Cost / PsychicStats.CostOf(Cost) - 0.001f; // what is left
            float before = bar;
            stats.TrySpendMk(PowerKind.Telekinesis, Mathf.Max(0.01f, amount));
            LastCost = before - stats.Mk(PowerKind.Telekinesis);
            Riding = true;
            Rides++;
            lift = 0f;
            tank.LockMovement(true);
            Sfx.Play(Sfx.Lift, transform.position, 0.8f);
            RunTelemetry.Event("gurney_on", $"{name}:cost={LastCost:0}");
            Carry();
            return true;
        }

        /// <summary>Over a floor: the gurney sets down and she steps off. Over the gap: nothing.</summary>
        public bool StepOff()
        {
            if (!Riding || !OverFloor()) return false;
            Riding = false;
            Vector3 p = transform.position;
            if (Physics.Raycast(p + Vector3.up * 0.2f, Vector3.down, out RaycastHit floor, 3f, mask, QueryTriggerInteraction.Ignore)) p.y = floor.point.y;
            transform.position = p;
            restY = p.y;
            Physics.SyncTransforms();
            tank.Teleport(transform.position + transform.right * (footprint.x * 0.5f + 0.45f), transform.eulerAngles.y);
            tank.LockMovement(false);
            RunTelemetry.Event("gurney_off", $"{name}:{transform.position.x:0.0},{transform.position.z:0.0}");
            return true;
        }

        /// <summary>A floor under the deck, within a short drop (not the dark of the gap).</summary>
        public bool OverFloor()
        {
            Vector3 c = transform.position + Vector3.up * 0.1f;
            float down = (Riding ? lift : 0f) + 0.6f;
            foreach (float z in new[] { -0.35f, 0f, 0.35f })
            {
                Vector3 from = c + transform.forward * (footprint.y * z);
                if (!Physics.Raycast(from, Vector3.down, down, mask, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }

        private void Update()
        {
            if (!Riding || !Find()) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            GameManager gm = GameManager.Instance;
            bool live = gm == null || gm.IsPlaying;
            if (lift < RideHeight)
            {
                float up = Mathf.Min(RideHeight - lift, dt * 1.2f);
                lift += up;
                transform.position += Vector3.up * up;
            }
            if (live)
            {
                float turn = (GameInput.GetAction(InputManager.GameAction.TurnRight, KeyCode.D) ? 1f : 0f) - (GameInput.GetAction(InputManager.GameAction.TurnLeft, KeyCode.A) ? 1f : 0f);
                float move = (GameInput.GetAction(InputManager.GameAction.Forward, KeyCode.W) ? 1f : 0f) - (GameInput.GetAction(InputManager.GameAction.Back, KeyCode.S) ? 0.6f : 0f);
                Steer(move, turn, dt);
            }
            Carry();
        }

        /// <summary>One step of the ride: a turn, then a move along the deck, both stopped by walls and bodies.</summary>
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
                Vector3 step = transform.forward * (move * Speed * dt);
                if (!Blocked(transform.position + step))
                {
                    transform.position += step;
                    Travelled += step.magnitude;
                }
            }
        }

        private bool Blocked(Vector3 at)
        {
            Vector3 centre = at + Vector3.up * (0.25f + RiderHeight * 0.5f);
            Vector3 half = new Vector3(footprint.x * 0.5f, RiderHeight * 0.5f, footprint.y * 0.5f);
            return Physics.CheckBox(centre, half, transform.rotation, mask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>She stands on the deck, facing along it.</summary>
        private void Carry()
        {
            Physics.SyncTransforms();
            tank.Teleport(transform.position + Vector3.up * (deck + 0.05f), transform.eulerAngles.y);
        }
    }
}
