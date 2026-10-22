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
    /// Build order step 4, the look test (03_GAME_DESIGN §7.4, CONCEPTS §7): the Gallery of floor 1 as a room
    /// (raw concrete, planters, hard pools of light), Lucy and two figures in it, three fixed cameras, and the
    /// three dressings LookDirector switches between: the greybox, the real-time room with the 1999 sheen, and
    /// the plate (the room rendered once per camera, the characters drawn over it).
    /// Assets, all from code: the three renderers next to the stock one on the URP assets (GB_Full with the
    /// dither, GB_Expensive with ambient occlusion for the plate, GB_Plate with the plate blit and the room
    /// depth), the materials (Simple Lit: the Blinn-Phong highlight of 1999), the textures made by
    /// tools/look_textures.sh (RFab, the dogfood rule), the scene, the build list.
    /// Menu: GlorifiedBody > Build Look Test. Batch: tools/unity.sh exec GlorifiedBody.Editor.LookTestSetup.BuildLookTest
    /// </summary>
    public static class LookTestSetup
    {
        public const string SceneName = "LookTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string RenderingFolder = "Assets/_Game/Rendering";
        public const string RenderersFolder = RenderingFolder + "/Renderers";
        public const string MaterialsFolder = RenderingFolder + "/Materials";
        public const string TexturesFolder = RenderingFolder + "/Textures";
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
        public const int TextureSize = 1024;
        public static readonly string[] TextureNames = { "concrete_wall", "concrete_floor", "steel_panel", "soil", "gear_emblem" };

        // The room: 16 m long (z), 8 m wide (x), 4.5 m high. The textures repeat every 2 m.
        public const float RoomLength = 16f, RoomWidth = 8f, RoomHeight = 4.5f, TileMetres = 2f;

        private static int RoomLayerMask => 1 << LayerMask.NameToLayer("Default");
        private static int CharacterLayerMask => LayerMask.GetMask("Player", "Enemy");

        [MenuItem("GlorifiedBody/Build Look Test")]
        public static void BuildLookTest()
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
            BuildScene();
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[LookTestSetup] {ScenePath} built: the Gallery, three dressings, three cameras.");
        }

        // ── Renderers ─────────────────────────────────────────────────

        /// <summary>
        /// Three copies of the stock renderer, each with its features, at indices 1 to 3 of the URP assets. The
        /// assets also get the look's numbers: the 720p render scale, hard shadows only, per-pixel spot lights
        /// with shadows.
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
                // The 1999 frame (CONCEPTS 7.4): 720p of 1080p, scaled up with a plain filter.
                so.FindProperty("m_RenderScale").floatValue = LookDirector.LookRenderScale;
                so.FindProperty("m_UpscalingFilter").intValue = (int)UpscalingFilterSelection.Linear;
                // ART_LESSONS 1.3: hard shadows, never soften them. The spots need per-pixel lights with shadows.
                so.FindProperty("m_SoftShadowsSupported").boolValue = false;
                so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
                so.FindProperty("m_MainLightShadowmapResolution").intValue = 2048;
                so.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
                so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
                so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = 2048;
                so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 8;
                so.FindProperty("m_ShadowDistance").floatValue = 30f;
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
        /// tint, a smoothness and one specular colour. No normal map, no roughness map.
        /// </summary>
        private static Material SurfaceMaterial(string name, string textureName, Color tint, float smoothness, Color specular)
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
                m.DisableKeyword("_EMISSION");
                m.SetFloat("_ReceiveShadows", 1f);
            });
        }

        /// <summary>The pictures from tools/look_textures.sh, imported as 1024 colour maps that repeat by mirror (no seam from a picture that is not tileable).</summary>
        internal static void EnsureTextures()
        {
            foreach (string name in TextureNames)
            {
                string path = $"{TexturesFolder}/{name}.png";
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[LookTestSetup] {path} missing: run tools/look_textures.sh, then tools/look_textures.sh import.");
                    continue;
                }
                AssetDatabase.ImportAsset(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool emblem = name == "gear_emblem";
                importer.textureType = TextureImporterType.Default;
                importer.maxTextureSize = TextureSize;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 1;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = emblem ? TextureWrapMode.Clamp : TextureWrapMode.Mirror;
                importer.SaveAndReimport();
            }
        }

        // ── The scene ─────────────────────────────────────────────────

        private static readonly List<Renderer> roomRenderers = new List<Renderer>();
        private static readonly List<Material> dressedMaterials = new List<Material>();

        internal static void BuildScene()
        {
            Scene scene = MenuLoopSetup.OpenOrCreate(ScenePath);
            roomRenderers.Clear();
            dressedMaterials.Clear();

            Camera camera = MenuLoopSetup.EnsureCamera(new Vector3(-3.2f, 1.6f, -7.2f), new Vector3(0f, 1.2f, 2f), 55f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            MenuLoopSetup.RemoveObject("Directional Light"); // indoors: the spots only
            // ART_LESSONS 1.4: hard pools of light, near-black corners, no bounce.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.025f, 0.025f, 0.03f);
            RenderSettings.fog = false;

            Material greybox = SurfaceMaterial("GB_Greybox", null, new Color(0.55f, 0.55f, 0.56f), 0.1f, new Color(0.2f, 0.2f, 0.2f));
            Material wall = SurfaceMaterial("GB_ConcreteWall", "concrete_wall", new Color(0.78f, 0.78f, 0.76f), 0.25f, new Color(0.25f, 0.25f, 0.25f));
            Material ceiling = SurfaceMaterial("GB_ConcreteCeiling", "concrete_wall", new Color(0.5f, 0.5f, 0.5f), 0.15f, new Color(0.15f, 0.15f, 0.15f));
            Material floor = SurfaceMaterial("GB_ConcreteFloor", "concrete_floor", new Color(0.75f, 0.75f, 0.74f), 0.55f, new Color(0.45f, 0.45f, 0.45f));
            Material planter = SurfaceMaterial("GB_ConcretePlanter", "concrete_wall", new Color(0.85f, 0.84f, 0.8f), 0.3f, new Color(0.3f, 0.3f, 0.3f));
            Material steel = SurfaceMaterial("GB_Steel", "steel_panel", new Color(0.7f, 0.72f, 0.75f), 0.8f, new Color(0.8f, 0.8f, 0.8f));
            Material soil = SurfaceMaterial("GB_Soil", "soil", new Color(0.6f, 0.55f, 0.5f), 0.1f, new Color(0.1f, 0.1f, 0.1f));
            Material plant = SurfaceMaterial("GB_Plant", null, new Color(0.1f, 0.24f, 0.09f), 0.35f, new Color(0.25f, 0.3f, 0.2f));
            Material emblem = SurfaceMaterial("GB_GearEmblem", "gear_emblem", Color.white, 0.2f, new Color(0.2f, 0.2f, 0.2f));

            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            float hl = RoomLength * 0.5f, hw = RoomWidth * 0.5f, hh = RoomHeight * 0.5f;
            Face(room, "Floor", new Vector3(0f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f), RoomWidth, RoomLength, floor, 0.3f);
            Face(room, "Ceiling", new Vector3(0f, RoomHeight, 0f), Quaternion.Euler(-90f, 0f, 0f), RoomWidth, RoomLength, ceiling, 0.3f);
            Face(room, "Wall_North", new Vector3(0f, hh, hl), Quaternion.identity, RoomWidth, RoomHeight, wall, 0.5f);
            Face(room, "Wall_South", new Vector3(0f, hh, -hl), Quaternion.Euler(0f, 180f, 0f), RoomWidth, RoomHeight, wall, 0.5f);
            Face(room, "Wall_East", new Vector3(hw, hh, 0f), Quaternion.Euler(0f, 90f, 0f), RoomLength, RoomHeight, wall, 0.5f);
            Face(room, "Wall_West", new Vector3(-hw, hh, 0f), Quaternion.Euler(0f, -90f, 0f), RoomLength, RoomHeight, wall, 0.5f);

            // Planters (ART_LESSONS 1.5: nature is tamed, planters only), along the walls, alternating sides.
            float[] planterZ = { -3f, 1.5f, 5f };
            for (int i = 0; i < planterZ.Length; i++)
            {
                float x = i % 2 == 0 ? 2.7f : -2.7f;
                GameObject box = Block(room, "Planter_" + i, PrimitiveType.Cube, new Vector3(x, 0.35f, planterZ[i]), new Vector3(2.4f, 0.7f, 0.9f), planter);
                Block(room, "Soil_" + i, PrimitiveType.Cube, new Vector3(x, 0.72f, planterZ[i]), new Vector3(2.2f, 0.08f, 0.7f), soil);
                Block(room, "Shrub_" + i + "a", PrimitiveType.Sphere, new Vector3(x - 0.7f, 1.05f, planterZ[i]), new Vector3(0.7f, 0.6f, 0.6f), plant);
                Block(room, "Shrub_" + i + "b", PrimitiveType.Sphere, new Vector3(x, 1.15f, planterZ[i] + 0.1f), new Vector3(0.8f, 0.8f, 0.7f), plant);
                Block(room, "Shrub_" + i + "c", PrimitiveType.Sphere, new Vector3(x + 0.7f, 1.0f, planterZ[i] - 0.1f), new Vector3(0.6f, 0.55f, 0.6f), plant);
                box.isStatic = true;
            }
            Block(room, "Column_A", PrimitiveType.Cube, new Vector3(3.4f, hh, -2f), new Vector3(0.7f, RoomHeight, 0.7f), wall);
            Block(room, "Column_B", PrimitiveType.Cube, new Vector3(-3.4f, hh, 3.2f), new Vector3(0.7f, RoomHeight, 0.7f), wall);
            Block(room, "Conduit_A", PrimitiveType.Cylinder, new Vector3(-1.2f, 4.3f, 0f), new Vector3(0.24f, hl, 0.24f), steel).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Block(room, "Conduit_B", PrimitiveType.Cylinder, new Vector3(1.4f, 4.35f, 0f), new Vector3(0.18f, hl, 0.18f), steel).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Block(room, "Door", PrimitiveType.Cube, new Vector3(0f, 1.5f, hl - 0.07f), new Vector3(2.2f, 3f, 0.12f), steel);
            Block(room, "DoorFrame", PrimitiveType.Cube, new Vector3(0f, 3.1f, hl - 0.1f), new Vector3(2.6f, 0.25f, 0.2f), steel);
            // The gear on the panels (CONCEPTS 5: "the church shows itself, nobody says it").
            Face(room, "Panel_Door", new Vector3(0f, 3.75f, hl - 0.03f), Quaternion.identity, 1.2f, 1.2f, emblem, 0f, tile: 1.2f);
            Face(room, "Panel_East", new Vector3(hw - 0.03f, 2.6f, 2.5f), Quaternion.Euler(0f, 90f, 0f), 1.6f, 1.6f, emblem, 0f, tile: 1.6f);
            Face(room, "Panel_West", new Vector3(-hw + 0.03f, 2.6f, -4f), Quaternion.Euler(0f, -90f, 0f), 1.6f, 1.6f, emblem, 0f, tile: 1.6f);

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
            GameObject lucy = MenuLoopSetup.EnsureLucy(catalog, new Vector3(-2f, 0f, -5.5f), 0f);
            SetLayer(lucy, "Player");
            // No red lock marker over the figures: the picture is the point of this room.
            var lockSo = new SerializedObject(lucy.GetComponent<LockOnController>());
            lockSo.FindProperty("showMarker").boolValue = false;
            lockSo.ApplyModifiedPropertiesWithoutUndo();
            GameObject soldier = MenuLoopSetup.EnsureDummy("Figure_Soldier", new Vector3(1.5f, 0f, 2f), catalog, "soldier");
            GameObject guard = MenuLoopSetup.EnsureDummy("Figure_Guard", new Vector3(-1f, 0f, 5.5f), catalog, "guard");
            SetLayer(soldier, "Enemy");
            SetLayer(guard, "Enemy");

            // Cameras: inside the room, at a level angle (ART_LESSONS 1.6). The wide one by default.
            GameObject camerasRoot = MenuLoopSetup.EnsureRoot("[Cameras]");
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera wide = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Wide", new Vector3(-3.2f, 1.6f, -7.2f), new Vector3(0f, 1.2f, 2f), 55f);
            FixedCamera mid = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Mid", new Vector3(3.6f, 1.7f, 0.5f), new Vector3(-1.5f, 1f, 2.5f), 50f);
            FixedCamera door = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Door", new Vector3(1.6f, 1.5f, 7f), new Vector3(-0.8f, 1.1f, -2f), 50f);
            director.SetDefaultCamera(wide);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Mid", new Vector3(0f, 1f, 1f), new Vector3(RoomWidth, 3f, 4f), mid, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Door", new Vector3(0f, 1f, 5.5f), new Vector3(RoomWidth, 3f, 5f), door, 1);
            MenuLoopSetup.EnsureShotPoints(new[] { wide, mid, door });

            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();

            GameObject lookRoot = MenuLoopSetup.EnsureRoot("[Look]");
            LookDirector look = MenuLoopSetup.EnsureComponent<LookDirector>(lookRoot);
            var greyboxes = new Material[roomRenderers.Count];
            for (int i = 0; i < greyboxes.Length; i++) greyboxes[i] = greybox;
            look.Configure(FullRendererIndex, ExpensiveRendererIndex, PlateRendererIndex, RoomLayerMask, roomRenderers.ToArray(), greyboxes, dressedMaterials.ToArray());
            EditorUtility.SetDirty(look);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[LookTestSetup] {ScenePath}: {roomRenderers.Count} room surfaces, 5 spots, 3 cameras.");
        }

        private static GameObject Face(GameObject parent, string name, Vector3 position, Quaternion rotation, float width, float height, Material material, float colliderDepth, float tile = TileMetres)
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
            RoomFace face = MenuLoopSetup.EnsureComponent<RoomFace>(obj);
            face.width = width;
            face.height = height;
            face.tileMetres = tile;
            face.colliderDepth = colliderDepth;
            face.Build();
            if (colliderDepth <= 0f) MenuLoopSetup.RemoveComponent<BoxCollider>(obj);
            roomRenderers.Add(renderer);
            dressedMaterials.Add(material);
            return obj;
        }

        private static GameObject Block(GameObject parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj = MenuLoopSetup.EnsureChildPrimitive(parent, name, type, position, scale);
            obj.transform.rotation = Quaternion.identity;
            obj.layer = LayerMask.NameToLayer("Default");
            obj.isStatic = true;
            Renderer renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            roomRenderers.Add(renderer);
            dressedMaterials.Add(material);
            return obj;
        }

        private static void Spot(GameObject parent, string name, Vector3 position, Quaternion rotation, Color colour, float intensity, float spotAngle)
        {
            GameObject obj = MenuLoopSetup.EnsureChild(parent, name);
            obj.transform.SetPositionAndRotation(position, rotation);
            Light light = MenuLoopSetup.EnsureComponent<Light>(obj);
            light.type = LightType.Spot;
            light.color = colour;
            light.intensity = intensity;
            light.range = 12f;
            light.spotAngle = spotAngle;
            light.innerSpotAngle = spotAngle * 0.5f;
            light.shadows = LightShadows.Hard;
            light.shadowStrength = 1f;
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.3f;
        }

        private static void SetLayer(GameObject root, string layerName)
        {
            if (root == null) return;
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0) return;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
#endif
