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
    /// <summary>The four looks of the look test (build order step 4).</summary>
    public enum LookDressing
    {
        /// <summary>The room as a greybox: flat grey materials, the stock renderer, no dither.</summary>
        Greybox = 0,
        /// <summary>Dressed, lit live every frame, the 720p frame with the dither on the background. No bounce light (the ruling).</summary>
        RealTime = 1,
        /// <summary>The real-time dressing plus the baked bounce light (the lightmaps and the probes of LookTestSetup.BakeAll).</summary>
        RealTimeBaked = 2,
        /// <summary>Dressed, the room rendered once per camera into a plate (bounce light, ambient occlusion), the characters drawn over it.</summary>
        Plate = 3
    }

    /// <summary>
    /// Runs the look test in a room (CONCEPTS 7, the 1999 sheen in real time): switches the dressing (F3
    /// cycles, the bot and the tests call SetDressing), swaps the room materials between greybox and dressed,
    /// picks the renderer of the main camera (the stock one, GB_Full with the dither, GB_Plate with the plate
    /// blit), turns the baked light on or off (the lightmaps of the room and the probes of the characters: off
    /// in the real-time dressing by Bruno's ruling of no bounce, on in the baked dressing and in the plate) and,
    /// in the plate dressing, renders the static room once per camera with the expensive renderer (GB_Expensive:
    /// ambient occlusion, 4096 shadow maps, the baked bounce) into the plate. A camera cut marks the plate dirty
    /// and the next Update captures it again. The plate has the size of the view at the look's render scale
    /// (720p of 1080p), so it matches the live 720p frame pixel for pixel; a bot sets the size by hand before
    /// it renders a shot of another size.
    /// </summary>
    public sealed class LookDirector : MonoBehaviour
    {
        public const float LookRenderScale = 2f / 3f; // 720p of 1080p (CONCEPTS 7.4, Bruno's T2 ruling)
        public const int ExpensiveShadowResolution = 4096;
        public const KeyCode CycleKey = KeyCode.F3; // F10 opens the menu bar of the editor on Linux (Bruno, 2026-10-22)
        public const int DressingCount = 4;

        [SerializeField] private int fullRendererIndex = 1;
        [SerializeField] private int expensiveRendererIndex = 2;
        [SerializeField] private int plateRendererIndex = 3;
        [SerializeField] private LayerMask roomLayers = 1; // Default
        [SerializeField] private LookDressing startDressing = LookDressing.RealTime;
        [SerializeField] private Renderer[] roomRenderers = new Renderer[0];
        [SerializeField] private Material[] greyboxMaterials = new Material[0];
        [SerializeField] private Material[] dressedMaterials = new Material[0];

        public static LookDirector Instance { get; private set; }

        public LookDressing Dressing { get; private set; }
        public int RendererIndex { get; private set; } = -1;
        public bool IsDressed { get; private set; }
        public bool BakedLightOn { get; private set; }
        /// <summary>True when the scene carries baked lightmaps (LookTestSetup.BakeAll ran on it).</summary>
        public bool HasBakedLight => bakedLightmaps != null && bakedLightmaps.Length > 0;
        public RenderTexture Plate => plate;
        public int PlateCaptures { get; private set; }
        public string PlateCameraName { get; private set; } = "none";
        /// <summary>The bot sets the plate size of the shot it renders; zero = the view size at the render scale.</summary>
        public Vector2Int PlateSizeOverride { get; set; }
        public int RoomRendererCount => roomRenderers.Length;
        /// <summary>Where Lucy stood when the room loaded: the bot returns her there before every dressing.</summary>
        public Vector3 Home { get; private set; }
        public float HomeYaw { get; private set; }

        public event Action<LookDressing> OnDressingChanged;

        private Camera cam;
        private UniversalAdditionalCameraData camData;
        private RenderTexture plate;         // colour only: the render graph imports a texture with one format
        private RenderTexture captureTarget; // colour plus depth: the camera renders the room into this one
        private FixedCameraDirector director;
        private bool plateDirty;
        private LightmapData[] bakedLightmaps = new LightmapData[0];
        private readonly List<Renderer> lightmapped = new List<Renderer>();
        private readonly List<int> lightmapIndices = new List<int>();
        private readonly List<Renderer> characterRenderers = new List<Renderer>();

        public void Configure(int full, int expensive, int plateIndex, LayerMask room, Renderer[] renderers, Material[] greybox, Material[] dressed)
        {
            fullRendererIndex = full;
            expensiveRendererIndex = expensive;
            plateRendererIndex = plateIndex;
            roomLayers = room;
            roomRenderers = renderers;
            greyboxMaterials = greybox;
            dressedMaterials = dressed;
        }

        private void Awake()
        {
            Instance = this;
            cam = Camera.main;
            camData = cam != null ? cam.GetUniversalAdditionalCameraData() : null;
            // The baked light as the scene loaded: the lightmaps and the index of every lightmapped room renderer.
            bakedLightmaps = LightmapSettings.lightmaps ?? new LightmapData[0];
            foreach (Renderer renderer in roomRenderers)
            {
                if (renderer == null || renderer.lightmapIndex < 0 || renderer.lightmapIndex >= bakedLightmaps.Length) continue;
                lightmapped.Add(renderer);
                lightmapIndices.Add(renderer.lightmapIndex);
            }
        }

        private void Start()
        {
            director = FixedCameraDirector.Instance;
            if (director != null) director.OnCameraChanged += OnCameraCut;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Home = player.transform.position;
                HomeYaw = player.transform.eulerAngles.y;
            }
            // The characters take the baked probes only when the baked light is on: Lucy and the figures stand on
            // the Player and Enemy layers, the room on Default.
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (((1 << renderer.gameObject.layer) & roomLayers.value) != 0) continue;
                if (renderer is SkinnedMeshRenderer || renderer is MeshRenderer) characterRenderers.Add(renderer);
            }
            SetDressing(startDressing);
        }

        private void OnDestroy()
        {
            if (director != null) director.OnCameraChanged -= OnCameraCut;
            PlateBlitFeature.Plate = null;
            if (plate != null) plate.Release();
            if (captureTarget != null) captureTarget.Release();
            if (Instance == this) Instance = null;
        }

        private void OnCameraCut(FixedCamera camera)
        {
            plateDirty = true;
        }

        private void Update()
        {
            if (GameInput.GetKeyDown(CycleKey)) SetDressing((LookDressing)(((int)Dressing + 1) % DressingCount));
            if (Dressing == LookDressing.Plate && plateDirty) CapturePlate();
        }

        public void SetDressing(LookDressing dressing)
        {
            Dressing = dressing;
            IsDressed = dressing != LookDressing.Greybox;
            for (int i = 0; i < roomRenderers.Length; i++)
            {
                if (roomRenderers[i] == null) continue;
                Material wanted = IsDressed ? Pick(dressedMaterials, i) : Pick(greyboxMaterials, i);
                if (wanted != null) roomRenderers[i].sharedMaterial = wanted;
            }

            PlateBlitFeature.Plate = null;
            switch (dressing)
            {
                case LookDressing.Greybox:
                    SetBakedLight(false);
                    UseRenderer(0);
                    break;
                case LookDressing.RealTime:
                    SetBakedLight(false);
                    UseRenderer(fullRendererIndex);
                    break;
                case LookDressing.RealTimeBaked:
                    SetBakedLight(true);
                    UseRenderer(fullRendererIndex);
                    break;
                default:
                    SetBakedLight(true);
                    UseRenderer(plateRendererIndex);
                    plateDirty = true;
                    CapturePlate();
                    break;
            }
            RunTelemetry.Event("look_dressing", dressing.ToString().ToLowerInvariant());
            OnDressingChanged?.Invoke(dressing);
        }

        /// <summary>
        /// The baked bounce light on or off: the lightmaps of the room (the global list and the index of every
        /// lightmapped renderer) and the probes of the room and of the characters. A room renderer without its
        /// lightmap falls back to the baked probes, so the probes go off with the lightmaps, or "real time"
        /// carries the bounce through them (the first run: the baked dressing came out darker than real time).
        /// Without a bake the call changes nothing.
        /// </summary>
        private void SetBakedLight(bool on)
        {
            BakedLightOn = on && HasBakedLight;
            if (!HasBakedLight) return;
            LightmapSettings.lightmaps = on ? bakedLightmaps : new LightmapData[0];
            for (int i = 0; i < lightmapped.Count; i++)
            {
                if (lightmapped[i] != null) lightmapped[i].lightmapIndex = on ? lightmapIndices[i] : -1;
            }
            LightProbeUsage usage = on ? LightProbeUsage.BlendProbes : LightProbeUsage.Off;
            foreach (Renderer renderer in roomRenderers)
            {
                if (renderer != null) renderer.lightProbeUsage = usage;
            }
            foreach (Renderer renderer in characterRenderers)
            {
                if (renderer != null) renderer.lightProbeUsage = usage;
            }
        }

        /// <summary>Renders the static room once with the expensive renderer into the plate, for the current camera.</summary>
        public void CapturePlate()
        {
            if (cam == null || camData == null) return;
            Vector2Int size = PlateSizeOverride.x > 0 && PlateSizeOverride.y > 0
                ? PlateSizeOverride
                : new Vector2Int(Mathf.Max(16, Mathf.RoundToInt(cam.pixelWidth * LookRenderScale)), Mathf.Max(16, Mathf.RoundToInt(cam.pixelHeight * LookRenderScale)));
            if (plate != null && (plate.width != size.x || plate.height != size.y))
            {
                plate.Release();
                captureTarget.Release();
                plate = null;
                captureTarget = null;
            }
            if (plate == null)
            {
                // The camera needs a depth buffer to render the room; the render graph import needs a texture
                // with a colour format only. Two textures, one copy per capture.
                captureTarget = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32) { name = "GB_PlateCapture" };
                captureTarget.Create();
                plate = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32) { name = "GB_Plate", filterMode = FilterMode.Bilinear };
                plate.Create();
            }

            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            float scale = asset != null ? asset.renderScale : 1f;
            int mainRes = asset != null ? asset.mainLightShadowmapResolution : 0;
            int addRes = asset != null ? asset.additionalLightsShadowmapResolution : 0;
            int mask = cam.cullingMask;
            RenderTexture target = cam.targetTexture;
            PlateBlitFeature.Plate = null;
            try
            {
                if (asset != null)
                {
                    // The plate is rendered at its own size (720p): the scale would shrink it a second time.
                    asset.renderScale = 1f;
                    asset.mainLightShadowmapResolution = ExpensiveShadowResolution;
                    asset.additionalLightsShadowmapResolution = ExpensiveShadowResolution;
                }
                camData.SetRenderer(expensiveRendererIndex);
                cam.cullingMask = roomLayers;
                cam.targetTexture = captureTarget;
                cam.Render();
                Graphics.Blit(captureTarget, plate);
            }
            finally
            {
                cam.targetTexture = target;
                cam.cullingMask = mask;
                if (asset != null)
                {
                    asset.renderScale = scale;
                    asset.mainLightShadowmapResolution = mainRes;
                    asset.additionalLightsShadowmapResolution = addRes;
                }
                UseRenderer(plateRendererIndex);
            }
            PlateBlitFeature.Plate = plate;
            plateDirty = false;
            PlateCaptures++;
            PlateCameraName = director != null ? director.CurrentName : "none";
            RunTelemetry.Event("plate_capture", $"{PlateCameraName} {plate.width}x{plate.height}");
        }

        private void UseRenderer(int index)
        {
            RendererIndex = index;
            if (camData != null) camData.SetRenderer(index);
        }

        private static Material Pick(Material[] list, int i) => list != null && i < list.Length ? list[i] : null;

        private void OnGUI()
        {
            string camName = director != null ? director.CurrentName : "none";
            string baked = HasBakedLight ? (BakedLightOn ? "on" : "off") : "none";
            GUI.Label(new Rect(12f, 8f, 1100f, 24f), $"LOOK TEST  {Dressing}  (F3 cycles, F11 the next room)   camera {camName}   baked light {baked}   plate {PlateCameraName} x{PlateCaptures}");
        }
    }
}
