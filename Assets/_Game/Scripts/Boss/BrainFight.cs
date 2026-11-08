using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Cameras;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;
using GlorifiedBody.Rooms;
using GlorifiedBody.Saving;
using GlorifiedBody.Subtitles;

namespace GlorifiedBody.Boss
{
    /// <summary>
    /// The brain fight, B1 (DEMO_PLAN §14.1 and LORE §5, CANON round 10; the demo waits for it, CANON round 52). The camera
    /// is the brain's eye, high in the sector (a FixedCamera that follows her). Its strike (Bruno, 2026-11-08: "the brain
    /// starts unlocked, then it tracks the player at some speed that they can outrun/dodge. then once fully locked it charges
    /// up and then fires/damages the player; making enough damage staggers the brain to cancel the attack"): its aim, a ring
    /// on the floor, starts in the far corner and follows her at trackSpeed (slower than her walk). While she is inside the
    /// ring the lock fills; out of it, it drains. Full: the charge, the aim still follows her, and at its end the strike lands
    /// on the aim. staggerDamage to the brain during the charge cancels it and stuns it. The floor is brain: standing still
    /// for StillSeconds slows her (her slow walk is slower than the aim). Grabbers come from the walls on a cooldown and pin
    /// her (Grabber). Its life is its body (BrainNode, one mass at the eye, the default lock), on a bar of its own (BrainHud, "it also needs hp for the
    /// boss"), and the work lights go out as its nodes die. At its death every
    /// light flares, then they go out one by one (round 58), its last line, and the exit opens. The short does not touch it
    /// and mk_edgar refuses it (its parts are shock immune). All numbers PROPOSED.
    /// </summary>
    public sealed class BrainFight : MonoBehaviour
    {
        public enum Phase { Waiting, Fighting, Dying, Dead }
        public enum AimState { Resting, Tracking, Charging, Staggered }

        public Room room;
        public FixedCamera eye;
        public BrainNode[] nodes = new BrainNode[0];
        public Renderer[] workLights = new Renderer[0];
        public Light[] workGlow = new Light[0];
        public Transform[] grabberMouths = new Transform[0];
        public GameObject exitLeaf;
        public string deadFlag = "brain_dead";
        [Tooltip("Raised when the fight begins (the test room: the Crown HUD goes, PlayerHudFlags.CrownGone). Empty: none.")]
        public string beginFlag = "";

        [Header("The aim and the strike")]
        [Tooltip("Metres a second: slower than her walk (1.6), faster than her slow walk (about 1).")]
        public float trackSpeed = 1.4f;
        public float lockRadius = 0.9f;
        public float lockSeconds = 1f;
        public float chargeSeconds = 2f;
        public float restSeconds = 1f;
        public float strikeRadius = 1.1f;
        public float strikeDamage = 25f;
        [Tooltip("Damage to the brain during its charge that cancels the strike.")]
        public float staggerDamage = 30f;
        public float staggerSeconds = 2.5f;

        [Header("The floor")]
        public float stillSeconds = 1f;
        public float slowSeconds = 2.5f;
        public float stillSpeed = 0.25f;

        [Header("The grabbers")]
        public float firstGrab = 4f;
        public float grabEvery = 7f;
        [Tooltip("The tell at the mouth before the lunge.")]
        public float grabWindup = 0.6f;
        public float reachSeconds = 0.5f;
        public float pinSeconds = 2f;

        public string deathLine = "I was meant to be beautiful.";
        public const string KillerId = "brain";

        public Phase Current { get; private set; } = Phase.Waiting;
        /// <summary>The charge of a strike: 0 at its start, 1 when it fires. Below 0: no charge now (the HUD's frame).</summary>
        public float RingProgress => Aiming == AimState.Charging ? chargeProgress : -1f;
        public AimState Aiming { get; private set; } = AimState.Resting;
        /// <summary>The point on the floor it aims at.</summary>
        public Vector3 Aim { get; private set; }
        /// <summary>0 to 1: full is the lock, and the charge starts.</summary>
        public float LockProgress { get; private set; }
        /// <summary>The damage it took in this charge (the stagger).</summary>
        public float ChargeDamage { get; private set; }
        public int Strikes { get; private set; }
        public int StrikeHits { get; private set; }
        public int Staggers { get; private set; }
        public bool Slowing { get; private set; }
        public Grabber ActiveGrabber { get; private set; }
        public int Grabs { get; private set; }
        public bool Pinned => ActiveGrabber != null && ActiveGrabber.Holding;
        public int LightsOn { get; private set; }
        public static BrainFight Instance { get; private set; }

