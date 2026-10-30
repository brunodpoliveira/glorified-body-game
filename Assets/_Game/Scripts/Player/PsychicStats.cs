using System;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// The three bars (design §2.7): HP lives in Health. NALCON is the psychic energy the powers spend.
    /// SHORT rises with every kill and with damage taken. The outfit sets the maximums and the passive recharge.
    /// The difficulty scales them (Bruno, 2026-10-26): her maximum HP, the MK and HP recharge, the SHORT gain, and
    /// ONE-HIT WONDER (1 HP, no second wind).
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
        private bool secondWindByDefault;
        private bool oneHitApplied;

        /// <summary>While true the bars stop their passive change (a short, a cutscene).</summary>
        public bool RegenPaused { get; set; }

        public event Action OnChanged;

        private void Awake()
        {
            health = GetComponent<Health>();
            secondWindByDefault = health.IsSecondWindEnabled;
            if (outfit == null) outfit = Resources.Load<OutfitProfile>("Outfits/outfit_nude");
            ApplyOutfit();
            ApplyOneHit();
        }

        private void OnEnable()
        {
            health.OnDamaged ??= new UnityEngine.Events.UnityEvent<float>();
            health.OnDamaged.AddListener(OnDamaged);
            HitEventBus.OnHit += OnHit;
            DifficultyManager.OnChanged += OnDifficultyChanged;
        }

        private void OnDisable()
        {
            health.OnDamaged?.RemoveListener(OnDamaged);
            HitEventBus.OnHit -= OnHit;
            DifficultyManager.OnChanged -= OnDifficultyChanged;
        }

        /// <summary>Her maximum HP: the outfit and the level, scaled by the difficulty. 1 with ONE-HIT WONDER.</summary>
        public float MaxHpTarget
        {
            get
            {
                DifficultyManager difficulty = DifficultyManager.Instance;
                if (difficulty != null && difficulty.OneHitKills) return 1f;
                float hp = (outfit != null ? outfit.maxHp : health.MaxHealth) + ExperienceRules.BonusHp(Level);
                return Mathf.Max(1f, Mathf.Round(hp * (difficulty != null ? difficulty.PlayerMaxHpScale : 1f)));
            }
        }

        private static float Scale(Func<DifficultyManager, float> read) => DifficultyManager.Instance != null ? read(DifficultyManager.Instance) : 1f;

        /// <summary>A module went on or off: the new maximum HP (she keeps her HP up to it), and the second wind.</summary>
        private void OnDifficultyChanged()
        {
            RefreshMaxHp();
            ApplyOneHit();
            OnChanged?.Invoke();
        }

        private void RefreshMaxHp()
        {
            if (outfit != null) health.SetMaxHealth(MaxHpTarget, fillToMax: false);
        }

        /// <summary>ONE-HIT WONDER takes the second wind away; off again, she has the second wind of her setup.</summary>
        private void ApplyOneHit()
        {
            bool oneHit = DifficultyManager.Instance != null && DifficultyManager.Instance.OneHitKills;
            if (oneHit == oneHitApplied) return;
            oneHitApplied = oneHit;
            health.SetSecondWindEnabled(!oneHit && secondWindByDefault);
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
            health.SetMaxHealth(MaxHpTarget, fillToMax: true);
        }

        private void Update()
        {
            if (RegenPaused || outfit == null || Time.deltaTime <= 0f) return;
            float dt = Time.deltaTime;
            bool changed = false;
            if (outfit.nalconRegenPerSecond > 0f && Nalcon < MaxNalcon)
            {
                Nalcon = Mathf.Min(MaxNalcon, Nalcon + outfit.nalconRegenPerSecond * Scale(d => d.MkRegenScale) * dt);
                changed = true;
            }
            // A full SHORT bar holds (Bruno, 2026-10-20). Only the short (ResetShort at its end) or the SHORT capsule
            // lowers it. A part-full bar drains at the outfit rate.
            if (outfit.shortDecayPerSecond > 0f && Short > 0f && !IsShortFull)
            {
                Short = Mathf.Max(0f, Short - outfit.shortDecayPerSecond * dt);
                changed = true;
            }
            // No HP comes back by itself, in any outfit (Bruno, 2026-10-30): only feeding on a body gives HP (Feeding).
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

        /// <summary>The MK capsule: MK full.</summary>
        public void FillMk()
        {
            Nalcon = MaxNalcon;
            OnChanged?.Invoke();
        }

        /// <summary>Debug key and bots only (F7): every bar as new. No drug does this since 2026-10-30.</summary>
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
            AddShort(shortPerKill * Scale(d => d.ShortGainScale));
            RunTelemetry.Event("enemy_killed", enemyId);
        }

        private void OnDamaged(float amount)
        {
            AddShort(amount * shortPerDamagePoint * Scale(d => d.ShortGainScale));
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
                // A level raises the maximum HP only: the HP she has stays (Bruno, 2026-10-30: only feeding gives HP).
                health.SetMaxHealth(MaxHpTarget, fillToMax: false);
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
            RefreshMaxHp(); // the difficulty of now, not of the save
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
