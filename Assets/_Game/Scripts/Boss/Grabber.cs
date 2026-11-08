using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;

namespace GlorifiedBody.Boss
{
    /// <summary>
    /// A grabber of the brain (DEMO_PLAN §14.1, CANON round 10: "Grabbers come from the walls on a cooldown and pin her for
    /// two seconds: a blast frees her, telekinesis rips a grabber out and throws it"). Greybox: a chain of grey-pink
    /// capsules from a mouth in the wall. It winds up at the mouth (the head swells and pulses), then locks her place and
    /// lunges at it in a straight line; it holds her only if its head touches her on the way (Bruno, 2026-11-08: "the
    /// grabbers are still impossible to dodge": the reach followed her chest to its end). A step aside, a run or a dodge in
    /// the lunge makes it miss. Any hit of
    /// hers on the head frees her (the blast of the design, and the others too: PROPOSED). mk_orzabal lifts the head: it
    /// is ripped out, she is free, and she can throw it at a node of the brain (LockOnTargetable, not a prop, so the
    /// auto-lock finds it while it holds her).
    /// </summary>
    public sealed class Grabber : MonoBehaviour
    {
        public enum State { Winding, Reaching, Holding, Retracting, Ripped }

        public const int Segments = 7;
        /// <summary>The head touches her chest closer than this in the lunge: it holds her.</summary>
        public const float TouchRadius = 0.6f;
        public static readonly Color Flesh = new Color(0.62f, 0.45f, 0.48f);

        public State Current { get; private set; }
        public bool Holding => Current == State.Holding;
        public string Ending { get; private set; } = "";

        private BrainFight fight;
        private Vector3 mouth;
        private Transform her;
        private TankController tank;
        private float windupSeconds, reachSeconds, pinSeconds, t;
        private bool locked;
        private Vector3 reachFrom, reachTo;
        private Vector3 baseScale;

        /// <summary>The point the lunge goes to: her chest when the wind-up ended (the tests).</summary>
        public Vector3 LungeTarget => reachTo;
        private readonly List<Transform> chain = new List<Transform>();

        /// <summary>A new grabber from the mouth toward her.</summary>
        public static Grabber Create(BrainFight fight, Vector3 mouth, Transform her, float windupSeconds, float reachSeconds, float pinSeconds, Material flesh)
        {
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Grabber";
            head.tag = "Enemy";
            head.layer = LayerMask.NameToLayer("Enemy");
            head.transform.position = mouth;
            head.transform.localScale = Vector3.one * 0.42f;
            head.GetComponent<Renderer>().sharedMaterial = flesh;
            Health health = head.AddComponent<Health>();
            health.SetMaxHealth(30f);
            LockOnTargetable target = head.AddComponent<LockOnTargetable>();
            target.SetShockImmune(true); // a part of the boss: no mk_edgar
            CharacterController body = her.GetComponent<CharacterController>();
            if (body != null) Physics.IgnoreCollision(body, head.GetComponent<Collider>()); // it reaches into her, it does not push her
            Grabber g = head.AddComponent<Grabber>();
            g.fight = fight;
            g.mouth = mouth;
            g.her = her;
            g.tank = her.GetComponent<TankController>();
            g.windupSeconds = windupSeconds;
            g.reachSeconds = reachSeconds;
            g.baseScale = head.transform.localScale;
            g.pinSeconds = pinSeconds;
            g.reachFrom = mouth;
            for (int i = 0; i < Segments; i++)
            {
                var seg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                seg.name = "Grabber_Segment";
                Object.Destroy(seg.GetComponent<Collider>());
                seg.transform.localScale = new Vector3(0.22f, 0.3f, 0.22f);
                seg.GetComponent<Renderer>().sharedMaterial = flesh;
                g.chain.Add(seg.transform);
            }
            RunTelemetry.Event("grabber", $"{mouth.x:0.0},{mouth.z:0.0}");
            return g;
        }

        private void OnEnable() => HitEventBus.OnHit += OnHit;

        private void OnDisable() => HitEventBus.OnHit -= OnHit;

        private Vector3 HerChest => her.position + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight;

