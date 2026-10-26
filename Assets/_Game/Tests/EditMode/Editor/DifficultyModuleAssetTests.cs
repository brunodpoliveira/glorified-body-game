using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using GlorifiedBody.Systems;
using ModuleType = GlorifiedBody.Systems.DifficultyModuleDefinition.ModuleType;
using Section = GlorifiedBody.Systems.DifficultyModuleDefinition.Section;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>
    /// Validates the generated difficulty assets (Resources/DifficultyModules, CreateDifficultyModules.CreateAll):
    /// one asset per module, the base 100 %, the presets 100 / 150 / 200 % as sums of their harder modules, and the
    /// old numbers of the presets (one, two, every shooter; 1.5, 1.0, 0.8 s).
    /// </summary>
    public class DifficultyModuleAssetTests
    {
        private Dictionary<ModuleType, DifficultyModuleDefinition> byType;
        private DifficultyModuleDefinition[] all;

        [SetUp]
        public void SetUp()
        {
            all = Resources.LoadAll<DifficultyModuleDefinition>("DifficultyModules");
            byType = new Dictionary<ModuleType, DifficultyModuleDefinition>();
            foreach (DifficultyModuleDefinition module in all)
            {
                Assert.IsFalse(byType.ContainsKey(module.moduleType), $"Two assets for {module.moduleType}.");
                byType[module.moduleType] = module;
            }
        }

        [Test]
        public void AllModuleTypes_HaveAnAsset()
        {
            foreach (ModuleType type in System.Enum.GetValues(typeof(ModuleType)))
            {
                Assert.IsTrue(byType.ContainsKey(type), $"Missing difficulty module asset for {type}.");
            }
            Assert.AreEqual(System.Enum.GetValues(typeof(ModuleType)).Length, all.Length, "no stale asset");
        }

        [Test]
        public void Base_Is100Percent_OneShooter_AndTheSlowTelegraph()
        {
            DifficultyModuleDefinition b = byType[ModuleType.Base];
            Assert.IsTrue(b.isBaseline);
            Assert.AreEqual(Section.Base, b.section);
            Assert.AreEqual(100, b.difficultyPercent);
            Assert.AreEqual(1f, b.GetParam(DifficultyModuleDefinition.ParamMaxSimultaneousShooters, -1f), 0.001f);
            Assert.AreEqual(1.5f, b.GetParam(DifficultyModuleDefinition.ParamAttackSeconds, -1f), 0.001f);
            Assert.AreEqual(2f, b.GetParam(DifficultyModuleDefinition.ParamRestSeconds, -1f), 0.001f);
        }

        [Test]
        public void HarderModulesAdd_EasierModulesTake()
        {
            foreach (DifficultyModuleDefinition module in all)
            {
                if (module.section == Section.Harder) Assert.Greater(module.difficultyPercent, 0, module.moduleId);
                if (module.section == Section.Easier) Assert.Less(module.difficultyPercent, 0, module.moduleId);
                Assert.IsFalse(string.IsNullOrEmpty(module.description), module.moduleId + " has a description");
            }
        }

        [TestCase(100)]
        [TestCase(150)]
        [TestCase(200)]
        public void EachPreset_SumsToItsPercent_FromHarderModulesOnly(int percent)
        {
            int total = 0;
            var groups = new HashSet<string>();
            foreach (DifficultyModuleDefinition module in all)
            {
                if (module.isBaseline) { total += module.difficultyPercent; continue; }
                if (!module.InPreset(percent)) continue;
                Assert.AreEqual(Section.Harder, module.section, $"{module.moduleId}: a preset uses harder modules only");
                if (!string.IsNullOrEmpty(module.exclusiveGroup))
                    Assert.IsTrue(groups.Add(module.exclusiveGroup), $"{percent}: two modules of the group {module.exclusiveGroup}");
                total += module.difficultyPercent;
            }
            Assert.AreEqual(percent, total, $"the {percent} % preset");
        }

        [TestCase(100, 1, 1.5f, 1f)]
        [TestCase(150, 2, 1.0f, 1f)]
        [TestCase(200, 0, 0.8f, 1.5f)]
        public void EachPreset_GivesItsNumbers(int percent, int shooters, float attackSeconds, float damage)
        {
            var go = new GameObject("DifficultyManager");
            var savedPrefs = PlayerPrefs.HasKey("difficulty_modules_v2") ? PlayerPrefs.GetString("difficulty_modules_v2") : null;
            try
            {
                var manager = go.AddComponent<DifficultyManager>();
                foreach (DifficultyModuleDefinition module in all) manager.RegisterModule(module);
                Assert.IsTrue(manager.SetPreset(percent));
                Assert.AreEqual(percent, manager.ActivePresetPercent);
                Assert.AreEqual(percent, manager.TotalDifficultyPercent);
                Assert.AreEqual(shooters, manager.MaxSimultaneousShooters, "the shooters at a time");
                Assert.AreEqual(attackSeconds, manager.AttackSeconds, 0.001f, "the telegraph");
                Assert.AreEqual(2f, manager.RestSeconds, 0.001f, "the rest");
                Assert.AreEqual(damage, manager.EnemyDamageScale, 0.001f, "the enemy damage");
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (savedPrefs != null) PlayerPrefs.SetString("difficulty_modules_v2", savedPrefs);
                else PlayerPrefs.DeleteKey("difficulty_modules_v2");
            }
        }
    }
}
