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
    /// The bars (design §2.7): HP lives in Health. Each power has its own MK bar (the code name is Nalcon; Bruno, 2026-11-06:
    /// "that basically makes mk an ammo counter and the pills clips to restore the ammo"): no bar comes back by itself in any
    /// outfit, only its capsule fills it. SHORT rises with every kill, with damage taken and with every point of MK she
    /// spends (Bruno, 2026-11-06: "the short bar should increase when the player uses a power"). The outfit sets the maximums.
    /// The difficulty scales them (Bruno, 2026-10-26): her maximum HP, what a power costs, the SHORT gain, and
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
        /// <summary>The SHORT of one point of MK spent: a whole bar spent counts as one kill, a full mk_warspite gives 5. PROPOSED.</summary>
        public const float ShortPerMkPoint = 0.25f;

        private Health health;

        public Health Health => health;
        public OutfitProfile Outfit => outfit;
        private readonly float[] bars = { MaxNalcon, MaxNalcon, MaxNalcon };

        /// <summary>The power she has selected (PsychicPowers sets it): Nalcon reads its bar.</summary>
        public PowerKind Selected { get; set; } = PowerKind.Blast;
        /// <summary>The bar of the selected power.</summary>
        public float Nalcon => bars[(int)Selected];
        /// <summary>The bar of a power.</summary>
        public float Mk(PowerKind kind) => bars[(int)kind];
        public float Short { get; private set; }
        public bool IsShortFull => Short >= MaxShort - 0.001f;
        public int Kills { get; private set; }
        /// <summary>Every person she killed, armed or not (her own kills, each body once): the bulletin's count (2026-10-31).</summary>
        public int People { get; private set; }
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

        /// <summary>
        /// Puts on the outfit: its HP, slots and recharge, and its model (LucyModel). keepDamage (a change in play): she
        /// keeps the damage she took, so the uniform adds its armour and taking it off takes the armour away (never
        /// below 1 HP). Without it (a new game, a load) the HP fills, and the load sets the saved HP after.
        /// </summary>
        public void SetOutfit(OutfitProfile profile, bool keepDamage = false)
        {
            if (profile == null) return;
            float hp = health.CurrentHealth, oldMax = health.MaxHealth;
            outfit = profile;
            if (keepDamage)
            {
                health.SetMaxHealth(MaxHpTarget, fillToMax: false);
                health.SetHealth(Mathf.Clamp(hp + (health.MaxHealth - oldMax), 1f, health.MaxHealth));
            }
            else ApplyOutfit();
            GlorifiedBody.Characters.LucyModel.Wear(gameObject, profile.modelClass);
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
            // Ammunition (Bruno, 2026-11-06): both outfits hold 0 now, the field stays for one number to bring it back.
            for (int i = 0; i < bars.Length && outfit.nalconRegenPerSecond > 0f; i++)
            {
                if (bars[i] >= MaxNalcon) continue;
                bars[i] = Mathf.Min(MaxNalcon, bars[i] + outfit.nalconRegenPerSecond * dt);
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

        /// <summary>What an amount costs now: SCARCITY doubles it, DEEP WELL halves it.</summary>
        public static float CostOf(float amount) => amount / Mathf.Max(0.01f, Scale(d => d.MkSupplyScale));

        public bool CanSpendMk(PowerKind kind, float amount) => bars[(int)kind] >= CostOf(amount) - 0.001f;

        /// <summary>A power spends its own bar, and the use fills the SHORT bar.</summary>
        public bool TrySpendMk(PowerKind kind, float amount)
        {
            if (!CanSpendMk(kind, amount)) return false;
            float cost = CostOf(amount);
            bars[(int)kind] = Mathf.Max(0f, bars[(int)kind] - cost);
            AddShort(cost * ShortPerMkPoint * Scale(d => d.ShortGainScale));
            return true;
        }

        /// <summary>The capsule of a power: its bar full. A clip, not a sip (Bruno, 2026-11-06).</summary>
        public void FillMk(PowerKind kind)
        {
            bars[(int)kind] = MaxNalcon;
            OnChanged?.Invoke();
        }

        /// <summary>Debug key and bots only (F7): every bar as new. No drug does this since 2026-10-30.</summary>
        public void RefillAll()
        {
            for (int i = 0; i < bars.Length; i++) bars[i] = MaxNalcon;
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
            People++;
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

        /// <summary>A loaded save: the bars, the kill count and the exp as they were. The saved maximum HP already holds the level's. An older file has one MK: every bar takes it.</summary>
        public void Restore(float nalcon, float shortValue, int kills, int exp = 0, int people = -1, IReadOnlyList<float> mk = null)
        {
            Kills = Mathf.Max(0, kills);
            People = people >= 0 ? people : Kills; // an older file has no count: its kills stand in
            Exp = Mathf.Max(0, exp);
            Level = ExperienceRules.LevelFor(Exp);
            RefreshMaxHp(); // the difficulty of now, not of the save
            DebugSet(nalcon, shortValue);
            for (int i = 0; mk != null && i < bars.Length && i < mk.Count; i++) bars[i] = Mathf.Clamp(mk[i], 0f, MaxNalcon);
            if (mk != null) OnChanged?.Invoke();
        }

        /// <summary>Tests and bots: set the SHORT bar directly.</summary>
        public void DebugSetShort(float value)
        {
            Short = Mathf.Clamp(value, 0f, MaxShort);
            OnChanged?.Invoke();
        }

        /// <summary>Tests and bots: set one bar directly.</summary>
        public void DebugSetMk(PowerKind kind, float value)
        {
            bars[(int)kind] = Mathf.Clamp(value, 0f, MaxNalcon);
            OnChanged?.Invoke();
        }

        /// <summary>Tests and bots: set the bars directly (every MK bar takes the value).</summary>
        public void DebugSet(float nalcon, float shortValue)
        {
            for (int i = 0; i < bars.Length; i++) bars[i] = Mathf.Clamp(nalcon, 0f, MaxNalcon);
            Short = Mathf.Clamp(shortValue, 0f, MaxShort);
            OnChanged?.Invoke();
        }
    }
}
