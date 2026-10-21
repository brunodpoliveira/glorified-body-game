using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Characters;
using GlorifiedBody.Editor.Characters;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>
    /// The import of every character FBX follows the measured travel of its clips (tools/characters_clips.py):
    /// a clip that travels gives its travel to the root motion, a clip that stays keeps its root in the pose,
    /// and no prefab applies root motion. The slide-back of 2026-10-21 came from a walk clip baked with its
    /// travel. A new class without its measurement file fails here before anyone sees it slide.
    /// </summary>
    public class CharacterImportTests
    {
        private static string[] CharacterFbxPaths()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { CharacterImportSettings.Folder.TrimEnd('/') });
            var paths = new System.Collections.Generic.List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
            }
            return paths.ToArray();
        }

        [Test]
        public void EveryCharacterFbx_HasItsClipMeasurement_AndTheImportFollowsIt()
        {
            string[] paths = CharacterFbxPaths();
            Assert.Greater(paths.Length, 0, "character FBX files under " + CharacterImportSettings.Folder);
            foreach (string path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Assert.IsNotNull(importer, path + " imports as a model");
                ClipTravelFile measured = CharacterImportSettings.ReadClipTravel(path);
                Assert.IsNotNull(measured, $"{path}: {CharacterImportSettings.ClipsJsonPath(path)} exists (tools/characters.sh writes it)");
                ModelImporterClipAnimation[] clips = importer.clipAnimations;
                Assert.Greater(clips.Length, 0, path + " has clips");
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    ClipTravel info = measured.Find(clip.name);
                    Assert.IsNotNull(info, $"{path}: the clip '{clip.name}' is measured");
                    string expected = info.travels ? "to the root motion (not baked)" : "baked into the pose";
                    Assert.AreEqual(!info.travels, clip.lockRootPositionXZ, $"{path} '{clip.name}': travel {info.travel:0.00} per cycle, root XZ {expected}");
                    Assert.AreEqual(!info.travels, clip.keepOriginalPositionXZ, $"{path} '{clip.name}': the XZ reference ({(info.travels ? "centre of mass" : "original")})");
                    Assert.IsTrue(clip.loopTime, $"{path} '{clip.name}': a loop");
                }
                foreach (ClipTravel info in measured.clips)
                {
                    bool present = false;
                    foreach (ModelImporterClipAnimation clip in clips)
                    {
                        if (string.Equals(clip.name, info.name, StringComparison.OrdinalIgnoreCase)) present = true;
                    }
                    Assert.IsTrue(present, $"{path}: the measured clip '{info.name}' is in the import");
                }
            }
        }

        [Test]
        public void TheMeasurement_FlagsTheWalkAndTheRun_NotTheIdle()
        {
            // lucy_bare: the source travels 1.53 m per walk cycle and 2.95 m per run cycle, the idle stays (2026-10-21).
            ClipTravelFile measured = CharacterImportSettings.ReadClipTravel(CharacterImportSettings.Folder + "lucy_bare.fbx");
            Assert.IsNotNull(measured, "lucy_bare.clips.json");
            Assert.IsNotNull(measured.Find("idle"), "idle measured");
            Assert.IsNotNull(measured.Find("walk"), "walk measured");
            Assert.IsNotNull(measured.Find("run"), "run measured");
            Assert.IsFalse(measured.Find("idle").travels, "the idle stays");
            Assert.IsTrue(measured.Find("walk").travels, "the walk travels");
            Assert.IsTrue(measured.Find("run").travels, "the run travels");
            Assert.Greater(measured.Find("run").travel, measured.Find("walk").travel, "the run travels farther than the walk");
            Assert.Greater(measured.Find("walk").travel, 1f, "the walk travels over 1 m per cycle at 1 m scale");
        }

        [Test]
        public void NoCharacterPrefab_AppliesRootMotion()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CreateCharacterAssets.CatalogPath);
            Assert.IsNotNull(catalog, "the catalog (CreateCharacterAssets.BuildAll)");
            Assert.Greater(catalog.entries.Count, 0, "classes in the catalog");
            foreach (CharacterEntry entry in catalog.entries)
            {
                Assert.IsNotNull(entry.prefab, entry.id + " has a prefab");
                Animator animator = entry.prefab.GetComponentInChildren<Animator>(true);
                Assert.IsNotNull(animator, entry.id + " has an Animator");
                Assert.IsFalse(animator.applyRootMotion, entry.id + ": the Animator applies no root motion (the controller moves the root)");
            }
        }
    }
}
