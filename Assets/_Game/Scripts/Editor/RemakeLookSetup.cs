#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.Rendering;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// T26 (Bruno, 2026-11-01, restarted the same evening): the remake look test in ONE scene, RemakeLook.unity, a copy of
    /// LookTest2 with its recovery room dressed as W1 of the demo in the act 1 look (black on black, design §13.8: black
    /// tile with white grout, polished black granite, the black ceiling grid, chrome, brass, one small ceiling light).
    /// Profile A (the current look: Simple Lit with the colour map, A's bake and shadow numbers, the Authentic dither, the
    /// game's 2,500 characters with 1024 maps) and profile B (real-time 3D that emulates the result of the pre-rendered
    /// Resident Evil remake: ten switches that turn off one at a time) share the room, the props and the light, so the
    /// comparison shows only the rendering. RemakeDirector runs both; two bakes (RemakeBakeSwap).
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.RemakeLookSetup.Build, then ...RemakeLookSetup.Bake.
    /// </summary>
    public static class RemakeLookSetup
    {
        public const string SceneName = "RemakeLook";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string Root = "Assets/_Game/Remake";
        public const string TexturesFolder = Root + "/Textures";
        public const string PropsFolder = Root + "/Props";
        public const string CharactersFolder = Root + "/Characters";
        public const string MaterialsFolder = Root + "/Materials";
        public const string SettingsFolder = Root + "/Settings";
        public const string BakeFolder = Root + "/Bake";
        public const string RemakeRendererPath = SettingsFolder + "/GB_Remake.asset";
        public const string RemakeDitherRendererPath = SettingsFolder + "/GB_RemakeDither.asset";
        public const string PipelineHighPath = SettingsFolder + "/GB_Remake_High.asset";
        public const string PipelineLowPath = SettingsFolder + "/GB_Remake_Low.asset";
        public const string PipelineMidPath = SettingsFolder + "/GB_Remake_Mid.asset"; // a measure only: the lamp at 2048, the panel at 1024 a face
        public const string BakeHighPath = SettingsFolder + "/GB_RemakeBakeHigh.lighting";
        public const string BakeLowPath = SettingsFolder + "/GB_RemakeBakeLow.lighting";
        public const string GradePath = SettingsFolder + "/GB_RemakeGrade.asset";
        public const string CurtainMeshPath = SettingsFolder + "/curtain_mesh.asset";
        public const float HighTexels = 40f;

        public static readonly Vector3 RoomMin = LookTestSetup.RecoveryMin, RoomMax = LookTestSetup.RecoveryMax;

        // The long lens (switch 10): from the south-west corner, beside the locker, onto the bed, the lamp and the curtain.
        public static readonly Vector3 LongCamPos = new Vector3(2.55f, 1.55f, 2.75f), LongCamAt = new Vector3(6.3f, 1.05f, 6.4f);

        [MenuItem("GlorifiedBody/T26 Remake Look/Build (profile B)")]
        public static void Build()
        {
            foreach (string f in new[] { Root, TexturesFolder, PropsFolder, CharactersFolder, MaterialsFolder, SettingsFolder, BakeFolder }) Directory.CreateDirectory(f);
            AssetDatabase.Refresh();
            ImportTextures();
            ImportProps();
            UniversalRendererData remake, remakeDither;
            if (!EnsureRenderers(out remake, out remakeDither)) return;
            UniversalRenderPipelineAsset high = EnsurePipeline(PipelineHighPath, remake, remakeDither, 8192, 512, 2048, 4096);
            UniversalRenderPipelineAsset low = EnsurePipeline(PipelineLowPath, remake, remakeDither, 4096, 256, 512, 1024);
            UniversalRenderPipelineAsset mid = EnsurePipeline(PipelineMidPath, remake, remakeDither, 4096, 512, 1024, 2048);
            EnsureBakeSettings();
            VolumeProfile grade = EnsureGrade();
            BuildScene(high, low, mid, remake, remakeDither, grade);
            AssetDatabase.SaveAssets();
            Debug.Log("[RemakeLookSetup] built. Next: RemakeLookSetup.Bake.");
        }

        // ── Imports ──────────────────────────────────────────────────

        private static void ImportTextures()
        {
            foreach (string path in Directory.GetFiles(TexturesFolder).Concat(Directory.GetFiles(PropsFolder)).Concat(Directory.GetFiles(CharactersFolder)))
            {
                if (!(path.EndsWith(".png") || path.EndsWith(".jpg"))) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                bool normal = name.EndsWith("_n") || name.Contains("_n_");
                bool linear = name.EndsWith("_ms") || name.Contains("_ms_");
                bool decal = name.StartsWith("decal_") || name.StartsWith("dust_");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !linear && !normal;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = decal;
                importer.mipmapEnabled = true;
                importer.wrapMode = decal ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.maxTextureSize = path.StartsWith(CharactersFolder) ? 4096 : 2048;
                // A's 1024 room maps stay uncompressed (the game's clarity rule). B's 2K and 4K maps are BC7 / BC5 at high
                // quality: about 40 of them uncompressed overran the 2 GB of the GTX 1050 in the first pass (270 ms a frame).
                bool aMap = name.StartsWith("blk_") && !name.Contains("_2k");
                importer.textureCompression = aMap || decal ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
                importer.crunchedCompression = false;
                importer.SaveAndReimport();
            }
        }

        private static void ImportProps()
        {
            foreach (string path in Directory.GetFiles(PropsFolder, "*.fbx"))
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                // The lamp's many small parts break Unity's unwrap ("could not pack uvs"): it hangs in the air, so the probes light it.
                importer.generateSecondaryUV = !path.EndsWith("lamp.fbx");
                importer.secondaryUVHardAngle = 70f;
                importer.secondaryUVPackMargin = 8f;
                importer.importNormals = ModelImporterNormals.Import;
                importer.globalScale = 1f;
                importer.SaveAndReimport();
            }
        }

        // ── Renderers and pipelines ─────────────────────────────────

        /// <summary>
        /// GB_Remake: the stock renderer (its ambient occlusion, set to contact distance) plus decals and the light in
        /// the air. GB_RemakeDither: GB_Expensive (A's stencil mark and 32-level dither, ambient occlusion) plus the same.
        /// </summary>
        private static bool EnsureRenderers(out UniversalRendererData remake, out UniversalRendererData remakeDither)
        {
            remake = CopyOnce(LookTestSetup.StockRendererPath, RemakeRendererPath);
            remakeDither = CopyOnce(LookTestSetup.ExpensiveRendererPath, RemakeDitherRendererPath);
            if (remake == null || remakeDither == null) return false;
            Shader volumetric = Shader.Find("Hidden/GB/Volumetric");
            if (volumetric == null) { Debug.LogError("[RemakeLookSetup] Hidden/GB/Volumetric not found."); return false; }
            foreach (UniversalRendererData data in new[] { remake, remakeDither })
            {
                List<ScriptableRendererFeature> features = data.rendererFeatures;
                foreach (ScriptableRendererFeature f in features)
                {
                    if (f is ScreenSpaceAmbientOcclusion)
                    {
                        // Contact distance: the creases and the feet, not a grey halo (the bake has the room's AO).
                        var so = new SerializedObject(f);
                        SerializedProperty s = so.FindProperty("m_Settings");
                        s.FindPropertyRelative("Intensity").floatValue = 1.6f;
                        s.FindPropertyRelative("Radius").floatValue = 0.22f;
                        s.FindPropertyRelative("Downsample").boolValue = false;
                        s.FindPropertyRelative("AfterOpaque").boolValue = false;
                        SerializedProperty direct = s.FindPropertyRelative("DirectLightingStrength");
                        if (direct != null) direct.floatValue = 0.35f;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        f.SetActive(true);
                    }
                }
                if (!features.Any(f => f is DecalRendererFeature))
                {
                    var decal = ScriptableObject.CreateInstance<DecalRendererFeature>();
                    decal.name = "Decals";
                    AddFeature(data, decal);
                }
                VolumetricLightFeature vol = features.OfType<VolumetricLightFeature>().FirstOrDefault();
                if (vol == null)
                {
                    vol = ScriptableObject.CreateInstance<VolumetricLightFeature>();
                    vol.name = "LightInTheAir";
                    AddFeature(data, vol);
                }
                vol.SetShader(volumetric);
                EditorUtility.SetDirty(vol);
                data.depthPrimingMode = DepthPrimingMode.Disabled;
                data.SetDirty();
                EditorUtility.SetDirty(data);
            }
            return true;
        }

        private static UniversalRendererData CopyOnce(string from, string to)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(to);
            if (data != null) return data;
            if (!AssetDatabase.CopyAsset(from, to)) { Debug.LogError($"[RemakeLookSetup] cannot copy {from}"); return null; }
            AssetDatabase.ImportAsset(to);
            data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(to);
            data.name = Path.GetFileNameWithoutExtension(to);
            return data;
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

        /// <summary>A copy of the game's URP asset with B's renderer list (stock, GB_Remake, GB_RemakeDither) and its shadow numbers.</summary>
        private static UniversalRenderPipelineAsset EnsurePipeline(string path, UniversalRendererData remake, UniversalRendererData dither, int atlas, int low, int medium, int high)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                AssetDatabase.CopyAsset(LookTestSetup.StockPipelinePath, path);
                AssetDatabase.ImportAsset(path);
                asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            }
            var stock = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(LookTestSetup.StockRendererPath);
            var so = new SerializedObject(asset);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            list.arraySize = 3;
            list.GetArrayElementAtIndex(0).objectReferenceValue = stock;
            list.GetArrayElementAtIndex(1).objectReferenceValue = remake;
            list.GetArrayElementAtIndex(2).objectReferenceValue = dither;
            so.FindProperty("m_DefaultRendererIndex").intValue = 1;
            so.FindProperty("m_RenderScale").floatValue = 1f;
            so.FindProperty("m_SupportsHDR").boolValue = true;
            so.FindProperty("m_MSAA").intValue = 4;
            so.FindProperty("m_SoftShadowsSupported").boolValue = false; // hard shadows (ART_LESSONS 1.3)
            so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = atlas;
            so.FindProperty("m_AdditionalLightsShadowResolutionTierLow").intValue = low;
            so.FindProperty("m_AdditionalLightsShadowResolutionTierMedium").intValue = medium;
            so.FindProperty("m_AdditionalLightsShadowResolutionTierHigh").intValue = high;
            so.FindProperty("m_ShadowDistance").floatValue = 40f;
            SerializedProperty grading = so.FindProperty("m_ColorGradingMode");
            if (grading != null) grading.intValue = (int)ColorGradingMode.HighDynamicRange;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // ── The bakes ────────────────────────────────────────────────

        private static void EnsureBakeSettings()
        {
            LightingSettings a = LookTestSetup.EnsureLightingSettings();
            // Low: A's numbers exactly (a copy of GB_LookBake), so switch 1 off shows A's bake.
            LightingSettings low = LoadOrCreate(BakeLowPath);
            EditorUtility.CopySerialized(a, low);
            low.name = "GB_RemakeBakeLow";
            EditorUtility.SetDirty(low);
            LightingSettings high = LoadOrCreate(BakeHighPath);
            EditorUtility.CopySerialized(a, high);
            high.name = "GB_RemakeBakeHigh";
            high.lightmapResolution = HighTexels;
            high.lightmapMaxSize = 4096;
            high.lightmapPadding = 4;
            high.directionalityMode = LightmapsMode.CombinedDirectional;
            high.maxBounces = 4;
            high.minBounces = 2;
            high.directSampleCount = 64;
            high.indirectSampleCount = 512;
            high.environmentSampleCount = 128;
            high.aoMaxDistance = 0.6f;
            high.aoExponentIndirect = 1.2f;
            high.lightProbeSampleCountMultiplier = 8f;
            EditorUtility.SetDirty(high);
        }

        private static LightingSettings LoadOrCreate(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (s != null) return s;
            s = new LightingSettings();
            AssetDatabase.CreateAsset(s, path);
            return s;
        }

        /// <summary>
        /// Two bakes in one run: the low one (A's numbers, the area light off), kept in RemakeBakeSwap with copies of its
        /// lightmaps, then the high one (B's), which stays in the scene. Minutes on the CPU lightmapper.
        /// Batch: tools/unity.sh exec GlorifiedBody.Editor.RemakeLookSetup.Bake
        /// </summary>
        /// <summary>The grade alone (switch 6), no new scene and no new bake. Batch: ...RemakeLookSetup.Grade</summary>
        [MenuItem("GlorifiedBody/T26 Remake Look/Grade only")]
        public static void Grade()
        {
            EnsureGrade();
            AssetDatabase.SaveAssets();
            Debug.Log("[RemakeLookSetup] grade written.");
        }

        [MenuItem("GlorifiedBody/T26 Remake Look/Bake (low, then high)")]
        public static void Bake()
        {
            if (!File.Exists(ScenePath)) { Debug.LogError("[RemakeLookSetup] build first."); return; }
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var low = AssetDatabase.LoadAssetAtPath<LightingSettings>(BakeLowPath);
            var high = AssetDatabase.LoadAssetAtPath<LightingSettings>(BakeHighPath);
            GameObject area = GameObject.Find("[RemakeB]/Lights/Remake_PanelArea");
            RemakeBakeSwap swap = Object.FindFirstObjectByType<RemakeBakeSwap>();
            if (low == null || high == null || swap == null) { Debug.LogError("[RemakeLookSetup] missing bake settings or RemakeBakeSwap."); return; }

            // 1. The low bake.
            if (area != null) area.SetActive(false);
            Lightmapping.SetLightingSettingsForScene(scene, low);
            double t0 = EditorApplication.timeSinceStartup;
            if (!BakeOnce(scene.name + " low")) return;
            double lowSeconds = EditorApplication.timeSinceStartup - t0;
            LightmapData[] maps = LightmapSettings.lightmaps;
            var lowColor = new List<Texture2D>();
            for (int i = 0; i < maps.Length; i++)
            {
                string src = AssetDatabase.GetAssetPath(maps[i].lightmapColor);
                string dst = $"{BakeFolder}/low_{i}{Path.GetExtension(src)}";
                AssetDatabase.DeleteAsset(dst);
                AssetDatabase.CopyAsset(src, dst);
                AssetDatabase.ImportAsset(dst);
                lowColor.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(dst));
            }
            var renderers = new List<Renderer>();
            var index = new List<int>();
            var offsets = new List<Vector4>();
            foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.lightmapIndex < 0 || r.lightmapIndex >= maps.Length) continue;
                renderers.Add(r);
                index.Add(r.lightmapIndex);
                offsets.Add(r.lightmapScaleOffset);
            }
            var probes = new List<float>();
            if (LightmapSettings.lightProbes != null)
            {
                foreach (SphericalHarmonicsL2 sh in LightmapSettings.lightProbes.bakedProbes)
                {
                    for (int c = 0; c < 3; c++)
                    for (int k = 0; k < 9; k++) probes.Add(sh[c, k]);
                }
            }
            swap.lowColor = lowColor.ToArray();
            swap.renderers = renderers.ToArray();
            swap.lowIndex = index.ToArray();
            swap.lowScaleOffset = offsets.ToArray();
            swap.lowProbes = probes.ToArray();
            EditorUtility.SetDirty(swap);
            Debug.Log($"[RemakeLookSetup] low bake: {maps.Length} lightmaps, {renderers.Count} renderers, {probes.Count / 27} probes, {lowSeconds:0} s.");

            // 2. The high bake, which stays.
            if (area != null) area.SetActive(true);
            Lightmapping.SetLightingSettingsForScene(scene, high);
            t0 = EditorApplication.timeSinceStartup;
            if (!BakeOnce(scene.name + " high")) return;
            int highProbes = LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0;
            int highMaps = LightmapSettings.lightmaps.Length;
            var sizes = string.Join(", ", LightmapSettings.lightmaps.Select(m => m.lightmapColor != null ? $"{m.lightmapColor.width}" : "?"));
            Debug.Log($"[RemakeLookSetup] high bake: {highMaps} lightmaps ({sizes}), {highProbes} probes (low had {probes.Count / 27}), {EditorApplication.timeSinceStartup - t0:0} s.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static bool BakeOnce(string label)
        {
            int count = 0;
            for (int attempt = 1; attempt <= 3 && count == 0; attempt++)
            {
                Lightmapping.Clear();
                Lightmapping.Bake();
                count = LightmapSettings.lightmaps != null ? LightmapSettings.lightmaps.Length : 0;
                if (count == 0) Debug.LogWarning($"[RemakeLookSetup] bake {label}: attempt {attempt} left no lightmaps.");
            }
            if (count == 0) Debug.LogError($"[RemakeLookSetup] bake {label} FAILED.");
            return count > 0;
        }

        // ── The grade (switch 6) ────────────────────────────────────

        private static VolumeProfile EnsureGrade()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, GradePath);
            }
            foreach (VolumeComponent c in profile.components.ToArray())
            {
                profile.components.Remove(c);
                Object.DestroyImmediate(c, true);
            }
            var tone = Add<Tonemapping>(profile);
            // Neutral, not ACES: ACES crushed the black room below A (frame mean 0.011 against A's 0.024 with the air off);
            // Neutral keeps the dark tones and still rolls the light's highlights off.
            tone.mode.Override(TonemappingMode.Neutral);
            var adjust = Add<ColorAdjustments>(profile);
            adjust.postExposure.Override(0.2f);
            adjust.contrast.Override(5f);
            adjust.saturation.Override(-10f);
            var split = Add<SplitToning>(profile);
            split.shadows.Override(new Color(0.49f, 0.52f, 0.53f));
            split.highlights.Override(new Color(0.55f, 0.52f, 0.47f));
            split.balance.Override(0f);
            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.62f);
            bloom.highQualityFiltering.Override(true);
            var grain = Add<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(0.25f);
            grain.response.Override(0.75f);
            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.25f);
            vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        // ── Materials: A (Simple Lit, the colour map only, as LookTest2) and B (Lit with normal and smoothness maps) ──

        /// <summary>One black-room surface in its two forms: A (the current look) and B (switch 4 on).</summary>
        private struct Pair
        {
            public Material a, b;
            public Pair(Material a, Material b) { this.a = a; this.b = b; }
        }

        private static Material SimpleMaterial(string name, Texture2D tex, Color tint, float smooth, Color spec, float tile = 1f,
            Color? emission = null, bool cutout = false, float alpha = 1f)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(LookTestSetup.SimpleLitShaderName);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.shaderKeywords = new string[0];
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", Vector2.one * tile);
            tint.a = alpha;
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", smooth);
            m.SetColor("_SpecColor", spec);
            m.SetFloat("_SpecularHighlights", 1f);
            m.EnableKeyword("_SPECULAR_COLOR");
            m.SetFloat("_SmoothnessSource", 0f);
            m.SetFloat("_ReceiveShadows", 1f);
            Emission(m, tex, emission);
            Surface(m, cutout, alpha < 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material LitMaterial(string name, System.Action<Material> setup)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (m == null)
            {
                m = new Material(lit);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = lit;
            m.shaderKeywords = new string[0];
            m.SetFloat("_WorkflowMode", 1f);
            m.SetFloat("_EnvironmentReflections", 1f);
            m.SetFloat("_SpecularHighlights", 1f);
            m.SetFloat("_ReceiveShadows", 1f);
            Surface(m, false, false);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>B: a Lit material from a 2k colour map with its _ms (metal, smoothness) and _n maps where they exist.</summary>
        private static Material LitMapped(string name, string tex, Color tint, float smoothScale, float metal, float bump, float tile = 1f, float alpha = 1f)
        {
            return LitMaterial(name, m =>
            {
                Texture2D baseMap = tex != null ? Tex(TexturesFolder, tex) : null;
                m.SetTexture("_BaseMap", baseMap);
                m.SetTextureScale("_BaseMap", Vector2.one * tile);
                tint.a = alpha;
                m.SetColor("_BaseColor", tint);
                m.SetFloat("_Metallic", metal);
                m.SetFloat("_Smoothness", smoothScale);
                Texture2D ms = tex != null ? Tex(TexturesFolder, tex + "_ms") : null;
                if (ms != null)
                {
                    m.SetTexture("_MetallicGlossMap", ms);
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                    m.SetFloat("_SmoothnessTextureChannel", 0f);
                }
                Texture2D n = tex != null ? Tex(TexturesFolder, tex + "_n") : null;
                if (n != null && bump > 0f)
                {
                    m.SetTexture("_BumpMap", n);
                    m.SetFloat("_BumpScale", bump);
                    m.EnableKeyword("_NORMALMAP");
                }
                if (alpha < 1f) Surface(m, false, true);
            });
        }

        private static void Emission(Material m, Texture tex, Color? emission)
        {
            if (emission.HasValue)
            {
                m.SetTexture("_EmissionMap", tex);
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
        }

        private static void Surface(Material m, bool cutout, bool transparent)
        {
            m.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            m.SetFloat("_Cutoff", 0.5f);
            if (cutout) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", (float)(cutout ? CullMode.Off : CullMode.Back));
            m.doubleSidedGI = cutout;
            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : "Opaque");
                m.renderQueue = cutout ? (int)RenderQueue.AlphaTest : -1;
            }
        }

        private static Texture2D Tex(string folder, string name)
        {
            foreach (string ext in new[] { ".png", ".jpg" })
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{name}{ext}");
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>
        /// The black-room material set (design §13.8, T22 textures, the W1 board): black tile with white grout on the walls,
        /// polished black granite on the floor, the black ceiling grid, chrome, black steel, black leather, glass, brass,
        /// amber glass, the char of the burned straps, the gear, the small ceiling light.
        /// </summary>
        private sealed class BlackSet
        {
            public Pair Tile, Granite, Ceiling, Chrome, BlackSteel, Leather, Glass, Brass, Amber, Char, Gear, Lens, Lamp, Cavity;
        }

        private static BlackSet EnsureBlackSet()
        {
            Color white = Color.white;
            Color spec(float v) => new Color(v, v, v);
            var s = new BlackSet();
            // Tiles of about 27 cm (11 rows on a 3 m wall, the board); the ceiling cells about 55 cm.
            const float tileScale = 1.25f, ceilingScale = 0.6f;
            s.Tile = new Pair(
                SimpleMaterial("RA_Tile", Tex(TexturesFolder, "blk_tile"), white, 0.85f, spec(0.9f), tileScale),
                LitMapped("RB_Tile", "blk_tile_2k", white, 1f, 0f, 0.6f, tileScale));
            s.Granite = new Pair(
                SimpleMaterial("RA_Granite", Tex(TexturesFolder, "blk_granite"), new Color(0.75f, 0.75f, 0.75f), 0.8f, spec(0.6f)),
                LitMapped("RB_Granite", "blk_granite_2k", white, 1f, 0f, 0.25f));
            s.Ceiling = new Pair(
                SimpleMaterial("RA_Ceiling", Tex(TexturesFolder, "blk_ceiling"), white, 0.2f, spec(0.15f), ceilingScale),
                LitMapped("RB_Ceiling", "blk_ceiling_2k", white, 1f, 0f, 0.8f, ceilingScale));
            s.Chrome = new Pair(
                SimpleMaterial("RA_Chrome", Tex(TexturesFolder, "blk_chrome"), new Color(0.5f, 0.5f, 0.52f), 0.9f, spec(1f)),
                LitMapped("RB_Chrome", "blk_chrome_2k", white, 1f, 1f, 0.3f));
            Color dark = new Color(0.12f, 0.12f, 0.13f);
            s.BlackSteel = new Pair(
                SimpleMaterial("RA_BlackSteel", Tex(TexturesFolder, "blk_chrome"), dark, 0.7f, spec(0.5f)),
                LitMapped("RB_BlackSteel", "blk_chrome_2k", dark, 0.85f, 0.7f, 0.3f));
            s.Leather = new Pair(
                SimpleMaterial("RA_Leather", Tex(TexturesFolder, "blk_leather"), white, 0.45f, spec(0.25f)),
                LitMapped("RB_Leather", "blk_leather_2k", white, 1f, 0f, 0.7f));
            s.Glass = new Pair(
                SimpleMaterial("RA_Glass", null, new Color(0.1f, 0.11f, 0.12f), 0.95f, spec(0.9f), alpha: 0.28f),
                LitMapped("RB_Glass", null, new Color(0.08f, 0.09f, 0.1f), 0.97f, 0f, 0f, alpha: 0.2f));
            Color brass = new Color(0.78f, 0.58f, 0.25f);
            s.Brass = new Pair(
                SimpleMaterial("RA_Brass", null, brass, 0.75f, new Color(0.9f, 0.75f, 0.4f)),
                LitMapped("RB_Brass", null, brass, 0.78f, 1f, 0f));
            Color amber = new Color(0.55f, 0.27f, 0.04f);
            s.Amber = new Pair(
                SimpleMaterial("RA_Amber", null, amber, 0.9f, spec(0.8f)),
                LitMapped("RB_Amber", null, amber, 0.95f, 0f, 0f));
            Color charred = new Color(0.035f, 0.026f, 0.02f);
            s.Char = new Pair(
                SimpleMaterial("RA_Char", null, charred, 0.1f, spec(0.05f)),
                LitMapped("RB_Char", null, charred, 0.12f, 0f, 0f));
            s.Gear = new Pair(
                SimpleMaterial("RA_Gear", Tex(TexturesFolder, "blk_gear"), white, 0.3f, spec(0.25f), cutout: true),
                LitMaterial("RB_Gear", m =>
                {
                    // the gear alone on the tiles (its black square cut away), as on the board
                    m.SetTexture("_BaseMap", Tex(TexturesFolder, "blk_gear_2k"));
                    m.SetColor("_BaseColor", white);
                    m.SetFloat("_Smoothness", 0.45f);
                    Surface(m, true, false);
                }));
            Color lensColour = new Color(0.02f, 0.02f, 0.025f);
            s.Lens = new Pair(
                SimpleMaterial("RA_Lens", null, lensColour, 0.95f, spec(0.9f)),
                LitMapped("RB_Lens", null, lensColour, 0.97f, 0f, 0f));
            Color warm = new Color(0.97f, 0.98f, 1f);
            s.Lamp = new Pair(
                SimpleMaterial("RA_Lamp", null, white, 0.1f, Color.black, emission: warm * 4f),
                LitMaterial("RB_Lamp", m =>
                {
                    m.SetColor("_BaseColor", white);
                    m.SetColor("_EmissionColor", warm * 6f);
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }));
            s.Cavity = new Pair(
                SimpleMaterial("RA_Cavity", null, new Color(0.01f, 0.01f, 0.01f), 0f, Color.black),
                LitMapped("RB_Cavity", null, new Color(0.01f, 0.01f, 0.01f), 0f, 0f, 0f));
            return s;
        }

        /// <summary>LookTest2's recovery-room materials and their black-room pair.</summary>
        private static Pair? PairFor(string gbName, BlackSet s)
        {
            switch (gbName)
            {
                case "GB_TileWall": return s.Tile;
                case "GB_TileFloor": return s.Granite;
                case "GB_ConcreteCeiling": return s.Ceiling;
                case "GB_Steel": return s.Chrome;
                case "GB_DarkSteel": return s.BlackSteel;
                case "GB_Skirting": return s.Granite;
                case "GB_Mattress": return s.Leather;
                case "GB_Leather": return s.Leather;
                case "GB_Glass": return s.Glass;
                case "GB_GearEmblem": return s.Gear;
                default: return null;
            }
        }

        private static Pair PropPair(string prop, float smooth, float metal)
        {
            Texture2D baseMap = Tex(PropsFolder, prop + "_base_blk");
            Material a = SimpleMaterial("RA_prop_" + prop, baseMap, Color.white, Mathf.Min(0.8f, smooth), new Color(metal > 0.5f ? 0.85f : 0.4f, metal > 0.5f ? 0.85f : 0.4f, metal > 0.5f ? 0.85f : 0.4f));
            Material b = LitMaterial("RB_prop_" + prop, m =>
            {
                m.SetTexture("_BaseMap", baseMap);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Metallic", metal);
                m.SetFloat("_Smoothness", smooth);
                m.SetTexture("_BumpMap", Tex(PropsFolder, prop + "_n"));
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            });
            return new Pair(a, b);
        }

        private static Material DecalMaterial(string texture, float drawOrder = 0f)
        {
            string path = $"{MaterialsFolder}/RB_{texture}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Shader Graphs/Decal");
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetTexture("Base_Map", Tex(TexturesFolder, texture));
            if (m.HasProperty("_AffectNormal")) m.SetFloat("_AffectNormal", 0f);
            m.DisableKeyword("_MATERIAL_AFFECTS_NORMAL");
            if (m.HasProperty("_AffectAlbedo")) m.SetFloat("_AffectAlbedo", 1f);
            m.EnableKeyword("_MATERIAL_AFFECTS_ALBEDO");
            if (m.HasProperty("_DrawOrder")) m.SetFloat("_DrawOrder", drawOrder);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material CharacterMaterial(string classId, string texture)
        {
            var original = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Game/Characters/Materials/{classId}.mat");
            string path = $"{MaterialsFolder}/RB_{classId}_4k.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = original != null ? new Material(original) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            else if (original != null) m.CopyPropertiesFromMaterial(original);
            m.SetTexture("_BaseMap", Tex(CharactersFolder, texture));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Lucy's canon look (T20 C3, the sunken gold veins, under the T21 Crown) on the game's lucy_bare mesh: the
        /// same mesh and UVs, so only the maps change (tools/remake_lucy_c3.py). 4k for B; 1k for A (the game's
        /// character size), in Resources so the bot can find it.
        /// </summary>
        private static Material LucyC3Material(string tag, string folder)
        {
            Directory.CreateDirectory(folder);
            string path = $"{folder}/RB_lucy_c3_{tag}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (m == null)
            {
                m = new Material(lit);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = lit;
            m.shaderKeywords = new string[0];
            m.SetTexture("_BaseMap", Tex(CharactersFolder, $"lucy_c3_base_{tag}"));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_WorkflowMode", 1f);
            m.SetTexture("_MetallicGlossMap", Tex(CharactersFolder, $"lucy_c3_ms_{tag}"));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetTexture("_BumpMap", Tex(CharactersFolder, $"lucy_c3_n_{tag}"));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ── The scene ────────────────────────────────────────────────

        // The one small ceiling light of W1 (DEMO_PLAN), a little toward the bed: a pool on the floor where the figures stand.
        public static readonly Vector3 DownlightPos = new Vector3(5.1f, 3.2f, 5.4f);

        private static readonly List<Renderer> surfaceRenderers = new List<Renderer>();
        private static readonly List<Material> matsA = new List<Material>();
        private static readonly List<Material> matsB = new List<Material>();

        private static void Register(Renderer r, Pair p)
        {
            r.sharedMaterial = p.a; // the scene is saved in A's dress; the director puts B's on
            surfaceRenderers.Add(r);
            matsA.Add(p.a);
            matsB.Add(p.b);
        }

        private static GameObject Part(GameObject parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Pair p, Quaternion? rotation = null, bool collider = false, bool casts = true)
        {
            GameObject g = LookTestSetup.Block(parent, name, type, position, scale, p.a, rotation ?? Quaternion.identity);
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            Renderer r = g.GetComponent<Renderer>();
            r.shadowCastingMode = casts ? ShadowCastingMode.On : ShadowCastingMode.Off;
            Register(r, p);
            return g;
        }

        private static void BuildScene(UniversalRenderPipelineAsset high, UniversalRenderPipelineAsset low, UniversalRenderPipelineAsset mid, UniversalRendererData remake, UniversalRendererData dither, VolumeProfile grade)
        {
            // A fresh copy of LookTest2 every build (LookTest2 is only read). Both profiles live in this scene.
            AssetDatabase.DeleteAsset(ScenePath);
            string lightingFolder = MenuLoopSetup.ScenesFolder + "/" + SceneName;
            if (AssetDatabase.IsValidFolder(lightingFolder)) AssetDatabase.DeleteAsset(lightingFolder);
            if (!AssetDatabase.CopyAsset(LookTestSetup.Scene2Path, ScenePath)) { Debug.LogError("[RemakeLookSetup] cannot copy LookTest2."); return; }
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Lightmapping.lightingDataAsset = null;
            Lightmapping.SetLightingSettingsForScene(scene, AssetDatabase.LoadAssetAtPath<LightingSettings>(BakeHighPath));
            surfaceRenderers.Clear();
            matsA.Clear();
            matsB.Clear();

            Object.DestroyImmediate(GameObject.Find("[Look]")); // RemakeDirector runs both profiles
            // The white-tile room's pieces that W1 replaces: the panel light, the planter, the cabinet, the gear on the
            // west wall (no camera sees that wall), the old straps.
            var gone = new List<string> { "Recovery_Planter", "Recovery_Soil", "Recovery_Fern_a", "Recovery_Fern_b", "Recovery_LightPanel",
                "Recovery_Light", "Cabinet", "Cabinet_Glass", "Recovery_Panel" };
            foreach (float sz in new[] { -0.6f, -0.1f, 0.4f }) { gone.Add($"Strap_W_{sz}"); gone.Add($"Strap_E_{sz}"); }
            foreach (string name in gone)
            {
                GameObject g = GameObject.Find(name);
                if (g != null) Object.DestroyImmediate(g);
            }

            BlackSet set = EnsureBlackSet();
            // No sky in a closed room: LookTest2 keeps Unity's default sky, and B's polished floor blended its blue-grey into
            // the reflection at the room's edge. A black default reflection, the room's probe for everything inside.
            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            GameObject b = MenuLoopSetup.EnsureRoot("[RemakeB]");
            GameObject w1 = MenuLoopSetup.EnsureRoot("[W1]"); // the black room's own pieces (both profiles)
            GameObject lights = MenuLoopSetup.EnsureChild(b, "Lights");
            GameObject decals = MenuLoopSetup.EnsureChild(b, "Decals");
            GameObject live = MenuLoopSetup.EnsureChild(b, "Live");
            GameObject air = MenuLoopSetup.EnsureChild(b, "Air");

            // ── The room's surfaces: LookTest2's recovery room in black. Lightmap density: the room at 40 texels a metre in
            // the high bake, everything else at a quarter.
            Bounds room = new Bounds((RoomMin + RoomMax) * 0.5f, RoomMax - RoomMin + Vector3.one * 0.7f);
            GameObject roomRoot = GameObject.Find("[Room]");
            int recoloured = 0;
            foreach (Renderer r in roomRoot.GetComponentsInChildren<Renderer>(true))
            {
                bool inRoom = room.Contains(r.bounds.center) && r.bounds.size.magnitude < 12f;
                var so = new SerializedObject(r);
                SerializedProperty scale = so.FindProperty("m_ScaleInLightmap");
                if (scale != null) { scale.floatValue = inRoom ? 1f : 10f / HighTexels; so.ApplyModifiedPropertiesWithoutUndo(); }
                if (!inRoom || r.sharedMaterial == null) continue;
                Pair? p = PairFor(r.sharedMaterial.name, set);
                if (p == null) continue;
                Register(r, p.Value);
                recoloured++;
            }

            BuildFurniture(w1, set);

            // ── The light (switch 2 sets its shadow map): the one small ceiling light, a hard spot straight down. Live and
            // hard, the bounce baked (Mixed).
            Part(w1, "W1_Downlight_Ring", PrimitiveType.Cylinder, DownlightPos + new Vector3(0f, -0.015f, 0f), new Vector3(0.34f, 0.015f, 0.34f), set.Chrome, casts: false);
            GameObject disc = Part(w1, "W1_Downlight_Disc", PrimitiveType.Cylinder, DownlightPos + new Vector3(0f, -0.032f, 0f), new Vector3(0.24f, 0.004f, 0.24f), set.Lamp, casts: false);
            GameObject spotObj = MenuLoopSetup.EnsureChild(lights, "W1_Downlight");
            spotObj.transform.SetPositionAndRotation(DownlightPos + new Vector3(0f, -0.06f, 0f), Quaternion.Euler(90f, 0f, 0f));
            Light spot = MenuLoopSetup.EnsureComponent<Light>(spotObj);
            spot.type = LightType.Spot;
            // A neutral white (a ward light, the board): the warm white of the first frames turned the guard's grey-blue to brown-grey.
            spot.color = new Color(0.97f, 0.98f, 1f);
            spot.intensity = 14f;
            spot.range = 7f;
            // Wide (120 degrees): at 92 the cone stopped short of the cabinet, the fern and the drip stand on the north
            // wall, and W1's props went black in every frame. The falloff with distance still leaves the walls black.
            spot.spotAngle = 120f;
            spot.innerSpotAngle = 60f;
            spot.shadows = LightShadows.Hard;
            spot.shadowStrength = 1f;
            spot.shadowBias = 0.01f;
            spot.shadowNormalBias = 0.2f;
            spot.shadowNearPlane = 0.1f;
            spot.lightmapBakeType = LightmapBakeType.Mixed;
            ShadowTier(spot, UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh);
            // A thin cone: at 1.0 the cone washed the black room out and hid the guard (first frames of the restart).
            MenuLoopSetup.EnsureComponent<VolumetricLight>(spotObj).strength = 0.08f;

            // The lights outside the room drop to the low shadow tier: from inside the room they still count in the
            // atlas (their range reaches through the walls) and would halve the key light.
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l == spot || room.Contains(l.transform.position)) continue;
                ShadowTier(l, UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow);
            }

            // Switch 1: the fixture's glow as a small disc area light, baked only (the high bake; off in the low one).
            GameObject areaObj = MenuLoopSetup.EnsureChild(lights, "Remake_PanelArea");
            areaObj.transform.SetPositionAndRotation(DownlightPos + new Vector3(0f, -0.04f, 0f), Quaternion.Euler(90f, 0f, 0f));
            Light areaLight = MenuLoopSetup.EnsureComponent<Light>(areaObj);
            areaLight.type = LightType.Disc;
            areaLight.areaSize = new Vector2(0.12f, 0.12f);
            areaLight.color = spot.color;
            areaLight.intensity = 3f;
            areaLight.range = 8f;
            areaLight.lightmapBakeType = LightmapBakeType.Baked;
            areaLight.shadows = LightShadows.Soft;

            // ── Decals (switch 4): what a clean ward shows after this night. The scorch where each strap burned through, on
            // the mattress and on the floor under the charred ends; a faint mop haze on the polished floor; trolley scuffs by
            // the door. No grime: the patient rooms are clean (bible).
            Material scorch = DecalMaterial("decal_scorch", 2f);
            Quaternion down = Quaternion.Euler(90f, 0f, 0f);
            foreach ((float x, float z, float size) in new[] { (6.25f, 5.75f, 0.32f), (6.95f, 6.0f, 0.3f), (6.25f, 6.55f, 0.3f), (6.95f, 6.75f, 0.28f), (6.6f, 6.95f, 0.36f) })
            {
                Decal(decals, $"Scorch_Bed_{x}_{z}", scorch, new Vector3(x, 0.75f, z), down * Quaternion.Euler(0f, 0f, x * 90f), new Vector3(size, size, 0.2f));
            }
            Decal(decals, "Scorch_Floor_W", scorch, new Vector3(6.05f, 0.02f, 6.15f), down, new Vector3(0.22f, 0.22f, 0.2f));
            Decal(decals, "Mop_Haze", DecalMaterial("decal_mop", 0f), new Vector3(4.6f, 0.02f, 5.4f), down, new Vector3(4.4f, 4.4f, 0.2f));
            Decal(decals, "Scuffs_Door", DecalMaterial("decal_scuffs", 1f), new Vector3(2.95f, 0.02f, 5.0f), down * Quaternion.Euler(0f, 0f, 90f), new Vector3(1.2f, 1.2f, 0.2f));

            // ── Contact (switch 3): the foot blobs.
            GameObject contact = MenuLoopSetup.EnsureChild(b, "Contact");
            FootContactShadows feet = MenuLoopSetup.EnsureComponent<FootContactShadows>(contact);
            feet.decalMaterial = DecalMaterial("decal_contact", 3f);

            // ── Denser probes for the characters (switch 3): a 0.75 m grid at the knee, the chest and the head.
            GameObject probesObj = MenuLoopSetup.EnsureChild(b, "Probes");
            LightProbeGroup group = MenuLoopSetup.EnsureComponent<LightProbeGroup>(probesObj);
            var positions = new List<Vector3>();
            for (float x = RoomMin.x + 0.4f; x < RoomMax.x - 0.2f; x += 0.75f)
            for (float z = RoomMin.z + 0.4f; z < RoomMax.z - 0.2f; z += 0.75f)
            foreach (float y in new[] { 0.35f, 1.1f, 1.75f, 2.6f })
                positions.Add(new Vector3(x, y, z) - probesObj.transform.position);
            group.probePositions = positions.ToArray();

            // ── The light in the air (switch 5): the volumetric pass reads the marked spot; the dust in its cone.
            Dust(air, "Dust_Cone", DownlightPos + new Vector3(0f, -1.5f, 0f), new Vector3(2.4f, 2.8f, 2.4f), 320);

            // ── The live layers (switch 7): the light stutters now and then; the fan turns behind the round vent grille.
            LightFlicker flicker = MenuLoopSetup.EnsureComponent<LightFlicker>(spotObj);
            flicker.target = spot;
            flicker.emitter = disc.GetComponent<Renderer>();
            Spinner fan = VentFan(live, w1, set);

            // ── Reflections (switch 8): one baked, box-projected probe for the room (the polished floor, the chrome).
            GameObject probeObj = MenuLoopSetup.EnsureChild(b, "ReflectionProbe");
            probeObj.transform.position = new Vector3((RoomMin.x + RoomMax.x) * 0.5f, 1.4f, (RoomMin.z + RoomMax.z) * 0.5f);
            ReflectionProbe reflection = MenuLoopSetup.EnsureComponent<ReflectionProbe>(probeObj);
            reflection.mode = ReflectionProbeMode.Baked;
            reflection.boxProjection = true;
            reflection.size = RoomMax - RoomMin + new Vector3(0.6f, 0.6f, 0.6f);
            reflection.center = new Vector3(0f, (RoomMin.y + RoomMax.y) * 0.5f - 1.4f, 0f);
            reflection.resolution = 512;
            reflection.hdr = true;
            reflection.importance = 2;
            reflection.blendDistance = 0.2f;

            // ── The grade (switch 6).
            GameObject gradeObj = MenuLoopSetup.EnsureChild(b, "Grade");
            Volume volume = MenuLoopSetup.EnsureComponent<Volume>(gradeObj);
            volume.isGlobal = true;
            volume.sharedProfile = grade;
            volume.priority = 10f;

            // ── The long lens (switch 10).
            GameObject camerasRoot = GameObject.Find("[Cameras]");
            FixedCamera longCam = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Recovery_Long", LongCamPos, LongCamAt, RemakeDirector.LongLensFov);

            // ── The B characters: the full-mesh guard when its build exists (else the 2,500 guard with the 4096 map).
            RemakeBodySwap guardSwap = null;
            GameObject guardFull = BuildFullMeshPrefab("guard_full", "guard", 1.80f);
            if (guardFull != null)
            {
                guardSwap = MenuLoopSetup.EnsureComponent<RemakeBodySwap>(MenuLoopSetup.EnsureChild(b, "Swap_Guard"));
                guardSwap.rootName = "Figure_Guard";
                guardSwap.bodyB = guardFull;
            }

            // ── The director.
            RemakeBakeSwap swap = MenuLoopSetup.EnsureComponent<RemakeBakeSwap>(b);
            RemakeDirector director = MenuLoopSetup.EnsureComponent<RemakeDirector>(b);
            director.pipelineHigh = high;
            director.pipelineLow = low;
            director.pipelineMid = mid;
            director.clearRendererIndex = 1;
            director.ditherRendererIndex = 2;
            director.ssaoFeatures = remake.rendererFeatures.Concat(dither.rendererFeatures).Where(f => f is ScreenSpaceAmbientOcclusion).ToArray();
            director.bakeSwap = swap;
            director.contactObjects = new[] { contact };
            director.surfaceRenderers = surfaceRenderers.ToArray();
            director.surfaceMaterialsA = matsA.ToArray();
            director.surfaceMaterialsB = matsB.ToArray();
            director.decals = new[] { decals };
            director.volumetricObjects = new[] { air };
            director.gradeVolume = volume;
            director.liveBehaviours = new Behaviour[] { flicker, fan };
            director.liveCloth = new Cloth[0];
            director.reflectionProbes = new[] { reflection };
            director.longCamera = longCam;
            director.characterMaterialsA = new[] { LucyC3Material("1k", Root + "/Resources"), AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Characters/Materials/guard.mat") };
            director.characterMaterials = new[] { LucyC3Material("4k", MaterialsFolder), CharacterMaterial("guard", "guard_basecolor_4k") };
            director.bodySwaps = guardSwap != null ? new[] { guardSwap } : new RemakeBodySwap[0];
            EditorUtility.SetDirty(director);

            // No static batching in this scene: a batch burns each renderer's lightmap scale and offset into its mesh,
            // so the swap to the low bake (profile A, switch 1 off) sampled A's lightmaps with B's layout (30,174
            // warnings in the first run of the restart). The room is small: the draw calls cost little, in both profiles.
            int unbatched = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
                unbatched++;
            }
            Debug.Log($"[RemakeLookSetup] static batching off on {unbatched} renderers.");

            Camera main = Camera.main;
            if (main != null)
            {
                main.allowHDR = true;
                main.allowMSAA = true;
                main.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var scenes = EditorBuildSettings.scenes.ToList();
            MenuLoopSetup.EnsureBuildScene(scenes, ScenePath);
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[RemakeLookSetup] {ScenePath}: {recoloured} room surfaces in black, {surfaceRenderers.Count} A/B surfaces in all, {positions.Count} extra probes, guard B = {(guardFull != null ? "full mesh" : "2,500 mesh with the 4096 map")}.");
        }

        /// <summary>
        /// W1's furniture on LookTest2's bed frame (DEMO_PLAN W1, the T22 board): the chrome bed with a black leather mattress
        /// and brass joints, the straps burned through from inside, the drip stand, the cabinet with amber ampoules, the
        /// locker with the gear, the fern planter, the dead camera, the gear on the wall, the round vent.
        /// </summary>
        private static void BuildFurniture(GameObject w1, BlackSet set)
        {
            Vector3 bed = new Vector3(RoomMax.x - 0.7f, 0f, 6.3f);
            // Brass at the joints of the frame (the board).
            foreach (var j in new[] { (-0.44f, 0.98f, 1.05f), (0.44f, 0.98f, 1.05f), (-0.44f, -0.98f, 0.95f), (0.44f, -0.98f, 0.95f) })
            {
                Part(w1, $"Bed_Brass_{j.Item1}_{j.Item2}", PrimitiveType.Cylinder, bed + new Vector3(j.Item1, j.Item3 - 0.1f, j.Item2), new Vector3(0.06f, 0.025f, 0.06f), set.Brass);
                Part(w1, $"Bed_BrassLow_{j.Item1}_{j.Item2}", PrimitiveType.Cylinder, bed + new Vector3(j.Item1, 0.5f, j.Item2), new Vector3(0.06f, 0.025f, 0.06f), set.Brass);
            }
            // The straps: three each side, the leather hanging from the mattress, the end charred and torn short (burned
            // through from inside), a brass buckle on the stub that stays on the mattress.
            float[] zs = { -0.6f, -0.1f, 0.4f };
            for (int i = 0; i < zs.Length; i++)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    float z = zs[i] + (side > 0 ? 0.2f : 0f);
                    float hang = 0.36f + 0.06f * ((i + (side > 0 ? 1 : 0)) % 3);
                    float x = side * 0.5f;
                    string id = $"{(side < 0 ? "W" : "E")}_{i}";
                    Part(w1, $"Strap_{id}", PrimitiveType.Cube, bed + new Vector3(x, 0.71f - hang * 0.5f, z), new Vector3(0.012f, hang, 0.055f), set.Leather, Quaternion.Euler(0f, 0f, side * 4f));
                    Part(w1, $"Strap_{id}_Char", PrimitiveType.Cube, bed + new Vector3(x + side * 0.004f, 0.71f - hang - 0.025f, z), new Vector3(0.016f, 0.05f, 0.06f), set.Char, Quaternion.Euler(0f, 0f, side * 4f + 9f));
                    Part(w1, $"Strap_{id}_Stub", PrimitiveType.Cube, bed + new Vector3(x - side * 0.09f, 0.716f, z), new Vector3(0.18f, 0.008f, 0.055f), set.Leather);
                    Part(w1, $"Strap_{id}_StubChar", PrimitiveType.Cube, bed + new Vector3(x - side * 0.2f, 0.717f, z), new Vector3(0.05f, 0.009f, 0.06f), set.Char);
                    Part(w1, $"Strap_{id}_Buckle", PrimitiveType.Cube, bed + new Vector3(x - side * 0.03f, 0.722f, z), new Vector3(0.04f, 0.01f, 0.07f), set.Brass);
                }
            }
            // The drip stand: LookTest2's pole and base in chrome; five castors and an empty bag.
            Vector3 drip = new Vector3(5.6f, 0f, 7.6f);
            for (int k = 0; k < 5; k++)
            {
                Quaternion turn = Quaternion.Euler(0f, k * 72f, 0f);
                Part(w1, $"Drip_Leg_{k}", PrimitiveType.Cube, drip + turn * new Vector3(0f, 0.06f, 0.16f), new Vector3(0.025f, 0.02f, 0.32f), set.Chrome, turn);
                Part(w1, $"Drip_Castor_{k}", PrimitiveType.Sphere, drip + turn * new Vector3(0f, 0.025f, 0.3f), Vector3.one * 0.05f, set.BlackSteel);
            }
            Part(w1, "Drip_Bag", PrimitiveType.Cube, drip + new Vector3(0.14f, 1.68f, 0f), new Vector3(0.12f, 0.2f, 0.025f), set.Glass, casts: false);

            // The cabinet with the amber ampoules (north wall): black steel, a glass front, two shelves of ampoules, three
            // bottles on top.
            Vector3 cab = new Vector3(4.5f, 0f, RoomMax.z - 0.22f);
            Part(w1, "Cab_Back", PrimitiveType.Cube, cab + new Vector3(0f, 0.55f, 0.18f), new Vector3(0.9f, 1.1f, 0.03f), set.BlackSteel, collider: true);
            Part(w1, "Cab_Side_W", PrimitiveType.Cube, cab + new Vector3(-0.435f, 0.55f, 0f), new Vector3(0.03f, 1.1f, 0.4f), set.BlackSteel, collider: true);
            Part(w1, "Cab_Side_E", PrimitiveType.Cube, cab + new Vector3(0.435f, 0.55f, 0f), new Vector3(0.03f, 1.1f, 0.4f), set.BlackSteel, collider: true);
            Part(w1, "Cab_Top", PrimitiveType.Cube, cab + new Vector3(0f, 1.085f, 0f), new Vector3(0.9f, 0.03f, 0.4f), set.BlackSteel);
            Part(w1, "Cab_Base", PrimitiveType.Cube, cab + new Vector3(0f, 0.08f, 0f), new Vector3(0.9f, 0.16f, 0.4f), set.BlackSteel, collider: true);
            Part(w1, "Cab_Glass", PrimitiveType.Cube, cab + new Vector3(0f, 0.62f, -0.19f), new Vector3(0.84f, 0.9f, 0.006f), set.Glass, casts: false);
            Part(w1, "Cab_Mullion", PrimitiveType.Cube, cab + new Vector3(0f, 0.62f, -0.195f), new Vector3(0.02f, 0.9f, 0.012f), set.BlackSteel);
            Part(w1, "Cab_Handle_W", PrimitiveType.Cube, cab + new Vector3(-0.04f, 0.65f, -0.205f), new Vector3(0.012f, 0.12f, 0.012f), set.Chrome);
            Part(w1, "Cab_Handle_E", PrimitiveType.Cube, cab + new Vector3(0.04f, 0.65f, -0.205f), new Vector3(0.012f, 0.12f, 0.012f), set.Chrome);
            foreach (float y in new[] { 0.45f, 0.77f })
            {
                Part(w1, $"Cab_Shelf_{y}", PrimitiveType.Cube, cab + new Vector3(0f, y, 0f), new Vector3(0.84f, 0.012f, 0.36f), set.Glass, casts: false);
                for (int k = 0; k < 9; k++)
                {
                    float x = -0.34f + k * 0.085f;
                    float zz = (k % 2 == 0) ? -0.02f : 0.07f;
                    Part(w1, $"Ampoule_{y}_{k}", PrimitiveType.Cylinder, cab + new Vector3(x, y + 0.055f, zz), new Vector3(0.028f, 0.05f, 0.028f), set.Amber);
                }
            }
            foreach ((float x, float h) in new[] { (0.2f, 0.11f), (0.27f, 0.09f), (0.33f, 0.12f) })
            {
                Part(w1, $"Bottle_{x}", PrimitiveType.Cylinder, cab + new Vector3(x, 1.1f + h, -0.02f), new Vector3(0.05f, h, 0.05f), set.Amber);
                Part(w1, $"Bottle_{x}_Cap", PrimitiveType.Cylinder, cab + new Vector3(x, 1.1f + 2f * h + 0.012f, -0.02f), new Vector3(0.03f, 0.012f, 0.03f), set.BlackSteel);
            }

            // The gear on the east wall, south of the bed (both cameras see it), and the round vent over the bed head.
            Part(w1, "W1_Gear", PrimitiveType.Quad, new Vector3(RoomMax.x - 0.012f, 1.95f, 4.35f), new Vector3(0.6f, 0.6f, 1f), set.Gear, Quaternion.Euler(0f, 90f, 0f), casts: false);
            // The dead camera in the south-east corner: LookTest2's body (black steel now) turned to look into the room, a
            // dark lens at its front, no light.
            GameObject camBody = GameObject.Find("Recovery_Camera");
            if (camBody != null)
            {
                Vector3 look = new Vector3(-1f, -0.6f, 1f).normalized;
                camBody.transform.rotation = Quaternion.LookRotation(look);
                Part(w1, "W1_Camera_Lens", PrimitiveType.Cylinder, camBody.transform.position + look * 0.145f, new Vector3(0.075f, 0.008f, 0.075f), set.Lens,
                    Quaternion.LookRotation(look) * Quaternion.Euler(90f, 0f, 0f), casts: false);
            }

            // The 3D Studio props of the first pass (meshes only), in black-room colours.
            // The locker against the east wall, south of the gear: on the south wall it stood in front of the long camera.
            Prop(w1, "locker", "cabinet", new Vector3(RoomMax.x - 0.33f, 0f, 3.25f), -90f, 1f, PropPair("cabinet", 0.6f, 0.4f));
            Prop(w1, "planter_fern", "planter_fern", new Vector3(RoomMin.x + 0.55f, 0f, RoomMax.z - 0.6f), 20f, 0.82f, PropPair("planter_fern", 0.55f, 0f));
            Prop(w1, "tray_set", "tray_set", new Vector3(4.32f, 1.1f, RoomMax.z - 0.24f), 15f, 0.25f, PropPair("tray_set", 0.8f, 1f));
        }

        private static GameObject Prop(GameObject parent, string name, string model, Vector3 position, float yaw, float scale, Pair p)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropsFolder}/{model}.fbx");
            if (asset == null) { Debug.LogError($"[RemakeLookSetup] no prop {model}.fbx"); return null; }
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.transform);
            obj.name = "Prop_" + name;
            obj.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f) * obj.transform.rotation); // the root carries the axis turn
            obj.transform.localScale = obj.transform.localScale * scale; // the FBX root carries the unit conversion (x100)
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                if (r is MeshRenderer mr) mr.receiveGI = ReceiveGI.Lightmaps;
                var so = new SerializedObject(r);
                SerializedProperty s = so.FindProperty("m_ScaleInLightmap");
                if (s != null) { s.floatValue = 0.5f; so.ApplyModifiedPropertiesWithoutUndo(); }
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
                if (r.GetComponent<MeshCollider>() == null && name != "tray_set") r.gameObject.AddComponent<MeshCollider>();
                Register(r, p);
            }
            Bounds bounds = obj.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; });
            Debug.Log($"[RemakeLookSetup] prop {name}: centre {bounds.center.ToString("F3")} size {bounds.size.ToString("F3")} min {bounds.min.ToString("F3")} root scale {obj.transform.localScale.ToString("F3")}");
            return obj;
        }

        /// <summary>
        /// The B guard: the full-mesh 3D Studio build (no face limit, T26 restart) imported Humanoid from
        /// Remake/Characters/&lt;id&gt;.fbx, with its own 4096 maps, the game's controller of the class (the Humanoid
        /// retarget plays the same clips), scaled to the class height. Null when the FBX is not there.
        /// </summary>
        private static GameObject BuildFullMeshPrefab(string id, string classId, float height)
        {
            string fbx = $"{CharactersFolder}/{id}.fbx";
            if (!File.Exists(fbx)) { Debug.LogWarning($"[RemakeLookSetup] no {fbx}: B uses the 2,500 {classId} with the 4096 map."); return null; }
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importAnimation = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.indexFormat = ModelImporterIndexFormat.UInt32;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                int bar = clip.takeName.LastIndexOf('|');
                clip.name = bar >= 0 ? clip.takeName.Substring(bar + 1) : clip.takeName;
                clip.loopTime = true;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            Material material = LitMaterial($"RB_{id}", m =>
            {
                m.SetTexture("_BaseMap", Tex(CharactersFolder, $"{id}_base"));
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", 0.25f);
                Texture2D n = Tex(CharactersFolder, $"{id}_n");
                if (n != null)
                {
                    m.SetTexture("_BumpMap", n);
                    m.SetFloat("_BumpScale", 1f);
                    m.EnableKeyword("_NORMALMAP");
                }
                m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            });
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"Assets/_Game/Characters/Anim/{classId}.controller");
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var root = new GameObject(id);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Model";
            instance.transform.SetParent(root.transform, false);
            Bounds bounds = instance.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; });
            // 3D Studio delivers every figure 1.0 m tall (the game divides by the same 1.0, Body.ModelSourceHeight); the
            // renderer bounds add a little slack, so they only go into the log.
            float source = 1.0f;
            Debug.Log($"[RemakeLookSetup] {id}: renderer bounds {bounds.size.y:0.000} m in the source.");
            instance.transform.localScale = Vector3.one * (height / source);
            Animator animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            int tris = 0;
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer sk) { sk.updateWhenOffscreen = true; tris += sk.sharedMesh != null ? sk.sharedMesh.triangles.Length / 3 : 0; }
            }
            string avatar = animator.avatar != null && animator.avatar.isHuman ? "human" : "MISSING";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{CharactersFolder}/{id}.prefab");
            Object.DestroyImmediate(root);
            Debug.Log($"[RemakeLookSetup] {id}: {tris} triangles, source height {source:0.000} m, scale {height / source:0.000}, avatar {avatar}, controller {(controller != null ? controller.name : "MISSING")}.");
            return prefab;
        }

        private static void Decal(GameObject parent, string name, Material material, Vector3 position, Quaternion rotation, Vector3 size)
        {
            GameObject obj = MenuLoopSetup.EnsureChild(parent, name);
            obj.transform.SetPositionAndRotation(position, rotation);
            DecalProjector p = MenuLoopSetup.EnsureComponent<DecalProjector>(obj);
            p.material = material;
            p.size = size;
            p.pivot = Vector3.zero;
            p.fadeFactor = 1f;
            p.drawDistance = 60f;
        }

        private static void Dust(GameObject parent, string name, Vector3 centre, Vector3 box, int count)
        {
            GameObject obj = MenuLoopSetup.EnsureChild(parent, name);
            obj.transform.SetPositionAndRotation(centre, Quaternion.identity);
            ParticleSystem ps = MenuLoopSetup.EnsureComponent<ParticleSystem>(obj);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.02f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.011f);
            main.startColor = new Color(1f, 0.97f, 0.9f, 0.55f);
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.0005f;
            var emission = ps.emission;
            emission.rateOverTime = count / 11f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.03f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.05f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.sharedMaterial = DustMaterial();
        }

        /// <summary>Lit particles, additive: a mote shows only where the light reaches it (in the cone).</summary>
        private static Material DustMaterial()
        {
            string path = $"{MaterialsFolder}/RB_dust.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetTexture("_BaseMap", Tex(TexturesFolder, "dust_mote"));
            m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f); // additive
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetFloat("_ReceiveShadows", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The round vent high on the north wall over the bed head (the board): a chrome ring, a dark throat, a
        /// grille of bars, a four-blade fan turning behind it (switch 7).</summary>
        private static Spinner VentFan(GameObject live, GameObject w1, BlackSet set)
        {
            Vector3 c = new Vector3(6.35f, 2.55f, RoomMax.z - 0.01f);
            Quaternion face = Quaternion.Euler(90f, 0f, 0f); // a cylinder's axis along z: into the wall
            // The ring stands 6 mm out of the wall, the dark throat 2 mm in front of the ring's face, the bars in front of both.
            Part(w1, "Vent_Ring", PrimitiveType.Cylinder, c + new Vector3(0f, 0f, 0.004f), new Vector3(0.42f, 0.01f, 0.42f), set.Chrome, face, casts: false);
            Part(w1, "Vent_Throat", PrimitiveType.Cylinder, c + new Vector3(0f, 0f, -0.004f), new Vector3(0.36f, 0.004f, 0.36f), set.Cavity, face, casts: false);
            for (int k = -3; k <= 3; k++)
            {
                Part(w1, $"Vent_Bar_{k}", PrimitiveType.Cube, c + new Vector3(k * 0.045f, 0f, -0.03f), new Vector3(0.008f, 0.36f * Mathf.Sqrt(1f - Mathf.Pow(k * 0.045f / 0.18f, 2f)), 0.008f), set.Chrome, casts: false);
            }
            GameObject rotor = MenuLoopSetup.EnsureChild(live, "Vent_Rotor");
            rotor.transform.SetPositionAndRotation(c + new Vector3(0f, 0f, -0.016f), Quaternion.identity);
            for (int i = 0; i < 4; i++)
            {
                GameObject blade = MenuLoopSetup.EnsureChildPrimitive(rotor, $"Vent_Blade_{i}", PrimitiveType.Cube, Quaternion.Euler(0f, 0f, i * 90f) * new Vector3(0.085f, 0f, 0f), new Vector3(0.15f, 0.05f, 0.004f));
                blade.transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f) * Quaternion.Euler(20f, 0f, 0f);
                Object.DestroyImmediate(blade.GetComponent<Collider>());
                Renderer r = blade.GetComponent<Renderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.lightProbeUsage = LightProbeUsage.BlendProbes;
                Register(r, set.BlackSteel);
            }
            Spinner spinner = MenuLoopSetup.EnsureComponent<Spinner>(rotor);
            spinner.axis = Vector3.forward;
            spinner.degreesPerSecond = 240f;
            return spinner;
        }

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
