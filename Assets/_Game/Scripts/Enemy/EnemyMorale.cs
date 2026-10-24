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
    /// rocket), an explosion. A plain mk_warspite kill is not. All numbers and the gruesome list are PROPOSED.
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
        private SoldierBrain brain;
        private DamageType lastType = DamageType.Unknown;
        private int deathFrame = -1;
        private bool announced;

        public Rank RankOf => rank;
        public int Fear { get; private set; }
        public int Shocks { get; private set; }
        public int Witnessed { get; private set; }
        public bool DiedGruesome { get; private set; }
        /// <summary>The fear at which this rank retreats. Never for soldiers and heavies.</summary>
        public int FearLimit => rank == Rank.UnarmedCivilian ? 0 : rank == Rank.ArmedCivilian || rank == Rank.Guard ? 3 : int.MaxValue;
        public bool NeverRuns => FearLimit == int.MaxValue;

        public void SetRank(Rank value) => rank = value;

        public static bool IsGruesome(DamageType type)
        {
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
            brain = GetComponent<SoldierBrain>();
        }

        private void OnEnable()
        {
            health.OnDeath ??= new UnityEngine.Events.UnityEvent();
            health.OnDeath.AddListener(OnDeath);
            HitEventBus.OnHit += OnHit;
            OnColleagueDied += OnDeathSeen;
        }

        private void OnDisable()
        {
            health.OnDeath?.RemoveListener(OnDeath);
            HitEventBus.OnHit -= OnHit;
            OnColleagueDied -= OnDeathSeen;
        }

        private void OnHit(HitInfo hit)
        {
            if (hit.Victim == gameObject) lastType = hit.DamageType;
        }

        private void OnDeath()
        {
            if (deathFrame < 0) deathFrame = Time.frameCount;
        }

        private void Update()
        {
            // The frame after the death: the hit event of the killing blow arrived, so the cause is known.
            if (announced || deathFrame < 0 || Time.frameCount <= deathFrame) return;
            announced = true;
            DiedGruesome = IsGruesome(lastType);
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
