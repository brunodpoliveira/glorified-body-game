using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// Her only way to get HP back (Bruno, 2026-10-30: "hp regen is to be disabled in all forms ... the only form to
    /// regain hp is by feeding on fallen enemies; civilians give more hp back as usual"). She crouches at the body (the
    /// vendor Crouch_Idle, the animator's "Feeding"), stands still, and the body's HP flows into her over FeedSeconds.
    /// A hit on her stops the feed: she keeps what she took and the body keeps the rest. The feed stops at her full
    /// HP too. No one tends a body she feeds on (CorpseCare). Numbers PROPOSED, after the exp of a kill (civilians
    /// most). Interactor adds this to her when a room's Lucy lacks it.
    /// The look (Bruno, 2026-10-30: "a crouch + sound + blood effects for now"): blood sprays from the body's chest and a
    /// wet sound every BloodSeconds (the gore driver's spray, without the hit bus: no other system takes it as a hit).
    /// The ones who see a feed react (OnFeedStarted, EnemyMorale.SeeFeed).
    /// </summary>
    public sealed class Feeding : MonoBehaviour
    {
        public const float FeedSeconds = 2.5f;
        public const float BloodSeconds = 0.6f; // a spray and a wet sound this often, PROPOSED

        /// <summary>A feed starts: where the body lies. The witnesses react (EnemyMorale).</summary>
        public static event System.Action<Feeding, FeedableBody> OnFeedStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => OnFeedStarted = null;

        /// <summary>The HP a body of the rank gives (Bruno: "civilians give more hp back as usual"). PROPOSED.</summary>
        public static int ValueOf(EnemyMorale.Rank rank)
        {
            switch (rank)
            {
                case EnemyMorale.Rank.UnarmedCivilian: return 40;
                case EnemyMorale.Rank.ArmedCivilian: return 30;
                case EnemyMorale.Rank.Heavy: return 30;
                default: return 20; // soldiers and guards
            }
        }

        /// <summary>The difficulty's MENDING doubles it.</summary>
        public static float DifficultyScale => GlorifiedBody.Systems.DifficultyManager.Instance != null ? GlorifiedBody.Systems.DifficultyManager.Instance.FeedScale : 1f;

        private Health health;
        private TankController tank;
        private FeedableBody target;
        private float rate;
        private float started;
        private float gained;
        private float nextBlood;

        public bool IsFeeding => target != null;
        public FeedableBody Target => target;
        public int Feeds { get; private set; }
        /// <summary>The HP of the last feed, and why it ended ("done", "full", "hit", "dead", "gone").</summary>
        public float LastGain { get; private set; }
        public string LastEnd { get; private set; } = "";
        /// <summary>The blood sprays of the feeds so far (the gore driver played them).</summary>
        public int BloodSprays { get; private set; }

        private void Awake()
        {
            health = GetComponentInChildren<Health>();
            tank = GetComponent<TankController>();
        }

        private void OnEnable() => HitEventBus.OnHit += OnHit;

        private void OnDisable()
        {
            HitEventBus.OnHit -= OnHit;
            if (IsFeeding) End("off");
        }

        /// <summary>Starts a feed on the body. False when she is dead, unhurt, busy, or the body gives nothing.</summary>
        public bool Begin(FeedableBody body)
        {
            if (body == null || IsFeeding || health == null || health.IsDead) return false;
            if (!body.IsDead || body.Left <= 0.5f || health.CurrentHealth >= health.MaxHealth - 0.5f) return false;
            target = body;
            rate = body.Total / FeedSeconds;
            started = Time.time;
            gained = 0f;
            Feeds++;
            if (tank != null) tank.LockMovement(true);
            CorpseCare.MarkTended(body.transform); // no carer drags it away from under her
            Vector3 to = body.Point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
            {
                if (tank != null) tank.Teleport(transform.position, Quaternion.LookRotation(to).eulerAngles.y);
                else transform.rotation = Quaternion.LookRotation(to);
            }
            nextBlood = 0f;
            Bleed();
            RunTelemetry.Event("feed_start", $"{body.name}:{body.Left:0}");
            OnFeedStarted?.Invoke(this, body);
            return true;
        }

        private void Update()
        {
            if (!IsFeeding) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (target == null) { End("gone"); return; }
            if (health.IsDead) { End("dead"); return; }
            if (Time.time >= nextBlood) Bleed();
            float give = Mathf.Min(rate * dt, target.Left, health.MaxHealth - health.CurrentHealth);
            if (give > 0f)
            {
                health.Heal(give);
                target.Take(give);
                gained += give;
            }
            if (target.Left <= 0.01f) End("done");
            else if (health.CurrentHealth >= health.MaxHealth - 0.01f) End("full");
            else if (Time.time - started > FeedSeconds + 0.5f) End("done");
        }

        /// <summary>A spray of blood from the body's chest and a wet sound. The Gore option decides the spray (Off: none).</summary>
        private void Bleed()
        {
            nextBlood = Time.time + BloodSeconds;
            if (target == null) return;
            Animator animator = target.GetComponentInChildren<Animator>();
            Transform chest = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
            Vector3 at = chest != null ? chest.position : target.Point;
            GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.FleshHit, at, 0.6f);
            var driver = GlorifiedBody.Gore.ThatIndividual.ThatIndividualGoreDriver.Instance;
            if (driver == null) return;
            var weight = driver.PlayForCheck(new HitInfo
            {
                Attacker = gameObject,
                Victim = target.gameObject,
                HitTransform = chest != null ? chest : target.transform,
                HitPoint = at,
                HitNormal = Vector3.up,
                DamageAmount = 12f,
                DamageType = DamageType.EnemyMelee,
            });
            if (weight != GlorifiedBody.Gore.ThatIndividual.GoreHitWeight.None) BloodSprays++;
        }

        /// <summary>A hit with damage on her stops the feed.</summary>
        private void OnHit(HitInfo hit)
        {
            if (!IsFeeding || hit == null || hit.DamageAmount <= 0f || hit.Victim == null) return;
            if (hit.Victim == gameObject || hit.Victim.transform.IsChildOf(transform)) End("hit");
        }

        private void End(string why)
        {
            string body = target != null ? target.name : "?";
            target = null;
            if (tank != null) tank.LockMovement(false);
            LastGain = gained;
            LastEnd = why;
            RunTelemetry.Event("feed_end", $"{body}:{why}:+{gained:0}");
        }
    }
}
