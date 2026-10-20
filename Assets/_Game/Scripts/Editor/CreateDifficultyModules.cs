#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// Creates the difficulty module assets under Assets/_Game/Resources/DifficultyModules, so
    /// DifficultyManager auto-loads them at runtime. Idempotent: it upserts each asset.
    /// Three presets (easy 1, normal 2, hard all: how many enemies fire at a time) plus free modifiers.
    /// Menu: GlorifiedBody > Create Difficulty Modules. Batch: tools/unity.sh exec GlorifiedBody.Editor.CreateDifficultyModules.CreateAll
    /// </summary>
    public static class CreateDifficultyModules
    {
        private const string ResourcesFolder = "Assets/_Game/Resources";
        private const string AssetFolder = ResourcesFolder + "/DifficultyModules";
        private const string PresetGroup = "preset";
        private const string NotYet = " No effect yet: it arrives with the HP and nalcon bars (build order step 3).";

        [MenuItem("GlorifiedBody/Create Difficulty Modules")]
        public static void CreateAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game"))
                AssetDatabase.CreateFolder("Assets", "_Game");
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder("Assets/_Game", "Resources");
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder(ResourcesFolder, "DifficultyModules");

            CreateModule("base", "BASE", "█", "The lab as designed. Always on.", 100,
                DifficultyModuleDefinition.ModuleType.Base, isBaseline: true, presetGroup: "", parameters: new ModuleParam[] { });

            CreateModule("preset_easy", "EASY", "I", "One enemy fires at a time.", -25,
                DifficultyModuleDefinition.ModuleType.PresetEasy, isBaseline: false, presetGroup: PresetGroup,
                parameters: new[] { new ModuleParam { key = DifficultyModuleDefinition.ParamMaxSimultaneousShooters, value = 1f } });

            CreateModule("preset_normal", "NORMAL", "II", "Two enemies fire at a time.", 0,
                DifficultyModuleDefinition.ModuleType.PresetNormal, isBaseline: false, presetGroup: PresetGroup,
                parameters: new[] { new ModuleParam { key = DifficultyModuleDefinition.ParamMaxSimultaneousShooters, value = 2f } });

            CreateModule("preset_hard", "HARD", "III", "Every enemy fires.", 25,
                DifficultyModuleDefinition.ModuleType.PresetHard, isBaseline: false, presetGroup: PresetGroup,
                parameters: new[] { new ModuleParam { key = DifficultyModuleDefinition.ParamMaxSimultaneousShooters, value = 0f } });

            CreateModule("fragile", "FRAGILE", "❤", "HP drains twice as fast." + NotYet, 10,
                DifficultyModuleDefinition.ModuleType.Fragile, isBaseline: false, presetGroup: "",
                parameters: new[] { new ModuleParam { key = "degenMultiplier", value = 2f } });

            CreateModule("one_hit_wonder", "ONE-HIT WONDER", "☢", "HP is pinned to 1. Any hit kills." + NotYet, 80,
                DifficultyModuleDefinition.ModuleType.OneHitWonder, isBaseline: false, presetGroup: "",
                parameters: new ModuleParam[] { });

            CreateModule("scarcity", "SCARCITY", "⚠", "Nalcon drains 50% faster." + NotYet, 10,
                DifficultyModuleDefinition.ModuleType.Scarcity, isBaseline: false, presetGroup: "",
                parameters: new[] { new ModuleParam { key = "drainMultiplier", value = 1.5f } });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CreateDifficultyModules] 7 difficulty module assets created or updated in " + AssetFolder);
        }

        private static void CreateModule(
            string id,
            string displayName,
            string symbol,
            string description,
            int difficultyPercent,
            DifficultyModuleDefinition.ModuleType moduleType,
            bool isBaseline,
            string presetGroup,
            ModuleParam[] parameters)
        {
            string path = $"{AssetFolder}/{id}.asset";

            var module = AssetDatabase.LoadAssetAtPath<DifficultyModuleDefinition>(path);
            bool isNew = module == null;
            if (isNew)
            {
                module = ScriptableObject.CreateInstance<DifficultyModuleDefinition>();
            }

            module.moduleId = id;
            module.displayName = displayName;
            module.symbol = symbol;
            module.description = description;
            module.difficultyPercent = difficultyPercent;
            module.moduleType = moduleType;
            module.isBaseline = isBaseline;
            module.presetGroup = presetGroup;
            module.parameters = parameters;

            if (isNew)
            {
                AssetDatabase.CreateAsset(module, path);
            }
            else
            {
                EditorUtility.SetDirty(module);
            }
        }
    }
}
#endif
