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
            // The T19 guard: the source travels 1.53 m per walk cycle and 2.94 m per run cycle, the idle stays
            // (2026-10-21). Lucy was the case here until T49 (2026-11-16): her clips play in place now.
            ClipTravelFile measured = CharacterImportSettings.ReadClipTravel(CharacterImportSettings.Folder + "guard.fbx");
            Assert.IsNotNull(measured, "guard.clips.json");
            Assert.IsNotNull(measured.Find("idle"), "idle measured");
            Assert.IsNotNull(measured.Find("walk"), "walk measured");
            Assert.IsNotNull(measured.Find("run"), "run measured");
            Assert.IsFalse(measured.Find("idle").travels, "the idle stays");
            Assert.IsTrue(measured.Find("walk").travels, "the walk travels");
            Assert.IsTrue(measured.Find("run").travels, "the run travels");
            Assert.Greater(measured.Find("run").travel, measured.Find("walk").travel, "the run travels farther than the walk");
            Assert.Greater(measured.Find("walk").travel, 1f, "the walk travels over 1 m per cycle at 1 m scale");
        }

        /// <summary>
        /// The T49 Lucy (rounds 175, 178): the body (13,958 triangles) and the T27 Crown (2,800, its own material), 23
        /// bones, idle, walk and run in place, the 2048 map sets, the skin matte everywhere (round 24) and the gold veins
        /// metal.
        /// </summary>
        [Test]
        public void LucyBare_IsTheT49Model_InPlace_WithItsMaps_AndMatteSkin()
        {
            CheckT49Class("lucy_bare", travels: false, new[] { ("lucy_t49_base", 13958), ("crown_t27", 2800) },
                maxClothSmoothness: 0.3f, wantsMetal: true);
            Material crown = AssetDatabase.LoadAssetAtPath<Material>(CreateCharacterAssets.MaterialsFolder + "/lucy_bare_crown_t27.mat");
            Assert.IsNotNull(crown, "the Crown has its own material (round 178)");
            Assert.IsNotNull(crown.GetTexture("_BumpMap"), "the Crown's normal map (the T27 wear maps)");
            Assert.IsNotNull(crown.GetTexture("_MetallicGlossMap"), "the Crown's metallic-smoothness map");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CreateCharacterAssets.PrefabsFolder + "/lucy_bare.prefab");
            Renderer crownRenderer = null;
            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true)) if (r.name == "crown_t27") crownRenderer = r;
            Assert.IsNotNull(crownRenderer, "the Crown's renderer in the prefab");
            Assert.AreEqual(crown, crownRenderer.sharedMaterial, "the Crown wears its own material");
        }

        /// <summary>The T49 guard, patient and nurse (round 178, the T26 full mesh + the T27 bake; the guard baked again in
        /// round 184 with the round shoulder patches): about 9,000 triangles, the walk and the run travel, the 2048 map set
        /// with the cloth capped at about 0.25 (ART_LESSONS §3 item 8).</summary>
        [TestCase("guard", "guard_t49", 8999)]
        [TestCase("patient", "patient_t49", 8999)]
        [TestCase("nurse", "nurse_t49", 9000)]
        public void TheRound178Classes_AreT49Models_WithTheirMaps(string id, string mesh, int triangles)
        {
            CheckT49Class(id, travels: true, new[] { (mesh, triangles) }, maxClothSmoothness: 0.3f, wantsMetal: false);
        }

        /// <summary>
        /// The T49 Lucy guard (round 179): the body baked to 9,000 with the plain head cut off (8,349 triangles) and the
        /// T27 Crown (2,800, its own material), 23 bones, the clips in place like Lucy bare, the 2048 map sets.
        /// </summary>
        [Test]
        public void LucyGuard_IsTheT49Model_WithTheCrown_AndItsMaps()
        {
            CheckT49Class("lucy_guard", travels: false, new[] { ("lucy_guard_t49", 8349), ("crown_t27", 2800) },
                maxClothSmoothness: 0.3f, wantsMetal: false);
            Material crown = AssetDatabase.LoadAssetAtPath<Material>(CreateCharacterAssets.MaterialsFolder + "/lucy_guard_crown_t27.mat");
            Assert.IsNotNull(crown, "the Crown has its own material");
            Assert.IsNotNull(crown.GetTexture("_BumpMap"), "the Crown's normal map (the T27 wear maps)");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CreateCharacterAssets.PrefabsFolder + "/lucy_guard.prefab");
            SkinnedMeshRenderer crownRenderer = null;
            foreach (SkinnedMeshRenderer r in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.name == "crown_t27") crownRenderer = r;
            Assert.IsNotNull(crownRenderer, "the Crown's renderer in the prefab");
            Assert.AreEqual(crown, crownRenderer.sharedMaterial, "the Crown wears its own material");
            // The Crown is skinned 100 % to the head (round 178, CANON 2).
            Mesh mesh = crownRenderer.sharedMesh;
            string head = null;
            foreach (BoneWeight w in mesh.boneWeights)
            {
                Assert.GreaterOrEqual(w.weight0, 0.999f, "the Crown: one bone a vertex");
                string bone = crownRenderer.bones[w.boneIndex0].name;
                if (head == null) head = bone;
                Assert.AreEqual(head, bone, "the Crown: every vertex on the same bone");
            }
            Assert.AreEqual("mixamorig:Head", head, "the Crown rides the head bone");
        }

        /// <summary>The T49 lodge staff and toll guard (round 179): 8,999 triangles, the walk and the run travel, the map set.</summary>
        [TestCase("lodge_staff", "lodge_staff_t49", 8999)]
        [TestCase("toll_guard", "toll_guard_t49", 8999)]
        public void TheRound179Figures_AreT49Models_WithTheirMaps(string id, string mesh, int triangles)
        {
            CheckT49Class(id, travels: true, new[] { (mesh, triangles) }, maxClothSmoothness: 0.3f, wantsMetal: false);
        }

        /// <summary>The T49 soldier and fireman (round 181): 8,999 and 8,998 triangles, the walk and the run travel, the cloth matte.</summary>
        [TestCase("soldier", "soldier_t49", 8999)]
        [TestCase("fireman", "fireman_t49", 8998)]
        public void TheRound181Classes_AreT49Models_WithTheirMaps(string id, string mesh, int triangles)
        {
            CheckT49Class(id, travels: true, new[] { (mesh, triangles) }, maxClothSmoothness: 0.3f, wantsMetal: false);
        }

        /// <summary>
        /// The T49 lab worker (round 181): 8,999 triangles. The suit keeps its baked gloss with metal 0 (DEMO_PLAN §3.2
        /// "glossy light blue", round 181 CANON 1): no cloth cap for this class, the gloss must stay.
        /// </summary>
        [Test]
        public void TheLabWorker_IsTheT49Model_AndKeepsItsGlossySuit()
        {
            CheckT49Class("labworker", travels: true, new[] { ("labworker_t49", 8999) }, maxClothSmoothness: 1f, wantsMetal: false, minClothSmoothness: 0.6f);
        }

        /// <summary>
        /// Round 179 ("Speed up the clips"): in every T49 class controller the walk and the run play at the game speed
        /// over the speed of the clip's planted foot at x1. Round 185 ("From the bot reading"): that speed is the bot
        /// lucy's reading in play (pace_from_bot.json), for every T49 class. It stays within 15 % of the source measure
        /// (clips.json ground x the class height), so a bad reading shows. Lucy: the TankController speeds. The figures:
        /// the Invector AI speeds (ClassPace).
        /// </summary>
        [TestCase("lucy_bare", 1.70f, true)]
        [TestCase("lucy_guard", 1.70f, true)]
        [TestCase("guard", 1.80f, false)]
        [TestCase("patient", 1.68f, false)]
        [TestCase("nurse", 1.68f, false)]
        [TestCase("lodge_staff", 1.75f, false)]
        [TestCase("toll_guard", 1.82f, false)]
        [TestCase("labworker", 1.78f, false)]
        [TestCase("soldier", 1.80f, false)]
        [TestCase("fireman", 1.80f, false)]
        [TestCase("power_armour", 2.90f, false)]
        public void T49Clips_PlayAtTheGameSpeed(string id, float height, bool lucy)
        {
            ClipTravelFile measured = CharacterImportSettings.ReadClipTravel($"{CreateCharacterAssets.ModelsFolder}/{id}.fbx");
            Assert.IsNotNull(measured, id + ".clips.json");
            float walkGround = measured.Find("walk").ground, runGround = measured.Find("run").ground;
            Assert.Greater(walkGround, 0.3f, id + ": the walk's ground speed is measured (tools/characters_clips.py)");
            Assert.Greater(runGround, walkGround, id + ": the run's feet move faster than the walk's");
            float walkSpeed = lucy ? 1.6f : ClassPace.FigureWalkSpeed, runSpeed = lucy ? 3.6f : ClassPace.FigureRunSpeed;
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>($"Assets/_Game/Characters/Anim/{id}.controller");
            Assert.IsNotNull(controller, id + ".controller");
            float walkPace = CreateCharacterAssets.PaceOf(controller, 0.5f), runPace = CreateCharacterAssets.PaceOf(controller, 1f);
            CreateCharacterAssets.PlaySpeed inPlay = CreateCharacterAssets.ReadPaceFromBot()?.Find(id);
            Assert.IsNotNull(inPlay, id + ": a bot reading in " + CreateCharacterAssets.PacePath + " (tools/characters_pace.py)");
            Debug.Log($"[CharacterImportTests] {id} pace: walk x{walkPace:0.00} (feet in play {inPlay.walk:0.00} m/s at x1, source {walkGround * height:0.00}, game {walkSpeed:0.00}), run x{runPace:0.00} (feet in play {inPlay.run:0.00} m/s at x1, source {runGround * height:0.00}, game {runSpeed:0.00})");
            Assert.AreEqual(walkGround * height, inPlay.walk, walkGround * height * 0.15f, id + ": the walk's bot reading is near the source measure");
            Assert.AreEqual(runGround * height, inPlay.run, runGround * height * 0.15f, id + ": the run's bot reading is near the source measure");
            Assert.AreEqual(walkSpeed, inPlay.walk * walkPace, 0.02f, id + ": the walk's feet move at the game walk speed");
            Assert.AreEqual(runSpeed, inPlay.run * runPace, 0.02f, id + ": the run's feet move at the game run speed");
        }

        /// <summary>
        /// The T49 power armour (round 183, rigged again in round 185): 8,974 triangles (the voxel remesh before the bake), 2.90 m in the game, the walk
        /// and the run travel. The white plates keep their baked gloss (hard parts, metal 0), the black rubber joints are
        /// matte: no cloth cap for this class, and the gloss must stay.
        /// </summary>
        [Test]
        public void ThePowerArmour_IsTheT49Model_AndKeepsItsGlossyPlates()
        {
            CheckT49Class("power_armour", travels: true, new[] { ("power_armour_t49", 8974) }, maxClothSmoothness: 1f, wantsMetal: false, minClothSmoothness: 0.6f);
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CreateCharacterAssets.CatalogPath);
            Assert.AreEqual(2.90f, catalog.Find("power_armour").height, 1e-3f, "DEMO_PLAN §3.2: 2.90 m");
        }

        /// <summary>
        /// The enemy prefabs of the T49 classes (Resources/Enemies, InvectorEnemyFactory) carry the current model: every
        /// skinned mesh is a mesh of the class FBX on disk. A model swap under the same file name leaves the old mesh
        /// reference in a prefab that was not built again, and the figure has no body in the game.
        /// </summary>
        [TestCase("guard_pistol", "guard")]
        [TestCase("guard_shield", "guard")]
        [TestCase("patient", "patient")]
        [TestCase("nurse", "nurse")]
        [TestCase("lodge_staff", "lodge_staff")]
        [TestCase("toll_guard_pistol", "toll_guard")]
        [TestCase("labworker", "labworker")]
        [TestCase("labworker_pistol", "labworker")]
        [TestCase("labworker_knife", "labworker")]
        [TestCase("soldier_pistol", "soldier")]
        [TestCase("soldier_rifle", "soldier")]
        [TestCase("soldier_gas", "soldier")]
        [TestCase("soldier_shield", "soldier")]
        [TestCase("soldier_shotgun", "soldier")]
        [TestCase("soldier_grenades", "soldier")]
        [TestCase("power_armour", "power_armour")]
        [TestCase("power_armour_guns", "power_armour")]
        public void T49EnemyPrefabs_WearTheCurrentModel(string kind, string classId)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Resources/Enemies/{kind}.prefab");
            Assert.IsNotNull(prefab, kind + ".prefab (InvectorEnemyFactory.BuildEnemyPrefabs)");
            string fbx = $"{CreateCharacterAssets.ModelsFolder}/{classId}.fbx";
            var meshes = new System.Collections.Generic.HashSet<Mesh>();
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(fbx)) if (o is Mesh m) meshes.Add(m);
            SkinnedMeshRenderer[] skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.Greater(skins.Length, 0, kind + ": a skinned mesh");
            foreach (SkinnedMeshRenderer skin in skins)
            {
                Assert.IsNotNull(skin.sharedMesh, $"{kind}/{skin.name}: a mesh");
                Assert.IsTrue(meshes.Contains(skin.sharedMesh), $"{kind}/{skin.name}: the mesh {skin.sharedMesh.name} comes from {fbx}");
            }
        }

        private static void CheckT49Class(string id, bool travels, (string mesh, int triangles)[] meshes, float maxClothSmoothness, bool wantsMetal, float minClothSmoothness = 0f)
        {
            string fbx = CharacterImportSettings.Folder + id + ".fbx";
            ClipTravelFile measured = CharacterImportSettings.ReadClipTravel(fbx);
            Assert.IsNotNull(measured, id + ".clips.json");
            Assert.IsFalse(measured.Find("idle").travels, id + ": the idle stays");
            foreach (string clip in new[] { "walk", "run" })
            {
                Assert.IsNotNull(measured.Find(clip), $"{id}: {clip} measured");
                Assert.AreEqual(travels, measured.Find(clip).travels, $"{id}: the {clip} {(travels ? "travels" : "plays in place")} (travel {measured.Find(clip).travel:0.00})");
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            Assert.IsNotNull(model, fbx);
            SkinnedMeshRenderer[] skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.AreEqual(meshes.Length, skins.Length, $"{id}: {meshes.Length} skinned mesh(es)");
            foreach ((string name, int count) in meshes)
            {
                SkinnedMeshRenderer skin = System.Array.Find(skins, k => k.name == name);
                Assert.IsNotNull(skin, $"{id}: the mesh {name}");
                int tris = skin.sharedMesh.triangles.Length / 3;
                Debug.Log($"[CharacterImportTests] {id}/{name}: {tris} triangles, {skin.bones.Length} bones");
                Assert.AreEqual(count, tris, $"{id}/{name}: triangles");
                Assert.AreEqual(23, skin.bones.Length, $"{id}/{name}: 23 mixamo bones");
            }
            Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(fbx);
            Assert.IsTrue(avatar != null && avatar.isValid && avatar.isHuman, id + ": a valid Humanoid avatar");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(CreateCharacterAssets.MaterialsFolder + "/" + id + ".mat");
            Assert.IsNotNull(material, id + ".mat (CreateCharacterAssets.BuildAll)");
            Texture baseMap = material.GetTexture("_BaseMap"), normal = material.GetTexture("_BumpMap"), ms = material.GetTexture("_MetallicGlossMap");
            Assert.IsNotNull(baseMap, id + ": the base colour");
            Assert.IsNotNull(normal, id + ": the normal map");
            Assert.IsNotNull(ms, id + ": the metallic-smoothness map");
            Assert.AreEqual(2048, baseMap.width, id + ": the maps at 2048");
            Assert.AreEqual(2048, normal.width, id + ": the normal map at 2048");
            Assert.IsTrue(material.IsKeywordEnabled("_NORMALMAP"), "_NORMALMAP");
            Assert.IsTrue(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), "_METALLICSPECGLOSSMAP");
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal));
            Assert.AreEqual(TextureImporterType.NormalMap, normalImporter.textureType, id + ": the normal map imports as a normal map");
            var msImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(ms));
            Assert.IsFalse(msImporter.sRGBTexture, id + ": the metallic-smoothness map is linear");

            // The gloss rule (round 24, ART_LESSONS §3 item 8): read the file, the smoothness as the material shows it
            // (alpha x _Smoothness). The median of what is not metal (the skin, the cloth) is matte; the gloss stays on
            // the hard parts and the metal.
            var pixels = new Texture2D(2, 2);
            Assert.IsTrue(pixels.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(ms))), id + ": the ms map reads");
            Color32[] data = pixels.GetPixels32();
            var soft = new System.Collections.Generic.List<float>();
            var hard = new System.Collections.Generic.List<float>();
            int glossy = 0;
            for (int i = 0; i < data.Length; i += 7)
            {
                float smooth = data[i].a / 255f * material.GetFloat("_Smoothness");
                if (data[i].r < 128) { soft.Add(smooth); if (smooth > 0.5f) glossy++; } else hard.Add(smooth);
            }
            UnityEngine.Object.DestroyImmediate(pixels);
            soft.Sort(); hard.Sort();
            float softMedian = soft[soft.Count / 2];
            float softP90 = soft[soft.Count * 9 / 10];
            float hardShare = hard.Count / (float)(soft.Count + hard.Count);
            float hardMedian = hard.Count > 0 ? hard[hard.Count / 2] : 0f;
            Debug.Log($"[CharacterImportTests] {id} gloss: not metal smoothness median {softMedian:0.00}, p90 {softP90:0.00}, glossy (> 0.5) {glossy * 100f / soft.Count:0.0} %; metal {hardShare * 100f:0.0} %, smoothness median {hardMedian:0.00}");
            Assert.LessOrEqual(softMedian, maxClothSmoothness, id + ": the skin and the cloth are matte (about 0.25)");
            Assert.GreaterOrEqual(softMedian, minClothSmoothness, id + ": the baked gloss stays (no cap)");
            if (wantsMetal)
            {
                Assert.LessOrEqual(softP90, 0.3f, id + ": the whole skin is matte, the neck band too (round 178)");
                Assert.Greater(hardShare, 0.01f, id + ": the gold veins are metal");
                Assert.Greater(hardMedian, 0.5f, id + ": the gold is glossier than the skin");
            }
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
