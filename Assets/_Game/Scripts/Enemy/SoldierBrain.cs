using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Destruction;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The fight of an FSM soldier: "move or shoot, never both" (Bruno, 2026-10-21). The Invector FSM decides
    /// when the soldier fights (it sees Lucy) and calls TickEngage every frame of that state. The brain then
    /// alternates two phases:
    ///   Move: the gun holds fire, the body walks to a firing spot at preferredRange from Lucy, off to one side
    ///         (flankDegrees), for a few seconds or until it arrives.
    ///   Shoot: the body stops, the gun may aim (laser, lock, the shrinking circle, the shot: design §3.1) when it
    ///         sees her, she is in range and the difficulty gives a slot (ShooterScheduler). After the shot, or
    ///         when no shot starts within shootWaitSeconds, it moves again.
    /// The body never moves while the gun holds a slot (aim or telegraph). A stagger stops it for its seconds.
    /// A lift (TelekinesisLift) suspends the body: no navigation, no physics, the gun holds fire.
    /// Awareness (Bruno, 2026-10-23: "on project warspite, the enemies weren't this dumb"): a soldier is unaware
    /// until it sees Lucy, takes a hit, hears a power hit near it, or hears an alert (EnemyAlert: a fellow soldier's
    /// sight, hit or shot). Then it stays alerted in the room, as Warspite's enemies did: it always knows where she
    /// is (the body tracks her as a fixed target), it calls the others, and it walks to a spot with a clear line
    /// to her. All numbers are PROPOSED.
    /// In the fight it runs, never walks, and sprints to a far spot (Bruno, 2026-10-24: "the enemies move too
    /// slowly ... they don't look like they're in combat at all"). When she comes within melee range during a move,
    /// it stops and the gun picks a melee strike (DummyShooter: the same slot and telegraph as a shot).
    /// Morale (EnemyMorale, design §11.4 and §11.5): a colleague's death shocks it (Shock: it stops, loses its
    /// aim, flinches, barks) and adds fear; a guard at its limit retreats (Retreat: it sprints away from her, then
    /// cowers in a crouch and never attacks again). CorpseCare: it tends a dead colleague (Phase.Care).
    /// A knife (DummyShooter.Weapon.Knife, the knife scientist, 2026-10-27) makes it a rusher: no firing spot and no
    /// cover, it runs at her, finds her again every RushRepathSeconds, and strikes when she is in melee range.
    /// An armed civilian (the lab worker with a pistol) trembles while alerted, and more when it cowers.
    /// A jammed fire door on its way (FireDoor, 2026-11-03): it stands, faces the door and swings while the door says so
    /// (IDoorWaiter); a soldier and the power armour kick a jammed door down, the others bang on it.
    /// The approach (2026-11-03, DEMO_PLAN 13.1, Bruno: "guards would try to face her head-on, soldiers and power armor
    /// flank her"): a soldier or the power armour claims a side of the fight (FlankBoard: two take opposite sides, the
    /// pinch) and prefers firing spots out of her view; a guard keeps to his own line to her; the knife runs to her side
    /// before it strikes. The armed scientist with a pistol comes head-on too (Bruno, 2026-11-03: "head on").
    /// A team soldier knows only what his team saw (FireTeam, 2026-11-06): when she hides, he holds and fires at the place
    /// where they last saw her (suppression), and when she stays lost, he goes through the rooms with a partner (Phase.Search).
    /// </summary>
    [RequireComponent(typeof(DummyShooter), typeof(Health))]
    public sealed class SoldierBrain : MonoBehaviour, IFearful, ICarer, IDoorWaiter, IDominable
    {
        public enum Phase { Idle, Move, Shoot, Hold, Dead, Care, Retreat, Cower, Duck, Search }

        [SerializeField, Min(1f)] private float preferredRange = 6f;
        [SerializeField] private Vector2 moveSeconds = new Vector2(1.5f, 3f);
        [SerializeField, Min(0.5f)] private float shootWaitSeconds = 3f;
        [SerializeField, Range(0f, 90f)] private float flankDegrees = 50f;
        [SerializeField, Min(0f)] private float recoverSeconds = 0.6f; // after a drop from the lift
        [SerializeField, Min(0f)] private float sprintOver = 8f;       // sprints to a spot farther than this
        [SerializeField, Min(0f)] private float minMoveSeconds = 0.6f;  // a new path needs a moment before "arrived" counts
        [SerializeField, Min(0.1f)] private float arriveRadius = 0.7f;
        [SerializeField, Min(1f)] private float alertRadius = 25f;
        [SerializeField, Min(1f)] private float hearPowerRadius = 12f;

        private IEnemyBody body;
        private DummyShooter gun;
        private Health health;
        private float timer;
        private int shotsAtShootStart;
        private int lastEngageFrame = -10;
        private bool suspendedByLift;
        private Vector3 shootPosition;
        private float moveStarted;
        private bool aimStarted;
        private bool driftLogged;
        private Transform player;
        private Tremble tremble;
        private float doorWait;
        private Vector3 doorPoint;

        public Phase Current { get; private set; } = Phase.Idle;
        public bool Engaged => Time.frameCount - lastEngageFrame <= 2;
        public Vector3 Spot { get; private set; }
        /// <summary>The barricade he stands behind, or null.</summary>
        /// <summary>His team (the soldiers of a fight), or null: guards, scientists and the armour fight alone.</summary>
        public FireTeam Team { get; private set; }
        private bool boundCalled;
        private const float TeamRange = 8f;          // m: a soldier of a team keeps this far from her, PROPOSED
        private const float TeamMinRange = 5f;       // m: no spot of a team nearer than this
        private const float TeamFallBackRange = 7f;  // m: the last two take cover at least this far
        private const float BoundLeaves = 1.5f;      // m: a bound goes at least this far from where he stands

        /// <summary>His element's turn: he goes when his shot is out.</summary>
        public void BoundNow() => boundCalled = true;

        /// <summary>The spot he holds (CoverMap), or null.</summary>
        public CoverSpot HeldCover { get; private set; }
        /// <summary>The breakable he holds as cover, or null (a spot behind a thing that does not break).</summary>
        public Breakable Cover => HeldCover != null ? HeldCover.Piece : null;
        public int Ducks { get; private set; }

        private const float RushRepathSeconds = 0.3f; // the knife finds her again this often
        private const float FrontCone = 60f;          // degrees each side of her facing: her view, PROPOSED
        private const float FrontCost = 8f;           // m added to a spot in her view (a flanker), PROPOSED
        private const float FlankStep = 60f;          // degrees around her a flanker turns in one move, PROPOSED
        private const float RushSideFrom = 3f;        // m: farther than this the knife runs to her side
        private const float RushSideOffset = 2f;      // m beside her (1.6 brought it in at 47 degrees, 2026-11-03)
        private const float CowerLineRange = 4f;      // m: she comes this near, and a figure that cowers speaks, PROPOSED
        private const float CowerLineGap = 6f;        // s between its lines, PROPOSED
        private const float CowerLineSeconds = 3f;
        private const float HitLineGap = 4f;          // s: one "hit" line in this time at most, PROPOSED
        private const float FireLineGap = 8f;         // s: one "fires" line in this time at most, PROPOSED
        private const float FireLineChance = 0.35f;   // a shot gives a line this often (the gap allows it), PROPOSED
        private const float ArmedCivilianNerves = 0.35f; // the tremble of an alerted armed civilian, PROPOSED
        private const float CoverMinRange = 2.5f;    // m from her: closer is no cover
        private const float CoverExtraRange = 4f;    // m beyond the preferred range
        private const float CoverSearch = 14f;       // m he walks for cover at most
        private const float CoverWalkSeconds = 6f;   // s the walk to cover may take
        private const float CoverFacing = 0.5f;      // the thing lies within 60 degrees of his line to her
        private const float CoverHolds = 0.3f;       // it still counts until she stands past 72 degrees
        private const float CornerCost = 3f;         // m added to a corner: low cover first, he fires from it with no walk
        private const float CoverPathClear = 3f;     // m: his way to cover keeps this far from her
        private const int CoverTries = 12;           // the nearest spots that get the costly line checks
        private static readonly Vector2 DuckSeconds = new Vector2(1.5f, 3f); // PROPOSED
        private const float CoverArriveRadius = 0.4f; // m: he stands behind the thing, not beside it
        private const float CoverStallRadius = 1.2f;  // m: the vendor AI can stop this short: a stop this near counts as there
        /// <summary>Frames in which the body moved while the gun held a slot. The rule wants zero.</summary>
        public int MovedWhileShootingFrames { get; private set; }
        public int Moves { get; private set; }
        /// <summary>The firing spots it picked, and those in her view at the pick (the flanking tests).</summary>
        public int SpotsPicked { get; private set; }
        public int SpotsInHerView { get; private set; }
        /// <summary>The angle of the last spot from his own line to her, at the pick (a guard keeps it small).</summary>
        public float LastSpotOffLine { get; private set; }
        /// <summary>The knife's runs to her side (the flanking tests).</summary>
        public int SideRuns { get; private set; }
        public enum Approach { Free, HeadOn, Flank }
        /// <summary>How it closes in (Bruno, 2026-11-03): guards and the pistol scientist head-on, soldiers and the power armour flank.</summary>
        public Approach Style => Rusher ? Approach.Free : RankOf == EnemyMorale.Rank.Guard || RankOf == EnemyMorale.Rank.ArmedCivilian ? Approach.HeadOn
            : RankOf == EnemyMorale.Rank.Soldier || RankOf == EnemyMorale.Rank.Heavy ? Approach.Flank : Approach.Free;
        public int ShootPhases { get; private set; }
        public IEnemyBody Body => body;
        /// <summary>A knife: it runs at her and strikes, it never stops at a firing spot.</summary>
        public bool Rusher => gun != null && gun.WeaponKind == DummyShooter.Weapon.Knife;
        /// <summary>It closes in to strike (the knife, or the baton): no firing spot, no cover.</summary>
        private bool Closer => Rusher || BatonOut;

        // ── The baton (the overconfident guard, round 134; Bruno, 2026-11-13: "he strikes") ──
        // GAME_NOTES.md: "He does not aim at a patient with no gun. He walks at her slowly with the baton raised, and chases her if
        // she runs. When she attacks, the baton goes and he draws his gun, then he aims and fires." She attacks: a hurt on him, a
        // lift on him, or a hit of her powers that he hears (hearPowerRadius, his room or next door), PROPOSED.
        /// <summary>The baton is out: he walks at her and strikes, the gun stays in the holster.</summary>
        public bool BatonOut { get; private set; }
        /// <summary>He runs after her: she ran from him with the baton out.</summary>
        public bool Chasing { get; private set; }
        public int Chases { get; private set; }
        /// <summary>Why the gun came out ("" while the baton is out, or when he never had one).</summary>
        public string DrewReason { get; private set; } = "";
        public const string BatonPersonality = "overconfident";
        /// <summary>
        /// The baton can come out. Off in batch runs (the tests and the bots): the personality at the spawn is random, and a
        /// test or a bot that needs a guard's gun must not get a baton by chance (as batch runs ignore the saved difficulty).
        /// BatonTests turns it on.
        /// </summary>
        public static bool BatonEnabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBaton() => BatonEnabled = !Application.isBatchMode;
        private const float ChaseHerSpeed = 2.6f;    // m/s: she runs (walk 1.6, run 3.6), PROPOSED
        private const float ChaseEndSeconds = 1.5f;  // s she walks or stands before he walks again, PROPOSED
        private const float ChaseLineGap = 6f;       // s, PROPOSED
        private const float BatonLineGap = 9f;       // s, PROPOSED
        private const float BatonFirstLine = 3f;     // s after the alert (after "spots Lucy"), PROPOSED
        private const float BatonRepathSeconds = 0.6f;
        private Vector3 herLast;
        private float herSpeed;
        private float slowFor;
        private float batonLineAt;
        public DummyShooter Gun => gun;
        public bool Alerted { get; private set; }
        public int ShockCount { get; private set; }
        public string RetreatReason { get; private set; } = "";
        /// <summary>Free to tend a body: alive, not retreating or cowering, not held, no aim.</summary>
        public bool CanCare => !IsDominated && !kneeling && Current != Phase.Dead && Current != Phase.Retreat && Current != Phase.Cower
            && Current != Phase.Hold && Current != Phase.Care && Current != Phase.Duck && !suspendedByLift && !gun.HoldsSlot;
        public bool Retreating => Current == Phase.Retreat || Current == Phase.Cower;
        /// <summary>A soldier tends a body only while he does not see her (CorpseCare).</summary>
        public bool CareIgnoresHer => false;
        // The care barks: the writer's rows of his enemy and personality (barks.csv, BarkBook, 2026-11-13).
        public string CareStartLine => BarkBook.Line(gameObject, BarkSituation.StartsCare);
        public string DragLine => BarkBook.Line(gameObject, BarkSituation.Drags);
        public string CprLine => BarkBook.Line(gameObject, BarkSituation.Cpr);
        private float nextCowerLine;

        /// <summary>
        /// What a figure that cowers says when she comes near (Bruno, 2026-11-05: "he won't attack and keeps turning its back on
        /// me?": the line tells the player that he gave up). The writer's "cower" rows of his personality (BarkBook): the
        /// "plays first" row first (the weapon goes down), then the others at random, with no repeat until all have played.
        /// </summary>
        private void CowerLine()
        {
            if (Time.time < nextCowerLine) return;
            Vector3 her = TargetPosition;
            if (Flat(transform.position, her) > CowerLineRange || !GlorifiedBody.Rooms.Room.Same(transform.position, her)) return;
            nextCowerLine = Time.time + CowerLineGap;
            BarkBook.Say(gameObject, BarkSituation.Cower, CowerLineSeconds);
        }

        private bool IsArmedCivilian => GetComponent<EnemyMorale>() is EnemyMorale m && m.RankOf == EnemyMorale.Rank.ArmedCivilian;
        private EnemyMorale.Rank RankOf => GetComponent<EnemyMorale>() is EnemyMorale m ? m.RankOf : EnemyMorale.Rank.Soldier;
        public string AlertReason { get; private set; } = "";
        /// <summary>Where she is: her real position once alerted (a team: what the team saw), else the last seen position.</summary>
        public Vector3 TargetPosition => Alerted && player != null ? (Team != null && Team.HerHidden && quarry == null ? Team.LastSeen : player.position) : body.TargetPosition;

        /// <summary>
        /// He sees her now, for his team. In the fight a clear line is enough: he watches her. When she is lost he must look
        /// the right way too, or stand near (FireTeam).
        /// </summary>
        public bool SpotsHer(bool lost)
        {
            if (player == null || quarry != null || Current == Phase.Dead || suspendedByLift || !ClearLineToHer()) return false;
            if (!lost) return true;
            float distance = Flat(transform.position, player.position);
            if (distance <= SearchNearSight) return true;
            if (distance > SearchFarSight) return false;
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            return Vector3.Angle(transform.forward, to) <= SearchCone;
        }

        private bool ClearLineToHer() => gun.Sees(player.position + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight);

        public Vector3 HerPosition => player != null ? player.position : transform.position;

        private const float SearchNearSight = 4f;  // m: he notices her this near, whichever way he looks, PROPOSED
        private const float SearchFarSight = 18f;  // m, PROPOSED
        private const float SearchCone = 75f;      // degrees each side of his facing, PROPOSED
        private const float SearchLegSeconds = 12f; // s a leg of the search may take
        private const float SearchTurnRate = 120f; // degrees a second, the partner who covers turns to the walker's goal
        private Vector3? searchGoal;
        private float searchLegStart;
        private Vector3? suppressPlace;
        private bool suppressTried;

        private void Awake()
        {
            body = GetComponent<IEnemyBody>();
            gun = GetComponent<DummyShooter>();
            health = GetComponent<Health>();
            gun.HoldFire = true;
            gun.RequireLineOfSight = true;
            gun.OnStaggered += Hold;
            tremble = GetComponent<Tremble>();
        }

        private void OnEnable()
        {
            health.OnDeath ??= new UnityEngine.Events.UnityEvent();
            health.OnDeath.AddListener(OnDeath);
            health.OnDamaged ??= new UnityEngine.Events.UnityEvent<float>();
            health.OnDamaged.AddListener(OnHurt);
            EnemyAlert.OnAlert += OnAlertHeard;
            HitEventBus.OnHit += OnHitHeard;
            gun.OnFired += OnOwnShot;
        }

        private void OnDisable()
        {
            health.OnDeath?.RemoveListener(OnDeath);
            health.OnDamaged?.RemoveListener(OnHurt);
            EnemyAlert.OnAlert -= OnAlertHeard;
            HitEventBus.OnHit -= OnHitHeard;
            gun.OnFired -= OnOwnShot;
        }

        // ── Awareness ─────────────────────────────────────────────────

        /// <summary>The soldier knows where Lucy is from now on, and calls the others when it is the first to know.</summary>
        public void Alert(string reason, bool callOthers)
        {
            if (Alerted || Current == Phase.Dead || (ScriptHeld && passive)) return;
            if (player == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                player = p != null ? p.transform : null;
                if (player == null) return;
            }
            if (HoldsPost(reason))
            {
                PostHeld++;
                return;
            }
            ScriptHeld = false;
            Alerted = true;
            AlertReason = reason;
            if (reason == "sight") BarkBook.Say(gameObject, BarkSituation.SpotsLucy, 2.2f); // the first sight of her (barks.csv, "spots Lucy")
            if (TakesBaton())
            {
                if (SheAttacked(reason)) DrawGun(reason, say: true); // she attacked first: no baton
                else TakeBaton();
            }
            if (RankOf == EnemyMorale.Rank.Soldier) Team = FireTeam.Join(this, player.position);
            body.Track(player);
            RunTelemetry.Event("soldier_alerted", $"{name}:{reason}");
            if (callOthers) EnemyAlert.Raise(transform.position, alertRadius, reason);
        }

        /// <summary>
        /// A scripted talk (the scientist of W1B, Bruno, 2026-11-02: "it should do the dialogue then he attacks her"): he
        /// sees her but does not attack, walks where the script sends him and turns to her. The script's Alert ends it,
        /// and so does a hurt or a power near him (she attacks first).
        /// </summary>
        public bool ScriptHeld { get; private set; }

        public void ScriptHold(Vector3? walkTo)
        {
            if (Alerted || Current == Phase.Dead) return;
            ScriptHeld = true;
            scriptFacesHer = true;
            scriptWalk = walkTo; // sent from Update: the vendor AI takes a path only after its Start
        }

        /// <summary>
        /// It waits where it stands, unaware, until the story flag goes up (the scientist of W1B at his bed before the talk of
        /// cutscene 02). An Alert, a hurt or a power near it ends the wait too. A load
        /// past the flag lets it go at once.
        /// </summary>
        public void ScriptHoldUntil(string flag)
        {
            if (Alerted || Current == Phase.Dead) return;
            ScriptHeld = true;
            scriptFacesHer = false;
            holdFlag = flag;
        }

        /// <summary>
        /// A test room with its AI off (Bruno, 2026-11-07: "please disable the ai or something"): as ScriptHoldUntil, but a hurt, a power, a
        /// scream or a sight of her does not wake it. Only the flag does, or mk_edgar taking its mind.
        /// </summary>
        public void PassiveUntil(string flag)
        {
            ScriptHoldUntil(flag);
            passive = true;
        }

        /// <summary>
        /// A post he holds (the second fireteam in the Dining Hall, DEMO_PLAN §3.4: "It does not leave the room to hunt her
        /// before she takes the duty key"): he fights inside the room, and every move ends inside it until the flag goes up.
        /// </summary>
        public void LeashUntil(GlorifiedBody.Rooms.Room room, string flag)
        {
            leashRoom = room;
            leashFlag = flag;
        }

        /// <summary>
        /// Stay at post (Bruno, round 130, DEMO_PLAN §3.4 item 4: the six of the Dining Hall stay in G18 during a fight in
        /// G2): a man on a post hears a fight, sees her or feels a power outside his post, and holds it. He wakes when she
        /// is in his post, when he is hurt, a shield blocks her, or a script calls him. After the duty key the leash goes.
        /// </summary>
        private bool HoldsPost(string reason)
        {
            if (!Leashed || player == null) return false;
            if (reason == "hurt" || reason == "blocked" || reason == "script" || reason == "control") return false;
            return GlorifiedBody.Rooms.Room.At(player.position + Vector3.up * 0.1f) != leashRoom;
        }

        /// <summary>The alerts he let pass to hold his post (a test reads it).</summary>
        public int PostHeld { get; private set; }

        public bool Leashed => leashRoom != null && (string.IsNullOrEmpty(leashFlag) || !GlorifiedBody.Saving.StoryFlags.Has(leashFlag));
        public GlorifiedBody.Rooms.Room LeashRoom => leashRoom;
        private GlorifiedBody.Rooms.Room leashRoom;
        private string leashFlag = "";
        public const float LeashMargin = 0.8f; // m inside the room's walls

        /// <summary>Every move of the fight goes here: on a leash, a point outside the room becomes the nearest point inside it.</summary>
        private Vector3 Go(Vector3 point, MoveSpeed speed)
        {
            if (Leashed && !leashRoom.Contains(point, -LeashMargin))
            {
                Vector3 local = point - leashRoom.transform.position;
                local.x = Mathf.Clamp(local.x, leashRoom.rect.x + LeashMargin, leashRoom.rect.z - LeashMargin);
                local.z = Mathf.Clamp(local.z, leashRoom.rect.y + LeashMargin, leashRoom.rect.w - LeashMargin);
                Vector3 inside = leashRoom.transform.position + local;
                inside.y = leashRoom.BaseY;
                point = UnityEngine.AI.NavMesh.SamplePosition(inside, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas) ? hit.position : inside;
            }
            body.MoveTo(point, speed);
            return point;
        }

        private bool passive;
        private Vector3? scriptWalk;
        private bool scriptFacesHer;
        private string holdFlag = "";

        private void FaceHerWhileHeld(float dt)
        {
            if (scriptWalk.HasValue)
            {
                body.MoveTo(scriptWalk.Value, MoveSpeed.Walk);
                scriptWalk = null;
                return;
            }
            if (player == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                player = p != null ? p.transform : null;
            }
            if (!scriptFacesHer || player == null || body.IsMoving) return;
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            body.Face(Vector3.RotateTowards(transform.forward, to.normalized, ScriptTurnRate * Mathf.Deg2Rad * dt, 0f));
        }

        private const float ScriptTurnRate = 180f; // degrees a second, a held talker turns to her

        private void OnAlertHeard(Vector3 origin, float radius, string reason)
        {
            if (Alerted || Current == Phase.Dead) return;
            if (Flat(transform.position, origin) <= radius && GlorifiedBody.Rooms.Room.Hear(transform.position, origin)) Alert("heard " + reason, callOthers: false); // walls stop it two rooms away (2026-11-02)
        }

        private void OnHurt(float amount)
        {
            if (health.IsDead) return;
            BarkVoice.Of(gameObject)?.SayNow(BarkSituation.Hit, HitLineGap);
            DrawGun("hurt", say: true);
            Alert("hurt", callOthers: true);
            Team?.Heard(player != null ? player.position : transform.position); // she did this: he knows where she is
        }

        private void OnOwnShot(Vector3 origin, Vector3 direction)
        {
            BarkVoice.Of(gameObject)?.SayNow(BarkSituation.Fires, FireLineGap, FireLineChance);
            EnemyAlert.Raise(transform.position, alertRadius, "gunshot");
        }

        /// <summary>A power that hits something near it is loud: a blast on a crate, a burn on a fellow soldier.</summary>
        private void OnHitHeard(HitInfo hit)
        {
            if (Current == Phase.Dead || hit.Attacker == null || !hit.Attacker.CompareTag("Player")) return;
            if (Flat(transform.position, hit.HitPoint) > hearPowerRadius || !GlorifiedBody.Rooms.Room.Hear(transform.position, hit.HitPoint)) return;
            if (!Alerted) Alert("power", callOthers: true);
            else Team?.Heard(hit.Attacker.transform.position); // a power is loud: it gives her away
            DrawGun("power", say: true);
        }

        private void OnDestroy()
        {
            if (gun != null) gun.OnStaggered -= Hold;
            FlankBoard.Release(this);
        }

        private void OnDeath()
        {
            DrawGun("death", say: false);
            Current = Phase.Dead;
            ReleaseCover();
            ClearSuppression();
            FlankBoard.Release(this);
            gun.HoldFire = true;
            body?.Stop();
        }

        /// <summary>Stops the soldier for some seconds (a stagger, a drop). The gun lost its aim already.</summary>
        public void Hold(float seconds)
        {
            if (Current == Phase.Dead) return;
            if (Current == Phase.Retreat || Current == Phase.Cower)
            {
                gun.HoldFire = true;
                return; // a frightened one does not come back to the fight
            }
            if (Current == Phase.Care) GetComponent<CorpseCare>()?.Stop("held");
            Current = Phase.Hold;
            timer = Mathf.Max(timer, seconds);
            gun.HoldFire = true;
            body?.Stop();
        }

        // ── mk_edgar ──────────────────────────────────────────────────

        private bool kneeling;
        private float nextQuarryPick;
        private Transform quarry; // the dominated figure he hunts now, or null: her

        public Dominated Mind { get; private set; }
        public bool IsDominated => Mind != null;
        public bool IsDeadFigure => Current == Phase.Dead;

        public void BeginKneel()
        {
            if (Current == Phase.Dead) return;
            kneeling = true;
            if (ducked) StandUp();
            if (Current == Phase.Care) GetComponent<CorpseCare>()?.Stop("kneel");
            if (gun.HoldsSlot) gun.Stagger(0.1f);
            gun.HoldFire = true;
            body.Stop();
            body.Crouch(true);
            KneelPose.Set(gameObject, true);
            Dominated.SayKneel(gameObject, fighter: true);
        }

        public void EndKneel()
        {
            if (!kneeling) return;
            kneeling = false;
            if (Current == Phase.Dead) return;
            body.Crouch(Current == Phase.Cower);
            KneelPose.Set(gameObject, false);
            if (Current == Phase.Hold) timer = recoverSeconds;
        }

        public void TakeMind(Dominated mind)
        {
            DrawGun("dominated", say: false);
            EndKneel();
            Mind = mind;
            if (Current == Phase.Care) GetComponent<CorpseCare>()?.Stop("dominated");
            FlankBoard.Release(this);
            ReleaseCover();
            ClearSuppression();
            Team = null;
            ScriptHeld = false;
            Alerted = true;
            AlertReason = "dominated";
            body.Crouch(false);
            Current = Phase.Idle;
            gun.FromCover = false;
            gun.RequireLineOfSight = true;
            if (tremble != null) tremble.Amount = 0f; // the writer: the shaking stops
        }

        public void ReleaseMind()
        {
            Mind = null;
            gun.SetTarget(null);
            player = null;
            GameObject her = GameObject.FindWithTag("Player");
            player = her != null ? her.transform : null;
            if (player != null) body.Track(player);
            if (RankOf == EnemyMorale.Rank.Soldier && player != null) Team = FireTeam.Join(this, player.position);
            if (Current != Phase.Dead) Current = Phase.Idle;
        }

        /// <summary>The fight of a dominated figure: it hunts this one like a soldier hunts her.</summary>
        public void Hunt(Transform target)
        {
            if (gun.HoldsSlot || target == player) return;
            player = target;
            gun.SetTarget(target);
            body.Track(target);
        }

        /// <summary>No one to fight: the gun down, and the fight's phases end.</summary>
        public void StandDown()
        {
            gun.HoldFire = true;
            if (Current == Phase.Move || Current == Phase.Shoot || Current == Phase.Duck) Current = Phase.Idle;
        }

        /// <summary>The nearer of her and the dominated figure, or the figure when he has no line to her. It changes only between aims.</summary>
        private void PickQuarry()
        {
            if (Time.time < nextQuarryPick || gun.HoldsSlot) return;
            nextQuarryPick = Time.time + QuarryGap;
            Dominated mind = Domination.Current;
            Transform wanted = null;
            if (mind != null && mind.gameObject != gameObject && GlorifiedBody.Rooms.Room.Hear(transform.position, mind.transform.position))
            {
                GameObject her = GameObject.FindWithTag("Player");
                bool herNearer = her != null && Flat(transform.position, her.transform.position) <= Flat(transform.position, mind.transform.position)
                    && gun.Sees(her.transform.position + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight);
                if (!herNearer) wanted = mind.transform;
            }
            if (wanted == quarry) return;
            quarry = wanted;
            if (wanted != null) Dominated.SaySeen(gameObject);
            GameObject lucy = wanted == null ? GameObject.FindWithTag("Player") : null;
            player = wanted != null ? wanted : lucy != null ? lucy.transform : null;
            gun.SetTarget(wanted);
            if (player != null) body.Track(player);
        }

        private const float QuarryGap = 0.3f; // s

        private void Update()
        {
            if (ducked && Current != Phase.Duck) StandUp(); // a hit, a lift or a fear took him out of the duck
            if (body == null || Current == Phase.Dead) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (kneeling)
            {
                Current = Phase.Hold;
                timer = recoverSeconds;
                gun.HoldFire = true;
                body.Stop();
                return;
            }

            TelekinesisLift lift = GetComponent<TelekinesisLift>();
            // Lifted, or limp on the floor and standing up (EnemyRagdoll): the body does nothing.
            if ((lift != null && lift.IsLifted) || RagdollBodies.IsDown(this))
            {
                if (!suspendedByLift)
                {
                    suspendedByLift = true;
                    if (lift != null && lift.IsLifted) DrawGun("lift", say: false); // the line when he lands
                    body.Suspend(true);
                    gun.HoldFire = true;
                    if (Current == Phase.Care) GetComponent<CorpseCare>()?.Stop("down");
                    if (!Retreating) Current = Phase.Hold; // a frightened one stays frightened after the fall
                    RunTelemetry.Event(lift != null && lift.IsLifted ? "soldier_lifted" : "soldier_down", name);
                }
                timer = recoverSeconds;
                return;
            }
            if (suspendedByLift)
            {
                suspendedByLift = false;
                body.Suspend(false);
                Hold(recoverSeconds);
            }

            if (ScriptHeld && !string.IsNullOrEmpty(holdFlag) && GlorifiedBody.Saving.StoryFlags.Has(holdFlag)) ScriptHeld = false;
            if (ScriptHeld)
            {
                FaceHerWhileHeld(dt);
                return;
            }
            if (doorWait > 0f)
            {
                doorWait -= dt;
                gun.HoldFire = true;
                body.Stop();
                body.Face(doorPoint - transform.position);
                if (doorWait <= 0f) DoorDone();
                return;
            }
            if (!Alerted && body.CanSeeTarget) Alert("sight", callOthers: true);
            if (BatonOut) WatchHerRun(dt);
            else if (pendingDrawLine && !suspendedByLift && Current != Phase.Dead)
            {
                pendingDrawLine = false;
                BarkBook.Say(gameObject, BarkSituation.DrawsGun);
            }
            // An armed civilian is no fighter: its hands shake while it hunts her, and it shakes hard when it cowers.
            if (tremble != null && IsArmedCivilian) tremble.Amount = Current == Phase.Cower ? 1f : Alerted ? ArmedCivilianNerves : 0f;

            if (Current == Phase.Retreat)
            {
                gun.HoldFire = true;
                timer -= dt;
                if (timer <= 0f || (timer < 7.4f && Flat(transform.position, Spot) <= 1f)) // Invector's own arrival flag lies
                {
                    Current = Phase.Cower;
                    body.Stop();
                    body.Crouch(true);
                    if (TryGetComponent(out EnemyGun weapon)) weapon.Drop();
                    if (TryGetComponent(out EnemyShield shield)) shield.Drop();
                    RunTelemetry.Event("soldier_cower", name);
                }
                return;
            }
            if (Current == Phase.Cower)
            {
                gun.HoldFire = true;
                body.Stop();
                body.Face(transform.position - TargetPosition); // its back to her, head down
                CowerLine();
                return;
            }

            if (Current == Phase.Hold)
            {
                body.Stop();
                timer -= dt;
                if (timer <= 0f) Current = Phase.Idle;
            }

            // The rule: no step while the gun aims or counts down. Measured from the start of the aim: more than
            // 5 cm off that point is a step.
            if (gun.HoldsSlot)
            {
                if (!aimStarted)
                {
                    aimStarted = true;
                    driftLogged = false;
                    shootPosition = transform.position;
                    body.Brace(true);
                }
                else if (Flat(transform.position, shootPosition) > 0.05f)
                {
                    MovedWhileShootingFrames++;
                    if (!driftLogged)
                    {
                        driftLogged = true;
                        Debug.LogWarning($"[SoldierBrain] {name} drifted {Flat(transform.position, shootPosition):0.00} m during its aim (gun {gun.Current}, phase {Current}).");
                    }
                }
                body.Stop();
            }
            else if (aimStarted)
            {
                aimStarted = false;
                body.Brace(false);
            }

            // Out of the fight state (the FSM searches or idles): the gun holds fire after its shot.
            if (!Engaged && Current != Phase.Hold && Current != Phase.Care)
            {
                gun.HoldFire = true;
                if (Current != Phase.Idle && !gun.HoldsSlot) Current = Phase.Idle;
            }
        }

        // ── Doors ─────────────────────────────────────────────────────

        public bool Hunting => Alerted && !ScriptHeld && Current != Phase.Dead && !Retreating && !suspendedByLift;
        public Vector3 Quarry => TargetPosition;
        public bool CanBreach => RankOf == EnemyMorale.Rank.Soldier || RankOf == EnemyMorale.Rank.Heavy;
        public bool IsHeavy => RankOf == EnemyMorale.Rank.Heavy;
        /// <summary>A door holds it now (FireDoor).</summary>
        public bool AtDoor => doorWait > 0f;

        public void WaitAtDoor(Vector3 door, float seconds)
        {
            if (Current == Phase.Dead) return;
            doorPoint = door;
            if (seconds <= 0f)
            {
                if (doorWait > 0f) DoorDone();
                doorWait = 0f;
                return;
            }
            if (doorWait <= 0f)
            {
                if (gun.HoldsSlot) gun.Stagger(0.1f); // no aim through a door
                if (Current == Phase.Care) GetComponent<CorpseCare>()?.Stop("door");
            }
            doorWait = Mathf.Max(doorWait, seconds);
        }

        public void SwingAtDoor()
        {
            if (Current == Phase.Dead) return;
            gun.SwingAtDoor();
        }

        /// <summary>The door let it go (open, broken, or no reason left): a new path, through the doorway if it is free.</summary>
        private void DoorDone()
        {
            doorWait = 0f;
            if (Current == Phase.Move || Current == Phase.Shoot || Current == Phase.Duck) Current = Phase.Idle;
        }

        // ── Morale and care ───────────────────────────────────────────

        /// <summary>A colleague died in sight: it stops, loses its aim, flinches and barks.</summary>
        public void Shock(float seconds, bool gruesome)
        {
            if (Current == Phase.Dead || Retreating || IsDominated) return;
            ShockCount++;
            DrawGun("shock", say: false); // the shock line plays
            if (Current == Phase.Care) GetComponent<CorpseCare>()?.Stop("shock");
            gun.Stagger(seconds); // the aim breaks; OnStaggered holds the body for the seconds
            Hold(seconds);
            body?.Flinch(transform.position + transform.forward, false);
            // The writer's lines (barks.csv, round 135: one "shock"): a plain death is "partner down" when his voice has the
            // rows (the guard, the soldier), else "shock"; a gruesome one is always "shock".
            if (gruesome || !BarkBook.Say(gameObject, BarkSituation.PartnerDown)) BarkBook.Say(gameObject, BarkSituation.Shock);
            RunTelemetry.Event("soldier_shocked", $"{name}:{seconds:0.0}s{(gruesome ? ":gruesome" : "")}");
        }

        /// <summary>Its fear reached its rank's limit: it runs from her and cowers, for the rest of the room.</summary>
        public void Retreat(string reason)
        {
            if (Current == Phase.Dead || Retreating || IsDominated) return;
            if (Current == Phase.Care) GetComponent<CorpseCare>()?.Stop("retreat");
            RetreatReason = reason;
            DrawGun("retreat", say: false); // the baton falls, the gun shows, and the cower drops it
            FlankBoard.Release(this);
            gun.HoldFire = true;
            if (gun.HoldsSlot) gun.Stagger(0.1f); // drop the aim
            // The aim braced the body (kinematic): the retreat phase skips the release, and a braced body runs in
            // place (Bruno, 2026-10-24).
            if (aimStarted)
            {
                aimStarted = false;
                body.Brace(false);
            }
            Current = Phase.Retreat;
            Spot = FleeSpot();
            body.Crouch(false);
            Spot = Go(Spot, MoveSpeed.Sprint);
            timer = 8f;
            // A guard who panics is broken for the rest of the night (round 134). The line: "retreats, fear at the limit".
            BarkVoice.Of(gameObject)?.Panic();
            BarkBook.Say(gameObject, BarkSituation.Retreats);
            RunTelemetry.Event("soldier_retreat", $"{name}:{reason} to {Spot.x:0.0},{Spot.z:0.0}");
        }

        /// <summary>The NavMesh spot farthest from her, within 14 m of it.</summary>
        private Vector3 FleeSpot() => EnemyNav.FarthestFrom(transform.position, TargetPosition);

        public bool TryBeginCare()
        {
            if (!CanCare) return false;
            Current = Phase.Care;
            gun.HoldFire = true;
            return true;
        }

        public void EndCare()
        {
            if (Current == Phase.Care) Current = Phase.Idle;
        }

        /// <summary>The FSM fight state calls it every frame.</summary>
        public void TickEngage()
        {
            lastEngageFrame = Time.frameCount;
            Team?.Tick();
            if (body == null || Current == Phase.Dead || Current == Phase.Hold || suspendedByLift || AtDoor) return;
            if (Mind != null)
            {
                if (!Mind.TickFight(this)) return;
            }
            else PickQuarry();
            if (Current == Phase.Care || Retreating) { gun.HoldFire = true; return; }
            float dt = Time.deltaTime;

            if (Team != null && Team.Lost)
            {
                TickSearch(dt);
                return;
            }
            if (Current == Phase.Search)
            {
                searchGoal = null;
                Current = Phase.Idle; // contact
            }

            switch (Current)
            {
                case Phase.Idle:
                    StartMove();
                    break;

                case Phase.Move:
                    gun.HoldFire = true;
                    timer -= dt;
                    bool arrived = Time.time - moveStarted >= minMoveSeconds && (HeldCover != null ? Flat(transform.position, Spot) <= CoverArriveRadius || (!body.IsMoving && Flat(transform.position, Spot) <= CoverStallRadius) : Flat(transform.position, Spot) <= arriveRadius);
                    bool close = gun.HerInReach;
                    if (close)
                    {
                        StartShoot(); // she came close: stop and strike
                    }
                    // A walk to cover runs to the end (CoverWalkSeconds at most): no shot from the open on the way.
                    else if ((timer <= 0f && (HeldCover == null || Time.time - moveStarted >= CoverWalkSeconds)) || arrived)
                    {
                        // From cover the line counts from where he stands to fire: his gun still hangs low behind the thing.
                        if (BatonOut) StartMove(); // the baton: only a strike in reach
                        else if (HeldCover != null ? CoverStillHolds() : ClearLine() && Distance() <= gun.Range) StartShoot();
                        else StartMove();
                    }
                    break;

                case Phase.Shoot:
                    body.Stop();
                    // The walk carries the body for a moment after the stop: no aim until it stands.
                    gun.HoldFire = body.IsMoving && !gun.HoldsSlot;
                    UpdateSuppression();
                    timer -= dt;
                    bool fired = gun.ShotsFired > shotsAtShootStart && !gun.HoldsSlot;
                    bool gotAway = Closer && !gun.HoldsSlot && !gun.HerInReach; // a knife or a baton does not wait for her
                    if (boundCalled && !gun.HoldsSlot) GoOnTheBound();
                    else if (fired || gotAway || (timer <= 0f && !gun.HoldsSlot))
                    {
                        if (!gotAway && CoverStillHolds()) BeginDuck();
                        else StartMove();
                    }
                    break;

                case Phase.Duck:
                    gun.HoldFire = true;
                    timer -= dt;
                    if (gun.HerInReach)
                    {
                        StandUp();
                        StartShoot(); // she came close: stand and strike
                    }
                    else if (boundCalled)
                    {
                        StandUp();
                        GoOnTheBound();
                    }
                    else if (!CoverStillHolds())
                    {
                        StandUp();
                        ReleaseCover();
                        StartMove();
                    }
                    else if (timer <= 0f)
                    {
                        StandUp();
                        if (HeldCover.Low) StartShoot();
                        else StepOutToFire();
                    }
                    else
                    {
                        float off = Flat(transform.position, HeldCover.Position);
                        if (off <= CoverArriveRadius || (!body.IsMoving && off <= CoverStallRadius))
                        {
                            // There: only now he hides.
                            body.Stop();
                            if (HeldCover.Low) body.Crouch(true);
                            if (TryGetComponent(out LockOnTargetable hidden)) hidden.InCover = true;
                        }
                    }
                    break;
            }
        }

        /// <summary>The FSM leaves the fight state.</summary>
        public void Disengage()
        {
            gun.HoldFire = true;
            if (Current == Phase.Move || Current == Phase.Shoot) Current = Phase.Idle;
        }

        private float Distance() => Flat(transform.position, TargetPosition);

        /// <summary>A clear line from the gun to her chest now (the gun turns to her, so the field of view does not count).</summary>
        private bool ClearLine() => gun.Sees(TargetPosition + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight);

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// She hides (FireTeam.HerHidden): the gun fires at the place where the team last saw her, or just beside it when he
        /// has no line to that place, so she cannot step out and the others can move. Without a line to it he moves. She shows
        /// again: the place is forgotten, and the gun goes back to her.
        /// </summary>
        private void UpdateSuppression()
        {
            if (Team == null || !Team.HerHidden)
            {
                gun.SuppressPoint = null;
                return;
            }
            if (!suppressTried)
            {
                suppressTried = true;
                suppressPlace = SuppressPlace();
                if (suppressPlace.HasValue) Team.CallSuppression(this);
            }
            gun.SuppressPoint = suppressPlace;
        }

        private Vector3? SuppressPlace()
        {
            Vector3 place = Team.LastSeen + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight;
            if (gun.Sees(place)) return place;
            Vector3 line = place - transform.position;
            line.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, line).normalized;
            foreach (float step in new[] { 0.8f, -0.8f, 1.6f, -1.6f })
                if (gun.Sees(place + side * step)) return place + side * step;
            return null;
        }

        private void ClearSuppression()
        {
            suppressTried = false;
            suppressPlace = null;
            gun.SuppressPoint = null;
        }

        /// <summary>
        /// She is lost: he goes through the rooms with his partner (FireTeam). One walks while the other stands, faces the
        /// walker's goal and covers him. He walks: a search is no charge. The gun stays down until somebody sees her.
        /// </summary>
        private void TickSearch(float dt)
        {
            if (gun.HoldsSlot) return; // the shot first
            if (Current != Phase.Search)
            {
                if (ducked) StandUp();
                ReleaseCover();
                ClearSuppression();
                gun.FromCover = false;
                Current = Phase.Search;
                searchGoal = null;
                body.Stop();
            }
            gun.HoldFire = true;
            Vector3? order = Team.SearchOrder(this, out Vector3 watch);
            if (order.HasValue)
            {
                if (!searchGoal.HasValue || (searchGoal.Value - order.Value).sqrMagnitude > 0.01f)
                {
                    searchGoal = order;
                    searchLegStart = Time.time;
                    Spot = Go(order.Value, MoveSpeed.Walk);
                    Moves++;
                    RunTelemetry.Event("soldier_search", $"{name}:{order.Value.x:0.0},{order.Value.z:0.0}");
                }
                bool there = Flat(transform.position, order.Value) <= arriveRadius || (Time.time - searchLegStart >= minMoveSeconds && !body.IsMoving && Flat(transform.position, order.Value) <= CoverStallRadius);
                if (there || Time.time - searchLegStart >= SearchLegSeconds)
                {
                    searchGoal = null;
                    Team.LegDone(this);
                }
                return;
            }
            searchGoal = null;
            body.Stop();
            Vector3 to = watch - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) body.Face(Vector3.RotateTowards(transform.forward, to.normalized, SearchTurnRate * Mathf.Deg2Rad * dt, 0f));
        }

        private void StartMove()
        {
            if (gun.HoldsSlot) return; // the shot first, then the legs
            ClearSuppression();
            // A team: not his element's turn, and he can fire from where he stands: he holds and covers the others.
            if (Team != null && !Team.MayMove(this) && (HeldCover != null ? CoverStillHolds() : ClearLine() && Distance() <= gun.Range))
            {
                StartShoot();
                return;
            }
            gun.HoldFire = true;
            gun.FromCover = false;
            Vector3 spot = PickSpot();
            bool sprint = (spot - transform.position).magnitude > sprintOver;
            if (BatonOut)
            {
                // The baton: a slow walk at her, a run only after her (round 134).
                Spot = Go(spot, Chasing ? MoveSpeed.Run : MoveSpeed.Walk);
                if (!Chasing && Time.time >= batonLineAt) BarkVoice.Of(gameObject)?.SayNow(BarkSituation.Baton, BatonLineGap);
                timer = BatonRepathSeconds;
                moveStarted = Time.time;
                Current = Phase.Move;
                Moves++;
                RunTelemetry.Event("soldier_move", $"{name}:{spot.x:0.0},{spot.z:0.0} {(Chasing ? "chase" : "baton")}");
                return;
            }
            Spot = Go(spot, sprint ? MoveSpeed.Sprint : MoveSpeed.Run);
            timer = Rusher ? RushRepathSeconds : Random.Range(moveSeconds.x, moveSeconds.y);
            moveStarted = Time.time;
            Current = Phase.Move;
            Moves++;
            RunTelemetry.Event("soldier_move", $"{name}:{spot.x:0.0},{spot.z:0.0}{(sprint ? " sprint" : " run")}");
        }

        /// <summary>
        /// A firing spot at preferredRange from her (or closer), off to one side. It prefers a spot with a clear line
        /// to her chest, the nearest such one; without one, a random flank spot.
        /// </summary>
        private Vector3 PickSpot()
        {
            Vector3 target = TargetPosition;
            if (Rusher) return RushSpot(target);
            if (BatonOut) return NavMesh.SamplePosition(target, out NavMeshHit at, 1.5f, NavMesh.AllAreas) ? at.position : target; // straight at her
            Vector3 chest = target + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight;
            Approach style = Style;
            Vector3? side = style == Approach.Flank ? FlankBoard.Claim(this, target) : (Vector3?)null;
            Vector3 away = transform.position - target;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();
            // Cover on his side (a flanker) or on his line (a guard); the others take any.
            Vector3? coverDir = style == Approach.Flank ? side : style == Approach.HeadOn ? away : (Vector3?)null;
            float coverTolerance = style == Approach.Flank ? 50f : 30f;
            if (TryCoverSpot(target, chest, coverDir, coverTolerance, out Vector3 covered)) return Note(covered, target);
            // The directions to try: a flanker around his side, a guard around his own line, the others a random side.
            float[] turns;
            Vector3 from;
            if (style == Approach.Flank)
            {
                // He circles her at range toward his side: each move turns at most FlankStep around her (2026-11-03: one
                // straight walk to the far side passed her at arm's reach, and he stopped to strike instead of firing).
                float toSide = Vector3.SignedAngle(away, side.Value, Vector3.up);
                from = Quaternion.Euler(0f, Mathf.Clamp(toSide, -FlankStep, FlankStep), 0f) * away;
                turns = new[] { 0f, 20f, -20f, 40f, -40f };
            }
            else if (style == Approach.HeadOn) { from = away; turns = new[] { 0f, 15f, -15f, 30f, -30f }; }
            else
            {
                from = away;
                float start = Random.Range(-flankDegrees, flankDegrees);
                turns = new float[8];
                for (int i = 0; i < 8; i++) turns[i] = start + i * 45f;
            }
            if (BestSpot(target, chest, from, turns, style, out Vector3 best)) return Note(best, target);
            // A flanker whose side has no spot (a wall behind her, 2026-11-03: the armour stood at the wall and never shot)
            // looks all around her, with the cost of her view still on.
            if (style == Approach.Flank)
            {
                float[] around = new float[8];
                for (int i = 0; i < 8; i++) around[i] = i * 45f;
                if (BestSpot(target, chest, away, around, style, out best)) return Note(best, target);
            }
            Vector3 fallback = target + away * WantedRange;
            if (NavMesh.SamplePosition(fallback, out NavMeshHit near, 2.5f, NavMesh.AllAreas)) fallback = near.position;
            return Note(fallback, target);
        }

        /// <summary>The cheapest spot on the NavMesh with a clear line to her chest, at the turns from the direction.</summary>
        private bool BestSpot(Vector3 target, Vector3 chest, Vector3 from, float[] turns, Approach style, out Vector3 best)
        {
            int blockers = LayerMask.GetMask("Default");
            best = Vector3.zero;
            float bestCost = float.MaxValue;
            // The spot lies where the weapon fires (2026-11-03: the armour stood 3.45 m from her, its launchers refuse
            // under 3.5 m, and it never attacked).
            float nearest = gun.MinAttackDistance > 0f ? gun.MinAttackDistance + 0.6f : 0f;
            for (int i = 0; i < turns.Length; i++)
            {
                foreach (float range in new[] { WantedRange, WantedRange * 0.65f })
                {
                    Vector3 candidate = target + Quaternion.Euler(0f, turns[i], 0f) * from * Mathf.Max(range, nearest);
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
                    float reach = Flat(hit.position, target);
                    if (reach < nearest || reach > gun.Range) continue;
                    if (Physics.Linecast(hit.position + Vector3.up * GlorifiedBody.Characters.Body.MuzzleHeight, chest, blockers, QueryTriggerInteraction.Ignore)) continue;
                    float cost = Flat(transform.position, hit.position) + (style == Approach.Free ? (i == 0 ? 0f : 0.5f) : Mathf.Abs(Mathf.DeltaAngle(0f, turns[i])) * 0.05f);
                    if (style == Approach.Flank && InHerView(hit.position, target)) cost += FrontCost;
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        best = hit.position;
                    }
                }
            }
            return bestCost < float.MaxValue;
        }

        private float WantedRange => Team == null ? preferredRange : Team.FallingBack ? gun.Range : Mathf.Min(TeamRange, gun.Range);

        /// <summary>Her facing, flat.</summary>
        private Vector3 HerForward()
        {
            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                player = p != null ? p.transform : null;
            }
            Vector3 f = player != null ? player.forward : Vector3.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }

        /// <summary>The point stands in her front cone (she sees it).</summary>
        private bool InHerView(Vector3 point, Vector3 her)
        {
            Vector3 to = point - her;
            to.y = 0f;
            return to.sqrMagnitude > 0.01f && Vector3.Angle(HerForward(), to) < FrontCone;
        }

        /// <summary>Counts the pick for the tests: in her view or not, and how far off his own line it lies.</summary>
        private Vector3 Note(Vector3 spot, Vector3 her)
        {
            SpotsPicked++;
            if (InHerView(spot, her)) SpotsInHerView++;
            Vector3 line = transform.position - her, to = spot - her;
            line.y = to.y = 0f;
            LastSpotOffLine = line.sqrMagnitude > 0.01f && to.sqrMagnitude > 0.01f ? Vector3.Angle(line, to) : 0f;
            return spot;
        }

        /// <summary>An overconfident guard with a pistol takes the baton at his alert (round 134).</summary>
        private bool TakesBaton()
        {
            if (!BatonEnabled || DrewReason != "" || BatonOut || IsDominated || RankOf != EnemyMorale.Rank.Guard || gun.WeaponKind != DummyShooter.Weapon.Pistol) return false;
            BarkVoice voice = BarkVoice.Of(gameObject);
            return voice != null && voice.Personality == BatonPersonality && TryGetComponent(out EnemyGun _);
        }

        /// <summary>The alert came from her attack: a hurt, a power he heard, a block of his shield.</summary>
        private static bool SheAttacked(string reason) => reason == "hurt" || reason == "power" || reason == "blocked" || reason == "heard power";

        private void TakeBaton()
        {
            BatonOut = true;
            Chasing = false;
            slowFor = 0f;
            herLast = player != null ? player.position : transform.position;
            herSpeed = 0f;
            meleeBefore = gun.CanMelee;
            gun.CanMelee = true;
            gun.MeleeOnly = true;
            gun.MeleeKillerId = "guard_baton";
            if (TryGetComponent(out EnemyGun weapon)) weapon.SetBaton(true);
            batonLineAt = Time.time + BatonFirstLine;
            if (Current == Phase.Move || Current == Phase.Shoot || Current == Phase.Duck) Current = Phase.Idle;
            RunTelemetry.Event("soldier_baton", name);
        }

        private bool meleeBefore = true;
        private bool pendingDrawLine;

        /// <summary>
        /// She attacked: the baton falls and the gun comes out (round 134), then he fights as a guard does. The line now, or,
        /// in the air, when he lands. A guard who never had the baton out only notes the reason.
        /// </summary>
        public void DrawGun(string reason, bool say)
        {
            if (!BatonOut)
            {
                if (DrewReason == "" && (SheAttacked(reason) || reason == "shock") && TakesBaton())
                {
                    // Not alerted yet, and she attacked: the baton never comes out, the line still plays.
                    DrewReason = reason;
                    if (say) BarkBook.Say(gameObject, BarkSituation.DrawsGun);
                }
                return;
            }
            BatonOut = false;
            Chasing = false;
            DrewReason = reason;
            gun.MeleeOnly = false;
            gun.CanMelee = meleeBefore;
            gun.MeleeKillerId = null;
            if (TryGetComponent(out EnemyGun weapon)) weapon.SetBaton(false);
            if (say) BarkBook.Say(gameObject, BarkSituation.DrawsGun);
            else pendingDrawLine = reason == "lift";
            if (Current == Phase.Move) Current = Phase.Idle; // a new spot, at his range
            RunTelemetry.Event("soldier_draws", $"{name}:{reason}");
        }

        /// <summary>She runs from him: he runs after her. She walks or stands for a moment: he walks again.</summary>
        private void WatchHerRun(float dt)
        {
            if (player == null || !Alerted) return;
            Vector3 now = player.position;
            Vector3 step = now - herLast;
            step.y = 0f;
            herLast = now;
            herSpeed = Mathf.Lerp(herSpeed, step.magnitude / dt, 1f - Mathf.Exp(-8f * dt));
            Vector3 away = now - transform.position;
            away.y = 0f;
            bool runsAway = herSpeed > ChaseHerSpeed && Vector3.Dot(step, away) > 0f && away.magnitude > gun.MeleeRange;
            if (!Chasing && runsAway && Current != Phase.Hold && !Retreating)
            {
                Chasing = true;
                Chases++;
                slowFor = 0f;
                BarkVoice.Of(gameObject)?.SayNow(BarkSituation.Chases, ChaseLineGap);
                if (Current == Phase.Move) timer = 0f; // the run starts at the next step
                RunTelemetry.Event("soldier_chase", name);
            }
            else if (Chasing)
            {
                slowFor = herSpeed > ChaseHerSpeed ? 0f : slowFor + dt;
                if (slowFor >= ChaseEndSeconds)
                {
                    Chasing = false;
                    if (Current == Phase.Move) timer = 0f;
                }
            }
        }

        /// <summary>
        /// The knife (2026-11-03, DEMO_PLAN 13.1): far from her it runs to the point beside her on its own side, so it
        /// comes in from her flank; near, straight at her.
        /// </summary>
        private Vector3 RushSpot(Vector3 target)
        {
            Vector3 point = target;
            if (Distance() > RushSideFrom)
            {
                Vector3 forward = HerForward();
                Vector3 right = new Vector3(forward.z, 0f, -forward.x);
                float side = Vector3.Dot(transform.position - target, right) >= 0f ? 1f : -1f;
                point = target + right * side * RushSideOffset;
                SideRuns++;
            }
            return NavMesh.SamplePosition(point, out NavMeshHit at, 1.5f, NavMesh.AllAreas) ? at.position : target;
        }

        /// <summary>
        /// Cover first (Bruno, 2026-10-24: "cover only enemies, on purpose"; 2026-11-06: every room, by itself, CoverMap): the
        /// nearest free spot with the thing between him and her, inside the range of his gun, a clear line from where he fires,
        /// and no line from where he hides. Numbers PROPOSED.
        /// </summary>
        private bool TryCoverSpot(Vector3 target, Vector3 chest, Vector3? side, float tolerance, out Vector3 spot)
        {
            spot = Vector3.zero;
            if (RankOf == EnemyMorale.Rank.Heavy) return false; // the power armour stands 2.6 m tall
            float nearest = Mathf.Max(Team == null ? CoverMinRange : Team.FallingBack ? TeamFallBackRange : TeamMinRange, gun.MinAttackDistance + 0.6f);
            float farthest = Mathf.Min(gun.Range, preferredRange + CoverExtraRange);
            var options = new List<(float cost, CoverSpot spot)>();
            foreach (CoverSpot option in CoverMap.Spots)
            {
                if (!option.Usable || (option.Owner != null && option.Owner != this)) continue;
                Vector3 fire = option.FirePoint;
                if (Mathf.Abs(fire.y - transform.position.y) > 1.5f) continue;
                if (Team != null && Flat(fire, transform.position) < BoundLeaves) continue; // a bound goes somewhere
                Vector3 away = fire - target;
                away.y = 0f;
                float range = away.magnitude;
                if (range < nearest || range > farthest) continue;
                if (Vector3.Dot(option.Facing, -away / range) < CoverFacing) continue;
                if (side.HasValue && Vector3.Angle(side.Value, away) > tolerance) continue; // on his side or on his line only
                float cost = Flat(transform.position, fire);
                if (cost > CoverSearch) continue;
                if (Style == Approach.Flank && InHerView(fire, target)) cost += FrontCost;
                if (!option.Low) cost += CornerCost;
                options.Add((cost, option));
            }
            options.Sort((a, b) => a.cost.CompareTo(b.cost));
            int blockers = LayerMask.GetMask("Default");
            int tries = 0;
            foreach ((float cost, CoverSpot option) in options)
            {
                if (!CoverMap.Free(option, this)) continue;
                if (++tries > CoverTries) break;
                if (Physics.Linecast(option.FirePoint + Vector3.up * GlorifiedBody.Characters.Body.MuzzleHeight, chest, blockers, QueryTriggerInteraction.Ignore)) continue;
                float hideHeight = option.Low ? 0.7f : GlorifiedBody.Characters.Body.ChestHeight;
                if (!Physics.Linecast(option.Position + Vector3.up * hideHeight, chest, blockers, QueryTriggerInteraction.Ignore)) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(transform.position, option.FirePoint, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                if (PathPassesHer(path, target)) continue; // a walk to cover past her is a charge (2026-11-06)
                if (option != HeldCover)
                {
                    ReleaseCover();
                    HeldCover = option;
                    CoverMap.Claim(option, this);
                    RunTelemetry.Event("soldier_cover", $"{name}:{(option.Low ? "low" : "corner")}:{(option.Piece != null ? option.Piece.Id : "solid")}");
                }
                spot = option.FirePoint;
                return true;
            }
            ReleaseCover();
            return false;
        }

        private bool PathPassesHer(NavMeshPath path, Vector3 her)
        {
            Vector3[] corners = path.corners;
            for (int i = 0; i + 1 < corners.Length; i++)
            {
                Vector3 line = corners[i + 1] - corners[i];
                line.y = 0f;
                float length = line.magnitude;
                if (length < 0.01f) continue;
                Vector3 from = her - corners[i];
                from.y = 0f;
                float along = Mathf.Clamp(Vector3.Dot(from, line / length), 0f, length);
                if ((from - line / length * along).sqrMagnitude < CoverPathClear * CoverPathClear) return true;
            }
            return false;
        }

        private void ReleaseCover()
        {
            if (HeldCover != null) CoverMap.Release(HeldCover);
            HeldCover = null;
        }

        /// <summary>The spot still works: it stands, the thing lies between them, and he has his line and his range.</summary>
        private bool CoverStillHolds()
        {
            if (HeldCover == null || !HeldCover.Usable) return false;
            Vector3 her = TargetPosition;
            Vector3 fire = HeldCover.FirePoint;
            Vector3 to = her - fire;
            to.y = 0f;
            float range = to.magnitude;
            if (range < CoverMinRange || range > gun.Range) return false;
            if (Vector3.Dot(HeldCover.Facing, to / range) < CoverHolds) return false;
            return !Physics.Linecast(fire + Vector3.up * GlorifiedBody.Characters.Body.MuzzleHeight, her + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight,
                LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);
        }

        /// <summary>Between shots he hides: he crouches behind low cover, or goes back behind the corner.</summary>
        private void BeginDuck()
        {
            ClearSuppression();
            Current = Phase.Duck;
            Ducks++;
            ducked = true;
            timer = Random.Range(DuckSeconds.x, DuckSeconds.y);
            // He goes to the spot first: a walk that ran out of time left him short of it, and he crouched and hid in the open (2026-11-06).
            Spot = HeldCover.Position;
            Go(HeldCover.Position, MoveSpeed.Run);
            RunTelemetry.Event("soldier_duck", name);
        }

        private void StandUp()
        {
            ducked = false;
            if (TryGetComponent(out LockOnTargetable lockable)) lockable.InCover = false;
            body.Crouch(false);
        }

        private void StepOutToFire()
        {
            Spot = HeldCover.Step;
            Spot = Go(Spot, MoveSpeed.Run);
            timer = CoverWalkSeconds;
            moveStarted = Time.time;
            Current = Phase.Move;
            Moves++;
        }

        private bool ducked;

        /// <summary>The bound: he leaves where he stands for another spot.</summary>
        private void GoOnTheBound()
        {
            boundCalled = false;
            ReleaseCover();
            StartMove();
        }

        private void StartShoot()
        {
            body.Stop();
            gun.FromCover = HeldCover != null;
            ClearSuppression();
            shotsAtShootStart = gun.ShotsFired;
            timer = shootWaitSeconds;
            Current = Phase.Shoot;
            ShootPhases++;
            RunTelemetry.Event("soldier_shoot", name);
        }
    }
}
