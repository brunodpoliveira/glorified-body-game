using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// An unarmed civilian (design §3.4: "unarmed civilians cower, run and beg"; §11.5, Bruno 2026-10-22: "an
    /// unarmed civvie will always run/cower"; the demo story: "the civilians who are not armed run, and the ones who
    /// cannot run kneel"). Bruno, 2026-10-25: "initially, the civvies walk back slowly"; "they'll only run and cower
    /// if the player kills one (guard, civvie doesn't matter)"; they tremble; they tend bodies like the others, the
    /// nurse "even if player on sight". It never attacks. Phases:
    ///   Idle: at its work until it knows she is there.
    ///   Wary: it knows (it sees her, it hears a shot, an alert or a power hit). The first sight screams (the alert
    ///         calls the armed ones in the radius). It walks backward, slowly, facing her, and keeps waryDistance.
    ///   Flee: it panics (it sees a death, or it is hurt), then for good: it sprints to the spot farthest from her.
    ///   Cower: at the spot it crouches with its back to her.
    ///   Beg: she comes within begRange, or it has nowhere to go (a wall behind it on every side, or every route out
    ///        passes near her): it kneels, faces her and begs. When she walks away it cowers (panicked) or backs
    ///        away again.
    ///   Care: CorpseCare drags a body and does CPR (ICarer).
    /// It trembles while wary, cowering and begging (Tremble). The lift and the ragdoll suspend the body as they do
    /// a soldier's. The bark lines are the writer's (barks.csv). The poses are placeholders: the vendor crouch for the
    /// kneel and the cower (RFab gap 13). All numbers are PROPOSED.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class CivilianBrain : MonoBehaviour, IFearful, ICarer
    {
        public enum Phase { Idle, Wary, Flee, Cower, Beg, Care, Dead }

        public const string LineBeg = "Please. Please. I'm sheltering, I'm sheltering in place.";
        public const string LineRun = "Don't. Don't, don't, don't.";
        public const string LineCower = "I didn't see anything. I didn't see anything.";
        public const string LineBack = "Okay. Okay. I'm going back to my station. I'm going.";
        public const string LineNurseBack = "Nobody's angry with you. You're a little confused, that's all.";
        public const string LineKillSeen = "Oh God. Oh God, she's killing people!";

        [SerializeField] private bool nurse;
        [SerializeField, Min(1f)] private float alertRadius = 25f;
        [SerializeField, Min(1f)] private float hearPowerRadius = 12f;
        [SerializeField, Min(1f)] private float waryDistance = 6f;   // m: the wary one backs away until she is this far
        [SerializeField, Min(0.5f)] private float backStep = 1.5f;   // m: one step of the walk back
        [SerializeField, Min(0.1f)] private float backReplanSeconds = 0.5f;
        [SerializeField, Min(0.5f)] private float begRange = 3f;
        [SerializeField, Min(0.5f)] private float begLeaveRange = 5f;
        [SerializeField, Min(0.5f)] private float cornerGain = 3f;   // m: a flee spot that gains less than this is no escape
        [SerializeField, Min(0.5f)] private float avoidRadius = 2f;  // m: a route that passes this near her is no escape
        [SerializeField, Min(1f)] private float fleeSeconds = 8f;
        [SerializeField, Min(0.5f)] private float begBarkSeconds = 3.5f;
        [SerializeField, Range(0f, 1f)] private float waryTremble = 0.6f;

        private IEnemyBody body;
        private Health health;
        private Tremble tremble;
        private Transform player;
        private float timer;
        private float nextBark;
        private float nextStep;
        private bool backing;
        private bool suspended;

        public Phase Current { get; private set; } = Phase.Idle;
        public bool Alerted { get; private set; }
        /// <summary>It saw a death or it was hurt: it runs and cowers from now on.</summary>
        public bool Panicked { get; private set; }
        public string AlertReason { get; private set; } = "";
        public string PanicReason { get; private set; } = "";
        public bool Retreating => Panicked && Current != Phase.Dead;
        public bool IsNurse => nurse;
        public int Begs { get; private set; }
        public Vector3 Spot { get; private set; }
        public IEnemyBody Body => body;
        public Tremble Tremble => tremble;
        private Vector3 Her => player != null ? player.position : body.TargetPosition;

        public void SetNurse(bool value) => nurse = value;

        private void Awake()
        {
            body = GetComponent<IEnemyBody>();
            health = GetComponent<Health>();
            tremble = GetComponent<Tremble>();
            if (tremble == null) tremble = gameObject.AddComponent<Tremble>();
        }

        private void OnEnable()
        {
            health.OnDeath ??= new UnityEngine.Events.UnityEvent();
            health.OnDeath.AddListener(OnDeath);
            health.OnDamaged ??= new UnityEngine.Events.UnityEvent<float>();
            health.OnDamaged.AddListener(OnHurt);
            EnemyAlert.OnAlert += OnAlertHeard;
            HitEventBus.OnHit += OnHitHeard;
        }

        private void OnDisable()
        {
            health.OnDeath?.RemoveListener(OnDeath);
            health.OnDamaged?.RemoveListener(OnHurt);
            EnemyAlert.OnAlert -= OnAlertHeard;
            HitEventBus.OnHit -= OnHitHeard;
        }

        // ── Awareness ─────────────────────────────────────────────────

        /// <summary>It knows she is here: it backs away. The first to know screams, and the scream alerts the armed ones.</summary>
        public void Alert(string reason, bool callOthers)
        {
            if (Alerted || Current == Phase.Dead || body == null) return;
            if (player == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                player = p != null ? p.transform : null;
                if (player == null) return;
            }
            Alerted = true;
            AlertReason = reason;
            body.Track(player);
            RunTelemetry.Event("civilian_alerted", $"{name}:{reason}");
            if (callOthers) EnemyAlert.Raise(transform.position, alertRadius, "scream");
            if (!suspended && Current == Phase.Idle)
            {
                if (Panicked) Flee();
                else Wary();
            }
        }

        /// <summary>A death it saw, or a hurt: it runs and cowers from now on. A care goes on until it ends.</summary>
        public void Panic(string reason)
        {
            if (Panicked || Current == Phase.Dead || body == null) return;
            Panicked = true; // before the alert: an idle one runs at once, it does not back away first
            PanicReason = reason;
            panicLine = reason == "hurt" ? LineRun : LineKillSeen;
            bool wasAlerted = Alerted;
            Alert(reason, callOthers: false);
            if (!Alerted) { Panicked = false; return; }
            RunTelemetry.Event("civilian_panic", $"{name}:{reason}");
            if (wasAlerted && !suspended && Current != Phase.Care) Flee();
        }
        private string panicLine = LineRun;

        /// <summary>A death it saw (EnemyMorale): an unarmed civilian's fear limit is 0, so it always panics.</summary>
        public void Retreat(string reason) => Panic(reason);

        public void Shock(float seconds, bool gruesome) => Panic(gruesome ? "gruesome death" : "death");

        private void OnAlertHeard(Vector3 origin, float radius, string reason)
        {
            if (Alerted || Current == Phase.Dead) return;
            if (EnemyNav.Flat(transform.position, origin) <= radius) Alert("heard " + reason, callOthers: false);
        }

        private void OnHurt(float amount)
        {
            if (health.IsDead) return;
            Alert("hurt", callOthers: true);
            Panic("hurt");
        }

        private void OnHitHeard(HitInfo hit)
        {
            if (Alerted || Current == Phase.Dead || hit.Attacker == null || !hit.Attacker.CompareTag("Player")) return;
            if (EnemyNav.Flat(transform.position, hit.HitPoint) <= hearPowerRadius) Alert("power", callOthers: true);
        }

        private void OnDeath()
        {
            Current = Phase.Dead;
            tremble.Amount = 0f;
            body?.Stop();
        }

        // ── The care (CorpseCare) ─────────────────────────────────────

        /// <summary>Free to tend a body: alive, on its feet, not begging. A panicked one leaves its corner for it.</summary>
        public bool CanCare => Current != Phase.Dead && Current != Phase.Beg && Current != Phase.Care && !suspended && body != null;
        public bool CareIgnoresHer => nurse;
        // The care barks (barks.csv, the writer, 2026-10-25). The lab worker has no drag line.
        public string CareStartLine => nurse ? "Don't move. Don't try to move, I'm coming." : "Is she gone? Okay. Hey. Hey. Can you hear me?";
        public string DragLine => nurse ? "I've got you. Nearly there. Nearly there." : null;
        public string CprLine => nurse ? "Come on. Breathe for me. I'm not done with you." : "How hard do I push? Computer. How hard do I push?";

        public bool TryBeginCare()
        {
            if (!CanCare) return false;
            Current = Phase.Care;
            backing = false;
            body.Crouch(false);
            tremble.Amount = 0f; // the hands are busy (CorpseCare poses the arms)
            return true;
        }

        public void EndCare()
        {
            if (Current != Phase.Care) return;
            Current = Phase.Idle;
            if (!isActiveAndEnabled) return; // the room unloads (CorpseCare.OnDisable): no new phase, no bark
            if (!Alerted || suspended) { body.Stop(); return; }
            if (Panicked) Flee();
            else Wary();
        }

        // ── The phases ────────────────────────────────────────────────

        private void Wary()
        {
            Current = Phase.Wary;
            backing = false;
            nextStep = 0f;
            body.Crouch(false);
            tremble.Amount = waryTremble;
            Bark.Say(gameObject, nurse ? LineNurseBack : LineBack);
            RunTelemetry.Event("civilian_wary", name);
        }

        private void Flee()
        {
            tremble.Amount = 0f;
            if (!EnemyNav.TryEscape(transform.position, Her, avoidRadius, cornerGain, out Vector3 spot))
            {
                // Nowhere to run: it kneels and begs when she is near, else it cowers where it stands.
                if (EnemyNav.Flat(transform.position, Her) <= begLeaveRange) Beg("cornered");
                else Cower();
                return;
            }
            Spot = spot;
            Current = Phase.Flee;
            body.Crouch(false);
            body.MoveTo(Spot, MoveSpeed.Sprint);
            timer = fleeSeconds;
            Bark.Say(gameObject, panicLine); // the first run after a kill: the kill line, then the run line
            panicLine = LineRun;
            RunTelemetry.Event("civilian_flee", $"{name} to {Spot.x:0.0},{Spot.z:0.0}");
        }

        private void Cower()
        {
            Current = Phase.Cower;
            body.Stop();
            body.Crouch(true);
            tremble.Amount = 1f;
            Bark.Say(gameObject, LineCower);
            RunTelemetry.Event("civilian_cower", name);
        }

        private void Beg(string why)
        {
            Current = Phase.Beg;
            Begs++;
            backing = false;
            body.Stop();
            body.Crouch(true);
            tremble.Amount = 1f;
            Bark.Say(gameObject, LineBeg);
            nextBark = Time.time + begBarkSeconds;
            RunTelemetry.Event("civilian_beg", $"{name}:{why}");
        }

        private void Update()
        {
            if (body == null || Current == Phase.Dead) return;
            if (Time.deltaTime <= 0f) return;

            TelekinesisLift lift = GetComponent<TelekinesisLift>();
            if ((lift != null && lift.IsLifted) || RagdollBodies.IsDown(this))
            {
                if (!suspended)
                {
                    suspended = true;
                    if (Current == Phase.Care) GetComponent<CorpseCare>()?.Stop("held");
                    body.Suspend(true);
                }
                return;
            }
            if (suspended)
            {
                suspended = false;
                body.Suspend(false);
                if (Panicked) Flee();
                else if (Alerted) Wary();
            }

            if (!Alerted && Current != Phase.Care && body.CanSeeTarget) Alert("sight", callOthers: true);
            if (!Alerted || Current == Phase.Care) return;
            float gap = EnemyNav.Flat(transform.position, Her);

            switch (Current)
            {
                case Phase.Idle:
                    if (Panicked) Flee();
                    else Wary();
                    break;
                case Phase.Wary:
                    WaryTick(gap);
                    break;
                case Phase.Flee:
                    timer -= Time.deltaTime;
                    if (gap <= begRange * 0.7f) Beg("caught");
                    else if (timer <= 0f || (timer < fleeSeconds - 0.6f && EnemyNav.Flat(transform.position, Spot) <= 1f)) Cower();
                    break;
                case Phase.Cower:
                    body.Stop();
                    body.Face(transform.position - Her); // the back to her, the face to the wall
                    if (gap <= begRange) Beg("near");
                    break;
                case Phase.Beg:
                    body.Stop();
                    body.Face(Her - transform.position);
                    if (gap > begLeaveRange)
                    {
                        if (Panicked) Cower();
                        else Wary();
                    }
                    else if (Time.time >= nextBark)
                    {
                        Bark.Say(gameObject, LineBeg);
                        nextBark = Time.time + begBarkSeconds;
                    }
                    break;
            }
        }

        /// <summary>The slow walk back: facing her, one short step at a time, until she is waryDistance away.</summary>
        private void WaryTick(float gap)
        {
            if (gap <= begRange) { Beg("near"); return; }
            Vector3 toHer = Her - transform.position;
            if (backing && gap >= waryDistance + 1f) backing = false;
            else if (!backing && gap < waryDistance) backing = true;
            if (!backing)
            {
                body.Stop();
                body.Face(toHer);
                return;
            }
            if (Time.time < nextStep) return;
            nextStep = Time.time + backReplanSeconds;
            if (EnemyNav.TryBackStep(transform.position, Her, backStep, out Vector3 step))
            {
                body.StrafeTo(step, toHer);
                return;
            }
            // A wall behind it on every side: it begs when she is near, else it stands and trembles (the lean on the
            // wall is a study, 2026-10-25).
            if (gap <= begLeaveRange) Beg("cornered");
            else
            {
                body.Stop();
                body.Face(toHer);
            }
        }
    }
}
