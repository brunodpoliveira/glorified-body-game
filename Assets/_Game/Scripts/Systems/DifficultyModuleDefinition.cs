using UnityEngine;

namespace GlorifiedBody.Systems
{
    /// <summary>
    /// Defines one difficulty module. Two kinds share this asset type:
    /// a PRESET (presetGroup set: Easy, Normal, Hard; one of a group is active at a time) and
    /// a MODIFIER (presetGroup empty: a free toggle such as Fragile).
    /// Create via Assets > Create > GlorifiedBody > Difficulty Module.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDifficultyModule", menuName = "GlorifiedBody/Difficulty Module")]
    public class DifficultyModuleDefinition : ScriptableObject
    {
        public enum ModuleType
        {
            Base,           // Baseline. Always on. No gameplay effect.
            PresetEasy,     // Preset: one enemy fires at a time.
            PresetNormal,   // Preset: two enemies fire at a time.
            PresetHard,     // Preset: every enemy fires.
            Fragile,        // Modifier: HP degeneration doubled. Consumer: the HP bar (build order step 3).
            OneHitWonder,   // Modifier: HP pinned to 1, no second wind. Consumer: the HP bar (step 3).
            Scarcity        // Modifier: nalcon drains 50% faster. Consumer: the nalcon bar (step 3).
        }

        /// <summary>The parameter key that every preset carries. 0 = every enemy fires.</summary>
        public const string ParamMaxSimultaneousShooters = "maxSimultaneousShooters";

        /// <summary>Seconds from the laser to the shot (Bruno, 2026-10-24: easy 1.5, normal 1.0, hard 0.8).</summary>
        public const string ParamAttackSeconds = "attackSeconds";

        /// <summary>Seconds an enemy rests after an attack before its next aim (2.0 on every preset).</summary>
        public const string ParamRestSeconds = "restSeconds";

        [Header("Identity")]
        [Tooltip("Unique identifier used for persistence and lookups.")]
        public string moduleId;

        [Tooltip("Display name shown in the UI (e.g., KILL RECOVERY).")]
        public string displayName;

        [Tooltip("Unicode symbol or short glyph for the module icon.")]
        public string symbol = "\u2666";

        [TextArea(1, 3)]
        [Tooltip("Short description shown in tooltips or module list.")]
        public string description;

        [Header("Difficulty")]
        [Tooltip("Percentage this module adds to total difficulty.")]
        [Range(-100, 100)]
        public int difficultyPercent = 10;

        [Tooltip("Which gameplay system this module affects.")]
        public ModuleType moduleType;

        [Tooltip("If true, this module is always active and cannot be toggled off.")]
        public bool isBaseline = false;

        [Tooltip("If true, this module is hidden from the difficulty menu entirely.")]
        public bool hideInUI = false;

        [Tooltip("Presets share a group name. One module of a group is active at a time. Empty = a free toggle.")]
        public string presetGroup = "";

        public bool IsPreset => !string.IsNullOrEmpty(presetGroup);

        [Header("Parameters")]
        [Tooltip("Generic tuning values for this module. Interpretation depends on moduleType.")]
        public ModuleParam[] parameters;

        /// <summary>
        /// Returns the float value of a named parameter, or defaultValue if not found.
        /// </summary>
        public float GetParam(string key, float defaultValue = 0f)
        {
            if (parameters == null)
            {
                return defaultValue;
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].key == key)
                {
                    return parameters[i].value;
                }
            }

            return defaultValue;
        }
    }

    [System.Serializable]
    public struct ModuleParam
    {
        [Tooltip("Parameter name used in code lookups (e.g., decayRate, drainMultiplier).")]
        public string key;

        [Tooltip("Parameter value.")]
        public float value;
    }
}