        private Transform her;
        private Health herHealth;
        private TankController tank;
        private Vector3 lastHer;
        private float stillTime, slowUntil, nextGrab;
        private bool slowSet;
        private LineRenderer ring, laser;
        private GameObject marker;
        private Material flesh, red, pale;
        private float chargeProgress, aimTimer;

        private void Awake() => Instance = this;

        private void OnEnable()
        {
            RoomTracker.OnChanged += OnRoom;
            HitEventBus.OnHit += OnHit;
        }

        private void OnDisable()
        {
            RoomTracker.OnChanged -= OnRoom;
            HitEventBus.OnHit -= OnHit;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            red = Unlit(new Color(1f, 0.12f, 0.1f));
            pale = Unlit(new Color(0.85f, 0.85f, 0.8f));
            flesh = Unlit(Grabber.Flesh);
            ring = Line("[BrainRing]", 48, true);
            laser = Line("[BrainLaser]", 2, false);
            marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "[BrainStrikeMark]";
            Destroy(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial = red;
            marker.transform.localScale = new Vector3(strikeRadius * 2f, 0.01f, strikeRadius * 2f);
            marker.SetActive(false);
            LightsOn = workLights.Length;
            gameObject.AddComponent<GlorifiedBody.UI.BrainHud>();
            if (!string.IsNullOrEmpty(deadFlag) && StoryFlags.Has(deadFlag)) { Die(quiet: true); return; }
            if (RoomTracker.Current == room) Begin();
        }

        private void OnRoom(Room from, Room to)
        {
            if (to == room && Current == Phase.Waiting) Begin();
        }

        /// <summary>The fight starts (her first step into the sector; the tests).</summary>
        public void Begin()
        {
            if (Current != Phase.Waiting) return;
            GameObject lucy = GameObject.FindWithTag("Player");
            if (lucy == null) return;
            her = lucy.transform;
            herHealth = lucy.GetComponentInChildren<Health>();
            tank = lucy.GetComponent<TankController>();
            lastHer = her.position;
            Current = Phase.Fighting;
            nextGrab = Time.time + firstGrab;
            if (!string.IsNullOrEmpty(beginFlag)) StoryFlags.Raise(beginFlag);
            ResetAim(); // it starts unlocked, far from her
            RunTelemetry.Event("brain_fight", "begin");
        }

        private void Update()
        {
            if (Current != Phase.Fighting || her == null) return;
            Floor();
            UpdateAim(Time.deltaTime);
            if (ActiveGrabber == null && Time.time >= nextGrab) Grab();
            UpdateLights();
            bool alive = false;
            foreach (BrainNode n in nodes) if (n != null && !n.IsDead) alive = true;
            if (!alive) Die(quiet: false);
        }

        // ── The floor ─────────────────────────────────────────────────

        private void Floor()
        {
            Vector3 move = her.position - lastHer;
            move.y = 0f;
            lastHer = her.position;
            float speed = move.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            stillTime = speed < stillSpeed ? stillTime + Time.deltaTime : 0f;
            if (stillTime >= stillSeconds) slowUntil = Time.time + slowSeconds;
            bool slow = Time.time < slowUntil;
            if (slow == Slowing) return;
            Slowing = slow;
            if (tank == null) return;
            ShortController shortCtl = tank.GetComponent<ShortController>();
            if (slow && !tank.SlowWalk) { tank.SetSlowWalk(true); slowSet = true; }
            else if (!slow && slowSet && (shortCtl == null || !shortCtl.IsActive)) { tank.SetSlowWalk(false); slowSet = false; }
            RunTelemetry.Event("brain_floor", slow ? "slow" : "free");
        }

        // ── The strike ────────────────────────────────────────────────

        /// <summary>The aim goes back to the corner of the sector farthest from her, unlocked (the start, a stagger).</summary>
        public void ResetAim()
        {
            Vector3 best = her != null ? her.position : transform.position;
            float far = -1f;
            if (room != null)
            {
                Vector4 r = room.rect;
                foreach (Vector2 c in new[] { new Vector2(r.x + 1f, r.y + 1f), new Vector2(r.z - 1f, r.y + 1f), new Vector2(r.x + 1f, r.w - 1f), new Vector2(r.z - 1f, r.w - 1f) })
                {
                    Vector3 p = room.transform.position + new Vector3(c.x, 0f, c.y);
                    float d = her != null ? Flat(p - her.position) : 0f;
                    if (d > far) { far = d; best = p; }
                }
            }
            Aim = new Vector3(best.x, her != null ? her.position.y : best.y, best.z);
            LockProgress = 0f;
            Aiming = AimState.Tracking;
        }

        private void UpdateAim(float dt)
        {
            switch (Aiming)
            {
                case AimState.Resting:
                case AimState.Staggered:
                    aimTimer -= dt;
                    if (aimTimer > 0f) break;
                    if (Aiming == AimState.Staggered) ResetAim();
                    else { LockProgress = 0f; Aiming = AimState.Tracking; }
                    break;
                case AimState.Tracking:
                    Follow(dt);
                    // In the ring the lock fills, out of it it drains twice as fast. A pinned body cannot leave it.
                    bool inside = Flat(her.position - Aim) <= lockRadius || Pinned;
                    LockProgress = Mathf.Clamp01(LockProgress + (inside ? 1f : -2f) * dt / lockSeconds);
                    if (LockProgress >= 1f)
                    {
                        Aiming = AimState.Charging;
                        chargeProgress = 0f;
                        ChargeDamage = 0f;
                        RunTelemetry.Event("brain_aim", "locked");
                    }
                    break;
                case AimState.Charging:
                    Follow(dt);
                    chargeProgress += dt / chargeSeconds;
                    if (chargeProgress >= 1f)
                    {
                        Strike(Aim);
                        Aiming = AimState.Resting;
                        aimTimer = restSeconds;
                        LockProgress = 0f;
                    }
                    break;
            }
            DrawAim();
        }

        private void Follow(float dt)
        {
            Vector3 to = her.position - Aim;
            to.y = 0f;
            float step = trackSpeed * dt;
            Aim += to.magnitude <= step ? to : to.normalized * step;
            Aim = new Vector3(Aim.x, her.position.y, Aim.z);
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        /// <summary>Her hit on a part of it (a node, a grabber): in a charge, enough of it staggers the brain.</summary>
        private void OnHit(HitInfo hit)
        {
            if (Current != Phase.Fighting || Aiming != AimState.Charging || hit.Victim == null) return;
            if (hit.Attacker == null || !hit.Attacker.CompareTag("Player")) return;
            bool part = hit.Victim.GetComponentInParent<BrainNode>() != null || hit.Victim.GetComponentInParent<Grabber>() != null;
            if (!part) return;
            ChargeDamage += hit.DamageAmount;
            if (ChargeDamage < staggerDamage) return;
            Staggers++;
            Aiming = AimState.Staggered;
            aimTimer = staggerSeconds;
            LockProgress = 0f;
            GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.Slam, hit.HitPoint, 0.6f);
            if (ActiveGrabber != null) ActiveGrabber.Retract("stagger");
            RunTelemetry.Event("brain_aim", $"staggered {ChargeDamage:0}");
        }

        private void Strike(Vector3 point)
        {
            Strikes++;
            GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.Slam, point, 1f);
            Vector3 flat = her.position - point;
            flat.y = 0f;
            if (flat.magnitude > strikeRadius || herHealth == null || herHealth.IsDead) { RunTelemetry.Event("brain_strike", "missed"); return; }
            StrikeHits++;
            GlorifiedBody.UI.DeathMenuUI.LastKillerId = KillerId;
            float damage = GlorifiedBody.Systems.DifficultyManager.EnemyDamage(strikeDamage);
            herHealth.TakeDamage(damage);
            HitEventBus.Raise(new HitInfo
            {
                Attacker = gameObject,
                Victim = herHealth.gameObject,
                HitTransform = her,
                HitPoint = her.position + Vector3.up,
                HitNormal = Vector3.up,
                DamageAmount = damage,
                DamageType = DamageType.EnemyMelee,
                WasFatal = herHealth.IsDead
            });
            RunTelemetry.Event("brain_strike", $"hit {damage:0}");
        }

