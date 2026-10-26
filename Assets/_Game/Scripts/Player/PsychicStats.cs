using System;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// The three bars (design §2.7): HP lives in Health. NALCON is the psychic energy the powers spend.
    /// SHORT rises with every kill and with damage taken. The outfit sets the maximums and the passive recharge.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class PsychicStats : MonoBehaviour
    {
        public const float MaxNalcon = 100f;
        public const float MaxShort = 100f;

        [SerializeField] private OutfitProfile outfit;
        [SerializeField, Min(0f)] private float shortPerKill = 25f;
        [SerializeField, Min(0f)] private float shortPerDamagePoint = 0.6f;

        private Health health;

        public Health Health => health;
        public OutfitProfile Outfit => outfit;
        public float Nalcon { get; private set; } = MaxNalcon;
        public float Short { get; private set; }
        public bool IsShortFull => Short >= MaxShort - 0.001f;
        public int Kills { get; private set; }
        /// <summary>The exp of her kills (ExperienceRules) and the level it gives.</summary>
        public int Exp { get; private set; }
        public int Level { get; private set; } = 1;
        /// <summary>The power damage of her level (1 at level 1).</summary>
        public float LevelDamageScale => ExperienceRules.DamageScale(Level);
        public event Action<int> OnLevelUp;

        private readonly HashSet<int> credited = new HashSet<int>();

        /// <summary>While true the bars stop their passive change (a short, a cutscene).</summary>
        public bool RegenPaused { get; set; }

        public event Action OnChanged;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (outfit == null) outfit = Resources.Load<OutfitProfile>("Outfits/outfit_nude");
            ApplyOutfit();
        }

        private void OnEnable()
        {
            health.OnDamaged ??= new UnityEngine.Events.UnityEvent<float>();
            health.OnDamaged.AddListener(OnDamaged);
            HitEventBus.OnHit += OnHit;
        }

        private void OnDisable()
        {
            health.OnDamaged?.RemoveListener(OnDamaged);
            HitEventBus.OnHit -= OnHit;
        }

        public void SetOutfit(OutfitProfile profile)
        {
            if (profile == null) return;
            outfit = profile;
            ApplyOutfit();
            RunTelemetry.Event("outfit", profile.outfitId);
            OnChanged?.Invoke();
        }

        private void ApplyOutfit()
        {
            if (outfit == null) return;
            health.SetMaxHealth(outfit.maxHp + ExperienceRules.BonusHp(Level), fillToMax: true);
        }

        private void Update()
        {
            if (RegenPaused || outfit == null || Time.deltaTime <= 0f) return;
            float dt = Time.deltaTime;
            bool changed = false;
            if (outfit.nalconRegenPerSecond > 0f && Nalcon < MaxNalcon)
            {
                Nalcon = Mathf.Min(MaxNalcon, Nalcon + outfit.nalconRegenPerSecond * dt);
                changed = true;
            }
            // A full SHORT bar holds (Bruno, 2026-10-20). Only the short (ResetShort at its end) or a drug
            // (RefillAll) empties it. A part-full bar drains at the outfit rate.
            if (outfit.shortDecayPerSecond > 0f && Short > 0f && !IsShortFull)
            {
                Short = Mathf.Max(0f, Short - outfit.shortDecayPerSecond * dt);
                changed = true;
            }
            if (outfit.hpRegenPerSecond > 0f && !health.IsDead && health.CurrentHealth < health.MaxHealth)
            {
                health.Heal(outfit.hpRegenPerSecond * dt);
                changed = true;
            }
            if (changed) OnChanged?.Invoke();
        }

        public bool CanSpendNalcon(float amount) => Nalcon >= amount - 0.001f;

        public bool TrySpendNalcon(float amount)
        {
            if (!CanSpendNalcon(amount)) return false;
            Nalcon = Mathf.Max(0f, Nalcon - amount);
            OnChanged?.Invoke();
            return true;
        }

        public void RefillAll()
        {
            Nalcon = MaxNalcon;
            Short = 0f;
            health.ReviveToFull();
            OnChanged?.Invoke();
        }

        public void ResetShort()
        {
            Short = 0f;
            OnChanged?.Invoke();
        }

        public void AddShort(float amount)
        {
            Short = Mathf.Clamp(Short + amount, 0f, MaxShort);
            OnChanged?.Invoke();
        }

        /// <summary>Called for every enemy kill (design: SHORT rises with every kill).</summary>
        public void NotifyKill(string enemyId)
        {
            Kills++;
            AddShort(shortPerKill);
            RunTelemetry.Event("enemy_killed", enemyId);
        }

        private void OnDamaged(float amount)
        {
            AddShort(amount * shortPerDamagePoint);
        }

        /// <summary>
        /// The exp of a kill (design §11.2): the hit that killed, in the frame of the death, from her or from
        /// something she sent (a thrown body, a fire she lit, a returned grenade). Each body pays once.
        /// </summary>
        private void OnHit(HitInfo hit)
        {
            if (hit == null || !hit.WasFatal || hit.Victim == null || !IsMine(hit.Attacker)) return;
            Health victim = hit.Victim.GetComponentInParent<Health>();
            if (victim == null || victim == health || victim.DeathFrame != Time.frameCount) return;
            if (!credited.Add(victim.GetInstanceID())) return;
            int value = ExperienceRules.ValueOf(victim.gameObject);
            if (value > 0) AddExp(value, victim.name);
        }

        private bool IsMine(GameObject attacker) => attacker != null && (attacker == gameObject || attacker.transform.IsChildOf(transform));

        /// <summary>Exp, and a level for each threshold it passes: more maximum HP (given at once) and more power damage.</summary>
        public void AddExp(int amount, string source = "")
        {
            if (amount <= 0) return;
            Exp += amount;
            RunTelemetry.Event("exp", $"{source}:+{amount}={Exp}");
            int target = ExperienceRules.LevelFor(Exp);
            while (Level < target)
            {
                Level++;
                health.SetMaxHealth(health.MaxHealth + ExperienceRules.HpPerLevel, fillToMax: false);
                health.Heal(ExperienceRules.HpPerLevel);
                RunTelemetry.Event("level_up", $"{Level}:hp={health.MaxHealth:0}:damage=x{LevelDamageScale:0.00}");
                OnLevelUp?.Invoke(Level);
            }
            OnChanged?.Invoke();
        }

        /// <summary>A loaded save: the bars, the kill count and the exp as they were. The saved maximum HP already holds the level's.</summary>
        public void Restore(float nalcon, float shortValue, int kills, int exp = 0)
        {
            Kills = Mathf.Max(0, kills);
            Exp = Mathf.Max(0, exp);
            Level = ExperienceRules.LevelFor(Exp);
            DebugSet(nalcon, shortValue);
        }

        /// <summary>Tests and bots: set the bars directly.</summary>
        public void DebugSet(float nalcon, float shortValue)
        {
            Nalcon = Mathf.Clamp(nalcon, 0f, MaxNalcon);
            Short = Mathf.Clamp(shortValue, 0f, MaxShort);
            OnChanged?.Invoke();
        }
    }
}
