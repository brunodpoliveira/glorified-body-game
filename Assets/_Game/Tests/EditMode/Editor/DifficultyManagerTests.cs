using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.EditMode
{
    public class DifficultyManagerTests
    {
        private GameObject go;
        private DifficultyManager manager;

        [SetUp]
        public void SetUp()
        {
            ClearSingleton();
            go = new GameObject("DifficultyManager");
            manager = go.AddComponent<DifficultyManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            ClearSingleton();
        }

        [Test]
        public void DifficultyManager_IsModuleActive_ReturnsTrueForActive()
        {
            var module = ScriptableObject.CreateInstance<DifficultyModuleDefinition>();
            module.moduleId = "test_module_unique";
            module.moduleType = DifficultyModuleDefinition.ModuleType.Fragile;
            module.isBaseline = false;

            manager.RegisterModule(module);
            Assert.IsFalse(manager.IsModuleActive(module), "Module should be inactive before enabling.");

            manager.EnableModule(module);
            Assert.IsTrue(manager.IsModuleActive(module), "Module should be active after enabling.");

            Object.DestroyImmediate(module);
        }

        [Test]
        public void Presets_AreExclusive_AndOneStaysActive()
        {
            var easy = MakePreset("preset_easy_test", DifficultyModuleDefinition.ModuleType.PresetEasy, 1f);
            var normal = MakePreset("preset_normal_test", DifficultyModuleDefinition.ModuleType.PresetNormal, 2f);
            var hard = MakePreset("preset_hard_test", DifficultyModuleDefinition.ModuleType.PresetHard, 0f);
            manager.RegisterModule(easy);
            manager.RegisterModule(normal);
            manager.RegisterModule(hard);

            manager.EnableModule(easy);
            Assert.IsTrue(manager.IsModuleActive(easy));
            Assert.AreEqual(1, manager.MaxSimultaneousShooters, "Easy: one shooter at a time.");

            manager.EnableModule(hard);
            Assert.IsFalse(manager.IsModuleActive(easy), "Enabling Hard must turn Easy off.");
            Assert.IsTrue(manager.IsModuleActive(hard));
            Assert.AreEqual(0, manager.MaxSimultaneousShooters, "Hard: 0 = every enemy fires.");

            manager.DisableModule(hard);
            Assert.IsTrue(manager.IsModuleActive(hard), "A preset cannot be turned off, only replaced.");

            Assert.IsTrue(manager.SetPreset(DifficultyModuleDefinition.ModuleType.PresetNormal));
            Assert.AreSame(normal, manager.ActivePreset);
            Assert.AreEqual(2, manager.MaxSimultaneousShooters);

            Object.DestroyImmediate(easy);
            Object.DestroyImmediate(normal);
            Object.DestroyImmediate(hard);
        }

        private static DifficultyModuleDefinition MakePreset(string id, DifficultyModuleDefinition.ModuleType type, float shooters)
        {
            var module = ScriptableObject.CreateInstance<DifficultyModuleDefinition>();
            module.moduleId = id;
            module.moduleType = type;
            module.presetGroup = "preset";
            module.parameters = new[] { new ModuleParam { key = DifficultyModuleDefinition.ParamMaxSimultaneousShooters, value = shooters } };
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