        // The aim: a pale ring that turns red as the lock fills; in the charge it shrinks to the strike's size and a laser
        // comes from the eye. Nothing while it rests or reels.
        private void DrawAim()
        {
            bool shown = Aiming == AimState.Tracking || Aiming == AimState.Charging;
            ring.enabled = shown;
            laser.enabled = Aiming == AimState.Charging;
            marker.SetActive(Aiming == AimState.Charging && Mathf.Repeat(chargeProgress * 6f, 1f) < 0.5f);
            if (!shown) return;
            float radius = Aiming == AimState.Charging ? Mathf.Lerp(lockRadius, strikeRadius * 0.5f, chargeProgress) : lockRadius;
            ring.sharedMaterial = Aiming == AimState.Charging || LockProgress > 0.5f ? red : pale;
            Vector3 c = Aim + Vector3.up * 0.05f;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
            ring.widthMultiplier = Mathf.Lerp(0.03f, 0.09f, Aiming == AimState.Charging ? 1f : LockProgress);
            marker.transform.position = Aim + Vector3.up * 0.02f;
            laser.SetPosition(0, eye != null ? eye.transform.position : Aim + Vector3.up * 5f);
            laser.SetPosition(1, Aim);
        }

        // ── The grabbers ──────────────────────────────────────────────

