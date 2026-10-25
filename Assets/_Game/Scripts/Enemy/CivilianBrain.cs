using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// An unarmed civilian (design §3.4: "unarmed civilians cower, run and beg"; §11.5, Bruno 2026-10-22: "an
    /// unarmed civvie will always run/cower"; the demo story: "the civilians who are not armed run, and the ones who
    /// cannot run kneel"). It never attacks. Phases:
    ///   Idle: at its work until it knows she is there.
    ///   Flee: it knows (it sees her, it is hurt, it hears a shot, an alert or a power hit, it sees a death). It
    ///         screams (the alert calls the armed ones in the radius) and sprints to the spot farthest from her.
    ///   Cower: at the spot it crouches with its back to her.
    ///   Beg: she comes within begRange, or it has nowhere to run (every route out passes near her, or gains
    ///        less than cornerGain): it kneels, faces her and begs. It cowers again when she walks away.
    /// The lift and the ragdoll suspend the body as they do a soldier's; after the drop it runs again. The bark lines
    /// are the writer's (barks.csv, civilian). The poses are placeholders: the vendor crouch for the kneel and the
    /// cower. All numbers are PROPOSED.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class CivilianBrain : MonoBehaviour, IFearful
    {
        public enum Phase { Idle, Flee, Cower, Beg, Dead }

        public const string LineBeg = "Please. Please. I'm sheltering, I'm sheltering in place.";
        public const string LineRun = "Don't. Don't, don't, don't.";
        public const string LineCower = "I didn't see anything. I didn't see anything.";

        [SerializeField, Min(1f)] private float alertRadius = 25f;
        [SerializeField, Min(1f)] private float hearPowerRadius = 12f;
        [SerializeField, Min(0.5f)] private float begRange = 3f;
        [SerializeField, Min(0.5f)] private float begLeaveRange = 5f;
        [SerializeField, Min(0.5f)] private float cornerGain = 3f;   // m: a flee spot that gains less than this is no escape
        [SerializeField, Min(0.5f)] private float avoidRadius = 2f;  // m: a route that passes this near her is no escape
        [SerializeField, Min(1f)] private float fleeSeconds = 8f;
        [SerializeField, Min(0.5f)] private float begBarkSeconds = 3.5f;

        private IEnemyBody body;
        private Health health;
        private Transform player;
        private float timer;
        private float nextBark;
        private bool suspended;

        public Phase Current { get; private set; } = Phase.Idle;
        public bool Alerted { get; private set; }
        public string AlertReason { get; private set; } = "";
        public bool Retreating => Current == Phase.Flee || Current == Phase.Cower || Current == Phase.Beg;
        public int Begs { get; private set; }
        public Vector3 Spot { get; private set; }
        public IEnemyBody Body => body;
        private Vector3 Her => player != null ? player.position : body.TargetPosition;

        private void Awake()
        {
            body = GetComponent<IEnemyBody>();
            health = GetComponent<Health>();
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

        /// <summary>It knows she is here: it runs. The first to know screams, and the scream alerts the armed ones.</summary>
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
            if (!suspended) Flee();
        }

        /// <summary>A death it saw (EnemyMorale): an unarmed civilian's fear limit is 0, so it always runs.</summary>
        public void Retreat(string reason) => Alert(reason, callOthers: false);

        public void Shock(float seconds, bool gruesome) => Alert(gruesome ? "gruesome death" : "death", callOthers: false);

        private void OnAlertHeard(Vector3 origin, float radius, string reason)
        {
            if (Alerted || Current == Phase.Dead) return;
            if (EnemyNav.Flat(transform.position, origin) <= radius) Alert("heard " + reason, callOthers: false);
        }

        private void OnHurt(float amount)
        {
            if (!health.IsDead) Alert("hurt", callOthers: true);
        }

        private void OnHitHeard(HitInfo hit)
        {
            if (Alerted || Current == Phase.Dead || hit.Attacker == null || !hit.Attacker.CompareTag("Player")) return;
            if (EnemyNav.Flat(transform.position, hit.HitPoint) <= hearPowerRadius) Alert("power", callOthers: true);
        }

        private void OnDeath()
        {
            Current = Phase.Dead;
            body?.Stop();
        }

        // ── The phases ────────────────────────────────────────────────

        private void Flee()
        {
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
            Bark.Say(gameObject, LineRun);
            RunTelemetry.Event("civilian_flee", $"{name} to {Spot.x:0.0},{Spot.z:0.0}");
        }

        private void Cower()
        {
            Current = Phase.Cower;
            body.Stop();
            body.Crouch(true);
            Bark.Say(gameObject, LineCower);
            RunTelemetry.Event("civilian_cower", name);
        }

        private void Beg(string why)
        {
            Current = Phase.Beg;
            Begs++;
            body.Stop();
            body.Crouch(true);
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
                    body.Suspend(true);
                }
                return;
            }
            if (suspended)
            {
                suspended = false;
                body.Suspend(false);
                if (Alerted) Flee();
            }

            if (!Alerted && body.CanSeeTarget) Alert("sight", callOthers: true);
            if (!Alerted) return;
            float gap = EnemyNav.Flat(transform.position, Her);

            switch (Current)
            {
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
                    if (gap > begLeaveRange) Cower();
                    else if (Time.time >= nextBark)
                    {
                        Bark.Say(gameObject, LineBeg);
                        nextBark = Time.time + begBarkSeconds;
                    }
                    break;
            }
        }
    }
}
