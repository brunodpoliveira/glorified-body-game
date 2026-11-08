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
    /// is the brain's eye, high in the sector (a FixedCamera that follows her). Its strike: a ring closes on her, and when it
    /// closes the strike lands where she stood ("Keep moving: it strikes where you stand when the ring closes"). The floor is
    /// brain: standing still for StillSeconds slows her. Grabbers come from the walls on a cooldown and pin her (Grabber).
    /// It has no HP bar: the work lights go out as its nodes die (BrainNode, PROPOSED: how she hurts it). At its death every
    /// light flares, then they go out one by one (round 58), its last line, and the exit opens. The short does not touch it
    /// and mk_edgar refuses it (its parts are shock immune). All numbers PROPOSED.
    /// </summary>
    public sealed class BrainFight : MonoBehaviour
    {
        public enum Phase { Waiting, Fighting, Dying, Dead }

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

        [Header("The strike")]
        public float ringSeconds = 1.6f;
        public float pinnedRingSeconds = 0.7f;
        public float strikeDelay = 0.35f;
        public float restSeconds = 1.2f;
        public float strikeRadius = 1.1f;
        public float strikeDamage = 25f;

        [Header("The floor")]
        public float stillSeconds = 1f;
        public float slowSeconds = 2.5f;
        public float stillSpeed = 0.25f;

        [Header("The grabbers")]
        public float firstGrab = 4f;
        public float grabEvery = 7f;
        public float reachSeconds = 0.7f;
        public float pinSeconds = 2f;

        public string deathLine = "I was meant to be beautiful.";
        public const string KillerId = "brain";

        public Phase Current { get; private set; } = Phase.Waiting;
        /// <summary>The ring closes on her: 0 at its start, 1 at the close. Below 0: no ring now.</summary>
        public float RingProgress { get; private set; } = -1f;
        public Vector3? StrikePoint { get; private set; }
        public int Strikes { get; private set; }
        public int StrikeHits { get; private set; }
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
        private Material flesh, red;
        private Coroutine strikes;

        private void Awake() => Instance = this;

        private void OnEnable() => RoomTracker.OnChanged += OnRoom;

        private void OnDisable() => RoomTracker.OnChanged -= OnRoom;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            red = Unlit(new Color(1f, 0.12f, 0.1f));
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
            strikes = StartCoroutine(StrikeLoop());
            RunTelemetry.Event("brain_fight", "begin");
        }

        private void Update()
        {
            if (Current != Phase.Fighting || her == null) return;
            Floor();
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

        private IEnumerator StrikeLoop()
        {
            while (Current == Phase.Fighting)
            {
                yield return new WaitForSeconds(restSeconds);
                if (Current != Phase.Fighting) yield break;
                float t = 0f;
                while (true)
                {
                    float duration = Pinned ? pinnedRingSeconds : ringSeconds;
                    t += Time.deltaTime / duration;
                    if (t >= 1f || Current != Phase.Fighting) break;
                    RingProgress = t;
                    DrawRing(t);
                    yield return null;
                }
                if (Current != Phase.Fighting) break;
                // The ring closed: the strike lands where she stands now.
                RingProgress = -1f;
                ring.enabled = false;
                laser.enabled = false;
                Vector3 point = her.position;
                StrikePoint = point;
                marker.transform.position = point + Vector3.up * 0.02f;
                marker.SetActive(true);
                yield return new WaitForSeconds(strikeDelay);
                Strike(point);
                marker.SetActive(false);
                StrikePoint = null;
            }
            RingProgress = -1f;
            ring.enabled = false;
            laser.enabled = false;
            marker.SetActive(false);
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

        private void DrawRing(float progress)
        {
            ring.enabled = true;
            laser.enabled = true;
            float radius = Mathf.Lerp(2.2f, 0.35f, progress);
            Vector3 c = her.position + Vector3.up * 0.05f;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
            laser.SetPosition(0, eye != null ? eye.transform.position : her.position + Vector3.up * 5f);
            laser.SetPosition(1, her.position + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight);
        }

        // ── The grabbers ──────────────────────────────────────────────

        /// <summary>A grabber from the mouth nearest her (the tests call it too).</summary>
        public Grabber Grab()
        {
            if (Current != Phase.Fighting || grabberMouths.Length == 0 || her == null) return null;
            Transform best = grabberMouths[0];
            foreach (Transform m in grabberMouths)
                if (Vector3.Distance(m.position, her.position) < Vector3.Distance(best.position, her.position)) best = m;
            ActiveGrabber = Grabber.Create(this, best.position, her, reachSeconds, pinSeconds, flesh);
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
            if (strikes != null) StopCoroutine(strikes);
            RingProgress = -1f;
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
