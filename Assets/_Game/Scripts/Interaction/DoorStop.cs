using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Rooms;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A planter in a doorway holds it (DEMO_PLAN §10.5, the planters, CANON round 43: "A planter against a door holds it
    /// against guards, not soldiers"). While the prop rests in a doorway, the NavMesh goes around it (its obstacle carves),
    /// and an enemy after her on the other side stops at it, as at a jammed fire door (IDoorWaiter). A soldier or the power
    /// armour kicks it out of the way. A guard or a scientist only bangs on it. Numbers PROPOSED.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DoorStop : MonoBehaviour
    {
        public const float HoldReach = 0.9f;     // m from the doorway's line: it holds the doorway
        public const float CatchRadius = 1.9f;   // m from the prop: an enemy this near stops at it
        public const int ShoveKicks = 2;         // a soldier's kicks before it moves (the power armour: one)
        public const float ShoveThrough = 1.2f;  // m the kick moves it through the doorway
        public const float ShoveAside = 1.0f;    // m, then to one side, out of the way
        public const float ShoveSeconds = 0.3f;  // s each leg
        public const float ScanSeconds = 0.25f;

        /// <summary>The room of the doorway it holds now (null: none), and the doorway's index in that room's exits.</summary>
        public Room HoldingRoom { get; private set; }
        public int HoldingIndex { get; private set; } = -1;
        public bool Holds => HoldingRoom != null;
        private Room.Exit Holding => HoldingRoom.exits[HoldingIndex];
        public int Kicks { get; private set; }
        public int Bangs { get; private set; }
        public int Shoves { get; private set; }
        public IReadOnlyCollection<IDoorWaiter> Waiting => waiting.Keys;

        private Rigidbody body;
        private float nextScan;
        private int kicksSinceShove;
        private readonly Dictionary<IDoorWaiter, float> waiting = new Dictionary<IDoorWaiter, float>();
        private readonly Collider[] near = new Collider[16];
        private readonly HashSet<GameObject> seen = new HashSet<GameObject>();
        private readonly List<IDoorWaiter> scratch = new List<IDoorWaiter>();

        private void Awake() => body = GetComponent<Rigidbody>();

        private void OnDisable() => ReleaseAll();

        private void Update()
        {
            if (Time.deltaTime <= 0f || Time.time < nextScan) { Hits(); return; }
            nextScan = Time.time + ScanSeconds;
            bool resting = GetComponent<GlorifiedBody.Combat.TelekinesisLift>() == null && (body.isKinematic || body.linearVelocity.sqrMagnitude < 0.01f);
            Room room = null;
            int index = resting ? FindDoorway(out room) : -1;
            if (room != HoldingRoom || index != HoldingIndex)
            {
                ReleaseAll();
                kicksSinceShove = 0;
                HoldingRoom = room;
                HoldingIndex = index;
                if (room != null) RunTelemetry.Event("doorstop_holds", $"{name}:{room.id}>{Holding.to}");
            }
            if (Holds) Scan();
            Hits();
        }

        /// <summary>The doorway whose line it stands on: its centre within HoldReach of the line, inside the doorway's width.</summary>
        private int FindDoorway(out Room owner)
        {
            owner = null;
            foreach (Room r in Room.All)
                for (int i = 0; i < r.exits.Length; i++)
                {
                    Room.Exit e = r.exits[i];
                    Vector3 d = transform.position - Mouth(r, e);
                    Vector3 across = new Vector3(e.outward.z, 0f, -e.outward.x);
                    if (Mathf.Abs(Vector3.Dot(d, e.outward)) > HoldReach) continue;
                    if (Mathf.Abs(Vector3.Dot(d, across)) > e.width * 0.5f + 0.3f) continue;
                    if (Mathf.Abs(d.y) > 1.5f) continue;
                    owner = r;
                    return i;
                }
            return -1;
        }

        private static Vector3 Mouth(Room r, Room.Exit e) => r.transform.position + e.mouth;

        private Vector3 HeldMouth => Mouth(HoldingRoom, Holding);

        /// <summary>+1 on the outward side of the doorway, -1 inside.</summary>
        private float Side(Vector3 p) => Mathf.Sign(Vector3.Dot(p - HeldMouth, Holding.outward));

        /// <summary>The enemies near it that are after her on the other side of the doorway: they stop and wait at it.</summary>
        private void Scan()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, CatchRadius, near, LayerMask.GetMask("Enemy"), QueryTriggerInteraction.Ignore);
            seen.Clear();
            scratch.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider c = near[i];
                if (c == null) continue;
                Health health = c.GetComponentInParent<Health>();
                GameObject who = health != null ? health.gameObject : c.gameObject;
                if (!seen.Add(who) || (health != null && health.IsDead)) continue;
                IDoorWaiter waiter = who.GetComponent<IDoorWaiter>();
                if (waiter == null || !waiter.Hunting || Side(who.transform.position) == Side(waiter.Quarry)) continue;
                scratch.Add(waiter);
                if (!waiting.ContainsKey(waiter))
                {
                    waiting[waiter] = Time.time + 0.35f;
                    RunTelemetry.Event(waiter.CanBreach ? "doorstop_kick_start" : "doorstop_bang_start", $"{name}:{who.name}");
                }
                waiter.WaitAtDoor(transform.position, 0.5f);
            }
            var gone = new List<IDoorWaiter>();
            foreach (IDoorWaiter w in waiting.Keys)
                if (!scratch.Contains(w)) gone.Add(w);
            foreach (IDoorWaiter w in gone) waiting.Remove(w);
        }

        private void Hits()
        {
            if (waiting.Count == 0 || !Holds) return;
            var due = new List<IDoorWaiter>();
            foreach (KeyValuePair<IDoorWaiter, float> pair in waiting)
                if (Time.time >= pair.Value) due.Add(pair.Key);
            foreach (IDoorWaiter w in due)
            {
                if (!Holds) return;
                Component who = w as Component;
                Vector3 from = who != null ? who.transform.position : transform.position;
                w.SwingAtDoor();
                if (w.CanBreach)
                {
                    waiting[w] = Time.time + (w.IsHeavy ? FireDoor.HeavySeconds : FireDoor.KickSeconds);
                    Kicks++;
                    kicksSinceShove += w.IsHeavy ? ShoveKicks : 1;
                    GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.DoorKick, transform.position + Vector3.up * 0.5f, 0.8f);
                    RunTelemetry.Event("doorstop_kick", $"{name}:{(who != null ? who.name : "?")}");
                    if (kicksSinceShove >= ShoveKicks) Shove(from);
                }
                else
                {
                    waiting[w] = Time.time + FireDoor.BangSeconds * Random.Range(0.85f, 1.15f);
                    Bangs++;
                    GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.DoorBang, transform.position + Vector3.up * 0.6f, 0.6f);
                    RunTelemetry.Event("doorstop_bang", $"{name}:{(who != null ? who.name : "?")}");
                }
            }
        }

        /// <summary>
        /// The kick moves it through the doorway, then to one side, out of the way: a slide, so the result does not hang on the
        /// friction. A wall or a body stops a leg (a sweep).
        /// </summary>
        private void Shove(Vector3 from)
        {
            Vector3 through = -Side(from) * Holding.outward;
            Vector3 across = new Vector3(Holding.outward.z, 0f, -Holding.outward.x);
            float aside = Vector3.Dot(transform.position - HeldMouth, across) >= 0f ? 1f : -1f; // the side it already leans to
            Shoves++;
            kicksSinceShove = 0;
            RunTelemetry.Event("doorstop_shoved", name);
            ReleaseAll();
            HoldingRoom = null;
            HoldingIndex = -1;
            nextScan = Time.time + 2f * ShoveSeconds + 0.5f; // it slides first
            StartCoroutine(Slide(through, across * aside));
        }

        private System.Collections.IEnumerator Slide(Vector3 first, Vector3 second)
        {
            bool wasKinematic = body.isKinematic;
            foreach ((Vector3 dir, float metres) in new[] { (first, ShoveThrough), (second, ShoveAside) })
            {
                float distance = body.SweepTest(dir, out RaycastHit hit, metres, QueryTriggerInteraction.Ignore) ? Mathf.Max(0f, hit.distance - 0.05f) : metres;
                body.isKinematic = true;
                Vector3 start = body.position, end = start + dir * distance;
                for (float t = 0f; t < 1f;)
                {
                    yield return new WaitForFixedUpdate();
                    t = Mathf.Min(1f, t + Time.fixedDeltaTime / ShoveSeconds);
                    body.MovePosition(Vector3.Lerp(start, end, t));
                }
            }
            body.isKinematic = wasKinematic;
        }

        private void ReleaseAll()
        {
            foreach (IDoorWaiter w in waiting.Keys)
                if (w as Component != null) w.WaitAtDoor(transform.position, 0f);
            waiting.Clear();
        }
    }
}
