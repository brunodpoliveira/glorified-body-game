using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GlorifiedBody.Cameras;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Rendering
{
    /// <summary>The ten switches of profile B (T26, "real-time that emulates the result of the pre-rendered remake").</summary>
    public enum RemakeSwitch
    {
        Bake = 0,        // 1. lightmaps at 40 texels a metre, four bounces, the panel as an area light, directional
        Shadows = 1,     // 2. the key lights' shadow maps: the lamp at 4096, the panel at 2048 a face (off: A's 1024 / 512)
        Probes = 2,      // 3. contact shadows (SSAO at contact distance, the foot blobs); the figures keep their probes either way
        Surfaces = 3,    // 4. Lit materials with normal and smoothness maps, decals (off: A's Simple Lit, no decals)
        Volumetric = 4,  // 5. the light in the air and the dust
        Grade = 5,       // 6. tone map, colour grade, film grain, bloom, vignette
        Live = 6,        // 7. the flicker, the fan, the curtain
        Reflections = 7, // 8. the reflection probe (off: no environment reflection, as A)
        Clarity = 8,     // 9. no dither, MSAA 4x, SMAA (off: A's Authentic dither, no anti-aliasing)
        Lens = 9,        // 10. the long lens on Cam_Recovery_Long (off: 60 degrees)
    }

    /// <summary>
    /// Runs profile B in RemakeLook.unity (T26). Every switch turns off on its own; off means "as profile A" where A
    /// has the same element (the bake, the shadow resolution, the materials, the dither), and "not there" where A has
    /// none (the light in the air, the grade, the live layers). F3 cycles: all on, each switch off in turn, all off.
    /// The characters wear their 4096 base colour here. RemakeLookSetup builds the scene and fills the fields.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class RemakeDirector : MonoBehaviour
    {
        public const int SwitchCount = 10;
        public const KeyCode CycleKey = KeyCode.F3;
        public const float LongLensFov = 26f, WideLensFov = 60f;

        [Header("Pipelines and renderers")]
        public UniversalRenderPipelineAsset pipelineHigh; // the B shadow numbers (8192 atlas, tiers 512/2048/4096)
        public UniversalRenderPipelineAsset pipelineLow;  // A's shadow numbers (4096 atlas, tiers 256/512/1024)
        public UniversalRenderPipelineAsset pipelineMid;  // not a switch: a measure (4096 atlas, tiers 512/1024/2048)
        [NonSerialized] public bool midShadows;
        public int clearRendererIndex = 1;  // GB_Remake: no dither
        public int ditherRendererIndex = 2; // GB_RemakeDither: A's 32-level dither
        public ScriptableRendererFeature[] ssaoFeatures = new ScriptableRendererFeature[0];

        [Header("Switch 1: the bake")]
        public RemakeBakeSwap bakeSwap;

        [Header("Switch 3: probes and contact")]
        public GameObject[] contactObjects = new GameObject[0];

        [Header("Switch 4: surfaces")]
        public Renderer[] surfaceRenderers = new Renderer[0];
        public Material[] surfaceMaterialsA = new Material[0];
        public Material[] surfaceMaterialsB = new Material[0];
        public GameObject[] decals = new GameObject[0];

        [Header("Switch 5, 6, 7, 8")]
        [Tooltip("The density of the light in the air. 1.0 with the 120-degree ceiling light hazed the whole black room.")]
        public float airDensity = 0.5f;
        public GameObject[] volumetricObjects = new GameObject[0];
        public Volume gradeVolume;
        public Behaviour[] liveBehaviours = new Behaviour[0];
        public Cloth[] liveCloth = new Cloth[0];
        public ReflectionProbe[] reflectionProbes = new ReflectionProbe[0];

        [Header("Switch 10")]
        public FixedCamera longCamera;

        [Header("The characters (always in B)")]
        public string[] characterRoots = { "Lucy", "Figure_Guard" };
        public string[] characterClasses = { "lucy_bare", "guard" };
        public Material[] characterMaterials = new Material[0];   // B: Lucy C3 4096, the guard 4096 (when no full mesh)
        public Material[] characterMaterialsA = new Material[0];  // A: Lucy C3 1024, the game's guard (1024)
        public RemakeBodySwap[] bodySwaps = new RemakeBodySwap[0]; // B: the full-mesh bodies (cutscene tier only)

        [Header("B-game: the 2,500 game meshes at one texture size (Bruno, 2026-11-01)")]
        public Material[] lucyTiers = new Material[0];  // Lucy C3 at 1024, 2048, 4096
        public Material[] guardTiers = new Material[0]; // the guard (light kit) at 1024, 2048, 4096
        public static readonly int[] TierSizes = { 1024, 2048, 4096 };

        public enum Profile { A, BFull, BGame }

        /// <summary>A: every switch off, the game's characters and maps (the look of the game before T26).
        /// BFull: the T26 profile B (switches, the full-mesh guard). BGame: Bruno's pick of 2026-11-01 (design §28a): B
        /// with no dither and with anti-aliasing, switch 3 (the most expensive one under the air) off, the air a room
        /// choice, the 2,500 game meshes with the maps of one tier.</summary>
        public Profile Current { get; private set; } = Profile.BFull;
        public bool ProfileA => Current == Profile.A;
        public int GameTier { get; private set; } = 2048;

        public static RemakeDirector Instance { get; private set; }
        public event Action OnChanged;

        private readonly bool[] on = new bool[SwitchCount];
        private RenderPipelineAsset previousPipeline;
        private bool pipelineSwapped;
        private Camera cam;
        private UniversalAdditionalCameraData camData;
        private int cycle;
        private readonly List<Renderer> characterRenderers = new List<Renderer>();
        private float nextCharacterCheck;

        public bool IsOn(RemakeSwitch s) => on[(int)s];
        public int CycleStep => cycle;
        public string Label { get; private set; } = "all on";

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < SwitchCount; i++) on[i] = true;
        }

        private void Start()
        {
            cam = Camera.main;
            camData = cam != null ? cam.GetUniversalAdditionalCameraData() : null;
            previousPipeline = QualitySettings.renderPipeline;
            if (bakeSwap != null) bakeSwap.CaptureHigh();
            ApplyAll();
        }

        private void OnDestroy()
        {
            VolumetricLightFeature.On = false;
            if (pipelineSwapped) QualitySettings.renderPipeline = previousPipeline;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!GameInput.GetKeyDown(CycleKey)) return;
            // F3: B all on, each switch off, all off, then profile A, then around.
            int next = (cycle + 1) % (SwitchCount + 3);
            if (next == SwitchCount + 2) { cycle = next; SetProfileA(true); } else Cycle(next);
        }

        private void LateUpdate()
        {
            // The outfit code puts the catalog model back at its start: wear the 4096 colour over it.
            if (Time.unscaledTime >= nextCharacterCheck)
            {
                nextCharacterCheck = Time.unscaledTime + 0.5f;
                WearCharacterTextures();
            }
        }

        /// <summary>Profile B: 0 = all on, 1..10 = that switch off alone, 11 = all off (A's settings, B's characters).</summary>
        public void Cycle(int step)
        {
            Current = Profile.BFull;
            cycle = step;
            for (int i = 0; i < SwitchCount; i++) on[i] = step == 0 || (step <= SwitchCount ? i != step - 1 : false);
            Label = step == 0 ? "all on" : step <= SwitchCount ? $"switch {step} off ({(RemakeSwitch)(step - 1)})" : "all off";
            ApplyAll();
        }

        /// <summary>Profile A (true) or profile B with every switch on (false).</summary>
        public void SetProfileA(bool a)
        {
            if (!a) { Cycle(0); return; }
            Current = Profile.A;
            for (int i = 0; i < SwitchCount; i++) on[i] = false;
            Label = "PROFILE A (the current look)";
            ApplyAll();
        }

        /// <summary>B-game: switch 3 off, switch 5 (the air) as the room wants, every other switch on, the given tier.</summary>
        public void SetProfileGame(bool air, int tier)
        {
            Current = Profile.BGame;
            GameTier = tier;
            for (int i = 0; i < SwitchCount; i++) on[i] = true;
            on[(int)RemakeSwitch.Probes] = false;
            on[(int)RemakeSwitch.Volumetric] = air;
            Label = $"B-game, air {(air ? "on" : "off")}, {tier} maps";
            ApplyAll();
        }

        public void SetAll(bool value)
        {
            Current = Profile.BFull;
            for (int i = 0; i < SwitchCount; i++) on[i] = value;
            Label = value ? "all on" : "all off";
            ApplyAll();
        }

        public void Set(RemakeSwitch s, bool value)
        {
            on[(int)s] = value;
            ApplyAll();
        }

        public void ApplyAll()
        {
            // 2: the shadow numbers live on the pipeline asset.
            UniversalRenderPipelineAsset wanted = IsOn(RemakeSwitch.Shadows) ? (midShadows && pipelineMid != null ? pipelineMid : pipelineHigh) : pipelineLow;
            if (wanted != null && QualitySettings.renderPipeline != wanted)
            {
                QualitySettings.renderPipeline = wanted;
                pipelineSwapped = true;
            }
            // 9: the dither, MSAA and SMAA.
            bool clear = IsOn(RemakeSwitch.Clarity);
            foreach (UniversalRenderPipelineAsset asset in new[] { pipelineHigh, pipelineLow, pipelineMid })
            {
                if (asset != null) asset.msaaSampleCount = clear ? 4 : 1;
            }
            if (camData != null)
            {
                camData.SetRenderer(clear ? clearRendererIndex : ditherRendererIndex);
                camData.antialiasing = clear ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
                camData.antialiasingQuality = AntialiasingQuality.High;
                camData.renderPostProcessing = true;
            }
            if (cam != null) cam.allowMSAA = clear;

            // 1: the high or the low bake.
            if (bakeSwap != null) bakeSwap.Use(IsOn(RemakeSwitch.Bake));

            // 3: the characters take the baked probes; SSAO and the foot blobs.
            bool probes = IsOn(RemakeSwitch.Probes);
            WearCharacterTextures();
            foreach (ScriptableRendererFeature f in ssaoFeatures) if (f != null) f.SetActive(probes);
            foreach (GameObject g in contactObjects) if (g != null) g.SetActive(probes);

            // 4: materials and decals.
            bool surfaces = IsOn(RemakeSwitch.Surfaces);
            for (int i = 0; i < surfaceRenderers.Length; i++)
            {
                if (surfaceRenderers[i] == null) continue;
                Material m = surfaces ? Pick(surfaceMaterialsB, i) : Pick(surfaceMaterialsA, i);
                if (m != null) surfaceRenderers[i].sharedMaterial = m;
            }
            foreach (GameObject g in decals) if (g != null) g.SetActive(surfaces);

            // 5: the light in the air.
            bool air = IsOn(RemakeSwitch.Volumetric);
            VolumetricLightFeature.On = air;
            VolumetricLightFeature.Density = airDensity;
            foreach (GameObject g in volumetricObjects) if (g != null) g.SetActive(air);

            // 6: the grade.
            if (gradeVolume != null) gradeVolume.weight = IsOn(RemakeSwitch.Grade) ? 1f : 0f;

            // 7: the live layers.
            bool live = IsOn(RemakeSwitch.Live);
            foreach (Behaviour b in liveBehaviours) if (b != null) b.enabled = live;
            foreach (Cloth c in liveCloth) if (c != null) c.enabled = live;

            // 8: reflections.
            bool reflections = IsOn(RemakeSwitch.Reflections);
            foreach (ReflectionProbe p in reflectionProbes) if (p != null) p.enabled = reflections;
            RenderSettings.reflectionIntensity = reflections ? 1f : 0f;

            // 10: the lens.
            if (longCamera != null)
            {
                longCamera.fieldOfView = IsOn(RemakeSwitch.Lens) ? LongLensFov : WideLensFov;
                FixedCameraDirector director = FixedCameraDirector.Instance;
                if (director != null && director.Current == longCamera && cam != null) cam.fieldOfView = longCamera.fieldOfView;
            }

            RunTelemetry.Event("remake_switches", Describe());
            OnChanged?.Invoke();
        }

        public string Describe()
        {
            var parts = new string[SwitchCount];
            for (int i = 0; i < SwitchCount; i++) parts[i] = on[i] ? "1" : "0";
            return string.Join("", parts);
        }

        private void WearCharacterTextures()
        {
            foreach (RemakeBodySwap swap in bodySwaps) if (swap != null) swap.Use(Current == Profile.BFull);
            Material[] wanted = Current == Profile.A ? characterMaterialsA : Current == Profile.BFull ? characterMaterials : TierMaterials();
            characterRenderers.Clear();
            for (int i = 0; i < characterRoots.Length; i++)
            {
                GameObject root = GameObject.Find(characterRoots[i]);
                if (root == null) continue;
                Material material = i < wanted.Length ? wanted[i] : null;
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(r is SkinnedMeshRenderer)) continue;
                    characterRenderers.Add(r);
                    if (material == null || IsBodyB(r)) continue;
                    Material current = r.sharedMaterial;
                    bool ours = current != null && (current.name.IndexOf(characterClasses[i], StringComparison.OrdinalIgnoreCase) >= 0
                        || Array.IndexOf(characterMaterials, current) >= 0 || Array.IndexOf(characterMaterialsA, current) >= 0
                        || Array.IndexOf(lucyTiers, current) >= 0 || Array.IndexOf(guardTiers, current) >= 0);
                    if (current != material && ours) r.sharedMaterial = material;
                }
            }
            // The figures take the baked probes in every profile, as the game does (switch 3 is the contact shadows only).
            foreach (Renderer r in characterRenderers) r.lightProbeUsage = LightProbeUsage.BlendProbes;
        }

        private Material[] TierMaterials()
        {
            int i = Array.IndexOf(TierSizes, GameTier);
            if (i < 0) i = 1;
            return new[] { i < lucyTiers.Length ? lucyTiers[i] : null, i < guardTiers.Length ? guardTiers[i] : null };
        }

        private bool IsBodyB(Renderer r)
        {
            foreach (RemakeBodySwap swap in bodySwaps)
            {
                if (swap != null && swap.Instance != null && r.transform.IsChildOf(swap.Instance.transform)) return true;
            }
            return false;
        }

        private static Material Pick(Material[] list, int i) => list != null && i < list.Length ? list[i] : null;

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(GlorifiedBody.Automation.BotArgs.Bot)) return;
            GUI.Label(new Rect(12f, 8f, 1200f, 24f), $"REMAKE LOOK ({Current})  {Label}  switches {Describe()}  (F3 cycles)");
        }
    }
}
