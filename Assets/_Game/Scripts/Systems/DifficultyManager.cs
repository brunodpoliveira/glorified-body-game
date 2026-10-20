using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Systems
{
    /// <summary>
    /// Central manager for the modular difficulty system.
    /// Tracks which difficulty modules are active, computes total difficulty,
    /// and provides query methods for other systems.
    /// Presets (Easy, Normal, Hard) are exclusive: EnableModule on a preset turns the other presets of
    /// its group off. One preset is always active (PresetNormal by default).
    /// Persists across scenes via DontDestroyOnLoad.
    /// Selections are saved to PlayerPrefs.
    /// </summary>
    public class DifficultyManager : MonoBehaviour
    {
        public static DifficultyManager Instance { get; private set; }

        [Header("Module Registry")]
        [Tooltip("All available difficulty modules. Populate in inspector or auto-loaded from Resources/DifficultyModules/.")]
        [SerializeField] private List<DifficultyModuleDefinition> allModules = new List<DifficultyModuleDefinition>();

        private HashSet<string> activeModuleIds = new HashSet<string>();

        private Dictionary<string, DifficultyModuleDefinition> moduleById = new Dictionary<string, DifficultyModuleDefinition>();
        private Dictionary<DifficultyModuleDefinition.ModuleType, DifficultyModuleDefinition> moduleByType = new Dictionary<DifficultyModuleDefinition.ModuleType, DifficultyModuleDefinition>();

        private const string PREFS_PREFIX = "difficulty_";
        private const string PREFS_ACTIVE_LIST = "difficulty_activeModules";

        // ── Public Properties ────────────────────────────────────────────

        public int BaseDifficultyPercent => 0;

        public int TotalDifficultyPercent
        {
            get
            {
                int total = BaseDifficultyPercent;
                foreach (var mod in allModules)
                {
                    if (IsModuleActive(mod))
                        total += mod.difficultyPercent;
                }
                return total;
            }
        }

        public IReadOnlyList<DifficultyModuleDefinition> AllModules => allModules;

        public int ActiveModuleCount => activeModuleIds.Count;

        /// <summary>The active preset (Easy, Normal or Hard), or null before Awake.</summary>
        public DifficultyModuleDefinition ActivePreset
        {
            get
            {
                foreach (var mod in allModules)
                {
                    if (mod != null && mod.IsPreset && activeModuleIds.Contains(mod.moduleId))
                        return mod;
                }
                return null;
            }
        }

        /// <summary>How many enemies may fire at the same time. 0 = every enemy (design: easy 1, normal 2, hard all).</summary>
        public int MaxSimultaneousShooters
        {
            get
            {
                var preset = ActivePreset;
                return preset != null ? Mathf.RoundToInt(preset.GetParam(DifficultyModuleDefinition.ParamMaxSimultaneousShooters, 0f)) : 0;
            }
        }

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
            LoadSelections();
            EnsureDefaultPreset();
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

        // ── Module Activation ────────────────────────────────────────────

        public void EnableModule(DifficultyModuleDefinition mod)
        {
            if (mod == null) return;
            if (activeModuleIds.Contains(mod.moduleId)) return;

            Activate(mod);
            SaveSelections();
        }

        /// <summary>A preset cannot be turned off, only replaced by another preset of its group.</summary>
        public void DisableModule(DifficultyModuleDefinition mod)
        {
            if (mod == null) return;
            if (mod.isBaseline || mod.IsPreset) return;
            if (!activeModuleIds.Contains(mod.moduleId)) return;

            activeModuleIds.Remove(mod.moduleId);
            SaveSelections();
        }

        /// <summary>Selects the preset of the given type. Returns false when no such preset is registered.</summary>
        public bool SetPreset(DifficultyModuleDefinition.ModuleType type)
        {
            var mod = GetModuleByType(type);
            if (mod == null || !mod.IsPreset) return false;
            EnableModule(mod);
            return true;
        }

        public bool ToggleModule(DifficultyModuleDefinition mod)
        {
            if (mod == null) return false;
            if (mod.isBaseline) return true;
            if (mod.IsPreset)
            {
                EnableModule(mod);
                return true;
            }

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
            activeModuleIds.Clear();
            ActivateBaselineModules();
            EnsureDefaultPreset();
            SaveSelections();
        }

        public void PromoteToBaseline(DifficultyModuleDefinition mod)
        {
            if (mod == null) return;
            mod.isBaseline = true;
            if (!activeModuleIds.Contains(mod.moduleId))
                activeModuleIds.Add(mod.moduleId);
        }

        public void RegisterModule(DifficultyModuleDefinition mod)
        {
            if (mod == null) return;
            if (moduleById.ContainsKey(mod.moduleId)) return;

            allModules.Add(mod);
            moduleById[mod.moduleId] = mod;
            moduleByType[mod.moduleType] = mod;

            if (mod.isBaseline)
                activeModuleIds.Add(mod.moduleId);
        }

        // ── Persistence ──────────────────────────────────────────────────

        private void SaveSelections()
        {
            var nonBaseline = new List<string>();
            foreach (var id in activeModuleIds)
            {
                if (moduleById.TryGetValue(id, out var mod) && !mod.isBaseline)
                    nonBaseline.Add(id);
            }

            string serialized = string.Join(",", nonBaseline);
            PlayerPrefs.SetString(PREFS_ACTIVE_LIST, serialized);
            PlayerPrefs.Save();
        }

        private void LoadSelections()
        {
            string serialized = PlayerPrefs.GetString(PREFS_ACTIVE_LIST, "");
            if (string.IsNullOrEmpty(serialized)) return;

            string[] ids = serialized.Split(',');
            foreach (string id in ids)
            {
                string trimmed = id.Trim();
                if (!string.IsNullOrEmpty(trimmed) && moduleById.TryGetValue(trimmed, out var mod))
                {
                    Activate(mod);
                }
            }
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

        /// <summary>Adds a module to the active set. For a preset, the other presets of its group leave the set.</summary>
        private void Activate(DifficultyModuleDefinition mod)
        {
            if (mod.IsPreset)
            {
                foreach (var other in allModules)
                {
                    if (other != null && other != mod && other.IsPreset && other.presetGroup == mod.presetGroup)
                        activeModuleIds.Remove(other.moduleId);
                }
            }

            activeModuleIds.Add(mod.moduleId);
        }

        /// <summary>One preset is always active. PresetNormal is the default, else the first preset found.</summary>
        private void EnsureDefaultPreset()
        {
            if (ActivePreset != null) return;

            var normal = GetModuleByType(DifficultyModuleDefinition.ModuleType.PresetNormal);
            if (normal != null && normal.IsPreset)
            {
                Activate(normal);
                return;
            }

            foreach (var mod in allModules)
            {
                if (mod != null && mod.IsPreset)
                {
                    Activate(mod);
                    return;
                }
            }
        }
    }
}
