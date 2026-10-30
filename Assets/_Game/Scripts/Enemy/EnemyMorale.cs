using System;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// What a death does to the others (design §11.4 and §11.5, Bruno 2026-10-22: "i want the enemies to see their
    /// colleagues dying in a gruesome manner and reacting to shock/dismay"; "enemies retreating can be a thing:
    /// depending on gruesome kills and rank (so an unarmed civvie will always run/cower, armed civvies and guards
    /// will run once enough of them die, soldiers will never run)").
    ///
    /// When an enemy dies, it tells the room one frame later (by then the hit event carried the cause). Every live
    /// enemy that sees the corpse (a clear line within witnessRange, or within hearRange) reacts:
    ///   - Shock: it stops and loses its aim for shockSeconds (longer for a gruesome death), with a bark.
    ///   - Fear: +1 for a death, +2 for a gruesome one. At its rank's limit it retreats (SoldierBrain.Retreat):
    ///     it runs away from Lucy and cowers, and never attacks again. An unarmed civilian runs at once.
    /// Gruesome: the short's shocks, the fire, the telekinesis (the slam, a throw, a returned bullet, grenade or
    /// rocket), an explosion, a full-charge mk_warspite (it cuts a limb). A part-charge mk_warspite kill is not.
    /// All numbers and the gruesome list are PROPOSED.
    /// A feed (Bruno, 2026-10-30: "they should react to it"): a witness of her feed on a body (the same sight rule)
    /// takes it as a gruesome death: +2 fear, a shock or a retreat, and a line. The lines are PLACEHOLDERS for the writer.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class EnemyMorale : MonoBehaviour
    {
        public enum Rank { UnarmedCivilian, ArmedCivilian, Guard, Soldier, Heavy }

        [SerializeField] private Rank rank = Rank.Soldier;
        [SerializeField, Min(1f)] private float witnessRange = 15f;
        [SerializeField, Min(0f)] private float hearRange = 5f;
        [SerializeField, Min(0f)] private float shockSeconds = 1.0f;
        [SerializeField, Min(0f)] private float gruesomeShockSeconds = 2.0f;

        /// <summary>A death in the room: the corpse, its chest, and whether it was gruesome.</summary>
        public static event Action<EnemyMorale, Vector3, bool> OnColleagueDied;

        private Health health;
        private IFearful brain;
        private DamageType lastType = DamageType.Unknown;
        private float lastCharge;
        private int deathFrame = -1;
        private bool announced;

        public Rank RankOf => rank;
        public int Fear { get; private set; }
        public int Shocks { get; private set; }
        public int Witnessed { get; private set; }
        public int FeedsSeen { get; private set; }

        // PLACEHOLDER lines (the writer refines them): the fighters, then the civilians.
        public static readonly string[] FighterFeedLines = { "SHE'S FEEDING ON HIM!", "WHAT IS SHE DOING TO HIM?", "GET HER OFF HIM!" };
        public static readonly string[] CivilianFeedLines = { "Oh God. She's eating him.", "No, no, no. What is she?", "She's... she's feeding." };
        public bool DiedGruesome { get; private set; }
        /// <summary>The fear at which this rank retreats. Never for soldiers and heavies.</summary>
        public int FearLimit => rank == Rank.UnarmedCivilian ? 0 : rank == Rank.ArmedCivilian || rank == Rank.Guard ? 3 : int.MaxValue;
        public bool NeverRuns => FearLimit == int.MaxValue;

        public void SetRank(Rank value) => rank = value;

        /// <summary>A gruesome death. A full-charge mk_warspite kill cuts a limb (Bruno, 2026-10-24): gruesome too.</summary>
        public static bool IsGruesome(DamageType type, float charge = 0f)
        {
            if (type == DamageType.PsychicBlast) return charge >= 0.999f;
            switch (type)
            {
                case DamageType.ShortShock:
                case DamageType.PsychicFire:
                case DamageType.PsychicTelekinesis:
                case DamageType.Explosion:
                case DamageType.Grenade:
                case DamageType.EnemyRocket:
                    return true;
                default:
                    return false;
            }
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            brain = GetComponent<IFearful>();
        }

        private void OnEnable()
        {
            health.OnDeath ??= new UnityEngine.Events.UnityEvent();
            health.OnDeath.AddListener(OnDeath);
            HitEventBus.OnHit += OnHit;
            OnColleagueDied += OnDeathSeen;
            GlorifiedBody.Player.Feeding.OnFeedStarted += OnFeedSeen;
        }

        private void OnDisable()
        {
            health.OnDeath?.RemoveListener(OnDeath);
            HitEventBus.OnHit -= OnHit;
            OnColleagueDied -= OnDeathSeen;
            GlorifiedBody.Player.Feeding.OnFeedStarted -= OnFeedSeen;
        }

        private void OnHit(HitInfo hit)
        {
            if (hit.Victim != gameObject) return;
            lastType = hit.DamageType;
            lastCharge = hit.Charge;
        }

        /// <summary>A body that a load puts back (SaveSystem): its death is old, so no one sees it or fears it again.</summary>
        public void MarkOldDeath() => announced = true;

        private void OnDeath()
        {
            if (deathFrame < 0) deathFrame = Time.frameCount;
        }

        private void Update()
        {
            // The frame after the death: the hit event of the killing blow arrived, so the cause is known.
            if (announced || deathFrame < 0 || Time.frameCount <= deathFrame) return;
            announced = true;
            DiedGruesome = IsGruesome(lastType, lastCharge);
            Vector3 chest = transform.position + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight;
            RunTelemetry.Event("enemy_death_seen_by_room", $"{name}:{lastType}{(DiedGruesome ? ":gruesome" : "")}");
            OnColleagueDied?.Invoke(this, chest, DiedGruesome);
        }

        private void OnDeathSeen(EnemyMorale corpse, Vector3 chest, bool gruesome)
        {
            if (corpse == this || health.IsDead) return;
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            float distance = Vector3.Distance(eye, chest);
            bool near = distance <= hearRange;
            bool sees = distance <= witnessRange && !Physics.Linecast(eye, chest, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);
            if (!near && !sees) return;
            See(corpse, gruesome);
        }

        private void OnFeedSeen(GlorifiedBody.Player.Feeding who, GlorifiedBody.Interaction.FeedableBody body)
        {
            if (health.IsDead || body == null || body.gameObject == gameObject) return;
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 at = body.Point + Vector3.up * 0.3f;
            float distance = Vector3.Distance(eye, at);
            bool near = distance <= hearRange;
            bool sees = distance <= witnessRange && !Physics.Linecast(eye, at, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);
            if (near || sees) SeeFeed(body.name);
        }

        /// <summary>It saw her feed (the range and the line are checked before): a gruesome sight, then a line.</summary>
        public void SeeFeed(string bodyName)
        {
            if (health.IsDead) return;
            FeedsSeen++;
            Fear += 2;
            RunTelemetry.Event("enemy_saw_feed", $"{name}:{bodyName} fear={Fear}");
            if (brain == null) return;
            brain.Alert("feed", callOthers: false);
            if (Fear >= FearLimit) brain.Retreat("feed");
            else brain.Shock(gruesomeShockSeconds, true);
            bool civilian = rank == Rank.UnarmedCivilian || rank == Rank.ArmedCivilian;
            string[] lines = civilian ? CivilianFeedLines : FighterFeedLines;
            Bark.Say(gameObject, lines[UnityEngine.Random.Range(0, lines.Length)]); // after the shock's own line: this one shows
        }

        /// <summary>It saw the death (the range and the line are checked before): the fear, then a shock or a retreat.</summary>
        public void See(EnemyMorale corpse, bool gruesome)
        {
            if (health.IsDead) return;
            Witnessed++;
            Fear += gruesome ? 2 : 1;
            RunTelemetry.Event("enemy_witness", $"{name}:{(corpse != null ? corpse.name : "?")}{(gruesome ? ":gruesome" : "")} fear={Fear}");
            if (brain == null) return;
            brain.Alert("colleague died", callOthers: false);
            if (Fear >= FearLimit)
            {
                brain.Retreat(gruesome ? "gruesome death" : "deaths");
                return;
            }
            Shocks++;
            brain.Shock(gruesome ? gruesomeShockSeconds : shockSeconds, gruesome);
        }
    }
}
