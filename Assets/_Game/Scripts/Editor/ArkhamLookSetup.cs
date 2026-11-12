using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using GlorifiedBody.Rendering;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// T38 (Bruno, 2026-11-11: "do research on how arknight did its graphics, pull examples, and do experiments"): the
    /// Arkham Knight look as a TEST profile over Bruno's B-game. Two new scenes, copies of the two T26 rooms:
    /// ArkhamLook.unity (the dark abandoned room, air on) and ArkhamLookBright.unity (the bright hospital room, air off).
    /// The T26 scenes, their profiles and every shipped room stay as they are: the copies keep the T26 bake (the same
    /// lighting data) and add one root, [ArkhamAK], with ArkhamDirector and the parts of the eight switches
    /// (tests/T38_arkham_look/PLAN.md §3). The wet floor is the Shader Graph sample "Production Ready Shaders" (its rain
    /// floor), brought in by tools/ak_import_weather.py. The procedural maps: tools/ak_textures.py. The LUTs:
    /// tools/ak_lut.py (fitted to the reference median of tests/T38_arkham_look/reference_stats.txt).
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.ArkhamLookSetup.Build
    /// </summary>
    public static class ArkhamLookSetup
    {
        public const string Root = "Assets/_Game/Arkham";
        public const string TexturesFolder = Root + "/Textures";
        public const string MaterialsFolder = Root + "/Materials";
        public const string SettingsFolder = Root + "/Settings";
        public const string DarkSceneName = "ArkhamLook";
        public const string BrightSceneName = "ArkhamLookBright";
        public const string WetGraphPath = Root + "/AK_WetFloor.shadergraph";

        // The leak of the dark room (switch 7): in the frame of all three cameras, in front of the bed's foot.
        public static readonly Vector3 LeakAt = new Vector3(6.0f, 3.2f, 5.0f);
        // The practicals (switch 6): a red alarm lamp on the east wall by the bed's foot (all three cameras see it, 0.95 m
        // from the gear, so the white gear stays white), a teal monitor over the bed head (the bed and the long camera).
        public static readonly Vector3 RedAt = new Vector3(7.27f, 1.15f, 5.3f);
        public static readonly Vector3 TealAt = new Vector3(7.25f, 1.6f, 7.75f);

        [MenuItem("GlorifiedBody/T38 Arkham Look/Build both rooms")]
        public static void Build()
        {
            foreach (string f in new[] { Root, TexturesFolder, MaterialsFolder, SettingsFolder }) Directory.CreateDirectory(f);
            AssetDatabase.Refresh();
            ImportTextures();
            BuildRoom(false);
            BuildRoom(true);
            AssetDatabase.SaveAssets();
            Debug.Log("[ArkhamLookSetup] built ArkhamLook and ArkhamLookBright.");
        }

        /// <summary>The grades only (after tools/ak_lut.py wrote new LUTs). Batch: ...ArkhamLookSetup.Grades</summary>
        [MenuItem("GlorifiedBody/T38 Arkham Look/Grades only")]
        public static void Grades()
        {
            ImportTextures();
            Grade(false);
            Grade(true);
            AssetDatabase.SaveAssets();
            Debug.Log("[ArkhamLookSetup] grades written.");
        }

        // ── Textures ──────────────────────────────────────────────

        private static void ImportTextures()
        {
            foreach (string path in Directory.GetFiles(TexturesFolder, "*.png"))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                bool lut = name.StartsWith("ak_lut");
                bool normal = name.Contains("_n_");
                bool linear = name.Contains("_ms_") || name.Contains("mask") || lut;
                bool particle = name == "ak_drip" || name == "ak_ripple";
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D; // a .meta with only a GUID imported as a cube map
                importer.sRGBTexture = !linear && !normal;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = particle;
                importer.mipmapEnabled = !lut;
                importer.wrapMode = lut || particle ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                importer.filterMode = lut ? FilterMode.Bilinear : FilterMode.Trilinear;
                importer.anisoLevel = lut ? 0 : 8;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.textureCompression = lut || particle || name == "ak_lens_dirt" ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
                importer.crunchedCompression = false;
                importer.SaveAndReimport();
            }
        }

        private static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{name}.png");

        // ── Materials ─────────────────────────────────────────────

        private static Material Mat(string name, Shader shader)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            return m;
        }

        private static Material Lit(string name, Color colour, float smooth, float metal, Color? emission = null)
        {
            Material m = Mat(name, Shader.Find("Universal Render Pipeline/Lit"));
            m.shaderKeywords = new string[0];
            m.SetFloat("_WorkflowMode", 1f);
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_EnvironmentReflections", 1f);
            m.SetFloat("_SpecularHighlights", 1f);
            if (emission.HasValue)
            {
                m.SetColor("_EmissionColor", emission.Value);
                m.EnableKeyword("_EMISSION");
            }
            // EmissiveIsBlack, and also None, let URP's material check drop _EMISSION on a later import (run 2: the red lamp
            // and the teal screen went dark). BakedEmissive keeps it, as T26's RB_Lamp. The copies keep the T26 bake.
            m.globalIlluminationFlags = emission.HasValue ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Switch 3: the sample's rain floor with our granite. Indoors: no rain on the surface (no ripples), a high
        /// wetness, puddles of the sample's own mask.</summary>
        private static Material WetFloor()
        {
            Shader graph = AssetDatabase.LoadAssetAtPath<Shader>(WetGraphPath);
            if (graph == null) { Debug.LogError($"[ArkhamLookSetup] {WetGraphPath} did not import: run tools/ak_import_weather.py"); return null; }
            Material m = Mat("AK_WetFloor", graph);
            m.SetFloat("_Rain_Intensity", 0f);
            m.SetFloat("_Rain_Wetness", 0.85f);
            m.SetFloat("_Puddle_Size", 1.0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Switch 5: the B-game material of the tier with the AK smoothness map.</summary>
        private static Material Suit(string name, Material game, string map)
        {
            Material m = Mat(name, game.shader);
            m.CopyPropertiesFromMaterial(game);
            m.shaderKeywords = game.shaderKeywords;
            m.SetTexture("_MetallicGlossMap", Tex(map));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_Metallic", 1f); // the map's R (Lucy: the veins at 0.6; the guard: 0)
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Additive(string name, string texture, Color colour)
        {
            Material m = Mat(name, Shader.Find("GB/AK/AdditiveParticle"));
            m.SetTexture("_BaseMap", Tex(texture));
            m.SetColor("_Color", colour);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ── Volumes ───────────────────────────────────────────────

        private static VolumeProfile Profile(string name)
        {
            string path = $"{SettingsFolder}/{name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            foreach (VolumeComponent c in profile.components.ToArray())
            {
                profile.components.Remove(c);
                Object.DestroyImmediate(c, true);
            }
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

        /// <summary>The numbers of the grade volume (switch 1). tools/ak_lut.py fits the LUT over them.</summary>
        public struct GradeNumbers
        {
            public float exposure, contrast, saturation;
        }

        // ACES at 0 sits darker than B-game's Neutral at +0.2 in the mid-tones: +0.4 brings the figures back to B-game.
        public static GradeNumbers Numbers(bool bright) => new GradeNumbers { exposure = 0.4f, contrast = 0f, saturation = 0f };

        private static VolumeProfile Grade(bool bright)
        {
            string room = bright ? "bright" : "dark";
            VolumeProfile p = Profile($"AK_Grade_{room}");
            GradeNumbers n = Numbers(bright);
            // ACES for its highlight roll-off. No lift in the exposure (run 1: +1.85 / +2.15 turned the guard pale and Lucy
            // white and lifted the black walls): the LUT carries a highlight curve and the tints, fitted with guards on the
            // figures and the walls.
            Add<Tonemapping>(p).mode.Override(TonemappingMode.ACES);
            var adjust = Add<ColorAdjustments>(p);
            adjust.postExposure.Override(n.exposure);
            adjust.contrast.Override(n.contrast);
            adjust.saturation.Override(n.saturation);
            adjust.colorFilter.Override(Color.white);
            var lut = Add<ColorLookup>(p);
            Texture2D fitted = Tex($"ak_lut_{room}") ?? Tex("ak_lut_identity_32");
            lut.texture.Override(fitted);
            lut.contribution.Override(1f);
            var vignette = Add<Vignette>(p);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.5f);
            EditorUtility.SetDirty(p);
            Debug.Log($"[ArkhamLookSetup] grade {room}: exposure {n.exposure}, LUT {fitted?.name}");
            return p;
        }

        private static VolumeProfile Practicals()
        {
            VolumeProfile p = Profile("AK_Practicals");
            var bloom = Add<Bloom>(p);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.3f);
            bloom.scatter.Override(0.72f);
            bloom.highQualityFiltering.Override(true);
            bloom.dirtTexture.Override(Tex("ak_lens_dirt"));
            bloom.dirtIntensity.Override(4f);
            EditorUtility.SetDirty(p);
            return p;
        }

        private static VolumeProfile Lens()
        {
            VolumeProfile p = Profile("AK_Lens");
            Add<ChromaticAberration>(p).intensity.Override(0.08f);
            var dof = Add<DepthOfField>(p);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(5.0f);
            dof.gaussianEnd.Override(11f);
            dof.gaussianMaxRadius.Override(1.0f);
            dof.highQualitySampling.Override(false);
            var grain = Add<FilmGrain>(p);
            grain.type.Override(FilmGrainLookup.Medium2);
            grain.intensity.Override(0.3f);
            grain.response.Override(0.8f);
            EditorUtility.SetDirty(p);
            return p;
        }

        private static Volume VolumeObj(GameObject parent, string name, VolumeProfile profile, float priority)
        {
            GameObject go = MenuLoopSetup.EnsureChild(parent, name);
            Volume v = MenuLoopSetup.EnsureComponent<Volume>(go);
            v.isGlobal = true;
            v.sharedProfile = profile;
            v.priority = priority;
            v.weight = 1f;
            return v;
        }

        // ── Lens flares (switch 6) ────────────────────────────────

        private static LensFlareDataSRP FlareData()
        {
            string path = $"{SettingsFolder}/AK_Flare.asset";
            var data = AssetDatabase.LoadAssetAtPath<LensFlareDataSRP>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<LensFlareDataSRP>();
                AssetDatabase.CreateAsset(data, path);
            }
            var glow = new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Circle, localIntensity = 0.45f, tint = new Color(1f, 0.95f, 0.88f), uniformScale = 0.3f,
                sizeXY = Vector2.one, fallOff = 0.25f, edgeOffset = 0.0f, modulateByLightColor = false, blendMode = SRPLensFlareBlendMode.Additive,
            };
            // The anamorphic streak: a thin horizontal ellipse.
            var streak = new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Circle, localIntensity = 0.22f, tint = new Color(0.75f, 0.9f, 1f), uniformScale = 3.5f,
                sizeXY = new Vector2(1f, 0.012f), preserveAspectRatio = false, fallOff = 0.6f, edgeOffset = 0.0f, blendMode = SRPLensFlareBlendMode.Additive,
            };
            // Ghosts toward the centre of the frame.
            var ghosts = new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Polygon, sideCount = 6, sdfRoundness = 0.4f, localIntensity = 0.05f, tint = new Color(0.6f, 0.95f, 0.9f),
                uniformScale = 0.12f, sizeXY = Vector2.one, position = 0.6f, allowMultipleElement = true, count = 4, lengthSpread = 1.4f,
                distribution = SRPLensFlareDistribution.Uniform, fallOff = 0.8f, edgeOffset = 0.1f, blendMode = SRPLensFlareBlendMode.Additive,
            };
            data.elements = new[] { glow, streak, ghosts };
            EditorUtility.SetDirty(data);
            return data;
        }

        private static LensFlareComponentSRP Flare(GameObject parent, string name, Vector3 at, LensFlareDataSRP data, float intensity)
        {
            GameObject go = MenuLoopSetup.EnsureChild(parent, name);
            go.transform.position = at;
            LensFlareComponentSRP f = MenuLoopSetup.EnsureComponent<LensFlareComponentSRP>(go);
            f.lensFlareData = data;
            f.intensity = intensity;
            f.scale = 1f;
            f.attenuationByLightShape = false;
            f.useOcclusion = true;
            f.occlusionRadius = 0.04f;
            f.sampleCount = 16;
            f.occlusionOffset = 0.06f;
            f.allowOffScreen = false;
            return f;
        }

        // ── The room ──────────────────────────────────────────────

        private static GameObject Box(GameObject parent, string name, PrimitiveType type, Vector3 at, Vector3 scale, Material m, Quaternion? rot = null, bool casts = false)
        {
            GameObject g = MenuLoopSetup.EnsureChild(parent, name);
            if (g.GetComponent<MeshFilter>() == null)
            {
                GameObject prim = GameObject.CreatePrimitive(type);
                g.AddComponent<MeshFilter>().sharedMesh = prim.GetComponent<MeshFilter>().sharedMesh;
                g.AddComponent<MeshRenderer>();
                Object.DestroyImmediate(prim);
            }
            g.transform.SetPositionAndRotation(at, rot ?? Quaternion.identity);
            g.transform.localScale = scale;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = casts ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            GameObjectUtility.SetStaticEditorFlags(g, 0);
            return g;
        }

        private static Light PointLight(GameObject parent, string name, Vector3 at, Color colour, float intensity, float range)
        {
            GameObject go = MenuLoopSetup.EnsureChild(parent, name);
            go.transform.position = at;
            Light l = MenuLoopSetup.EnsureComponent<Light>(go);
            l.type = LightType.Point;
            l.color = colour;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        private static void Accent(Light l)
        {
            l.renderingLayerMask = (int)ArkhamDirector.AccentLayer;
            l.GetUniversalAdditionalLightData().renderingLayers = ArkhamDirector.AccentLayer;
        }

        private static void BuildRoom(bool bright)
        {
            string src = bright ? RemakeLookSetup.BrightScenePath : RemakeLookSetup.ScenePath;
            string name = bright ? BrightSceneName : DarkSceneName;
            string dst = $"{MenuLoopSetup.ScenesFolder}/{name}.unity";
            if (!File.Exists(src)) { Debug.LogError($"[ArkhamLookSetup] {src} is missing."); return; }
            AssetDatabase.DeleteAsset(dst);
            if (!AssetDatabase.CopyAsset(src, dst)) { Debug.LogError($"[ArkhamLookSetup] cannot copy {src}."); return; }
            Scene scene = EditorSceneManager.OpenScene(dst, OpenSceneMode.Single);
            RemakeDirector remake = Object.FindFirstObjectByType<RemakeDirector>();
            if (remake == null) { Debug.LogError("[ArkhamLookSetup] no RemakeDirector in the copy."); return; }

            GameObject root = MenuLoopSetup.EnsureRoot("[ArkhamAK]");
            ArkhamDirector director = MenuLoopSetup.EnsureComponent<ArkhamDirector>(root);
            director.remake = remake;
            director.roomAir = !bright;
            director.gameTier = 2048;

            // 1, 6, 8: the volumes over B-game's grade (priority 10).
            director.gradeVolume = VolumeObj(root, "Grade", Grade(bright), 20f);
            director.practicalVolume = VolumeObj(root, "PracticalBloom", Practicals(), 21f);
            director.lensVolume = VolumeObj(root, "Lens", Lens(), 22f);

            // 2: the rim colour: cold against the neutral key in both rooms (AK's teal rim).
            // A thin edge, not a fill (run 1: a cold rim at 16 to 22 turned the guard pale blue). Warm against the neutral key,
            // a narrow cone aimed at the shoulders and the head, from high behind.
            director.rimColor = new Color(1f, 0.9f, 0.78f);
            director.rimIntensity = bright ? 7f : 9f;
            director.rimAngle = 16f;
            director.rimOffset = new Vector3(1.3f, 2.3f, 0.5f);
            director.rimAimHeight = 1.5f;

            // 3: the floor (the horizontal granite pieces at floor level).
            var wet = new List<Renderer>();
            for (int i = 0; i < remake.surfaceRenderers.Length; i++)
            {
                Renderer r = remake.surfaceRenderers[i];
                Material b = i < remake.surfaceMaterialsB.Length ? remake.surfaceMaterialsB[i] : null;
                if (r == null || b == null || b.name != "RB_Granite") continue;
                Bounds bb = r.bounds;
                if (bb.size.y < 0.2f && bb.max.y < 0.15f && bb.size.x > 1f && bb.size.z > 1f) wet.Add(r);
            }
            director.wetRenderers = wet.ToArray();
            director.wetMaterial = WetFloor();

            // 4: the planar reflection: the overlay quad on the Water layer (the mirror camera leaves it out).
            GameObject planarObj = MenuLoopSetup.EnsureChild(root, "Planar");
            AKPlanarReflection planar = MenuLoopSetup.EnsureComponent<AKPlanarReflection>(planarObj);
            Vector3 min = RemakeLookSetup.RoomMin, max = RemakeLookSetup.RoomMax;
            Material overlay = Mat("AK_PlanarOverlay", Shader.Find("GB/AK/PlanarOverlay"));
            overlay.SetTexture("_MaskTex", Tex("ak_puddle_mask"));
            overlay.SetFloat("_Strength", 0.85f);
            EditorUtility.SetDirty(overlay);
            GameObject quad = Box(planarObj, "Planar_Overlay", PrimitiveType.Quad, new Vector3((min.x + max.x) * 0.5f, 0.003f, (min.z + max.z) * 0.5f),
                new Vector3(max.x - min.x, max.z - min.z, 1f), overlay, Quaternion.Euler(90f, 0f, 0f));
            quad.layer = 4; // Water
            planar.overlay = quad.GetComponent<Renderer>();
            planar.floorY = 0f;
            planar.rendererIndex = remake.clearRendererIndex;
            planar.resolution = 0.5f;
            director.planar = planar;

            // 5: the suits.
            director.lucySuit = Suit("AK_lucy_suit_2k", remake.lucyTiers[1], "ak_lucy_ms_2k");
            director.guardSuit = Suit("AK_guard_suit_2k", remake.guardTiers[1], "ak_guard_ms_2k");

            // 6: the practicals: a red alarm lamp and a teal monitor, each with its own small light; flares on the lamps.
            GameObject practicals = MenuLoopSetup.EnsureChild(root, "Practicals");
            Material steel = Lit("AK_Housing", new Color(0.03f, 0.03f, 0.035f), 0.7f, 0.6f);
            Material red = Lit("AK_RedLamp", new Color(0.2f, 0.01f, 0.01f), 0.9f, 0f, new Color(1f, 0.05f, 0.03f) * 9f);
            Material teal = Lit("AK_TealScreen", new Color(0.01f, 0.05f, 0.05f), 0.95f, 0f, new Color(0.15f, 0.85f, 0.8f) * 2.5f);
            Box(practicals, "Red_Housing", PrimitiveType.Cube, RedAt, new Vector3(0.05f, 0.14f, 0.14f), steel, casts: true);
            Box(practicals, "Red_Lens", PrimitiveType.Cylinder, RedAt + new Vector3(-0.03f, 0f, 0f), new Vector3(0.08f, 0.012f, 0.08f), red, Quaternion.Euler(0f, 0f, 90f));
            Accent(PointLight(practicals, "Red_Light", RedAt + new Vector3(-0.15f, 0f, 0f), new Color(1f, 0.12f, 0.07f), 0.6f, 1.6f));
            Box(practicals, "Teal_Housing", PrimitiveType.Cube, TealAt, new Vector3(0.06f, 0.3f, 0.42f), steel, casts: true);
            Box(practicals, "Teal_Screen", PrimitiveType.Cube, TealAt + new Vector3(-0.032f, 0f, 0f), new Vector3(0.004f, 0.24f, 0.36f), teal);
            Accent(PointLight(practicals, "Teal_Light", TealAt + new Vector3(-0.3f, 0f, 0f), new Color(0.2f, 0.9f, 0.85f), 0.45f, 1.8f));
            // The walls and the ceiling never take the accent layer (act 1 is black on black).
            var blocked = new List<Renderer>();
            for (int i = 0; i < remake.surfaceRenderers.Length; i++)
            {
                Material b = i < remake.surfaceMaterialsB.Length ? remake.surfaceMaterialsB[i] : null;
                if (remake.surfaceRenderers[i] != null && b != null && (b.name == "RB_Tile" || b.name == "RB_Ceiling" || b.name == "RB_Gear"))
                    blocked.Add(remake.surfaceRenderers[i]);
            }
            director.accentBlocked = blocked.ToArray();
            LensFlareDataSRP flareData = FlareData();
            var flares = new List<LensFlareComponentSRP>();
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.name.StartsWith("Emergency_Light_") || l.name.StartsWith("W1_PanelLight_"))
                    flares.Add(Flare(practicals, "Flare_" + l.name, l.transform.position, flareData, bright ? 0.3f : 0.6f));
            }
            director.practicalObjects = new[] { practicals };
            director.flares = flares.ToArray();

            // 7: the leak (the dark, abandoned room only).
            if (!bright)
            {
                GameObject leak = MenuLoopSetup.EnsureChild(root, "Leak");
                Material wetBlack = Lit("AK_Puddle", new Color(0.004f, 0.004f, 0.005f), 0.97f, 0f);
                Box(leak, "Leak_Stain", PrimitiveType.Cylinder, LeakAt + new Vector3(0f, -0.004f, 0f), new Vector3(0.42f, 0.002f, 0.36f), wetBlack);
                Box(leak, "Leak_Puddle", PrimitiveType.Cylinder, new Vector3(LeakAt.x, 0.002f, LeakAt.z), new Vector3(0.95f, 0.001f, 0.7f), wetBlack, Quaternion.Euler(0f, 25f, 0f));
                GameObject spotObj = MenuLoopSetup.EnsureChild(leak, "Leak_Light");
                spotObj.transform.SetPositionAndRotation(LeakAt + new Vector3(0f, -0.03f, 0f), Quaternion.Euler(90f, 0f, 0f));
                Light spot = MenuLoopSetup.EnsureComponent<Light>(spotObj);
                spot.type = LightType.Spot;
                spot.color = new Color(0.72f, 0.88f, 1f);
                spot.intensity = 5f;
                spot.range = 4.5f;
                spot.spotAngle = 16f;
                spot.innerSpotAngle = 6f;
                spot.shadows = LightShadows.None;
                spot.lightmapBakeType = LightmapBakeType.Realtime;
                MenuLoopSetup.EnsureComponent<VolumetricLight>(spotObj).strength = 0.35f;
                Drips(leak, Additive("AK_Drip", "ak_drip", new Color(0.75f, 0.9f, 1f) * 0.9f));
                Ripples(leak, Additive("AK_Ripple", "ak_ripple", new Color(0.7f, 0.85f, 1f) * 0.9f));
                director.leakObjects = new[] { leak };
            }
            else director.leakObjects = new GameObject[0];

            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var scenes = EditorBuildSettings.scenes.ToList();
            MenuLoopSetup.EnsureBuildScene(scenes, dst);
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[ArkhamLookSetup] {dst}: {wet.Count} wet floor pieces, {flares.Count} flares, leak {(!bright ? "yes" : "no (bright room)")}, lightmaps of {src}.");
        }

        private static void Drips(GameObject parent, Material material)
        {
            GameObject go = MenuLoopSetup.EnsureChild(parent, "Leak_Drips");
            go.transform.SetPositionAndRotation(LeakAt + new Vector3(0f, -0.02f, 0f), Quaternion.identity);
            ParticleSystem ps = MenuLoopSetup.EnsureComponent<ParticleSystem>(go);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.79f; // the fall of 3.2 m
            main.startSpeed = 0f;
            main.startSize = 0.016f;
            main.gravityModifier = 1f;
            main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = 7f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.04f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.035f;
            r.lengthScale = 1.5f;
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static void Ripples(GameObject parent, Material material)
        {
            GameObject go = MenuLoopSetup.EnsureChild(parent, "Leak_Ripples");
            go.transform.SetPositionAndRotation(new Vector3(LeakAt.x, 0.008f, LeakAt.z), Quaternion.identity);
            ParticleSystem ps = MenuLoopSetup.EnsureComponent<ParticleSystem>(go);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startSize = 0.05f;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = 7f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(1f, 1f)));
            main.startSize = 0.55f;
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.35f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colour.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}
