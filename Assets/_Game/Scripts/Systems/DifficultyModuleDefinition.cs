using UnityEngine;

namespace GlorifiedBody.Systems
{
    /// <summary>
    /// One difficulty module (Bruno, 2026-10-26): a HARDER module adds to the total, an EASIER one takes from it, and
    /// the BASE module is the 100 % the others start from. The three presets (100 %, 150 %, 200 %) are sets of modules:
    /// a module names the presets that turn it on. Modules of one exclusive group never run together (two kinds of
    /// telegraph, for example). Made by CreateDifficultyModules.CreateAll.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDifficultyModule", menuName = "GlorifiedBody/Difficulty Module")]
    public class DifficultyModuleDefinition : ScriptableObject
    {
        public enum ModuleType
        {
            Base,          // 100 %: one enemy fires at a time, 1.5 s from the laser to the attack, 2 s rest. Always on.
            Crossfire,     // harder: two enemies fire at a time
            FiringSquad,   // harder: every enemy fires
            QuickTrigger,  // harder: 1.0 s from the laser to the attack
            HairTrigger,   // harder: 0.8 s from the laser to the attack
            HardHitters,   // harder: enemy attacks do 1.5x damage
            Fragile,       // harder: her maximum HP is halved
            OneHitWonder,  // harder: any hit kills her, no second wind
            Scarcity,      // harder: MK recharges at half speed
            ShortFuse,     // harder: the SHORT bar fills 1.5x as fast
            SlowTrigger,   // easier: 2.0 s from the laser to the attack
            LongRest,      // easier: enemies rest 4 s after an attack
            ThickSkin,     // easier: enemy attacks do half damage
            SteadyMind,    // easier: the SHORT bar fills half as fast
            DeepWell,      // easier: MK recharges twice as fast
            Mending        // easier: HP recharges twice as fast
        }

        public enum Section { Base, Harder, Easier }

        // Overrides: the last active module that carries the key sets the value (the base module carries all three).
        /// <summary>How many enemies may fire at the same time. 0 = every enemy.</summary>
        public const string ParamMaxSimultaneousShooters = "maxSimultaneousShooters";
        /// <summary>Seconds from the laser to the attack (Bruno, 2026-10-24: 1.5, 1.0, 0.8).</summary>
        public const string ParamAttackSeconds = "attackSeconds";
        /// <summary>Seconds an enemy rests after an attack before its next aim.</summary>
        public const string ParamRestSeconds = "restSeconds";

        // Multipliers: the values of the active modules multiply.
        public const string ParamEnemyDamageScale = "enemyDamageScale";
        public const string ParamPlayerMaxHpScale = "playerMaxHpScale";
        public const string ParamMkRegenScale = "mkRegenScale";
        public const string ParamShortGainScale = "shortGainScale";
        public const string ParamHpRegenScale = "hpRegenScale";

        [Header("Identity")]
        [Tooltip("Unique identifier used for persistence and lookups.")]
        public string moduleId;

        [Tooltip("Display name shown in the UI (e.g., THICK SKIN).")]
        public string displayName;

        [Tooltip("Unicode symbol or short glyph for the module icon.")]
        public string symbol = "♦";

        [TextArea(1, 3)]
        [Tooltip("Short description shown in tooltips or module list.")]
        public string description;

        [Header("Difficulty")]
        [Tooltip("Percentage this module adds to the total (negative for an easier module).")]
        [Range(-100, 100)]
        public int difficultyPercent = 10;

        [Tooltip("Which gameplay system this module affects.")]
        public ModuleType moduleType;

        [Tooltip("Harder, easier, or the base.")]
        public Section section = Section.Harder;

        [Tooltip("If true, this module is always active and cannot be toggled off.")]
        public bool isBaseline = false;

        [Tooltip("If true, this module is hidden from the difficulty menu entirely.")]
        public bool hideInUI = false;

        [Tooltip("Modules of one group never run together. Empty = no group.")]
        public string exclusiveGroup = "";

        [Tooltip("The presets (100, 150, 200) that turn this module on.")]
        public int[] presets = new int[0];

        [Header("Parameters")]
        [Tooltip("Generic tuning values for this module. Interpretation depends on moduleType.")]
        public ModuleParam[] parameters;

        /// <summary>True when the preset of this percent turns the module on.</summary>
        public bool InPreset(int percent)
        {
            if (presets == null) return false;
            for (int i = 0; i < presets.Length; i++)
            {
                if (presets[i] == percent) return true;
            }
            return false;
        }

        public bool HasParam(string key)
        {
            if (parameters == null) return false;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].key == key) return true;
            }
            return false;
        }

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
        [Tooltip("Parameter name used in code lookups (e.g., enemyDamageScale).")]
        public string key;

        [Tooltip("Parameter value.")]
        public float value;
    }
}