        /// <summary>A grabber from the mouth nearest her (the tests call it too).</summary>
        public Grabber Grab()
        {
            if (Current != Phase.Fighting || grabberMouths.Length == 0 || her == null) return null;
            Transform best = grabberMouths[0];
            foreach (Transform m in grabberMouths)
                if (Vector3.Distance(m.position, her.position) < Vector3.Distance(best.position, her.position)) best = m;
            ActiveGrabber = Grabber.Create(this, best.position, her, grabWindup, reachSeconds, pinSeconds, flesh);
            Grabs++;
            nextGrab = Time.time + grabEvery;
            return ActiveGrabber;
        }

        /// <summary>mk_orzabal tore a grabber out: the next one waits its full cooldown.</summary>
        public void Ripped(Grabber g)
        {
            if (ActiveGrabber == g) ActiveGrabber = null;
            nextGrab = Time.time + grabEvery;
        }

        // ── Its life, its death ───────────────────────────────────────

        /// <summary>The share of its life left: the sum of its nodes.</summary>
        public float LifeShare
        {
            get
            {
                float now = 0f, max = 0f;
                foreach (BrainNode n in nodes)
                {
                    if (n == null) continue;
                    max += n.Health.MaxHealth;
                    now += Mathf.Max(0f, n.Health.CurrentHealth);
                }
                return max > 0f ? now / max : 0f;
            }
        }

        private void UpdateLights()
        {
            int on = Mathf.CeilToInt(LifeShare * workLights.Length);
            if (on == LightsOn) return;
            LightsOn = on;
            for (int i = 0; i < workLights.Length; i++) SetLight(i, i < on ? 1f : 0f);
            RunTelemetry.Event("brain_lights", on.ToString());
        }

        private void SetLight(int i, float level)
        {
            Renderer r = workLights[i];
            if (r != null)
            {
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", Color.Lerp(new Color(0.08f, 0.08f, 0.07f), new Color(1f, 0.92f, 0.7f), Mathf.Clamp01(level)));
                r.SetPropertyBlock(block);
            }
            if (i < workGlow.Length && workGlow[i] != null)
            {
                workGlow[i].enabled = level > 0f;
                workGlow[i].intensity = 1.2f * Mathf.Max(1f, level);
            }
        }

        private void Die(bool quiet)
        {
            if (Current == Phase.Dying || Current == Phase.Dead) return;
            Current = Phase.Dying;
            Aiming = AimState.Resting;
            aimTimer = float.MaxValue;
            if (ring != null) ring.enabled = false;
            if (laser != null) laser.enabled = false;
            if (marker != null) marker.SetActive(false);
            if (ActiveGrabber != null) ActiveGrabber.Retract("its_death");
            if (slowSet && tank != null) { tank.SetSlowWalk(false); slowSet = false; }
            Slowing = false;
            if (!string.IsNullOrEmpty(deadFlag)) StoryFlags.Raise(deadFlag);
            RunTelemetry.Event("brain_fight", "dead");
            if (quiet)
            {
                for (int i = 0; i < workLights.Length; i++) SetLight(i, 0f);
                LightsOn = 0;
                Dead();
                return;
            }
            StartCoroutine(DeathLights());
        }

        // Every light flares at once, then they go out one by one (CANON, round 58).
        private IEnumerator DeathLights()
        {
            for (int i = 0; i < workLights.Length; i++) SetLight(i, 2f);
            LightsOn = workLights.Length;
            if (!string.IsNullOrEmpty(deathLine)) SubtitleService.Say("brain", "BRAIN", deathLine, 4f);
            yield return new WaitForSeconds(0.8f);
            for (int i = workLights.Length - 1; i >= 0; i--)
            {
                SetLight(i, 0f);
                LightsOn = i;
                yield return new WaitForSeconds(0.35f);
            }
            Dead();
        }

        private void Dead()
        {
            Current = Phase.Dead;
            if (exitLeaf != null) exitLeaf.SetActive(false);
        }

        // ── Drawing ───────────────────────────────────────────────────

        private static Material Unlit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c);
            return m;
        }

        private LineRenderer Line(string name, int points, bool loop)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = red;
            line.positionCount = points;
            line.loop = loop;
            line.widthMultiplier = loop ? 0.06f : 0.03f;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
            return line;
        }
    }
}