        private void Update()
        {
            if (her == null) { Retract("no_target"); return; }
            if (Current != State.Ripped && GetComponent<TelekinesisLift>() != null) { Rip(); return; }
            t += Time.deltaTime;
            switch (Current)
            {
                case State.Winding:
                {
                    // The tell: the head swells and pulses at the mouth, a little out toward her.
                    float k = Mathf.Clamp01(t / windupSeconds);
                    Vector3 toward = (HerChest - mouth).normalized;
                    transform.position = mouth + toward * 0.35f * k;
                    transform.localScale = baseScale * (1f + 0.35f * k + 0.12f * Mathf.Sin(t * 30f));
                    if (k >= 1f)
                    {
                        Current = State.Reaching;
                        t = 0f;
                        reachFrom = transform.position;
                        reachTo = HerChest; // her place now: the lunge does not follow her
                        transform.localScale = baseScale;
                        GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.Dodge, transform.position, 0.9f);
                        RunTelemetry.Event("grabber_lunges", "");
                    }
                    break;
                }
                case State.Reaching:
                {
                    float k = Mathf.Clamp01(t / reachSeconds);
                    transform.position = Vector3.Lerp(reachFrom, reachTo, k * k); // it speeds up toward the end
                    if (Vector3.Distance(transform.position, HerChest) < TouchRadius) Hold();
                    else if (k >= 1f) Retract("missed");
                    break;
                }
                case State.Holding:
                    transform.position = HerChest + her.forward * 0.15f;
                    if (t >= pinSeconds) Retract("let_go");
                    break;
                case State.Retracting:
                {
                    float k = Mathf.Clamp01(t / (reachSeconds * 0.6f));
                    transform.position = Vector3.Lerp(reachFrom, mouth, k);
                    if (k >= 1f) Destroy(gameObject);
                    break;
                }
            }
            if (Current != State.Ripped) DrawChain();
        }

        private void DrawChain()
        {
            Vector3 a = mouth, b = transform.position;
            for (int i = 0; i < chain.Count; i++)
            {
                float k = (i + 0.5f) / chain.Count;
                Vector3 p = Vector3.Lerp(a, b, k);
                chain[i].position = p;
                Vector3 along = b - a;
                if (along.sqrMagnitude > 0.0001f) chain[i].rotation = Quaternion.FromToRotation(Vector3.up, along.normalized);
            }
        }

        private void Hold()
        {
            Current = State.Holding;
            t = 0f;
            if (tank != null && !locked) { tank.LockMovement(true); locked = true; }
            RunTelemetry.Event("grabber_holds", "");
        }

        private void Free()
        {
            if (locked && tank != null) tank.LockMovement(false);
            locked = false;
        }

        /// <summary>It lets go and goes back into the wall.</summary>
        public void Retract(string why)
        {
            if (Current == State.Retracting || Current == State.Ripped) return;
            Free();
            Ending = why;
            Current = State.Retracting;
            reachFrom = transform.position;
            t = 0f;
            RunTelemetry.Event("grabber_retracts", why);
        }

        /// <summary>mk_orzabal tore it out of the wall: she is free, and it is a thing she holds.</summary>
        private void Rip()
        {
            Free();
            Current = State.Ripped;
            Ending = "ripped";
            foreach (Transform seg in chain) if (seg != null) Destroy(seg.gameObject);
            chain.Clear();
            fight?.Ripped(this);
            RunTelemetry.Event("grabber_ripped", "");
        }

        private void OnHit(HitInfo hit)
        {
            if (hit.Victim != gameObject || Current == State.Ripped || Current == State.Retracting) return;
            if (hit.Attacker == null || !hit.Attacker.CompareTag("Player")) return;
            Retract("hit");
        }

        private void LateUpdate()
        {
            // A ripped head that is no longer held (it flew, it fell): it rots away.
            if (Current == State.Ripped && GetComponent<TelekinesisLift>() == null && t > 0.1f) Destroy(gameObject, 0.6f);
        }

        private void OnDestroy()
        {
            Free();
            foreach (Transform seg in chain) if (seg != null) Destroy(seg.gameObject);
        }
    }
}
