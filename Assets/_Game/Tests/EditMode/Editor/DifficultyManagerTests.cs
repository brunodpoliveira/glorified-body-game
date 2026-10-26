using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using GlorifiedBody.Systems;
using Type = GlorifiedBody.Systems.DifficultyModuleDefinition.ModuleType;
using Section = GlorifiedBody.Systems.DifficultyModuleDefinition.Section;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>
    /// The difficulty engine (Bruno, 2026-10-26): presets as sets of modules, exclusive groups, CUSTOM when the set
    /// matches no preset, the override and product numbers. Test modules only; the saved choice is kept and put back.
    /// </summary>
    public class DifficultyManagerTests
    {
        private const string PrefsKey = "difficulty_modules_v2";

        private GameObject go;
        private DifficultyManager manager;
        private readonly List<DifficultyModuleDefinition> made = new List<DifficultyModuleDefinition>();
        private string savedPrefs;

        [SetUp]
        public void SetUp()
        {
            savedPrefs = PlayerPrefs.HasKey(PrefsKey) ? PlayerPrefs.GetString(PrefsKey) : null;
            ClearSingleton();
            go = new GameObject("DifficultyManager");
            manager = go.AddComponent<DifficultyManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            ClearSingleton();
            foreach (var module in made) Object.DestroyImmediate(module);
            made.Clear();
            if (savedPrefs != null) PlayerPrefs.SetString(PrefsKey, savedPrefs);
            else PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void DifficultyManager_IsModuleActive_ReturnsTrueForActive()
        {
            var module = Make("test_fragile", Type.Fragile, Section.Harder, 20, "");
            Assert.IsFalse(manager.IsModuleActive(module), "Module should be inactive before enabling.");

            manager.EnableModule(module);
            Assert.IsTrue(manager.IsModuleActive(module), "Module should be active after enabling.");
        }

        [Test]
        public void AModuleOfAGroup_TurnsTheOthersOfItsGroupOff()
        {
            var quick = Make("test_quick", Type.QuickTrigger, Section.Harder, 25, "trigger");
            var slow = Make("test_slow", Type.SlowTrigger, Section.Easier, -20, "trigger");
            var skin = Make("test_skin", Type.ThickSkin, Section.Easier, -25, "damage");

            manager.EnableModule(quick);
            manager.EnableModule(skin);
            manager.EnableModule(slow);
            Assert.IsFalse(manager.IsModuleActive(quick), "SLOW TRIGGER turns QUICK TRIGGER off");
            Assert.IsTrue(manager.IsModuleActive(slow));
            Assert.IsTrue(manager.IsModuleActive(skin), "another group stays");
        }

        [Test]
        public void APreset_IsItsSetOfModules_AndAnotherSetIsCustom()
        {
            Make("test_base", Type.Base, Section.Base, 100, "", true, null,
                P(DifficultyModuleDefinition.ParamMaxSimultaneousShooters, 1f), P(DifficultyModuleDefinition.ParamAttackSeconds, 1.5f));
            var cross = Make("test_cross", Type.Crossfire, Section.Harder, 25, "guns", false, new[] { 150 }, P(DifficultyModuleDefinition.ParamMaxSimultaneousShooters, 2f));
            var squad = Make("test_squad", Type.FiringSquad, Section.Harder, 50, "guns", false, new[] { 200 }, P(DifficultyModuleDefinition.ParamMaxSimultaneousShooters, 0f));
            var quick = Make("test_quick", Type.QuickTrigger, Section.Harder, 25, "trigger", false, new[] { 150 }, P(DifficultyModuleDefinition.ParamAttackSeconds, 1f));
            var hair = Make("test_hair", Type.HairTrigger, Section.Harder, 50, "trigger", false, new[] { 200 }, P(DifficultyModuleDefinition.ParamAttackSeconds, 0.8f));
            var skin = Make("test_skin", Type.ThickSkin, Section.Easier, -25, "damage", false, null, P(DifficultyModuleDefinition.ParamEnemyDamageScale, 0.5f));

            Assert.IsTrue(manager.SetPreset(100));
            Assert.AreEqual(100, manager.ActivePresetPercent);
            Assert.AreEqual(100, manager.TotalDifficultyPercent);
            Assert.AreEqual(1, manager.MaxSimultaneousShooters, "100 %: the base, one shooter");
            Assert.AreEqual(1.5f, manager.AttackSeconds, 0.001f);

            Assert.IsTrue(manager.SetPreset(150));
            Assert.IsTrue(manager.IsModuleActive(cross) && manager.IsModuleActive(quick), "150 % turns its modules on");
            Assert.IsFalse(manager.IsModuleActive(squad) || manager.IsModuleActive(hair), "and no other");
            Assert.AreEqual(150, manager.ActivePresetPercent);
            Assert.AreEqual(150, manager.TotalDifficultyPercent);
            Assert.AreEqual(2, manager.MaxSimultaneousShooters);
            Assert.AreEqual(1f, manager.AttackSeconds, 0.001f);
            Assert.AreEqual("150%", manager.Label);

            Assert.IsTrue(manager.SetPreset(200));
            Assert.IsFalse(manager.IsModuleActive(cross), "200 % turns the 150 % modules off");
            Assert.AreEqual(200, manager.TotalDifficultyPercent);
            Assert.AreEqual(0, manager.MaxSimultaneousShooters, "0 = every enemy");
            Assert.AreEqual(0.8f, manager.AttackSeconds, 0.001f);

            manager.EnableModule(skin);
            Assert.IsTrue(manager.IsCustom, "a set that matches no preset is custom");
            Assert.AreEqual(0, manager.ActivePresetPercent);
            Assert.AreEqual(175, manager.TotalDifficultyPercent, "an easier module takes from the total");
            Assert.AreEqual(0.5f, manager.EnemyDamageScale, 0.001f);
            Assert.AreEqual("CUSTOM 175%", manager.Label);

            manager.DisableModule(skin);
            Assert.AreEqual(200, manager.ActivePresetPercent, "back to the 200 % set");
            Assert.IsFalse(manager.SetPreset(125), "only 100, 150 and 200 are presets");
        }

        [Test]
        public void Multipliers_Multiply_AndNoModuleGivesOne()
        {
            var hard = Make("test_hard", Type.HardHitters, Section.Harder, 15, "", false, null, P(DifficultyModuleDefinition.ParamEnemyDamageScale, 1.5f));
            var skin = Make("test_skin", Type.ThickSkin, Section.Easier, -25, "", false, null, P(DifficultyModuleDefinition.ParamEnemyDamageScale, 0.5f));
            Assert.AreEqual(1f, manager.EnemyDamageScale, 0.001f);
            manager.EnableModule(hard);
            Assert.AreEqual(1.5f, manager.EnemyDamageScale, 0.001f);
            manager.EnableModule(skin);
            Assert.AreEqual(0.75f, manager.EnemyDamageScale, 0.001f);
            Assert.AreEqual(1f, manager.MkRegenScale, 0.001f, "no module carries it");
        }

        private static ModuleParam P(string key, float value) => new ModuleParam { key = key, value = value };

        private DifficultyModuleDefinition Make(string id, Type type, Section section, int percent, string group,
            bool isBaseline = false, int[] presets = null, params ModuleParam[] parameters)
        {
            var module = ScriptableObject.CreateInstance<DifficultyModuleDefinition>();
            module.moduleId = id;
            module.moduleType = type;
            module.section = section;
            module.difficultyPercent = percent;
            module.exclusiveGroup = group;
            module.isBaseline = isBaseline;
            module.presets = presets ?? new int[0];
            module.parameters = parameters;
            manager.RegisterModule(module);
            made.Add(module);
            return module;
        }

        // DifficultyManager.Awake self-destructs duplicates against a DontDestroyOnLoad
        // singleton; clear the static Instance between tests so AddComponent stays valid.
        // Instance has a private setter, so null its auto-property backing field directly.
        private static void ClearSingleton()
        {
            FieldInfo field = typeof(DifficultyManager).GetField("<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }
    }
}
