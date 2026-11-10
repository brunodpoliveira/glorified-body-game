using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Destruction;
using GlorifiedBody.Enemy;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A fire door at a chokepoint (2026-11-03, DEMO_PLAN 13.2). Bruno: "it needs to be only certain doors that can open
    /// and close in selected chokepoints on the map; maybe it would be better if there was a lever that closes+ locks/jams
    /// the door shut (justified by them being firebreaks, that's why guards can't open them even if they engage the lever
    /// on the other side)"; "breachable by the soldier and power armor enemies ... not by scientists and guards, they can
    /// bang on the doors though". A normal doorway has no door: she never stops to open one.
    ///   Open (the start): the leaf stands in its wall pocket. Everyone walks through.
    ///   Jammed: a lever (DoorLever, one on each side) shut it, and a bar lies across it on each face. Her lever opens it
    ///     again (Bruno: "yes"). Her powers can break it too (their splash, DoorLeaf). An enemy after her
    ///     stops at it: a soldier or the power armour breaks it (DoorLeaf), a guard or a scientist bangs on it.
    ///   Broken: a hole. Nothing shuts it again.
    /// A door that needs a card or a puzzle is a LockedDoor, not this: nothing breaks it. The leaf stays out of the NavMesh
    /// and does not carve it, so a chaser walks up to the jammed leaf. Numbers PROPOSED.
    /// </summary>
    public sealed class FireDoor : MonoBehaviour
    {
        public enum State { Open, Jammed, Broken }

        [Tooltip("The leaf that slides out of its wall pocket.")]
        public Transform leaf;
        [Tooltip("The leaf's move from its shut place to its pocket.")]
        public Vector3 openOffset = new Vector3(-1.7f, 0f, 0f);
        [Tooltip("Out of the doorway's room (the wall's normal).")]
        public Vector3 outward = Vector3.forward;
        [Min(0.5f)] public float width = 1.6f;
        [Min(0.05f)] public float slideSeconds = 0.6f;
        public bool canBreak = true;
        [Tooltip("Shut and jammed at the start (the W1 door: Bruno, 2026-11-05, the three in W2 \"won't bum rush the player at once\").")]
        public bool startsJammed;
        [Tooltip("Her levers do nothing until this story flag goes up (a script opens the door first). Empty: always.")]
        public string leverFlag = "";
        [Tooltip("The door shuts and jams when this story flag goes up (the W5 to W9 door at code silver: DEMO_PLAN §10.5, the route switch). Empty: never.")]
        public string jamFlag = "";
        [Tooltip("The bars across the doorway, one on each face: shown while the door is jammed (Bruno: \"a horizontal bar to indicate that she is jamming the door\").")]
        public GameObject[] bars = new GameObject[0];

        public const float CatchDepth = 1.1f;     // m each side of the wall: an enemy this near stops at the leaf
        public const float KickDamage = 25f;      // a soldier: four kicks break the 100 HP leaf
        public const float HeavyDamage = 50f;     // the power armour: two blows
        public const float KickSeconds = 1.2f;
        public const float HeavySeconds = 1.6f;
        public const float BangSeconds = 1.0f;
        private const float ScanSeconds = 0.1f;
        private const float ShakeSeconds = 0.18f;

        public State Current { get; private set; } = State.Open;
        /// <summary>The bars show (the tests).</summary>
        public bool BarsShown => bars.Length > 0 && bars[0] != null && bars[0].activeSelf;
        /// <summary>Kicks and bangs so far (the tests).</summary>
        public int Kicks { get; private set; }
        public int Bangs { get; private set; }
        public IReadOnlyCollection<IDoorWaiter> Waiting => waiting.Keys;

        private static readonly List<FireDoor> all = new List<FireDoor>();
        public static IReadOnlyList<FireDoor> All => all;

        private DoorLeaf breakable;
        private Vector3 shutPosition;
        private float nextScan;
        private float shake;
        private Vector3 shakeFrom;
        private Coroutine slide;
        private readonly Dictionary<IDoorWaiter, float> waiting = new Dictionary<IDoorWaiter, float>();
        private readonly Collider[] near = new Collider[16];
        private readonly HashSet<GameObject> seen = new HashSet<GameObject>();
        private readonly List<IDoorWaiter> scratch = new List<IDoorWaiter>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll() => all.Clear();

        private Vector3 Mouth => transform.position;

        private void Awake()
        {
            breakable = GetComponent<DoorLeaf>();
            if (breakable != null)
            {
                breakable.Standing = () => Current == State.Jammed;
                breakable.Broken = OnLeafBroken;
            }
            // The builder leaves the leaf shut in the doorway (DoorLeaf measures it there first): it opens into its pocket.
            if (leaf != null) shutPosition = leaf.position;
            if (startsJammed)
            {
                Current = State.Jammed;
                SetSolid(true);
                SetBars(true);
                return;
            }
            if (leaf != null) leaf.position = shutPosition + openOffset;
            SetSolid(false);
            SetBars(false);
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            GlorifiedBody.Saving.StoryFlags.OnChanged += OnFlag;
        }

        private void OnDisable()
        {
            all.Remove(this);
            GlorifiedBody.Saving.StoryFlags.OnChanged -= OnFlag;
        }

        /// <summary>
        /// Its flag shuts it. A load sets every flag at once (the event carries no name): the saved state of the door decides
        /// then, so a door she opened stays open.
        /// </summary>
        private void OnFlag(string flag)
        {
            if (string.IsNullOrEmpty(jamFlag) || flag != jamFlag || !GlorifiedBody.Saving.StoryFlags.Has(jamFlag)) return;
            if (Current == State.Open && !Jam("flag:" + jamFlag) && isActiveAndEnabled) StartCoroutine(JamWhenClear());
        }

        /// <summary>A body in the doorway when the flag went up: the door shuts as soon as the way is clear.</summary>
        private IEnumerator JamWhenClear()
        {
            while (Current == State.Open && !Jam("flag:" + jamFlag)) yield return new WaitForSeconds(0.25f);
        }

        /// <summary>A body, hers or another's, in the leaf's path.</summary>
        public bool Blocked()
        {
            int mask = LayerMask.GetMask("Player", "Enemy", "BodyPart");
            Vector3 size = new Vector3(width * 0.5f, 1f, 0.3f);
            Quaternion turn = Quaternion.LookRotation(outward, Vector3.up);
            return Physics.OverlapBoxNonAlloc(Mouth + Vector3.up * 1.1f, size, near, turn, mask, QueryTriggerInteraction.Ignore) > 0;
        }

        // ── The states ────────────────────────────────────────────────

        /// <summary>Shuts and jams it (a lever). Instant: no slide and no sound (a load). False when a body is in the way.</summary>
        public bool Jam(string by, bool instant = false)
        {
            if (Current != State.Open) return false;
            if (!instant && Blocked()) return false;
            Current = State.Jammed;
            SetSolid(true);
            SetBars(true);
            Move(shutPosition, instant);
            RunTelemetry.Event("door_jammed", $"{name}:{by}");
            if (!instant)
            {
                GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.DoorClose, Mouth + Vector3.up * 1.2f, 0.9f);
                GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.DoorLocked, Mouth + Vector3.up * 1.1f, 0.9f);
            }
            return true;
        }

        /// <summary>Opens it again (her lever, a load). Instant: no slide and no sound.</summary>
        public void Open(string by, bool instant = false)
        {
            if (Current != State.Jammed) return;
            Current = State.Open;
            SetSolid(false);
            SetBars(false);
            Move(shutPosition + openOffset, instant);
            ReleaseAll();
            RunTelemetry.Event("door_opened", $"{name}:{by}");
            if (!instant) GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.DoorOpen, Mouth + Vector3.up * 1.2f, 0.9f);
        }

        private void OnLeafBroken()
        {
            if (Current == State.Broken) return;
            Current = State.Broken;
            SetBars(false);
            ReleaseAll();
            RunTelemetry.Event("door_broken", name);
        }

        private void SetBars(bool shown)
        {
            foreach (GameObject bar in bars)
                if (bar != null) bar.SetActive(shown);
        }

        private void SetSolid(bool solid)
        {
            if (leaf == null) return;
            foreach (Collider c in leaf.GetComponentsInChildren<Collider>()) c.enabled = solid;
        }

        private void Move(Vector3 to, bool instant)
        {
            if (leaf == null) return;
            if (slide != null) StopCoroutine(slide);
            slide = null;
            shake = 0f;
            if (instant || !isActiveAndEnabled) leaf.position = to;
            else slide = StartCoroutine(Slide(to));
        }

        private IEnumerator Slide(Vector3 to)
        {
            Vector3 from = leaf.position;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / slideSeconds;
                leaf.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            slide = null;
        }

        // ── The enemies at a jammed door ──────────────────────────────

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;
            Shake();
            if (Current != State.Jammed)
            {
                if (waiting.Count > 0) ReleaseAll();
                return;
            }
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + ScanSeconds;
                Scan();
            }
            Hits();
        }

        /// <summary>The enemies near the leaf that are after her on the other side: they stop and wait at it.</summary>
        private void Scan()
        {
            Vector3 half = new Vector3(width * 0.5f + 0.3f, 1f, CatchDepth);
            Quaternion turn = Quaternion.LookRotation(outward, Vector3.up);
            int count = Physics.OverlapBoxNonAlloc(Mouth + Vector3.up, half, near, turn, LayerMask.GetMask("Enemy"), QueryTriggerInteraction.Ignore);
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
                    waiting[waiter] = Time.time + 0.35f; // the first blow after a step back
                    RunTelemetry.Event(waiter.CanBreach && canBreak ? "door_breach_start" : "door_bang_start", $"{name}:{who.name}");
                }
                waiter.WaitAtDoor(Mouth, 0.5f);
            }
            // Who left (dead, lifted, gave up, she came to his side): the door forgets them.
            var gone = new List<IDoorWaiter>();
            foreach (IDoorWaiter w in waiting.Keys)
                if (!scratch.Contains(w)) gone.Add(w);
            foreach (IDoorWaiter w in gone) waiting.Remove(w);
        }

        private void Hits()
        {
            if (waiting.Count == 0) return;
            var due = new List<IDoorWaiter>();
            foreach (KeyValuePair<IDoorWaiter, float> pair in waiting)
                if (Time.time >= pair.Value) due.Add(pair.Key);
            foreach (IDoorWaiter w in due)
            {
                if (Current != State.Jammed) return;
                Component body = w as Component;
                Vector3 from = body != null ? body.transform.position : Mouth;
                string who = body != null ? body.name : "?";
                w.SwingAtDoor();
                if (w.CanBreach && canBreak)
                {
                    waiting[w] = Time.time + (w.IsHeavy ? HeavySeconds : KickSeconds);
                    Kicks++;
                    StartShake(from, w.IsHeavy ? 0.07f : 0.04f);
                    GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.DoorKick, Mouth + Vector3.up, w.IsHeavy ? 1f : 0.85f);
                    RunTelemetry.Event("door_kick", $"{name}:{who}");
                    breakable?.Damage(w.IsHeavy ? HeavyDamage : KickDamage, GlorifiedBody.Combat.DamageType.EnemyMelee, Mouth + Vector3.up, -Side(from) * outward);
                }
                else
                {
                    waiting[w] = Time.time + BangSeconds * Random.Range(0.85f, 1.15f);
                    Bangs++;
                    StartShake(from, 0.015f);
                    GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.DoorBang, Mouth + Vector3.up * 1.3f, 0.75f);
                    RunTelemetry.Event("door_bang", $"{name}:{who}");
                }
            }
        }

        private void ReleaseAll()
        {
            foreach (IDoorWaiter w in waiting.Keys)
                if (w as Component != null) w.WaitAtDoor(Mouth, 0f);
            waiting.Clear();
        }

        /// <summary>+1 on the outward side of the wall, -1 inside.</summary>
        private float Side(Vector3 p) => Mathf.Sign(Vector3.Dot(p - Mouth, outward));

        private void StartShake(Vector3 from, float metres)
        {
            if (leaf == null || slide != null) return;
            shake = ShakeSeconds;
            shakeFrom = -Side(from) * outward * metres;
        }

        private void Shake()
        {
            if (shake <= 0f || leaf == null) return;
            shake -= Time.deltaTime;
            float k = Mathf.Max(0f, shake) / ShakeSeconds;
            leaf.position = shutPosition + shakeFrom * Mathf.Sin(k * Mathf.PI);
        }
    }
}
