using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using GlorifiedBody.Systems;
using ModuleType = GlorifiedBody.Systems.DifficultyModuleDefinition.ModuleType;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>
    /// Validates the generated difficulty assets (Resources/DifficultyModules): three presets with
    /// maxSimultaneousShooters 1 / 2 / 0 (the design: easy one, normal two, hard all) and the modifiers.
    /// </summary>
    public class DifficultyModuleAssetTests
    {
        private Dictionary<ModuleType, DifficultyModuleDefinition> byType;

        [SetUp]
        public void SetUp()
        {
            byType = new Dictionary<ModuleType, DifficultyModuleDefinition>();
            foreach (DifficultyModuleDefinition module in Resources.LoadAll<DifficultyModuleDefinition>("DifficultyModules"))
            {
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
        }

        [Test]
        public void Base_IsBaseline_AndNotAPreset()
        {
            Assert.IsTrue(byType[ModuleType.Base].isBaseline);
            Assert.IsFalse(byType[ModuleType.Base].IsPreset);
        }

        [Test]
        public void Presets_ShareOneGroup_AndCarryTheShooterCount()
        {
            AssertPreset(ModuleType.PresetEasy, 1f);
            AssertPreset(ModuleType.PresetNormal, 2f);
            AssertPreset(ModuleType.PresetHard, 0f);
            Assert.AreEqual(byType[ModuleType.PresetEasy].presetGroup, byType[ModuleType.PresetHard].presetGroup);
        }

        [Test]
        public void Modifiers_AreFreeToggles_WithTheirParams()
        {
            foreach (ModuleType type in new[] { ModuleType.Fragile, ModuleType.OneHitWonder, ModuleType.Scarcity })
            {
                Assert.IsFalse(byType[type].IsPreset, $"{type} must be a free toggle.");
                Assert.IsFalse(byType[type].isBaseline, $"{type} must not be baseline.");
            }

            AssertParam(ModuleType.Fragile, "degenMultiplier", 2f);
            AssertParam(ModuleType.Scarcity, "drainMultiplier", 1.5f);
        }

        private void AssertPreset(ModuleType type, float shooters)
        {
            Assert.IsTrue(byType.ContainsKey(type), $"Missing preset {type}.");
            Assert.IsTrue(byType[type].IsPreset, $"{type} must carry a presetGroup.");
            AssertParam(type, DifficultyModuleDefinition.ParamMaxSimultaneousShooters, shooters);
        }

        private void AssertParam(ModuleType type, string key, float expected)
        {
            Assert.IsTrue(byType.ContainsKey(type), $"Missing module {type}.");
            float value = byType[type].GetParam(key, float.NaN);
            Assert.AreEqual(expected, value, 0.001f, $"{type}.{key} expected {expected} but was {value}.");
        }
    }
}
