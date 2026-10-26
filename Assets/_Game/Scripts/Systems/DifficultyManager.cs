using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Systems
{
    /// <summary>
    /// The difficulty (Bruno, 2026-10-26): three presets, 100 %, 150 % and 200 %, or a custom set of modules in two
    /// sections, harder and easier. The total is the base 100 % plus the percent of every active module. A preset is a
    /// set of modules (DifficultyModuleDefinition.presets); a set that matches no preset is CUSTOM. The other systems
    /// read the numbers here: the shooters at a time, the telegraph, the rest, and the scales of damage, HP, MK and
    /// SHORT. Persists across scenes (DontDestroyOnLoad), and the selection is saved to PlayerPrefs.
    /// </summary>
    public class DifficultyManager : MonoBehaviour
    {
        public static DifficultyManager Instance { get; private set; }

        /// <summary>The three presets, in percent.</summary>
        public static readonly int[] PresetPercents = { 100, 150, 200 };

        /// <summary>A new game starts at 150 % (the old NORMAL: two enemies fire at a time, 1.0 s telegraph), PROPOSED.</summary>
        public const int DefaultPreset = 150;

        /// <summary>A module went on or off, or a preset was chosen.</summary>
        public static event Action OnChanged;

        [Header("Module Registry")]
        [Tooltip("All available difficulty modules. Populate in inspector or auto-loaded from Resources/DifficultyModules/.")]
        [SerializeField] private List<DifficultyModuleDefinition> allModules = new List<DifficultyModuleDefinition>();

        private HashSet<string> activeModuleIds = new HashSet<string>();

        private Dictionary<string, DifficultyModuleDefinition> moduleById = new Dictionary<string, DifficultyModuleDefinition>();
        private Dictionary<DifficultyModuleDefinition.ModuleType, DifficultyModuleDefinition> moduleByType = new Dictionary<DifficultyModuleDefinition.ModuleType, DifficultyModuleDefinition>();

        // v2 (2026-10-26): the old list held the EASY / NORMAL / HARD preset ids.
        private const string PREFS_ACTIVE_LIST = "difficulty_modules_v2";

        // ── Public Properties ────────────────────────────────────────────

        /// <summary>The base 100 % plus every active module.</summary>
        public int TotalDifficultyPercent
        {
            get
            {
                int total = 0;
                bool hasBase = false;
                foreach (var mod in allModules)
                {
                    if (mod == null || !IsModuleActive(mod)) continue;
                    total += mod.difficultyPercent;
                    if (mod.isBaseline) hasBase = true;
                }
                return hasBase ? total : 100 + total;
            }
        }

        public IReadOnlyList<DifficultyModuleDefinition> AllModules => allModules;

        public int ActiveModuleCount => activeModuleIds.Count;

        /// <summary>The preset that the active set matches (100, 150 or 200), or 0 for a custom set.</summary>
        public int ActivePresetPercent
        {
            get
            {
                foreach (int percent in PresetPercents)
                {
                    if (MatchesPreset(percent)) return percent;
                }
                return 0;
            }
        }

        public bool IsCustom => ActivePresetPercent == 0;

        /// <summary>"150 %", or "CUSTOM 135 %".</summary>
        public string Label => IsCustom ? $"CUSTOM {TotalDifficultyPercent}%" : $"{ActivePresetPercent}%";

        /// <summary>How many enemies may fire at the same time. 0 = every enemy.</summary>
        public int MaxSimultaneousShooters => Mathf.RoundToInt(Override(DifficultyModuleDefinition.ParamMaxSimultaneousShooters, 1f));

        /// <summary>Seconds from the laser to the attack.</summary>
        public float AttackSeconds => Override(DifficultyModuleDefinition.ParamAttackSeconds, 1.5f);

        /// <summary>Seconds an enemy rests after an attack.</summary>
        public float RestSeconds => Override(DifficultyModuleDefinition.ParamRestSeconds, 2f);

        /// <summary>The damage of every enemy attack on her.</summary>
        public float EnemyDamageScale => Product(DifficultyModuleDefinition.ParamEnemyDamageScale);

        /// <summary>Her maximum HP.</summary>
        public float PlayerMaxHpScale => Product(DifficultyModuleDefinition.ParamPlayerMaxHpScale);

        /// <summary>Any hit kills her, and she has no second wind.</summary>
        public bool OneHitKills => IsModuleActive(DifficultyModuleDefinition.ModuleType.OneHitWonder);

        public float MkRegenScale => Product(DifficultyModuleDefinition.ParamMkRegenScale);

        public float ShortGainScale => Product(DifficultyModuleDefinition.ParamShortGainScale);

        public float HpRegenScale => Product(DifficultyModuleDefinition.ParamHpRegenScale);

        /// <summary>An enemy attack's damage on her at the active difficulty (no manager: unchanged).</summary>
        public static float EnemyDamage(float amount) => Instance != null ? amount * Instance.EnemyDamageScale : amount;

        // ── Lifecycle ────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (allModules.Count == 0)
            {
                var loaded = Resources.LoadAll<DifficultyModuleDefinition>("DifficultyModules");
                if (loaded.Length > 0)
                {
                    allModules.AddRange(loaded);
                }
            }

            allModules.Sort((a, b) => a.moduleType.CompareTo(b.moduleType));

            BuildLookups();
            ActivateBaselineModules();
            if (!LoadSelections()) ApplyPreset(DefaultPreset);
        }

        // ── Query API ────────────────────────────────────────────────────

        /// <summary>
        /// Primary query method. Returns true if a module of the given type is active.
        /// </summary>
        public bool IsModuleActive(DifficultyModuleDefinition.ModuleType type)
        {
            if (moduleByType.TryGetValue(type, out var mod))
                return activeModuleIds.Contains(mod.moduleId);
            return false;
        }

        public bool IsModuleActive(DifficultyModuleDefinition mod)
        {
            if (mod == null) return false;
            return activeModuleIds.Contains(mod.moduleId);
        }

        public bool IsModuleActiveById(string moduleId)
        {
            return activeModuleIds.Contains(moduleId);
        }

        public float GetModuleParam(DifficultyModuleDefinition.ModuleType type, string key, float defaultValue = 0f)
        {
            if (!moduleByType.TryGetValue(type, out var mod)) return defaultValue;
            if (!activeModuleIds.Contains(mod.moduleId)) return defaultValue;
            return mod.GetParam(key, defaultValue);
        }

        public DifficultyModuleDefinition GetModuleByType(DifficultyModuleDefinition.ModuleType type)
        {
            moduleByType.TryGetValue(type, out var mod);
            return mod;
        }

        /// <summary>The value of the last active module (in type order) that carries the key, else the fallback.</summary>
        public float Override(string key, float fallback)
        {
            float value = fallback;
            foreach (var mod in allModules)
            {
                if (mod != null && IsModuleActive(mod) && mod.HasParam(key)) value = mod.GetParam(key, value);
            }
            return value;
        }

        /// <summary>The product of the key over the active modules (1 when none carries it).</summary>
        public float Product(string key)
        {
            float value = 1f;
            foreach (var mod in allModules)
            {
                if (mod != null && IsModuleActive(mod) && mod.HasParam(key)) value *= mod.GetParam(key, 1f);
            }
            return value;
        }

        /// <summary>True when the active set is the set of this preset.</summary>
        public bool MatchesPreset(int percent)
        {
            bool any = false;
            foreach (var mod in allModules)
            {
                if (mod == null || mod.isBaseline) continue;
                any = true;
                if (IsModuleActive(mod) != mod.InPreset(percent)) return false;
            }
            return any || percent == 100;
        }

        // ── Module Activation ────────────────────────────────────────────

        /// <summary>Turns a module on. The other modules of its exclusive group go off.</summary>
        public void EnableModule(DifficultyModuleDefinition mod)
        {
            if (mod == null) return;
            if (activeModuleIds.Contains(mod.moduleId)) return;

            Activate(mod);
            Changed();
        }

        public void DisableModule(DifficultyModuleDefinition mod)
        {
            if (mod == null) return;
            if (mod.isBaseline) return;
            if (!activeModuleIds.Contains(mod.moduleId)) return;

            activeModuleIds.Remove(mod.moduleId);
            Changed();
        }

        /// <summary>Selects a preset (100, 150 or 200): exactly its modules are on. False for any other number.</summary>
        public bool SetPreset(int percent)
        {
            if (Array.IndexOf(PresetPercents, percent) < 0) return false;
            ApplyPreset(percent);
            Changed();
            return true;
        }

        public bool ToggleModule(DifficultyModuleDefinition mod)
        {
            if (mod == null) return false;
            if (mod.isBaseline) return true;

            if (activeModuleIds.Contains(mod.moduleId))
            {
                DisableModule(mod);
                return false;
            }
            else
            {
                EnableModule(mod);
                return true;
            }
        }

        public void ResetToDefaults()
        {
            SetPreset(DefaultPreset);
        }

        public void RegisterModule(DifficultyModuleDefinition mod)
        {
            if (mod == null) return;
            if (moduleById.ContainsKey(mod.moduleId)) return;

            allModules.Add(mod);
            allModules.Sort((a, b) => a.moduleType.CompareTo(b.moduleType));
            moduleById[mod.moduleId] = mod;
            moduleByType[mod.moduleType] = mod;

            if (mod.isBaseline)
                activeModuleIds.Add(mod.moduleId);
        }

        // ── Persistence ──────────────────────────────────────────────────

        private void Changed()
        {
            SaveSelections();
            OnChanged?.Invoke();
        }

        private void SaveSelections()
        {
            if (Application.isBatchMode) return; // tests and bots: Bruno's own choice stays as it is
            var nonBaseline = new List<string>();
            foreach (var id in activeModuleIds)
            {
                if (moduleById.TryGetValue(id, out var mod) && !mod.isBaseline)
                    nonBaseline.Add(id);
            }

            PlayerPrefs.SetString(PREFS_ACTIVE_LIST, string.Join(",", nonBaseline));
            PlayerPrefs.Save();
        }

        /// <summary>The saved set. False when nothing is saved (a first start): the default preset applies.</summary>
        private bool LoadSelections()
        {
            // Tests and bots run at the default preset, whatever the editor saved.
            if (Application.isBatchMode || !PlayerPrefs.HasKey(PREFS_ACTIVE_LIST)) return false;
            string serialized = PlayerPrefs.GetString(PREFS_ACTIVE_LIST, "");
            int saved = 0, known = 0;
            foreach (string id in serialized.Split(','))
            {
                string trimmed = id.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                saved++;
                if (moduleById.TryGetValue(trimmed, out var mod))
                {
                    Activate(mod);
                    known++;
                }
            }
            // A list of ids that no module carries (a test's own modules, an old build): the default preset.
            return saved == 0 || known > 0;
        }

        // ── Internal ─────────────────────────────────────────────────────

        private void BuildLookups()
        {
            moduleById.Clear();
            moduleByType.Clear();

            foreach (var mod in allModules)
            {
                if (mod == null) continue;

                if (!moduleById.ContainsKey(mod.moduleId))
                    moduleById[mod.moduleId] = mod;

                moduleByType[mod.moduleType] = mod;
            }
        }

        private void ActivateBaselineModules()
        {
            foreach (var mod in allModules)
            {
                if (mod != null && mod.isBaseline)
                    activeModuleIds.Add(mod.moduleId);
            }
        }

        private void ApplyPreset(int percent)
        {
            activeModuleIds.Clear();
            ActivateBaselineModules();
            foreach (var mod in allModules)
            {
                if (mod != null && !mod.isBaseline && mod.InPreset(percent)) Activate(mod);
            }
        }

        /// <summary>Adds a module to the active set. The other modules of its exclusive group leave the set.</summary>
        private void Activate(DifficultyModuleDefinition mod)
        {
            if (!string.IsNullOrEmpty(mod.exclusiveGroup))
            {
                foreach (var other in allModules)
                {
                    if (other != null && other != mod && other.exclusiveGroup == mod.exclusiveGroup)
                        activeModuleIds.Remove(other.moduleId);
                }
            }

            activeModuleIds.Add(mod.moduleId);
        }
    }
}
