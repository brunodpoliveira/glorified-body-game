#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Player;
using GlorifiedBody.Rendering;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// Build order step 4, the look test (03_GAME_DESIGN §7.4, CONCEPTS §7). Two scenes, each a room set of the
    /// demo greyboxed and dressed, with Lucy, figures and fixed cameras, and the four dressings LookDirector
    /// switches between (the greybox, the real-time room with the 1999 sheen, the same with the baked bounce
    /// light, the plate: the room rendered once per camera, the characters drawn over it):
    ///   LookTest  = the Gallery of floor 1 (raw concrete, planters, hard pools of light).
    ///   LookTest2 = the skylight corridor (T12 GALLERY_v3), the recovery room and the therapy gym (T18 rooms).
    /// Assets, all from code: the three renderers next to the stock one on the URP assets (GB_Full with the
    /// dither, GB_Expensive with ambient occlusion for the plate, GB_Plate with the plate blit and the room
    /// depth), the materials (Simple Lit: the Blinn-Phong highlight of 1999), the 2K textures made by
    /// tools/look_textures.sh (RFab, the dogfood rule) and tools/look_textures_procedural.py, the scenes, the
    /// light probes, the lighting settings of the bake, the build list.
    /// Menu: GlorifiedBody > Build Look Tests. Batch: tools/unity.sh exec GlorifiedBody.Editor.LookTestSetup.BuildAll
    /// then tools/unity.sh exec GlorifiedBody.Editor.LookTestSetup.BakeAll (the bounce light, minutes).
    /// </summary>
    public static class LookTestSetup
    {
        public const string SceneName = "LookTest";
        public const string Scene2Name = "LookTest2";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string Scene2Path = MenuLoopSetup.ScenesFolder + "/" + Scene2Name + ".unity";
        public static readonly string[] ScenePaths = { ScenePath, Scene2Path };
        public const string RenderingFolder = "Assets/_Game/Rendering";
        public const string RenderersFolder = RenderingFolder + "/Renderers";
        public const string MaterialsFolder = RenderingFolder + "/Materials";
        public const string TexturesFolder = RenderingFolder + "/Textures";
        public const string LightingSettingsPath = RenderingFolder + "/GB_LookBake.lighting";
        public const string StockPipelinePath = "Assets/Settings/PC_RPAsset.asset";
        public const string MobilePipelinePath = "Assets/Settings/Mobile_RPAsset.asset";
        public const string StockRendererPath = "Assets/Settings/PC_Renderer.asset";
        public const string FullRendererPath = RenderersFolder + "/GB_Full.asset";
        public const string ExpensiveRendererPath = RenderersFolder + "/GB_Expensive.asset";
        public const string PlateRendererPath = RenderersFolder + "/GB_Plate.asset";
        public const int FullRendererIndex = 1;
        public const int ExpensiveRendererIndex = 2;
        public const int PlateRendererIndex = 3;
        public const string DitherShaderName = "Hidden/GB/Dither";
        public const string DepthOnlyShaderName = "Hidden/GB/DepthOnly";
        public const string SimpleLitShaderName = "Universal Render Pipeline/Simple Lit";
        public const float DitherLevels = 32f;
        /// <summary>Bruno (2026-10-22): "i prefer 1080p textures, we can scale it down if necessary. I want clarity as much as possible."</summary>
        public const int TextureSize = 2048;
        public const int TextureAniso = 8;
        public static readonly string[] TextureNames =
        {
            "concrete_wall", "concrete_floor", "concrete_ceiling", "steel_panel", "soil", "tile_wall", "tile_floor", "resin_floor",
            "vent_grille", "fern", "gear_emblem", "crt_face", "light_panel"
        };
        /// <summary>The whole picture once over a face (clamp wrap): the emblem, a grille, the fern card, a screen, a light.</summary>
        public static readonly string[] StretchedTextures = { "vent_grille", "fern", "gear_emblem", "crt_face", "light_panel" };
        /// <summary>The spots cast shadows at the high tier (1024 in the 4096 atlas), the point lights at the medium one (512 by six faces).</summary>
        public const int ShadowAtlas = 4096;

        // The Gallery: 16 m long (z), 8 m wide (x), 4.5 m high. The textures repeat every 2 m.
        public const float RoomLength = 16f, RoomWidth = 8f, RoomHeight = 4.5f, TileMetres = 2f;
        // LookTest2: the corridor along z, the recovery room east of it, the therapy gym north of it.
        public const float CorridorWidth = 4f, CorridorLength = 20f, CorridorHeight = 6f;
        public const float WallThickness = 0.3f;
        public static readonly Vector3 RecoveryMin = new Vector3(2.3f, 0f, 2.5f), RecoveryMax = new Vector3(7.3f, 3.2f, 8.5f);
        public static readonly Vector3 GymMin = new Vector3(-4f, 0f, 10.3f), GymMax = new Vector3(4f, 3.5f, 17.3f);

        private static int RoomLayerMask => 1 << LayerMask.NameToLayer("Default");
        private static int CharacterLayerMask => LayerMask.GetMask("Player", "Enemy");

        [MenuItem("GlorifiedBody/Build Look Test (the Gallery)")]
        public static void BuildLookTest() => Build(gallery: true, second: false);

        [MenuItem("GlorifiedBody/Build Look Test 2 (corridor, recovery, gym)")]
        public static void BuildLookTest2() => Build(gallery: false, second: true);

        [MenuItem("GlorifiedBody/Build Look Tests (both)")]
        public static void BuildAll() => Build(gallery: true, second: true);

        private static void Build(bool gallery, bool second)
        {
            foreach (string folder in new[] { MenuLoopSetup.ScenesFolder, RenderingFolder, RenderersFolder, MaterialsFolder, TexturesFolder })
            {
                Directory.CreateDirectory(folder);
            }
            AssetDatabase.Refresh();

            Material dither = EnsureMaterial(MaterialsFolder + "/GB_Dither.mat", DitherShaderName, m =>
            {
                m.SetFloat("_Levels", DitherLevels);
                m.SetFloat("_DitherStrength", 1f);
            });
            Material depthOnly = EnsureMaterial(MaterialsFolder + "/GB_DepthOnly.mat", DepthOnlyShaderName, null);
            if (dither == null || depthOnly == null) return;
            EnsureRenderers(dither, depthOnly);
            EnsureTextures();
            EnsureLightingSettings();
            if (gallery) BuildGallery();
            if (second) BuildScene2();
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[LookTestSetup] built: gallery={gallery} second={second}. Next: BakeAll for the bounce light.");
        }

        // ── The bake ──────────────────────────────────────────────────

        /// <summary>
        /// The bounce light of both scenes (minutes on the CPU lightmapper): the lightmaps of the static room
        /// (indirect only: the spots stay live and hard) and the probes for the characters. The real-time
        /// dressing hides them (no bounce, the ruling), the baked dressing and the plate show them.
        /// Batch: tools/unity.sh exec GlorifiedBody.Editor.LookTestSetup.BakeAll
        /// </summary>
        [MenuItem("GlorifiedBody/Bake Look Tests (the bounce light)")]
        public static void BakeAll()
        {
            foreach (string path in ScenePaths) Bake(path);
        }

        internal static void Bake(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[LookTestSetup] no scene at {scenePath}: build it first.");
                return;
            }
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            LightingSettings settings = EnsureLightingSettings();
            Lightmapping.SetLightingSettingsForScene(scene, settings);
            Lightmapping.Clear();
            double t0 = EditorApplication.timeSinceStartup;
            bool ok = Lightmapping.Bake();
            int count = LightmapSettings.lightmaps != null ? LightmapSettings.lightmaps.Length : 0;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            string line = $"[LookTestSetup] bake {scene.name}: {(ok ? "done" : "FAILED")}, {count} lightmaps, {EditorApplication.timeSinceStartup - t0:0} s.";
            if (ok && count > 0) Debug.Log(line); else Debug.LogError(line);
        }

        /// <summary>The bake numbers: CPU, 10 texels a metre, two bounces, the ambient occlusion, no compression, indirect only (the direct light stays live and hard).</summary>
        internal static LightingSettings EnsureLightingSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (settings == null)
            {
                settings = new LightingSettings();
                AssetDatabase.CreateAsset(settings, LightingSettingsPath);
            }
            settings.name = "GB_LookBake";
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
            settings.realtimeGI = false;
            settings.bakedGI = true;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            settings.lightmapResolution = 10f;
            settings.lightmapPadding = 2;
            settings.lightmapMaxSize = 2048;
            settings.lightmapCompression = LightmapCompression.None;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.directSampleCount = 32;
            settings.indirectSampleCount = 256;
            settings.environmentSampleCount = 64;
            settings.maxBounces = 2;
            settings.minBounces = 1;
            settings.ao = true;
            settings.aoMaxDistance = 1f;
            settings.aoExponentDirect = 0f;
            settings.aoExponentIndirect = 1f;
            settings.filteringMode = LightingSettings.FilterMode.Auto;
            settings.prioritizeView = false;
            settings.lightProbeSampleCountMultiplier = 4f;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // ── Renderers ─────────────────────────────────────────────────

        /// <summary>
        /// Three copies of the stock renderer, each with its features, at indices 1 to 3 of the URP assets. The
        /// assets also get the look's numbers: the 720p render scale, hard shadows only, per-pixel spot lights
        /// with shadows in a 4096 atlas (eleven spots did not fit the 2048 one: Unity halved them, Bruno's
        /// console of 2026-10-22).
        /// </summary>
        internal static void EnsureRenderers(Material dither, Material depthOnly)
        {
            var stock = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(StockRendererPath);
            if (stock == null)
            {
                Debug.LogError($"[LookTestSetup] no stock renderer at {StockRendererPath}.");
                return;
            }
            UniversalRendererData full = EnsureRendererCopy(stock, FullRendererPath);
            UniversalRendererData expensive = EnsureRendererCopy(stock, ExpensiveRendererPath);
            UniversalRendererData plate = EnsureRendererCopy(stock, PlateRendererPath);
            if (full == null || expensive == null || plate == null) return;
            int characters = CharacterLayerMask;
            int room = RoomLayerMask;

            // GB_Full: the room lit live, the characters marked in the stencil, the dither on the rest.
            ClearFeatures(full, keepAmbientOcclusion: false);
            AddFeature(full, CharacterStencil(characters));
            AddFeature(full, Dither(dither));
            full.opaqueLayerMask = -1;
            full.transparentLayerMask = -1;

            // GB_Expensive: the same plus the stock ambient occlusion, for the plate capture.
            ClearFeatures(expensive, keepAmbientOcclusion: true);
            AddFeature(expensive, CharacterStencil(characters));
            AddFeature(expensive, Dither(dither));
            expensive.opaqueLayerMask = -1;
            expensive.transparentLayerMask = -1;

            // GB_Plate: the plate first, the room as depth only, then the characters (the only opaques).
            ClearFeatures(plate, keepAmbientOcclusion: false);
            AddFeature(plate, PlateBlit());
            AddFeature(plate, RoomDepth(room, depthOnly));
            plate.opaqueLayerMask = characters;
            plate.transparentLayerMask = characters;

            foreach (UniversalRendererData data in new[] { full, expensive, plate })
            {
                data.depthPrimingMode = DepthPrimingMode.Disabled;
                data.SetDirty();
                EditorUtility.SetDirty(data);
            }

            foreach (string path in new[] { StockPipelinePath, MobilePipelinePath })
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (pipeline == null) continue;
                var so = new SerializedObject(pipeline);
                SerializedProperty list = so.FindProperty("m_RendererDataList");
                UnityEngine.Object first = list.arraySize > 0 ? list.GetArrayElementAtIndex(0).objectReferenceValue : null;
                list.arraySize = 4;
                list.GetArrayElementAtIndex(0).objectReferenceValue = first != null ? first : stock;
                list.GetArrayElementAtIndex(FullRendererIndex).objectReferenceValue = full;
                list.GetArrayElementAtIndex(ExpensiveRendererIndex).objectReferenceValue = expensive;
                list.GetArrayElementAtIndex(PlateRendererIndex).objectReferenceValue = plate;
                so.FindProperty("m_DefaultRendererIndex").intValue = 0;
                // The frame at 1080p (Bruno, 2026-10-22; 720p of 1080p before). MSAA off on disk: the Clear
                // look sets 4x at runtime (LookQualityApplier).
                so.FindProperty("m_RenderScale").floatValue = LookDirector.LookRenderScale;
                so.FindProperty("m_UpscalingFilter").intValue = (int)UpscalingFilterSelection.Linear;
                so.FindProperty("m_MSAA").intValue = 1;
                // ART_LESSONS 1.3: hard shadows, never soften them. The spots need per-pixel lights with shadows.
                so.FindProperty("m_SoftShadowsSupported").boolValue = false;
                so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
                so.FindProperty("m_MainLightShadowmapResolution").intValue = 2048;
                so.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
                so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
                so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = ShadowAtlas;
                so.FindProperty("m_AdditionalLightsShadowResolutionTierLow").intValue = 256;
                so.FindProperty("m_AdditionalLightsShadowResolutionTierMedium").intValue = 512;
                so.FindProperty("m_AdditionalLightsShadowResolutionTierHigh").intValue = 1024;
                so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 8;
                so.FindProperty("m_ShadowDistance").floatValue = 40f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
            }
            Debug.Log("[LookTestSetup] renderers: 0 stock, 1 GB_Full (stencil, dither), 2 GB_Expensive (plus ambient occlusion), 3 GB_Plate (plate blit, room depth).");
        }

        private static UniversalRendererData EnsureRendererCopy(UniversalRendererData stock, string path)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data != null) return data;
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(stock), path))
            {
                Debug.LogError($"[LookTestSetup] could not copy {StockRendererPath} to {path}.");
                return null;
            }
            AssetDatabase.ImportAsset(path);
            data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data != null) data.name = Path.GetFileNameWithoutExtension(path);
            return data;
        }

        /// <summary>Removes the features of a renderer copy. The stock ambient occlusion carries its own shader and noise textures, so it is kept where wanted, never made anew.</summary>
        private static void ClearFeatures(ScriptableRendererData data, bool keepAmbientOcclusion)
        {
            string path = AssetDatabase.GetAssetPath(data);
            var kept = new List<ScriptableRendererFeature>();
            foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            {
                if (!(sub is ScriptableRendererFeature feature)) continue;
                if (keepAmbientOcclusion && feature is ScreenSpaceAmbientOcclusion)
                {
                    var aoSo = new SerializedObject(feature);
                    SerializedProperty settings = aoSo.FindProperty("m_Settings");
                    settings.FindPropertyRelative("Intensity").floatValue = 1.2f;
                    settings.FindPropertyRelative("Radius").floatValue = 0.35f;
                    settings.FindPropertyRelative("Downsample").boolValue = false;
                    settings.FindPropertyRelative("AfterOpaque").boolValue = false;
                    aoSo.ApplyModifiedPropertiesWithoutUndo();
                    kept.Add(feature);
                    continue;
                }
                AssetDatabase.RemoveObjectFromAsset(sub);
                UnityEngine.Object.DestroyImmediate(sub, true);
            }
            var so = new SerializedObject(data);
            SerializedProperty list = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize = kept.Count;
            map.arraySize = kept.Count;
            for (int i = 0; i < kept.Count; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(kept[i], out _, out long localId);
                list.GetArrayElementAtIndex(i).objectReferenceValue = kept[i];
                map.GetArrayElementAtIndex(i).longValue = localId;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddFeature(ScriptableRendererData data, ScriptableRendererFeature feature)
        {
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            var so = new SerializedObject(data);
            SerializedProperty list = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            int n = list.arraySize;
            list.arraySize = n + 1;
            map.arraySize = n + 1;
            list.GetArrayElementAtIndex(n).objectReferenceValue = feature;
            map.GetArrayElementAtIndex(n).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The characters redrawn where they are visible (depth Equal, no write) to put 1 in the stencil: the dither skips them.</summary>
        private static RenderObjects CharacterStencil(int characterLayers)
        {
            var feature = ScriptableObject.CreateInstance<RenderObjects>();
            feature.name = "CharacterStencil";
            feature.settings.passTag = "GB CharacterStencil";
            feature.settings.Event = RenderPassEvent.AfterRenderingOpaques;
            feature.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
            feature.settings.filterSettings.LayerMask = characterLayers;
            feature.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
            feature.settings.overrideDepthState = true;
            feature.settings.depthCompareFunction = CompareFunction.Equal;
            feature.settings.enableWrite = false;
            feature.settings.stencilSettings.overrideStencilState = true;
            feature.settings.stencilSettings.stencilReference = 1;
            feature.settings.stencilSettings.stencilCompareFunction = CompareFunction.Always;
            feature.settings.stencilSettings.passOperation = StencilOp.Replace;
            feature.settings.stencilSettings.failOperation = StencilOp.Keep;
            feature.settings.stencilSettings.zFailOperation = StencilOp.Keep;
            return feature;
        }

        /// <summary>The 32-level dither on every pixel without the stencil mark, before post-processing.</summary>
        private static FullScreenPassRendererFeature Dither(Material dither)
        {
            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = "Dither";
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.None;
            feature.passMaterial = dither;
            feature.passIndex = 0;
            feature.bindDepthStencilAttachment = true;
            return feature;
        }

        private static PlateBlitFeature PlateBlit()
        {
            var feature = ScriptableObject.CreateInstance<PlateBlitFeature>();
            feature.name = "PlateBlit";
            return feature;
        }

        /// <summary>The room drawn as depth only, before the opaques, so the characters are hidden by it.</summary>
        private static RenderObjects RoomDepth(int roomLayers, Material depthOnly)
        {
            var feature = ScriptableObject.CreateInstance<RenderObjects>();
            feature.name = "RoomDepth";
            feature.settings.passTag = "GB RoomDepth";
            feature.settings.Event = RenderPassEvent.BeforeRenderingOpaques;
            feature.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
            feature.settings.filterSettings.LayerMask = roomLayers;
            feature.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.Material;
            feature.settings.overrideMaterial = depthOnly;
            feature.settings.overrideMaterialPassIndex = 0;
            return feature;
        }

        // ── Materials and textures ────────────────────────────────────

        private static Material EnsureMaterial(string path, string shaderName, Action<Material> setup)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[LookTestSetup] shader '{shaderName}' not found.");
                return null;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            setup?.Invoke(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A room surface: Simple Lit (Blinn-Phong, the hard highlight of 1999, CONCEPTS 7.3), one colour map, a
        /// tint, a smoothness and one specular colour. No normal map, no roughness map: the relief is painted in
        /// the picture (the seams, the chips, the stains). An emission makes a light or a screen (and feeds the
        /// bake). A cutout makes a leaf card, drawn on both sides.
        /// </summary>
        private static Material SurfaceMaterial(string name, string textureName, Color tint, float smoothness, Color specular, Color? emission = null, bool cutout = false)
        {
            Texture2D texture = textureName != null ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{textureName}.png") : null;
            if (textureName != null && texture == null) Debug.LogWarning($"[LookTestSetup] no texture {textureName}.png (tools/look_textures.sh): {name} gets its tint only.");
            return EnsureMaterial($"{MaterialsFolder}/{name}.mat", SimpleLitShaderName, m =>
            {
                m.SetTexture("_BaseMap", texture);
                m.SetColor("_BaseColor", tint);
                m.SetFloat("_Smoothness", smoothness);
                m.SetColor("_SpecColor", specular);
                m.SetFloat("_SpecularHighlights", 1f);
                m.EnableKeyword("_SPECULAR_COLOR");
                m.SetFloat("_SmoothnessSource", 0f);
                m.SetFloat("_ReceiveShadows", 1f);
                if (emission.HasValue)
                {
                    m.SetTexture("_EmissionMap", texture);
                    m.SetColor("_EmissionColor", emission.Value);
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                else
                {
                    m.SetColor("_EmissionColor", Color.black);
                    m.DisableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
                if (cutout)
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.5f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.SetFloat("_Cull", (float)CullMode.Off);
                    m.SetOverrideTag("RenderType", "TransparentCutout");
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                    m.doubleSidedGI = true;
                }
                else
                {
                    m.SetFloat("_AlphaClip", 0f);
                    m.DisableKeyword("_ALPHATEST_ON");
                    m.SetFloat("_Cull", (float)CullMode.Back);
                    m.SetOverrideTag("RenderType", "Opaque");
                    m.renderQueue = -1;
                    m.doubleSidedGI = false;
                }
            });
        }

        /// <summary>
        /// The pictures from tools/look_textures.sh and tools/look_textures_procedural.py, imported as 2K colour
        /// maps (Bruno: clarity), trilinear with anisotropy for the floors seen at a low angle, no compression.
        /// A surface repeats by mirror (no seam from a picture that is not tileable); a stretched picture clamps.
        /// </summary>
        internal static void EnsureTextures()
        {
            foreach (string name in TextureNames)
            {
                string path = $"{TexturesFolder}/{name}.png";
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[LookTestSetup] {path} missing: run tools/look_textures.sh (then import) or tools/look_textures_procedural.py.");
                    continue;
                }
                AssetDatabase.ImportAsset(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool stretched = Array.IndexOf(StretchedTextures, name) >= 0;
                importer.textureType = TextureImporterType.Default;
                importer.maxTextureSize = TextureSize;
                importer.filterMode = FilterMode.Trilinear;
                importer.mipmapEnabled = true;
                importer.anisoLevel = TextureAniso;
                importer.sRGBTexture = true;
                importer.alphaSource = name == "fern" ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = name == "fern";
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = stretched ? TextureWrapMode.Clamp : TextureWrapMode.Mirror;
                importer.SaveAndReimport();
            }
        }

        /// <summary>The material set of both scenes. Made once per build, shared by every room.</summary>
        private sealed class Palette
        {
            public Material Greybox, Wall, Ceiling, Floor, Planter, Steel, DarkSteel, Soil, Shrub, Fern, Emblem, Grille;
            public Material TileWall, TileFloor, ResinFloor, Skirting, Mirror, Glass, Lane, Canvas, Vest, Leather, Mattress, Sky, LightPanel, Crt;
        }

        private static Palette EnsurePalette()
        {
            var p = new Palette
            {
                Greybox = SurfaceMaterial("GB_Greybox", null, new Color(0.55f, 0.55f, 0.56f), 0.1f, new Color(0.2f, 0.2f, 0.2f)),
                Wall = SurfaceMaterial("GB_ConcreteWall", "concrete_wall", new Color(0.78f, 0.78f, 0.76f), 0.25f, new Color(0.25f, 0.25f, 0.25f)),
                Ceiling = SurfaceMaterial("GB_ConcreteCeiling", "concrete_ceiling", new Color(0.6f, 0.6f, 0.6f), 0.15f, new Color(0.15f, 0.15f, 0.15f)),
                Floor = SurfaceMaterial("GB_ConcreteFloor", "concrete_floor", new Color(0.75f, 0.75f, 0.74f), 0.55f, new Color(0.45f, 0.45f, 0.45f)),
                Planter = SurfaceMaterial("GB_ConcretePlanter", "concrete_wall", new Color(0.85f, 0.84f, 0.8f), 0.3f, new Color(0.3f, 0.3f, 0.3f)),
                Steel = SurfaceMaterial("GB_Steel", "steel_panel", new Color(0.7f, 0.72f, 0.75f), 0.8f, new Color(0.8f, 0.8f, 0.8f)),
                DarkSteel = SurfaceMaterial("GB_DarkSteel", "steel_panel", new Color(0.3f, 0.31f, 0.33f), 0.7f, new Color(0.6f, 0.6f, 0.6f)),
                Soil = SurfaceMaterial("GB_Soil", "soil", new Color(0.6f, 0.55f, 0.5f), 0.1f, new Color(0.1f, 0.1f, 0.1f)),
                Shrub = SurfaceMaterial("GB_Plant", null, new Color(0.1f, 0.24f, 0.09f), 0.35f, new Color(0.25f, 0.3f, 0.2f)),
                Fern = SurfaceMaterial("GB_Fern", "fern", new Color(0.85f, 0.95f, 0.8f), 0.3f, new Color(0.2f, 0.25f, 0.15f), cutout: true),
                Emblem = SurfaceMaterial("GB_GearEmblem", "gear_emblem", Color.white, 0.2f, new Color(0.2f, 0.2f, 0.2f)),
                Grille = SurfaceMaterial("GB_VentGrille", "vent_grille", new Color(0.8f, 0.8f, 0.8f), 0.5f, new Color(0.5f, 0.5f, 0.5f)),
                TileWall = SurfaceMaterial("GB_TileWall", "tile_wall", new Color(0.92f, 0.92f, 0.9f), 0.85f, new Color(0.9f, 0.9f, 0.9f)),
                TileFloor = SurfaceMaterial("GB_TileFloor", "tile_floor", new Color(0.85f, 0.85f, 0.83f), 0.6f, new Color(0.5f, 0.5f, 0.5f)),
                ResinFloor = SurfaceMaterial("GB_ResinFloor", "resin_floor", new Color(0.8f, 0.82f, 0.78f), 0.7f, new Color(0.6f, 0.6f, 0.6f)),
                Skirting = SurfaceMaterial("GB_Skirting", null, new Color(0.12f, 0.12f, 0.13f), 0.5f, new Color(0.3f, 0.3f, 0.3f)),
                Mirror = SurfaceMaterial("GB_Mirror", null, new Color(0.1f, 0.11f, 0.12f), 0.98f, new Color(1f, 1f, 1f)),
                Glass = SurfaceMaterial("GB_Glass", null, new Color(0.08f, 0.09f, 0.1f), 0.95f, new Color(0.9f, 0.9f, 0.9f)),
                Lane = SurfaceMaterial("GB_Lane", null, new Color(0.85f, 0.75f, 0.25f), 0.4f, new Color(0.3f, 0.3f, 0.3f)),
                Canvas = SurfaceMaterial("GB_Canvas", null, new Color(0.62f, 0.55f, 0.42f), 0.1f, new Color(0.1f, 0.1f, 0.1f)),
                Vest = SurfaceMaterial("GB_Vest", null, new Color(0.3f, 0.42f, 0.36f), 0.15f, new Color(0.1f, 0.1f, 0.1f)),
                Leather = SurfaceMaterial("GB_Leather", null, new Color(0.25f, 0.14f, 0.08f), 0.5f, new Color(0.3f, 0.3f, 0.3f)),
                Mattress = SurfaceMaterial("GB_Mattress", null, new Color(0.9f, 0.9f, 0.88f), 0.2f, new Color(0.2f, 0.2f, 0.2f)),
                Sky = SurfaceMaterial("GB_Skylight", "light_panel", Color.white, 0.1f, Color.black, emission: new Color(0.8f, 0.9f, 1f) * 5f),
                LightPanel = SurfaceMaterial("GB_LightPanel", "light_panel", Color.white, 0.1f, Color.black, emission: new Color(1f, 1f, 1.04f) * 3f),
                Crt = SurfaceMaterial("GB_CrtFace", "crt_face", Color.white, 0.9f, new Color(0.8f, 0.8f, 0.8f), emission: new Color(0.7f, 1f, 0.7f) * 1.5f),
            };
            return p;
        }

        // ── The scene builders ────────────────────────────────────────

        private static readonly List<Renderer> roomRenderers = new List<Renderer>();
        private static readonly List<Material> dressedMaterials = new List<Material>();
        private static readonly List<Vector3> probePositions = new List<Vector3>();

        private static Scene BeginScene(string path)
        {
            Scene scene = MenuLoopSetup.OpenOrCreate(path);
            roomRenderers.Clear();
            dressedMaterials.Clear();
            probePositions.Clear();
            MenuLoopSetup.RemoveObject("Directional Light"); // indoors: the spots only
            // ART_LESSONS 1.4: hard pools of light, near-black corners, no bounce.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.025f, 0.025f, 0.03f);
            RenderSettings.fog = false;
            return scene;
        }

        /// <summary>The cast, the cameras, the systems, the UI, the probes and the LookDirector of a look scene.</summary>
        private static void FinishScene(Scene scene, Palette palette, FixedCamera[] cameras, string summary)
        {
            MenuLoopSetup.EnsureShotPoints(cameras);
            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();

            GameObject probesRoot = MenuLoopSetup.EnsureRoot("[Probes]");
            LightProbeGroup probes = MenuLoopSetup.EnsureComponent<LightProbeGroup>(probesRoot);
            probes.probePositions = probePositions.ToArray();
            probes.dering = true;

            GameObject lookRoot = MenuLoopSetup.EnsureRoot("[Look]");
            LookDirector look = MenuLoopSetup.EnsureComponent<LookDirector>(lookRoot);
            var greyboxes = new Material[roomRenderers.Count];
            for (int i = 0; i < greyboxes.Length; i++) greyboxes[i] = palette.Greybox;
            look.Configure(FullRendererIndex, ExpensiveRendererIndex, PlateRendererIndex, RoomLayerMask, roomRenderers.ToArray(), greyboxes, dressedMaterials.ToArray(), LookDressing.RealTimeBaked);
            EditorUtility.SetDirty(look);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[LookTestSetup] {scene.path}: {summary}, {roomRenderers.Count} room surfaces, {cameras.Length} cameras, {probePositions.Count} probes.");
        }

        private static GameObject PlaceLucy(CharacterCatalog catalog, Vector3 position, float yaw)
        {
            GameObject lucy = MenuLoopSetup.EnsureLucy(catalog, position, yaw);
            // No red lock marker over the figures: the picture is the point of these rooms.
            var lockSo = new SerializedObject(lucy.GetComponent<LockOnController>());
            lockSo.FindProperty("showMarker").boolValue = false;
            lockSo.ApplyModifiedPropertiesWithoutUndo();
            return lucy;
        }

        private static GameObject PlaceFigure(CharacterCatalog catalog, string name, string classId, Vector3 position)
        {
            GameObject figure = MenuLoopSetup.EnsureDummy(name, position, catalog, classId);
            return figure;
        }

        private static void ProbeGrid(Vector3 min, Vector3 max, float step, params float[] heights)
        {
            for (float x = min.x + step * 0.5f; x < max.x; x += step)
            {
                for (float z = min.z + step * 0.5f; z < max.z; z += step)
                {
                    foreach (float y in heights) probePositions.Add(new Vector3(x, y, z));
                }
            }
        }

        // ── Scene 1: the Gallery ──────────────────────────────────────

        internal static void BuildGallery()
        {
            Scene scene = BeginScene(ScenePath);
            Camera camera = MenuLoopSetup.EnsureCamera(new Vector3(-3.2f, 1.6f, -7.2f), new Vector3(0f, 1.2f, 2f), 55f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            Palette p = EnsurePalette();

            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            float hl = RoomLength * 0.5f, hw = RoomWidth * 0.5f, hh = RoomHeight * 0.5f;
            Face(room, "Floor", new Vector3(0f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f), RoomWidth, RoomLength, p.Floor, 0.3f);
            Face(room, "Ceiling", new Vector3(0f, RoomHeight, 0f), Quaternion.Euler(-90f, 0f, 0f), RoomWidth, RoomLength, p.Ceiling, 0.3f);
            Face(room, "Wall_North", new Vector3(0f, hh, hl), Quaternion.identity, RoomWidth, RoomHeight, p.Wall, 0.5f);
            Face(room, "Wall_South", new Vector3(0f, hh, -hl), Quaternion.Euler(0f, 180f, 0f), RoomWidth, RoomHeight, p.Wall, 0.5f);
            Face(room, "Wall_East", new Vector3(hw, hh, 0f), Quaternion.Euler(0f, 90f, 0f), RoomLength, RoomHeight, p.Wall, 0.5f);
            Face(room, "Wall_West", new Vector3(-hw, hh, 0f), Quaternion.Euler(0f, -90f, 0f), RoomLength, RoomHeight, p.Wall, 0.5f);
            // The greebles of 1999 (Bruno, 2026-10-22: the walls carried the detail; on concrete keep it subtle):
            // a dark skirting, a steel band at the top, two vent grilles, a junction box with its conduit drop.
            Skirting(room, new Vector3(0f, 0f, 0f), RoomWidth, RoomLength, 0.2f, p.Skirting);
            Band(room, new Vector3(0f, 0f, 0f), RoomWidth, RoomLength, RoomHeight - 0.4f, 0.25f, p.DarkSteel);
            Face(room, "Grille_East", new Vector3(hw - 0.02f, 1.3f, -6.5f), Quaternion.Euler(0f, 90f, 0f), 1.2f, 0.8f, p.Grille, 0f, stretch: true);
            Face(room, "Grille_West", new Vector3(-hw + 0.02f, 1.3f, 4.5f), Quaternion.Euler(0f, -90f, 0f), 1.2f, 0.8f, p.Grille, 0f, stretch: true);
            Block(room, "JunctionBox", PrimitiveType.Cube, new Vector3(hw - 0.08f, 2.2f, -0.5f), new Vector3(0.14f, 0.4f, 0.3f), p.DarkSteel);
            Block(room, "ConduitDrop", PrimitiveType.Cylinder, new Vector3(hw - 0.08f, 3.3f, -0.5f), new Vector3(0.06f, 0.9f, 0.06f), p.Steel);

            // Planters (ART_LESSONS 1.5: nature is tamed, planters only), along the walls, alternating sides.
            float[] planterZ = { -3f, 1.5f, 5f };
            for (int i = 0; i < planterZ.Length; i++)
            {
                float x = i % 2 == 0 ? 2.7f : -2.7f;
                Block(room, "Planter_" + i, PrimitiveType.Cube, new Vector3(x, 0.35f, planterZ[i]), new Vector3(2.4f, 0.7f, 0.9f), p.Planter);
                Block(room, "Soil_" + i, PrimitiveType.Cube, new Vector3(x, 0.72f, planterZ[i]), new Vector3(2.2f, 0.08f, 0.7f), p.Soil);
                Block(room, "Shrub_" + i + "a", PrimitiveType.Sphere, new Vector3(x - 0.7f, 1.05f, planterZ[i]), new Vector3(0.7f, 0.6f, 0.6f), p.Shrub);
                Block(room, "Shrub_" + i + "b", PrimitiveType.Sphere, new Vector3(x, 1.15f, planterZ[i] + 0.1f), new Vector3(0.8f, 0.8f, 0.7f), p.Shrub);
                Block(room, "Shrub_" + i + "c", PrimitiveType.Sphere, new Vector3(x + 0.7f, 1.0f, planterZ[i] - 0.1f), new Vector3(0.6f, 0.55f, 0.6f), p.Shrub);
            }
            Block(room, "Column_A", PrimitiveType.Cube, new Vector3(3.4f, hh, -2f), new Vector3(0.7f, RoomHeight, 0.7f), p.Wall);
            Block(room, "Column_B", PrimitiveType.Cube, new Vector3(-3.4f, hh, 3.2f), new Vector3(0.7f, RoomHeight, 0.7f), p.Wall);
            Block(room, "Conduit_A", PrimitiveType.Cylinder, new Vector3(-1.2f, 4.3f, 0f), new Vector3(0.24f, hl, 0.24f), p.Steel, Quaternion.Euler(90f, 0f, 0f));
            Block(room, "Conduit_B", PrimitiveType.Cylinder, new Vector3(1.4f, 4.35f, 0f), new Vector3(0.18f, hl, 0.18f), p.Steel, Quaternion.Euler(90f, 0f, 0f));
            Block(room, "Door", PrimitiveType.Cube, new Vector3(0f, 1.5f, hl - 0.07f), new Vector3(2.2f, 3f, 0.12f), p.Steel);
            Block(room, "DoorFrame", PrimitiveType.Cube, new Vector3(0f, 3.1f, hl - 0.1f), new Vector3(2.6f, 0.25f, 0.2f), p.Steel);
            // The gear on the panels (CONCEPTS 5: "the church shows itself, nobody says it").
            Face(room, "Panel_Door", new Vector3(0f, 3.75f, hl - 0.03f), Quaternion.identity, 1.2f, 1.2f, p.Emblem, 0f, stretch: true);
            Face(room, "Panel_East", new Vector3(hw - 0.03f, 2.6f, 2.5f), Quaternion.Euler(0f, 90f, 0f), 1.6f, 1.6f, p.Emblem, 0f, stretch: true);
            Face(room, "Panel_West", new Vector3(-hw + 0.03f, 2.6f, -4f), Quaternion.Euler(0f, -90f, 0f), 1.6f, 1.6f, p.Emblem, 0f, stretch: true);

            // The lights: hard pools from the ceiling, wall washers high on the long walls (the cones slide down
            // the concrete, so the texture shows), one on each gear panel, one cold light at the door.
            GameObject lights = MenuLoopSetup.EnsureRoot("[Lights]");
            Color warm = new Color(1f, 0.93f, 0.8f);
            Spot(lights, "Pool_A", new Vector3(-1.4f, RoomHeight - 0.1f, -5.5f), Quaternion.Euler(90f, 0f, 0f), warm, 55f, 75f);
            Spot(lights, "Pool_B", new Vector3(1.6f, RoomHeight - 0.1f, -2f), Quaternion.Euler(90f, 0f, 0f), warm, 55f, 75f);
            Spot(lights, "Pool_C", new Vector3(-1.4f, RoomHeight - 0.1f, 1.5f), Quaternion.Euler(90f, 0f, 0f), warm, 55f, 75f);
            Spot(lights, "Pool_D", new Vector3(1.6f, RoomHeight - 0.1f, 5f), Quaternion.Euler(90f, 0f, 0f), warm, 50f, 75f);
            Spot(lights, "Wash_East_A", new Vector3(hw - 0.35f, RoomHeight - 0.3f, -4f), Quaternion.Euler(70f, 90f, 0f), warm, 35f, 90f);
            Spot(lights, "Wash_East_B", new Vector3(hw - 0.35f, RoomHeight - 0.3f, 3.5f), Quaternion.Euler(70f, 90f, 0f), warm, 35f, 90f);
            Spot(lights, "Wash_West_A", new Vector3(-hw + 0.35f, RoomHeight - 0.3f, -1f), Quaternion.Euler(70f, -90f, 0f), warm, 35f, 90f);
            Spot(lights, "Wash_West_B", new Vector3(-hw + 0.35f, RoomHeight - 0.3f, 6f), Quaternion.Euler(70f, -90f, 0f), warm, 35f, 90f);
            Spot(lights, "Panel_East_Light", new Vector3(hw - 1.2f, RoomHeight - 0.2f, 2.5f), Quaternion.Euler(50f, 90f, 0f), new Color(1f, 0.97f, 0.9f), 25f, 50f);
            Spot(lights, "Panel_West_Light", new Vector3(-hw + 1.2f, RoomHeight - 0.2f, -4f), Quaternion.Euler(50f, -90f, 0f), new Color(1f, 0.97f, 0.9f), 25f, 50f);
            Spot(lights, "Door_Cold", new Vector3(0f, RoomHeight - 0.1f, 7.2f), Quaternion.Euler(80f, 0f, 0f), new Color(0.75f, 0.85f, 1f), 40f, 80f);

            // Lucy at the near end, two figures down the gallery. Layers mark the characters for the stencil.
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog();
            PlaceLucy(catalog, new Vector3(-2f, 0f, -5.5f), 0f);
            PlaceFigure(catalog, "Figure_Soldier", "soldier", new Vector3(1.5f, 0f, 2f));
            PlaceFigure(catalog, "Figure_Guard", "guard", new Vector3(-1f, 0f, 5.5f));

            // Cameras: inside the room, at a level angle (ART_LESSONS 1.6). The wide one by default. The corner
            // camera covers the south-east section behind the wide one (Bruno, 2026-10-22: "needs a new camera
            // angle to cover this section").
            GameObject camerasRoot = MenuLoopSetup.EnsureRoot("[Cameras]");
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            // The numbers come from the coverage check (scratchpad coverage.py, the same frustum math as the
            // PlayMode test): the door camera on the WEST wall looks north-east at the door, so the north strip
            // is in its frame (a camera on the north wall cannot see along the north wall).
            FixedCamera wide = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Wide", new Vector3(-3.2f, 1.6f, -7.2f), new Vector3(0f, 1.2f, 2f), 55f);
            FixedCamera corner = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Corner", new Vector3(3.7f, 1.7f, -4f), new Vector3(-1.5f, 0.9f, -7.2f), 58f);
            FixedCamera mid = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Mid", new Vector3(3.6f, 1.7f, 0.5f), new Vector3(-1.5f, 1f, 2.5f), 55f);
            FixedCamera door = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Door", new Vector3(-3.5f, 1.6f, 4.8f), new Vector3(2f, 1f, 7f), 60f);
            director.SetDefaultCamera(wide);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Corner", new Vector3(1.6f, 1.5f, -5.75f), new Vector3(4.8f, 3f, 4.5f), corner, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Mid", new Vector3(0f, 1f, 1f), new Vector3(RoomWidth, 3f, 4f), mid, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Door", new Vector3(0f, 1f, 5.5f), new Vector3(RoomWidth, 3f, 5f), door, 1);
            ProbeGrid(new Vector3(-hw, 0f, -hl), new Vector3(hw, 0f, hl), 2f, 0.6f, 2.2f);

            FinishScene(scene, p, new[] { wide, corner, mid, door }, "the Gallery, 11 spots");
        }

        // ── Scene 2: the corridor, the recovery room, the therapy gym ─

        internal static void BuildScene2()
        {
            Scene scene = BeginScene(Scene2Path);
            Camera camera = MenuLoopSetup.EnsureCamera(new Vector3(0.9f, 1.6f, -9.3f), new Vector3(-0.2f, 1.3f, 2f), 55f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            Palette p = EnsurePalette();
            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            GameObject lights = MenuLoopSetup.EnsureRoot("[Lights]");
            Color cold = new Color(0.82f, 0.9f, 1f);
            Color warm = new Color(1f, 0.93f, 0.8f);
            float cw = CorridorWidth * 0.5f, cl = CorridorLength * 0.5f, ch = CorridorHeight;

            // ── The corridor (T12 GALLERY_v3: concrete, skylight slots, planters on the west, vents and tubes on the east).
            Face(room, "Floor", new Vector3(0f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f), CorridorWidth, CorridorLength, p.Floor, 0.3f);
            Face(room, "Wall_West", new Vector3(-cw, ch * 0.5f, 0f), Quaternion.Euler(0f, -90f, 0f), CorridorLength, ch, p.Wall, WallThickness);
            WallWithOpening(room, "Wall_East", new Vector3(cw, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), CorridorLength, ch, new Vector3(cw, 0f, 5f), 1.2f, 2.2f, p.Wall, WallThickness);
            Face(room, "Wall_South", new Vector3(0f, ch * 0.5f, -cl), Quaternion.Euler(0f, 180f, 0f), CorridorWidth, ch, p.Wall, WallThickness);
            WallWithOpening(room, "Wall_North", new Vector3(0f, 0f, cl), Quaternion.identity, CorridorWidth, ch, new Vector3(0f, 0f, cl), 2.2f, 3f, p.Wall, WallThickness);
            Skirting(room, Vector3.zero, CorridorWidth, CorridorLength, 0.2f, p.Skirting);
            // The ceiling with three skylight slots in a column east of the centre: strips around the slots, a
            // shaft of four walls each, a sky panel at the top, a narrow cold spot straight down through it.
            float sx0 = -0.2f, sx1 = 0.8f;
            float[] slotZ = { -5.5f, -0.5f, 4.5f };
            const float slotLength = 3f, shaftDepth = 0.8f;
            Face(room, "Ceiling_West", new Vector3((-cw + sx0) * 0.5f, ch, 0f), Quaternion.Euler(-90f, 0f, 0f), sx0 + cw, CorridorLength, p.Ceiling, 0.3f);
            Face(room, "Ceiling_East", new Vector3((sx1 + cw) * 0.5f, ch, 0f), Quaternion.Euler(-90f, 0f, 0f), cw - sx1, CorridorLength, p.Ceiling, 0.3f);
            float columnX = (sx0 + sx1) * 0.5f, columnW = sx1 - sx0;
            float[] gapsZ0 = { -cl, slotZ[0] + slotLength * 0.5f, slotZ[1] + slotLength * 0.5f, slotZ[2] + slotLength * 0.5f };
            float[] gapsZ1 = { slotZ[0] - slotLength * 0.5f, slotZ[1] - slotLength * 0.5f, slotZ[2] - slotLength * 0.5f, cl };
            for (int i = 0; i < gapsZ0.Length; i++)
            {
                Face(room, "Ceiling_Mid_" + i, new Vector3(columnX, ch, (gapsZ0[i] + gapsZ1[i]) * 0.5f), Quaternion.Euler(-90f, 0f, 0f), columnW, gapsZ1[i] - gapsZ0[i], p.Ceiling, 0.3f);
            }
            for (int i = 0; i < slotZ.Length; i++)
            {
                float zc = slotZ[i], ym = ch + shaftDepth * 0.5f;
                Face(room, $"Shaft_{i}_West", new Vector3(sx0, ym, zc), Quaternion.Euler(0f, -90f, 0f), slotLength, shaftDepth, p.Wall, 0f);
                Face(room, $"Shaft_{i}_East", new Vector3(sx1, ym, zc), Quaternion.Euler(0f, 90f, 0f), slotLength, shaftDepth, p.Wall, 0f);
                Face(room, $"Shaft_{i}_South", new Vector3(columnX, ym, zc - slotLength * 0.5f), Quaternion.Euler(0f, 180f, 0f), columnW, shaftDepth, p.Wall, 0f);
                Face(room, $"Shaft_{i}_North", new Vector3(columnX, ym, zc + slotLength * 0.5f), Quaternion.identity, columnW, shaftDepth, p.Wall, 0f);
                Face(room, $"Sky_{i}", new Vector3(columnX, ch + shaftDepth, zc), Quaternion.Euler(-90f, 0f, 0f), columnW, slotLength, p.Sky, 0f, stretch: true);
                Spot(lights, $"Skylight_{i}", new Vector3(columnX, ch + shaftDepth - 0.1f, zc), Quaternion.Euler(90f, 0f, 0f), cold, 90f, 26f, 14f); // straight down (Bruno, 2026-10-22)
            }
            // The greebles of the corridor: a dark steel beam along the top of each wall, two conduits, the vent
            // grilles and the tube lights on the east wall, a steel frame around the recovery door.
            Block(room, "Beam_West", PrimitiveType.Cube, new Vector3(-cw + 0.15f, ch - 0.25f, 0f), new Vector3(0.3f, 0.3f, CorridorLength), p.DarkSteel);
            Block(room, "Beam_East", PrimitiveType.Cube, new Vector3(cw - 0.15f, ch - 0.25f, 0f), new Vector3(0.3f, 0.3f, CorridorLength), p.DarkSteel);
            Block(room, "Conduit_A", PrimitiveType.Cylinder, new Vector3(cw - 0.5f, ch - 0.6f, 0f), new Vector3(0.16f, cl, 0.16f), p.Steel, Quaternion.Euler(90f, 0f, 0f));
            Block(room, "Conduit_B", PrimitiveType.Cylinder, new Vector3(cw - 0.75f, ch - 0.7f, 0f), new Vector3(0.1f, cl, 0.1f), p.Steel, Quaternion.Euler(90f, 0f, 0f));
            foreach (float z in new[] { -7f, -1f, 8f })
            {
                Face(room, $"Grille_{z:0}", new Vector3(cw - 0.02f, 1.1f, z), Quaternion.Euler(0f, 90f, 0f), 1.2f, 0.8f, p.Grille, 0f, stretch: true);
            }
            foreach (float z in new[] { -4f, 2f, 8f })
            {
                Block(room, $"Tube_{z:0}", PrimitiveType.Cube, new Vector3(cw - 0.1f, 4.6f, z), new Vector3(0.12f, 0.08f, 1.4f), p.LightPanel);
                Spot(lights, $"TubeLight_{z:0}", new Vector3(cw - 0.3f, 4.5f, z), Quaternion.Euler(70f, 90f, 0f), cold, 30f, 90f);
            }
            Block(room, "RecoveryDoorFrame_Top", PrimitiveType.Cube, new Vector3(cw - 0.05f, 2.27f, 5f), new Vector3(0.1f, 0.14f, 1.5f), p.Steel);
            Block(room, "RecoveryDoorFrame_S", PrimitiveType.Cube, new Vector3(cw - 0.05f, 1.1f, 4.33f), new Vector3(0.1f, 2.2f, 0.14f), p.Steel);
            Block(room, "RecoveryDoorFrame_N", PrimitiveType.Cube, new Vector3(cw - 0.05f, 1.1f, 5.67f), new Vector3(0.1f, 2.2f, 0.14f), p.Steel);
            Face(room, "Panel_RecoveryDoor", new Vector3(cw - 0.03f, 3.1f, 5f), Quaternion.Euler(0f, 90f, 0f), 1f, 1f, p.Emblem, 0f, stretch: true);
            Spot(lights, "RecoveryDoor_Pool", new Vector3(1.2f, ch - 0.1f, 5f), Quaternion.Euler(90f, 0f, 0f), warm, 70f, 60f);
            Spot(lights, "GymDoor_Pool", new Vector3(0f, ch - 0.1f, 9.2f), Quaternion.Euler(90f, 0f, 0f), cold, 70f, 60f);
            // Planters with ferns along the west wall (ART_LESSONS 1.5: tamed, no ivy on the walls).
            float[] planterZ = { -6.5f, -1.5f, 3.5f };
            for (int i = 0; i < planterZ.Length; i++)
            {
                Vector3 c = new Vector3(-cw + 0.45f, 0f, planterZ[i]);
                Block(room, "Planter_" + i, PrimitiveType.Cube, c + new Vector3(0f, 0.35f, 0f), new Vector3(0.7f, 0.7f, 2f), p.Planter);
                Block(room, "Soil_" + i, PrimitiveType.Cube, c + new Vector3(0f, 0.72f, 0f), new Vector3(0.55f, 0.08f, 1.85f), p.Soil);
                FernCard(room, "Fern_" + i + "a", c + new Vector3(0f, 0.72f, -0.5f), 1.3f, 1.1f, 20f, p.Fern);
                FernCard(room, "Fern_" + i + "b", c + new Vector3(0f, 0.72f, 0.5f), 1.2f, 1.0f, -35f, p.Fern);
            }
            ProbeGrid(new Vector3(-cw, 0f, -cl), new Vector3(cw, 0f, cl), 2f, 0.6f, 2.2f, 4.5f);

            // ── The recovery room (T18 room_recovery_room_1: tile, one flat panel light, the bed with the straps).
            Vector3 rMin = RecoveryMin, rMax = RecoveryMax;
            float rh = rMax.y, rw = rMax.x - rMin.x, rd = rMax.z - rMin.z;
            Vector3 rc = new Vector3((rMin.x + rMax.x) * 0.5f, 0f, (rMin.z + rMax.z) * 0.5f);
            Face(room, "Recovery_Floor", rc, Quaternion.Euler(90f, 0f, 0f), rw, rd, p.TileFloor, 0.3f);
            Face(room, "Recovery_Ceiling", rc + new Vector3(0f, rh, 0f), Quaternion.Euler(-90f, 0f, 0f), rw, rd, p.Ceiling, 0.3f);
            WallWithOpening(room, "Recovery_Wall_West", new Vector3(rMin.x, 0f, rc.z), Quaternion.Euler(0f, -90f, 0f), rd, rh, new Vector3(rMin.x, 0f, 5f), 1.2f, 2.2f, p.TileWall, WallThickness);
            Face(room, "Recovery_Wall_East", new Vector3(rMax.x, rh * 0.5f, rc.z), Quaternion.Euler(0f, 90f, 0f), rd, rh, p.TileWall, WallThickness);
            Face(room, "Recovery_Wall_North", new Vector3(rc.x, rh * 0.5f, rMax.z), Quaternion.identity, rw, rh, p.TileWall, WallThickness);
            Face(room, "Recovery_Wall_South", new Vector3(rc.x, rh * 0.5f, rMin.z), Quaternion.Euler(0f, 180f, 0f), rw, rh, p.TileWall, WallThickness);
            Skirting(room, rc, rw, rd, 0.15f, p.Skirting, "Recovery_");
            DoorTunnel(room, "RecoveryDoor", new Vector3(cw, 0f, 5f), Quaternion.Euler(0f, 90f, 0f), rMin.x - cw, 1.2f, 2.2f, p.TileWall, p.Steel);
            Face(room, "Recovery_LightPanel", new Vector3(rc.x, rh - 0.01f, rc.z), Quaternion.Euler(-90f, 0f, 0f), 1.2f, 0.6f, p.LightPanel, 0f, stretch: true);
            Point(lights, "Recovery_Light", new Vector3(rc.x, rh - 0.18f, rc.z), new Color(0.92f, 0.96f, 1f), 14f, 9f);
            Block(room, "Recovery_Conduit", PrimitiveType.Cylinder, new Vector3(rMin.x + 1.2f, rh - 0.08f, rc.z), new Vector3(0.1f, rd * 0.5f, 0.1f), p.Steel, Quaternion.Euler(90f, 0f, 0f));
            Block(room, "Recovery_Speaker", PrimitiveType.Cylinder, new Vector3(rMax.x - 0.8f, rh - 0.015f, rMin.z + 1.5f), new Vector3(0.3f, 0.015f, 0.3f), p.DarkSteel);
            Block(room, "Recovery_Camera", PrimitiveType.Cube, new Vector3(rMax.x - 0.2f, rh - 0.3f, rMin.z + 0.2f), new Vector3(0.12f, 0.12f, 0.28f), p.DarkSteel, Quaternion.Euler(20f, -135f, 0f));
            Block(room, "Recovery_CameraArm", PrimitiveType.Cube, new Vector3(rMax.x - 0.08f, rh - 0.2f, rMin.z + 0.08f), new Vector3(0.16f, 0.04f, 0.04f), p.DarkSteel, Quaternion.Euler(0f, -45f, 0f));
            Face(room, "Recovery_Panel", new Vector3(rMin.x + 0.02f, 1.9f, 3.5f), Quaternion.Euler(0f, -90f, 0f), 0.9f, 0.9f, p.Emblem, 0f, stretch: true);
            // The bed along the east wall: frame, mattress, rails, the cut straps hanging.
            Vector3 bed = new Vector3(rMax.x - 0.7f, 0f, 6.3f);
            Block(room, "Bed_Base", PrimitiveType.Cube, bed + new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 0.06f, 2f), p.Steel);
            Block(room, "Bed_Mattress", PrimitiveType.Cube, bed + new Vector3(0f, 0.62f, 0f), new Vector3(0.88f, 0.18f, 1.96f), p.Mattress);
            Block(room, "Bed_Pillow", PrimitiveType.Cube, bed + new Vector3(0f, 0.75f, 0.75f), new Vector3(0.6f, 0.08f, 0.4f), p.Mattress);
            foreach (var leg in new[] { (-0.4f, -0.9f), (0.4f, -0.9f), (-0.4f, 0.9f), (0.4f, 0.9f) })
            {
                Block(room, $"Bed_Leg_{leg.Item1}_{leg.Item2}", PrimitiveType.Cylinder, bed + new Vector3(leg.Item1, 0.25f, leg.Item2), new Vector3(0.06f, 0.25f, 0.06f), p.Steel);
            }
            Block(room, "Bed_Rail_West", PrimitiveType.Cylinder, bed + new Vector3(-0.46f, 0.95f, 0f), new Vector3(0.04f, 0.8f, 0.04f), p.Steel, Quaternion.Euler(90f, 0f, 0f));
            Block(room, "Bed_Rail_East", PrimitiveType.Cylinder, bed + new Vector3(0.46f, 0.95f, 0f), new Vector3(0.04f, 0.8f, 0.04f), p.Steel, Quaternion.Euler(90f, 0f, 0f));
            Block(room, "Bed_Head", PrimitiveType.Cylinder, bed + new Vector3(0f, 1.05f, 0.98f), new Vector3(0.04f, 0.45f, 0.04f), p.Steel, Quaternion.Euler(0f, 0f, 90f));
            Block(room, "Bed_Foot", PrimitiveType.Cylinder, bed + new Vector3(0f, 0.95f, -0.98f), new Vector3(0.04f, 0.45f, 0.04f), p.Steel, Quaternion.Euler(0f, 0f, 90f));
            foreach (var post in new[] { (-0.44f, 0.98f, 1.05f), (0.44f, 0.98f, 1.05f), (-0.44f, -0.98f, 0.95f), (0.44f, -0.98f, 0.95f) })
            {
                Block(room, $"Bed_Post_{post.Item1}_{post.Item2}", PrimitiveType.Cylinder, bed + new Vector3(post.Item1, (0.5f + post.Item3) * 0.5f, post.Item2), new Vector3(0.04f, (post.Item3 - 0.5f) * 0.5f, 0.04f), p.Steel);
            }
            foreach (float sz in new[] { -0.6f, -0.1f, 0.4f })
            {
                Block(room, $"Strap_W_{sz}", PrimitiveType.Cube, bed + new Vector3(-0.5f, 0.65f, sz), new Vector3(0.015f, 0.6f, 0.05f), p.Leather, Quaternion.Euler(0f, 0f, 6f));
                Block(room, $"Strap_E_{sz}", PrimitiveType.Cube, bed + new Vector3(0.5f, 0.68f, sz + 0.2f), new Vector3(0.015f, 0.55f, 0.05f), p.Leather, Quaternion.Euler(0f, 0f, -5f));
            }
            Block(room, "DripStand_Base", PrimitiveType.Cylinder, new Vector3(5.6f, 0.015f, 7.6f), new Vector3(0.5f, 0.015f, 0.5f), p.Steel);
            Block(room, "DripStand_Pole", PrimitiveType.Cylinder, new Vector3(5.6f, 0.95f, 7.6f), new Vector3(0.025f, 0.95f, 0.025f), p.Steel);
            Block(room, "DripStand_Hook", PrimitiveType.Cylinder, new Vector3(5.6f, 1.88f, 7.6f), new Vector3(0.015f, 0.18f, 0.015f), p.Steel, Quaternion.Euler(0f, 0f, 90f));
            Block(room, "Cabinet", PrimitiveType.Cube, new Vector3(4.5f, 0.5f, rMax.z - 0.22f), new Vector3(0.9f, 1f, 0.4f), p.Steel);
            Face(room, "Cabinet_Glass", new Vector3(4.5f, 0.6f, rMax.z - 0.425f), Quaternion.Euler(0f, 180f, 0f), 0.8f, 0.7f, p.Glass, 0f, stretch: true);
            Block(room, "Recovery_Planter", PrimitiveType.Cube, new Vector3(rMin.x + 0.5f, 0.3f, rMax.z - 0.5f), new Vector3(0.6f, 0.6f, 0.6f), p.Planter);
            Block(room, "Recovery_Soil", PrimitiveType.Cube, new Vector3(rMin.x + 0.5f, 0.62f, rMax.z - 0.5f), new Vector3(0.5f, 0.06f, 0.5f), p.Soil);
            FernCard(room, "Recovery_Fern_a", new Vector3(rMin.x + 0.5f, 0.62f, rMax.z - 0.5f), 1.0f, 0.9f, 30f, p.Fern);
            FernCard(room, "Recovery_Fern_b", new Vector3(rMin.x + 0.5f, 0.62f, rMax.z - 0.5f), 1.0f, 0.9f, -60f, p.Fern);
            ProbeGrid(rMin, rMax, 1.5f, 0.6f, 2.2f);

            // ── The therapy gym (T18 room_therapy_gym_1: tile, the wall of CRTs, the bars, the mirror, the dummies).
            Vector3 gMin = GymMin, gMax = GymMax;
            float gh = gMax.y, gw = gMax.x - gMin.x, gd = gMax.z - gMin.z;
            Vector3 gc = new Vector3((gMin.x + gMax.x) * 0.5f, 0f, (gMin.z + gMax.z) * 0.5f);
            Face(room, "Gym_Floor", gc, Quaternion.Euler(90f, 0f, 0f), gw, gd, p.ResinFloor, 0.3f);
            Face(room, "Gym_Ceiling", gc + new Vector3(0f, gh, 0f), Quaternion.Euler(-90f, 0f, 0f), gw, gd, p.Ceiling, 0.3f);
            WallWithOpening(room, "Gym_Wall_South", new Vector3(gc.x, 0f, gMin.z), Quaternion.Euler(0f, 180f, 0f), gw, gh, new Vector3(0f, 0f, gMin.z), 2.2f, 3f, p.TileWall, WallThickness);
            Face(room, "Gym_Wall_North", new Vector3(gc.x, gh * 0.5f, gMax.z), Quaternion.identity, gw, gh, p.TileWall, WallThickness);
            Face(room, "Gym_Wall_East", new Vector3(gMax.x, gh * 0.5f, gc.z), Quaternion.Euler(0f, 90f, 0f), gd, gh, p.TileWall, WallThickness);
            Face(room, "Gym_Wall_West", new Vector3(gMin.x, gh * 0.5f, gc.z), Quaternion.Euler(0f, -90f, 0f), gd, gh, p.TileWall, WallThickness);
            Skirting(room, gc, gw, gd, 0.15f, p.Skirting, "Gym_");
            DoorTunnel(room, "GymDoor", new Vector3(0f, 0f, cl), Quaternion.identity, gMin.z - cl, 2.2f, 3f, p.TileWall, p.Steel);
            // The wall of CRTs: four shelves, six monitors each, a green cursor on every screen.
            for (int row = 0; row < 4; row++)
            {
                float y = 0.25f + row * 0.52f;
                Block(room, $"Shelf_{row}", PrimitiveType.Cube, new Vector3(gMin.x + 1.75f, y, gMax.z - 0.3f), new Vector3(3.5f, 0.04f, 0.6f), p.DarkSteel);
                for (int col = 0; col < 6; col++)
                {
                    float x = gMin.x + 0.32f + col * 0.57f;
                    Vector3 c = new Vector3(x, y + 0.24f, gMax.z - 0.32f);
                    Block(room, $"Crt_{row}_{col}", PrimitiveType.Cube, c, new Vector3(0.5f, 0.42f, 0.46f), p.DarkSteel);
                    Face(room, $"CrtFace_{row}_{col}", c + new Vector3(0f, 0.01f, -0.235f), Quaternion.Euler(0f, 180f, 0f), 0.4f, 0.32f, p.Crt, 0f, stretch: true);
                }
            }
            Block(room, "Shelf_Post_W", PrimitiveType.Cube, new Vector3(gMin.x + 0.03f, 1.1f, gMax.z - 0.3f), new Vector3(0.05f, 2.2f, 0.6f), p.DarkSteel);
            Block(room, "Shelf_Post_E", PrimitiveType.Cube, new Vector3(gMin.x + 3.48f, 1.1f, gMax.z - 0.3f), new Vector3(0.05f, 2.2f, 0.6f), p.DarkSteel);
            // The mirror with its steel frame, the gear above it.
            Face(room, "Mirror", new Vector3(1.6f, 1.3f, gMax.z - 0.04f), Quaternion.identity, 1.3f, 2.3f, p.Mirror, 0f, stretch: true);
            Block(room, "Mirror_Frame_L", PrimitiveType.Cube, new Vector3(0.9f, 1.3f, gMax.z - 0.04f), new Vector3(0.08f, 2.4f, 0.06f), p.Steel);
            Block(room, "Mirror_Frame_R", PrimitiveType.Cube, new Vector3(2.3f, 1.3f, gMax.z - 0.04f), new Vector3(0.08f, 2.4f, 0.06f), p.Steel);
            Block(room, "Mirror_Frame_T", PrimitiveType.Cube, new Vector3(1.6f, 2.49f, gMax.z - 0.04f), new Vector3(1.48f, 0.08f, 0.06f), p.Steel);
            Block(room, "Mirror_Frame_B", PrimitiveType.Cube, new Vector3(1.6f, 0.11f, gMax.z - 0.04f), new Vector3(1.48f, 0.08f, 0.06f), p.Steel);
            Face(room, "Gym_Panel", new Vector3(1.6f, 3.0f, gMax.z - 0.02f), Quaternion.identity, 0.8f, 0.8f, p.Emblem, 0f, stretch: true);
            Block(room, "Gym_Planter", PrimitiveType.Cube, new Vector3(0.1f, 0.3f, gMax.z - 0.5f), new Vector3(0.6f, 0.6f, 0.6f), p.Planter);
            Block(room, "Gym_Soil", PrimitiveType.Cube, new Vector3(0.1f, 0.62f, gMax.z - 0.5f), new Vector3(0.5f, 0.06f, 0.5f), p.Soil);
            FernCard(room, "Gym_Fern_a", new Vector3(0.1f, 0.62f, gMax.z - 0.5f), 1.3f, 1.2f, 15f, p.Fern);
            FernCard(room, "Gym_Fern_b", new Vector3(0.1f, 0.62f, gMax.z - 0.5f), 1.3f, 1.2f, -70f, p.Fern);
            // The parallel bars over the painted lane.
            Face(room, "Lane", new Vector3(-0.6f, 0.004f, 13.5f), Quaternion.Euler(90f, 0f, 0f), 0.5f, 4.6f, p.Lane, 0f, stretch: true);
            foreach (float bx in new[] { -1.05f, -0.15f })
            {
                Block(room, $"Bar_{bx}", PrimitiveType.Cylinder, new Vector3(bx, 0.9f, 13.5f), new Vector3(0.05f, 2.2f, 0.05f), p.Steel, Quaternion.Euler(90f, 0f, 0f));
                foreach (float bz in new[] { 11.5f, 13.5f, 15.5f })
                {
                    Block(room, $"BarPost_{bx}_{bz}", PrimitiveType.Cylinder, new Vector3(bx, 0.45f, bz), new Vector3(0.05f, 0.45f, 0.05f), p.Steel);
                    Block(room, $"BarBase_{bx}_{bz}", PrimitiveType.Cylinder, new Vector3(bx, 0.01f, bz), new Vector3(0.3f, 0.01f, 0.3f), p.Steel);
                }
            }
            // The rack with the padded dummies on the east wall.
            float rackX = gMax.x - 0.45f;
            Block(room, "Rack_Top", PrimitiveType.Cube, new Vector3(rackX, 2.25f, 14.5f), new Vector3(0.06f, 0.06f, 3f), p.DarkSteel);
            Block(room, "Rack_Post_S", PrimitiveType.Cube, new Vector3(rackX, 1.125f, 13f), new Vector3(0.06f, 2.25f, 0.06f), p.DarkSteel);
            Block(room, "Rack_Post_N", PrimitiveType.Cube, new Vector3(rackX, 1.125f, 16f), new Vector3(0.06f, 2.25f, 0.06f), p.DarkSteel);
            foreach (var d in new[] { (13.6f, true), (14.5f, false), (15.4f, true) })
            {
                string n = $"Dummy_{d.Item1}";
                Block(room, n + "_Strap", PrimitiveType.Cube, new Vector3(rackX, 2.05f, d.Item1), new Vector3(0.03f, 0.4f, 0.04f), p.Leather);
                if (d.Item2)
                {
                    Block(room, n + "_Body", PrimitiveType.Capsule, new Vector3(rackX, 1.35f, d.Item1), new Vector3(0.42f, 0.5f, 0.3f), p.Canvas);
                    Block(room, n + "_Vest", PrimitiveType.Cube, new Vector3(rackX, 1.45f, d.Item1), new Vector3(0.44f, 0.45f, 0.26f), p.Vest);
                    Block(room, n + "_Head", PrimitiveType.Sphere, new Vector3(rackX, 1.95f, d.Item1), new Vector3(0.22f, 0.26f, 0.22f), p.Canvas);
                    Block(room, n + "_Arm_S", PrimitiveType.Cylinder, new Vector3(rackX, 1.35f, d.Item1 - 0.27f), new Vector3(0.1f, 0.28f, 0.1f), p.Canvas);
                    Block(room, n + "_Arm_N", PrimitiveType.Cylinder, new Vector3(rackX, 1.35f, d.Item1 + 0.27f), new Vector3(0.1f, 0.28f, 0.1f), p.Canvas);
                }
                else
                {
                    Block(room, n + "_Bag", PrimitiveType.Capsule, new Vector3(rackX, 1.3f, d.Item1), new Vector3(0.36f, 0.55f, 0.36f), p.Canvas);
                }
            }
            Block(room, "Gym_Speaker_W", PrimitiveType.Cylinder, new Vector3(gMin.x + 0.6f, gh - 0.015f, gMin.z + 0.6f), new Vector3(0.3f, 0.015f, 0.3f), p.DarkSteel);
            Block(room, "Gym_Speaker_E", PrimitiveType.Cylinder, new Vector3(gMax.x - 0.6f, gh - 0.015f, gMax.z - 0.6f), new Vector3(0.3f, 0.015f, 0.3f), p.DarkSteel);
            Spot(lights, "Gym_Pool_Bars", new Vector3(-0.6f, gh - 0.1f, 13.5f), Quaternion.Euler(90f, 0f, 0f), warm, 45f, 80f);
            Spot(lights, "Gym_Pool_Crt", new Vector3(-1.4f, gh - 0.1f, 16.2f), Quaternion.Euler(55f, 0f, 0f), warm, 40f, 70f);
            Spot(lights, "Gym_Pool_Rack", new Vector3(3.0f, gh - 0.1f, 14.5f), Quaternion.Euler(70f, 90f, 0f), warm, 40f, 70f);
            Spot(lights, "Gym_Door_Cold", new Vector3(0f, gh - 0.1f, 10.9f), Quaternion.Euler(80f, 180f, 0f), cold, 35f, 60f);
            ProbeGrid(gMin, gMax, 1.5f, 0.6f, 2.4f);

            // ── The cast: Lucy at the south end of the corridor, a guard at its far end, the lab worker in the
            // recovery room, a patient in the gym.
            // Lucy three metres from the south camera at the edge of the first skylight pool (the first pass
            // had her one metre from the lens, in the dark), the guard by the recovery door, the lab worker at
            // the back of the recovery room by the drip stand (not in front of the lens).
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog();
            PlaceLucy(catalog, new Vector3(-0.3f, 0f, -6.5f), 0f);
            PlaceFigure(catalog, "Figure_Guard", "guard", new Vector3(0.8f, 0f, 6f));
            PlaceFigure(catalog, "Figure_LabWorker", "labworker", new Vector3(4.2f, 0f, 6.6f));
            PlaceFigure(catalog, "Figure_Patient", "patient", new Vector3(2.2f, 0f, 12.5f));

            // ── Cameras (ART_LESSONS 1.6: inside the room, level): the corridor from its south end (the T12 view,
            // the default), from its north end, at the recovery door; the recovery room from the south wall and
            // from the bed corner; the gym from the door corner and from the bars corner.
            GameObject camerasRoot = MenuLoopSetup.EnsureRoot("[Cameras]");
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            // The numbers come from the coverage check (scratchpad coverage.py): the corridor cameras sit near
            // the centre line, the bars camera holds the west part of the gym, the gym camera the rest.
            FixedCamera corridorS = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Corridor_S", new Vector3(0.6f, 1.6f, -9.4f), new Vector3(-0.1f, 1.3f, 2f), 60f);
            FixedCamera corridorN = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Corridor_N", new Vector3(0f, 1.7f, 9.5f), new Vector3(0.3f, 1f, -4f), 52f);
            FixedCamera corridorDoor = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Corridor_Door", new Vector3(-1.6f, 1.6f, 7.8f), new Vector3(1.9f, 1.1f, 4.6f), 50f);
            FixedCamera recovery = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Recovery", new Vector3(rc.x, 1.6f, rMin.z + 0.15f), new Vector3(rc.x, 1.15f, rMax.z), 66f);
            FixedCamera recoveryBed = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Recovery_Bed", new Vector3(rMax.x - 0.3f, 1.7f, rMax.z - 0.3f), new Vector3(2.8f, 1f, 4.6f), 62f);
            FixedCamera gym = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Gym", new Vector3(3.3f, 1.7f, gMin.z + 0.4f), new Vector3(-1.6f, 1f, 15.5f), 62f);
            FixedCamera gymBars = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Gym_Bars", new Vector3(gMin.x + 0.5f, 1.7f, gMax.z - 0.5f), new Vector3(2.5f, 1f, 12f), 58f);
            director.SetDefaultCamera(corridorS);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Corridor_N", new Vector3(0f, 1.5f, 4f), new Vector3(CorridorWidth, 3f, 12f), corridorN, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Corridor_Door", new Vector3(1.25f, 1.5f, 5f), new Vector3(1.5f, 3f, 3f), corridorDoor, 2);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Recovery", new Vector3((cw + rMax.x) * 0.5f, 1.5f, rc.z), new Vector3(rMax.x - cw, 3f, rd), recovery, 3);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Recovery_Bed", new Vector3(rc.x, 1.5f, rMax.z - 1f), new Vector3(rw, 3f, 2f), recoveryBed, 4);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Gym", new Vector3(gc.x, 1.5f, (cl + gMax.z) * 0.5f), new Vector3(gw, 3f, gMax.z - cl), gym, 3);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Gym_Bars", new Vector3(-1.75f, 1.5f, 15.4f), new Vector3(4.5f, 3f, 3.8f), gymBars, 4);

            FinishScene(scene, p, new[] { corridorS, corridorN, corridorDoor, recovery, recoveryBed, gym, gymBars }, "the corridor, the recovery room, the gym, 12 spots and 1 panel light");
        }

        // ── Room helpers ──────────────────────────────────────────────

        private static GameObject Face(GameObject parent, string name, Vector3 position, Quaternion rotation, float width, float height, Material material, float colliderDepth, float tile = TileMetres, bool stretch = false)
        {
            GameObject obj = MenuLoopSetup.EnsureChild(parent, name);
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.transform.localScale = Vector3.one;
            obj.layer = LayerMask.NameToLayer("Default");
            obj.isStatic = true;
            MenuLoopSetup.EnsureComponent<MeshFilter>(obj);
            MeshRenderer renderer = MenuLoopSetup.EnsureComponent<MeshRenderer>(obj);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveGI = ReceiveGI.Lightmaps;
            RoomFace face = MenuLoopSetup.EnsureComponent<RoomFace>(obj);
            face.width = width;
            face.height = height;
            face.tileMetres = tile;
            face.colliderDepth = colliderDepth;
            face.stretchUv = stretch;
            face.Build();
            if (colliderDepth <= 0f) MenuLoopSetup.RemoveComponent<BoxCollider>(obj);
            roomRenderers.Add(renderer);
            dressedMaterials.Add(material);
            return obj;
        }

        private static GameObject Block(GameObject parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            return Block(parent, name, type, position, scale, material, Quaternion.identity);
        }

        private static GameObject Block(GameObject parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Quaternion rotation)
        {
            GameObject obj = MenuLoopSetup.EnsureChildPrimitive(parent, name, type, position, scale);
            obj.transform.rotation = rotation;
            obj.layer = LayerMask.NameToLayer("Default");
            obj.isStatic = true;
            Renderer renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            if (renderer is MeshRenderer meshRenderer) meshRenderer.receiveGI = ReceiveGI.Lightmaps;
            roomRenderers.Add(renderer);
            dressedMaterials.Add(material);
            return obj;
        }

        /// <summary>
        /// A wall with a doorway: the part left of it, the part right of it and the lintel over it, three faces
        /// with the same facing. The wall stands on the floor at its bottom centre; the opening at a world point
        /// on that line.
        /// </summary>
        private static void WallWithOpening(GameObject parent, string name, Vector3 bottomCentre, Quaternion rotation, float width, float height, Vector3 openingCentre, float openingWidth, float openingHeight, Material material, float colliderDepth)
        {
            Vector3 right = rotation * Vector3.right;
            float ox = Vector3.Dot(openingCentre - bottomCentre, right);
            float leftWidth = ox - openingWidth * 0.5f + width * 0.5f;
            float rightWidth = width * 0.5f - (ox + openingWidth * 0.5f);
            Vector3 up = Vector3.up * (height * 0.5f);
            if (leftWidth > 0.01f) Face(parent, name + "_L", bottomCentre + right * ((-width * 0.5f + ox - openingWidth * 0.5f) * 0.5f) + up, rotation, leftWidth, height, material, colliderDepth);
            if (rightWidth > 0.01f) Face(parent, name + "_R", bottomCentre + right * ((ox + openingWidth * 0.5f + width * 0.5f) * 0.5f) + up, rotation, rightWidth, height, material, colliderDepth);
            float lintel = height - openingHeight;
            if (lintel > 0.01f) Face(parent, name + "_Lintel", bottomCentre + right * ox + Vector3.up * (openingHeight + lintel * 0.5f), rotation, openingWidth, lintel, material, colliderDepth);
        }

        /// <summary>
        /// The inside of a doorway through a wall of some thickness: two jambs that face each other, the soffit
        /// over it, a steel sill on the floor. The opening starts at a point on the first wall's line and runs
        /// along that wall's forward (out of the first room, into the second).
        /// </summary>
        private static void DoorTunnel(GameObject parent, string name, Vector3 start, Quaternion facing, float thickness, float width, float height, Material material, Material sill)
        {
            Vector3 forward = facing * Vector3.forward;
            Vector3 right = facing * Vector3.right;
            Vector3 mid = start + forward * (thickness * 0.5f);
            Face(parent, name + "_Jamb_R", mid + right * (width * 0.5f) + Vector3.up * (height * 0.5f), Quaternion.LookRotation(right, Vector3.up), thickness, height, material, 0.05f, stretch: false);
            Face(parent, name + "_Jamb_L", mid - right * (width * 0.5f) + Vector3.up * (height * 0.5f), Quaternion.LookRotation(-right, Vector3.up), thickness, height, material, 0.05f, stretch: false);
            Face(parent, name + "_Soffit", mid + Vector3.up * height, Quaternion.LookRotation(Vector3.up, forward), width, thickness, material, 0.05f);
            Face(parent, name + "_Sill", mid, Quaternion.LookRotation(Vector3.down, forward), width, thickness, sill, 0.1f, stretch: true);
        }

        /// <summary>A dark band at the foot of the four walls of a rectangular room (centre on the floor, x width, z depth).</summary>
        private static void Skirting(GameObject parent, Vector3 centre, float width, float depth, float height, Material material, string prefix = "")
        {
            Band(parent, centre, width, depth, 0f, height, material, prefix + "Skirting");
        }

        /// <summary>A band on the four walls at some height, a hair in front of them.</summary>
        private static void Band(GameObject parent, Vector3 centre, float width, float depth, float bottom, float height, Material material, string name = "Band")
        {
            const float gap = 0.012f;
            float y = bottom + height * 0.5f;
            float hw = width * 0.5f, hd = depth * 0.5f;
            Face(parent, name + "_N", centre + new Vector3(0f, y, hd - gap), Quaternion.identity, width, height, material, 0f);
            Face(parent, name + "_S", centre + new Vector3(0f, y, -hd + gap), Quaternion.Euler(0f, 180f, 0f), width, height, material, 0f);
            Face(parent, name + "_E", centre + new Vector3(hw - gap, y, 0f), Quaternion.Euler(0f, 90f, 0f), depth, height, material, 0f);
            Face(parent, name + "_W", centre + new Vector3(-hw + gap, y, 0f), Quaternion.Euler(0f, -90f, 0f), depth, height, material, 0f);
        }

        /// <summary>A fern: two crossed cutout cards standing on the soil, turned by a yaw so no two planters match.</summary>
        private static void FernCard(GameObject parent, string name, Vector3 soil, float width, float height, float yaw, Material material)
        {
            Vector3 centre = soil + Vector3.up * (height * 0.5f);
            Face(parent, name + "_1", centre, Quaternion.Euler(0f, yaw, 0f), width, height, material, 0f, stretch: true);
            Face(parent, name + "_2", centre, Quaternion.Euler(0f, yaw + 90f, 0f), width, height, material, 0f, stretch: true);
        }

        private static Light Spot(GameObject parent, string name, Vector3 position, Quaternion rotation, Color colour, float intensity, float spotAngle, float range = 12f)
        {
            GameObject obj = MenuLoopSetup.EnsureChild(parent, name);
            obj.transform.SetPositionAndRotation(position, rotation);
            Light light = MenuLoopSetup.EnsureComponent<Light>(obj);
            light.type = LightType.Spot;
            light.color = colour;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = spotAngle;
            light.innerSpotAngle = spotAngle * 0.5f;
            light.shadows = LightShadows.Hard;
            light.shadowStrength = 1f;
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.3f;
            light.lightmapBakeType = LightmapBakeType.Mixed; // the direct light live and hard, the bounce baked
            ShadowTier(light, UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh);
            return light;
        }

        /// <summary>A panel light: a point light just under the emissive panel, hard shadows on six faces at the medium tier.</summary>
        private static Light Point(GameObject parent, string name, Vector3 position, Color colour, float intensity, float range)
        {
            GameObject obj = MenuLoopSetup.EnsureChild(parent, name);
            obj.transform.SetPositionAndRotation(position, Quaternion.identity);
            Light light = MenuLoopSetup.EnsureComponent<Light>(obj);
            light.type = LightType.Point;
            light.color = colour;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Hard;
            light.shadowStrength = 1f;
            light.shadowBias = 0.03f;
            light.shadowNormalBias = 0.3f;
            light.lightmapBakeType = LightmapBakeType.Mixed;
            ShadowTier(light, UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium);
            return light;
        }

        /// <summary>The shadow resolution tier of a light (the URP field is read-only in code, so the serialized one).</summary>
        private static void ShadowTier(Light light, int tier)
        {
            UniversalAdditionalLightData data = light.GetUniversalAdditionalLightData();
            var so = new SerializedObject(data);
            so.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = tier;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }
    }
}
#endif
