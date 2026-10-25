using UnityEngine;
using GlorifiedBody.Characters;
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
    ///   Flee: it panics (it sees a death; Bruno, 2026-10-25: "after a kill"), then for good: it sprints to the spot
    ///         farthest from her. A hurt alone only alerts it (a direct hit kills a civilian anyway).
    ///   Cower: at the spot it takes its own pose (Bruno: "to avoid multiple npcs of being in exact same pose"): a
    ///         crouch with its back to her, the back flat on a wall (WallLean), a self-hug or a shaking stance (the
    ///         RFab fear presets, PoseOverride). Each civilian has its own variant, in turn.
    ///   Beg: she comes within begRange, or it has nowhere to go (a wall behind it on every side, or every route out
    ///        passes near her): it kneels, faces her and begs. When she walks away it cowers (panicked) or backs
    ///        away again.
    ///   Care: CorpseCare drags a body and does CPR (ICarer).
    /// A wary one that stands gives a startle, then a fear stance; with a wall behind it, it leans on the wall.
    /// A shock of the short stuns it (IStaggerable). It trembles while wary, cowering and begging (Tremble). The lift and the ragdoll suspend the body as they do
    /// a soldier's. The bark lines are the writer's (barks.csv). The poses are placeholders: the vendor crouch for the
    /// kneel and the cower (RFab gap 13). All numbers are PROPOSED.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class CivilianBrain : MonoBehaviour, IFearful, ICarer, IStaggerable
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
        private float stunUntil;

        /// <summary>The pose over the controller (PoseOverride, WallLean).</summary>
        public enum Pose { Controller, Crouch, Startle, Shake, Hug, Lean }
        private PoseOverride poseOverride;
        private WallLean lean;
        private bool startled;
        private int variant;
        private static int nextVariant;

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
        public WallLean Lean => lean;
        public PoseOverride PoseOverride => poseOverride;
        public Pose CurrentPose { get; private set; } = Pose.Controller;
        /// <summary>The cower of this civilian: 0 crouch, 1 wall lean, 2 self-hug, 3 shaking stance.</summary>
        public int Variant => variant;
        public bool Stunned => Time.time < stunUntil;
        private Vector3 Her => player != null ? player.position : body.TargetPosition;

        public void SetNurse(bool value) => nurse = value;
        /// <summary>Tests and bots: the cower variant (0 crouch, 1 wall lean, 2 self-hug, 3 shaking stance).</summary>
        public void SetVariant(int value) => variant = ((value % 4) + 4) % 4;

        private void Awake()
        {
            body = GetComponent<IEnemyBody>();
            health = GetComponent<Health>();
            tremble = GetComponent<Tremble>();
            if (tremble == null) tremble = gameObject.AddComponent<Tremble>();
            poseOverride = GetComponent<PoseOverride>();
            if (poseOverride == null) poseOverride = gameObject.AddComponent<PoseOverride>();
            lean = GetComponent<WallLean>();
            if (lean == null) lean = gameObject.AddComponent<WallLean>();
            variant = nextVariant++ % 4;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetVariants() => nextVariant = 0;

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
            panicLine = LineKillSeen;
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
            Alert("hurt", callOthers: true); // a hurt alerts it; only a kill makes it run (Bruno, 2026-10-25)
        }

        /// <summary>A shock of the short stuns it: it stops where it is, and a care stops.</summary>
        public void Stagger(float seconds)
        {
            if (Current == Phase.Dead || seconds <= 0f) return;
            stunUntil = Mathf.Max(stunUntil, Time.time + seconds);
            if (Current == Phase.Care) GetComponent<CorpseCare>()?.Stop("stunned");
            SetPose(Pose.Controller);
            body?.Stop();
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
            SetPose(Pose.Controller);
            body?.Stop();
        }

        // ── The care (CorpseCare) ─────────────────────────────────────

        /// <summary>Free to tend a body: alive, on its feet, not begging. A panicked one leaves its corner for it.</summary>
        public bool CanCare => Current != Phase.Dead && Current != Phase.Beg && Current != Phase.Care && !suspended && body != null;
        public bool CareIgnoresHer => nurse;
        // The care barks (barks.csv, the writer, 2026-10-25). The lab worker has no drag line, and no collar (Bruno).
        public string CareStartLine => nurse ? "Don't move. Don't try to move, I'm coming." : "Is she gone? Okay. Hey. Hey. Can you hear me?";
        public string DragLine => nurse ? "I've got you. Nearly there. Nearly there." : null;
        public string CprLine => nurse ? "Come on. Breathe for me. I'm not done with you." : "How hard do I push? How hard do I push?";

        public bool TryBeginCare()
        {
            if (!CanCare) return false;
            Current = Phase.Care;
            backing = false;
            SetPose(Pose.Controller);
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
            SetPose(Pose.Controller);
            body.Crouch(false);
            tremble.Amount = waryTremble;
            Bark.Say(gameObject, nurse ? LineNurseBack : LineBack);
            RunTelemetry.Event("civilian_wary", name);
        }

        private void Flee()
        {
            tremble.Amount = 0f;
            SetPose(Pose.Controller);
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
            tremble.Amount = 1f;
            // Its own pose (Bruno, 2026-10-25): the crouch, the wall, the self-hug or the shaking stance.
            // The lean needs a wall close behind; with none, the self-hug stands in.
            bool leaning = variant == 1 && TryLean();
            if (!leaning) SetPose(variant == 0 ? Pose.Crouch : variant == 3 ? Pose.Shake : Pose.Hug);
            body.Crouch(CurrentPose == Pose.Crouch);
            Bark.Say(gameObject, LineCower);
            RunTelemetry.Event("civilian_cower", name);
        }

        private void Beg(string why)
        {
            Current = Phase.Beg;
            Begs++;
            backing = false;
            SetPose(Pose.Crouch);
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
                    SetPose(Pose.Controller);
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

            if (Stunned) { body.Stop(); return; }

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
                    // The body slides on a little after Stop (the Invector walk): the lean variant looks for its wall
                    // again once it stands, every half second.
                    if (variant == 1 && CurrentPose != Pose.Lean && !body.IsMoving && Time.time >= nextStep)
                    {
                        nextStep = Time.time + 0.5f;
                        TryLean();
                    }
                    // The crouch: the back to her, the face to the wall. The lean keeps its back on the wall. The
                    // stances watch her.
                    if (CurrentPose == Pose.Crouch || CurrentPose == Pose.Controller) body.Face(transform.position - Her);
                    else if (CurrentPose != Pose.Lean) body.Face(Her - transform.position);
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

        /// <summary>The standing fear stance of this civilian: the shaking one or the self-hug.</summary>
        private Pose StandPose => variant % 2 == 0 ? Pose.Shake : Pose.Hug;

        /// <summary>The back flat on a wall close behind it, facing the room. False when no wall is close.</summary>
        private bool TryLean()
        {
            if (!WallLean.TryFindWall(transform.position, out Vector3 point, out Vector3 normal, out string why))
            {
                RunTelemetry.Event("civilian_no_wall", $"{name}: {why}");
                return false;
            }
            body.Stop();
            body.Face(normal);
            SetPose(Pose.Lean);
            lean.Begin(point, normal, player);
            RunTelemetry.Event("civilian_lean", name);
            return true;
        }

        /// <summary>The pose over the controller: a fear clip (PoseOverride), the wall (WallLean), or none.</summary>
        private void SetPose(Pose next)
        {
            if (next == CurrentPose || poseOverride == null) return;
            CurrentPose = next;
            if (next != Pose.Lean) lean.End();
            PoseClips clips = PoseClips.Load();
            switch (next)
            {
                case Pose.Startle: poseOverride.Play(clips != null ? clips.afraid : null, loop: false); break;
                case Pose.Shake: poseOverride.Play(clips != null ? clips.scared01 : null, loop: true); break;
                case Pose.Hug:
                case Pose.Lean: poseOverride.Play(clips != null ? clips.scared02 : null, loop: true); break;
                default: poseOverride.Stop(); break;
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
                // It stands and watches her: a startle the first time, then its fear stance (or the wall).
                body.Stop();
                if (CurrentPose == Pose.Lean) return;
                body.Face(toHer);
                if (!startled) { startled = true; SetPose(Pose.Startle); }
                else if (CurrentPose != Pose.Startle || !poseOverride.Playing) SetPose(StandPose);
                return;
            }
            if (Time.time < nextStep) return;
            nextStep = Time.time + backReplanSeconds;
            if (EnemyNav.TryBackStep(transform.position, Her, backStep, out Vector3 step))
            {
                SetPose(Pose.Controller);
                body.StrafeTo(step, toHer);
                return;
            }
            // A wall behind it on every side: it begs when she is near, else it presses its back on the wall.
            if (gap <= begLeaveRange) Beg("cornered");
            else
            {
                body.Stop();
                if (CurrentPose != Pose.Lean && !TryLean())
                {
                    body.Face(toHer);
                    SetPose(StandPose);
                }
            }
        }
    }
}
