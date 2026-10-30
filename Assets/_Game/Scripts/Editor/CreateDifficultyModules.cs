#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Systems;
using Type = GlorifiedBody.Systems.DifficultyModuleDefinition.ModuleType;
using Section = GlorifiedBody.Systems.DifficultyModuleDefinition.Section;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// Creates the difficulty module assets under Assets/_Game/Resources/DifficultyModules, so
    /// DifficultyManager auto-loads them at runtime. Idempotent: it upserts each asset and deletes the assets of
    /// modules that are gone (the EASY / NORMAL / HARD presets before 2026-10-26).
    /// Bruno, 2026-10-26: three presets, 100 %, 150 % and 200 %, or a custom set in two sections, harder and easier.
    /// The 150 % preset is the old NORMAL and the 200 % preset the old HARD plus harder hits. The numbers are PROPOSED.
    /// Menu: GlorifiedBody > Create Difficulty Modules. Batch: tools/unity.sh exec GlorifiedBody.Editor.CreateDifficultyModules.CreateAll
    /// </summary>
    public static class CreateDifficultyModules
    {
        private const string ResourcesFolder = "Assets/_Game/Resources";
        private const string AssetFolder = ResourcesFolder + "/DifficultyModules";

        private const string Guns = "guns";
        private const string Trigger = "trigger";
        private const string Damage = "damage";
        private const string Mk = "mk";
        private const string Short = "short";
        private const string Hp = "hp";

        private static readonly int[] None = new int[0];
        private static readonly int[] At150 = { 150 };
        private static readonly int[] At200 = { 200 };

        [MenuItem("GlorifiedBody/Create Difficulty Modules")]
        public static void CreateAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game"))
                AssetDatabase.CreateFolder("Assets", "_Game");
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder("Assets/_Game", "Resources");
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder(ResourcesFolder, "DifficultyModules");

            var made = new HashSet<string>();

            made.Add(CreateModule("base", "BASE", "█", "The lab as designed: one enemy fires at a time, 1.5 s from the laser to the attack, 2 s rest. Always on.",
                100, Type.Base, Section.Base, "", None, isBaseline: true,
                P(DifficultyModuleDefinition.ParamMaxSimultaneousShooters, 1f),
                P(DifficultyModuleDefinition.ParamAttackSeconds, 1.5f),
                P(DifficultyModuleDefinition.ParamRestSeconds, 2f)));

            // ── Harder ──
            made.Add(CreateModule("crossfire", "CROSSFIRE", "II", "Two enemies fire at a time.",
                25, Type.Crossfire, Section.Harder, Guns, At150, false, P(DifficultyModuleDefinition.ParamMaxSimultaneousShooters, 2f)));
            made.Add(CreateModule("firing_squad", "FIRING SQUAD", "III", "Every enemy fires at the same time.",
                50, Type.FiringSquad, Section.Harder, Guns, At200, false, P(DifficultyModuleDefinition.ParamMaxSimultaneousShooters, 0f)));
            made.Add(CreateModule("quick_trigger", "QUICK TRIGGER", "»", "1.0 s from the laser to the attack.",
                25, Type.QuickTrigger, Section.Harder, Trigger, At150, false, P(DifficultyModuleDefinition.ParamAttackSeconds, 1f)));
            made.Add(CreateModule("hair_trigger", "HAIR TRIGGER", "»»", "0.8 s from the laser to the attack.",
                35, Type.HairTrigger, Section.Harder, Trigger, At200, false, P(DifficultyModuleDefinition.ParamAttackSeconds, 0.8f)));
            made.Add(CreateModule("hard_hitters", "HARD HITTERS", "✚", "Enemy attacks do 1.5x damage.",
                15, Type.HardHitters, Section.Harder, Damage, At200, false, P(DifficultyModuleDefinition.ParamEnemyDamageScale, 1.5f)));
            made.Add(CreateModule("fragile", "FRAGILE", "❤", "Her maximum HP is halved.",
                20, Type.Fragile, Section.Harder, "", None, false, P(DifficultyModuleDefinition.ParamPlayerMaxHpScale, 0.5f)));
            made.Add(CreateModule("one_hit_wonder", "ONE-HIT WONDER", "☢", "Any hit kills her. No second wind.",
                80, Type.OneHitWonder, Section.Harder, "", None, false));
            made.Add(CreateModule("scarcity", "SCARCITY", "⚠", "MK recharges at half speed.",
                15, Type.Scarcity, Section.Harder, Mk, None, false, P(DifficultyModuleDefinition.ParamMkRegenScale, 0.5f)));
            made.Add(CreateModule("short_fuse", "SHORT FUSE", "ϟ", "The SHORT bar fills 1.5x as fast.",
                15, Type.ShortFuse, Section.Harder, Short, None, false, P(DifficultyModuleDefinition.ParamShortGainScale, 1.5f)));

            // ── Easier ──
            made.Add(CreateModule("slow_trigger", "SLOW TRIGGER", "«", "2.0 s from the laser to the attack.",
                -20, Type.SlowTrigger, Section.Easier, Trigger, None, false, P(DifficultyModuleDefinition.ParamAttackSeconds, 2f)));
            made.Add(CreateModule("long_rest", "LONG REST", "◷", "Enemies rest 4 s after an attack.",
                -15, Type.LongRest, Section.Easier, "", None, false, P(DifficultyModuleDefinition.ParamRestSeconds, 4f)));
            made.Add(CreateModule("thick_skin", "THICK SKIN", "◆", "Enemy attacks do half damage.",
                -25, Type.ThickSkin, Section.Easier, Damage, None, false, P(DifficultyModuleDefinition.ParamEnemyDamageScale, 0.5f)));
            made.Add(CreateModule("steady_mind", "STEADY MIND", "≈", "The SHORT bar fills half as fast.",
                -20, Type.SteadyMind, Section.Easier, Short, None, false, P(DifficultyModuleDefinition.ParamShortGainScale, 0.5f)));
            made.Add(CreateModule("deep_well", "DEEP WELL", "◉", "MK recharges twice as fast.",
                -15, Type.DeepWell, Section.Easier, Mk, None, false, P(DifficultyModuleDefinition.ParamMkRegenScale, 2f)));
            made.Add(CreateModule("mending", "MENDING", "✙", "Feeding on a body gives twice the HP.",
                -10, Type.Mending, Section.Easier, Hp, None, false, P(DifficultyModuleDefinition.ParamFeedScale, 2f)));

            foreach (string guid in AssetDatabase.FindAssets("t:DifficultyModuleDefinition", new[] { AssetFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!made.Contains(System.IO.Path.GetFileNameWithoutExtension(path))) AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CreateDifficultyModules] {made.Count} difficulty module assets created or updated in " + AssetFolder);
        }

        private static ModuleParam P(string key, float value) => new ModuleParam { key = key, value = value };

        private static string CreateModule(
            string id,
            string displayName,
            string symbol,
            string description,
            int difficultyPercent,
            Type moduleType,
            Section section,
            string exclusiveGroup,
            int[] presets,
            bool isBaseline,
            params ModuleParam[] parameters)
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
            module.section = section;
            module.isBaseline = isBaseline;
            module.exclusiveGroup = exclusiveGroup;
            module.presets = presets;
            module.parameters = parameters;

            if (isNew)
            {
                AssetDatabase.CreateAsset(module, path);
            }
            else
            {
                EditorUtility.SetDirty(module);
            }
            return id;
        }
    }
}
#endif
