using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Rendering
{
    /// <summary>The eight switches of profile AK (T38, the Arkham Knight look, tests/T38_arkham_look/PLAN.md §3).</summary>
    public enum ArkhamSwitch
    {
        Grade = 0,      // 1. ACES, teal shadows, warm highlights, crushed blacks, more saturation, the fitted LUT
        Rim = 1,        // 2. one back spot for each figure, placed for the camera in use, figures only (rendering layer 2)
        Wet = 2,        // 3. the floor in the sample's rain floor shader: darker, smoothness up, puddles
        Planar = 3,     // 4. a half-size planar reflection on the floor
        Suits = 4,      // 5. the guard and Lucy: a smoothness breakup map, the black parts glossy, a wet sheen on the skin
        Practicals = 5, // 6. one red and one teal accent light, lens flares on the lamps, bloom with lens dirt
        Leak = 6,       // 7. dark room only: a ceiling leak, lit from above, the drips and the ripples
        Lens = 7,       // 8. chromatic aberration, a Gaussian depth of field, grain
    }

    /// <summary>
    /// Runs profile AK in ArkhamLook.unity and ArkhamLookBright.unity (T38). The control is Bruno's B-game (design §28a),
    /// set on the scene's RemakeDirector at the start; profile AK is B-game plus the eight switches. Each switch turns off
    /// on its own. F3 cycles: AK all on, each switch off in turn, then B-game (all eight off). The bot uses
    /// <see cref="SetAlone"/> for the cost of each switch. ArkhamLookSetup builds the scenes and fills the fields.
    /// </summary>
    [DefaultExecutionOrder(550)]
    public sealed class ArkhamDirector : MonoBehaviour
    {
        public const int SwitchCount = 8;
        public const KeyCode CycleKey = KeyCode.F3;
        /// <summary>The rendering layer of the rim lights: only the figures carry it.</summary>
        public const uint RimLayer = 1u << 1;
        /// <summary>The rendering layer of the red and teal accent lights: every renderer but the walls and the ceiling
        /// (act 1 is black on black: the accents may light the props, the floor and the figures, never the grout).</summary>
        public const uint AccentLayer = 1u << 2;
        public static readonly string[] Names = { "grade", "rim", "wet", "planar", "specular suits", "practicals", "leak", "lens" };

        public RemakeDirector remake;
        [Tooltip("The light in the air of B-game in this room (design §28a item 5): on in the dark room, off in the bright one.")]
        public bool roomAir;
        public int gameTier = 2048;

        [Header("1 grade")]
        public Volume gradeVolume;

        [Header("2 rim")]
        public string[] figureRoots = { "Lucy", "Figure_Guard" };
        public Color rimColor = new Color(0.62f, 0.86f, 1f);
        public float rimIntensity = 9f, rimRange = 6f, rimAngle = 34f;
        [Tooltip("From the figure: metres back (away from the camera), up, and to the side.")]
        public Vector3 rimOffset = new Vector3(1.5f, 2.1f, 0.6f);
        public float rimAimHeight = 1.3f;

        [Header("3 wet")]
        public Renderer[] wetRenderers = new Renderer[0];
        public Material wetMaterial;
        public GameObject[] wetObjects = new GameObject[0];

        [Header("4 planar")]
        public AKPlanarReflection planar;

        [Header("5 specular suits")]
        public Material lucySuit;
        public Material guardSuit;

        [Header("6 practicals")]
        public GameObject[] practicalObjects = new GameObject[0];
        [Tooltip("The walls and the ceiling: they never carry the accent layer.")]
        public Renderer[] accentBlocked = new Renderer[0];
        public LensFlareComponentSRP[] flares = new LensFlareComponentSRP[0];
        public Volume practicalVolume;

        [Header("7 leak (dark room)")]
        public GameObject[] leakObjects = new GameObject[0];

        [Header("8 lens")]
        public Volume lensVolume;

        public enum Profile { BGame, AK, Alone }
        public Profile Current { get; private set; } = Profile.AK;
        public string Label { get; private set; } = "AK all on";
        public static ArkhamDirector Instance { get; private set; }
        public event Action OnChanged;

        private readonly bool[] on = new bool[SwitchCount];
        private int cycle;
        private Material lucyGame, guardGame;
        private readonly Light[] rims = new Light[2];
        private readonly List<GameObject> figures = new List<GameObject>();

        public bool IsOn(ArkhamSwitch s) => on[(int)s];
        public int CycleStep => cycle;

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < SwitchCount; i++) on[i] = true;
            if (remake != null)
            {
                remake.driven = true;
                // The "ours" test of RemakeDirector's material loop reads the class name in the material name: "lucy"
                // covers RB_lucy_c3_* and AK_lucy_*, so the loop can swap between them.
                if (remake.characterClasses.Length > 0) remake.characterClasses[0] = "lucy";
                int t = Array.IndexOf(RemakeDirector.TierSizes, gameTier);
                if (t >= 0 && t < remake.lucyTiers.Length) lucyGame = remake.lucyTiers[t];
                if (t >= 0 && t < remake.guardTiers.Length) guardGame = remake.guardTiers[t];
                remake.OnChanged += ApplyOverlays;
            }
            for (int i = 0; i < rims.Length; i++)
            {
                var go = new GameObject($"AK_Rim_{i}");
                go.transform.SetParent(transform, false);
                Light l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.shadows = LightShadows.None;
                l.color = rimColor;
                l.intensity = rimIntensity;
                l.range = rimRange;
                l.spotAngle = rimAngle;
                l.innerSpotAngle = rimAngle * 0.6f;
#if UNITY_EDITOR
                l.lightmapBakeType = LightmapBakeType.Realtime; // an editor-only property: the player build failed on it (2026-11-13)
#endif
                l.renderingLayerMask = (int)RimLayer;
                var data = l.GetUniversalAdditionalLightData();
                data.renderingLayers = RimLayer;
                go.SetActive(false);
                rims[i] = l;
            }
        }

        private void Start()
        {
            // The accent layer on every renderer but the walls and the ceiling (fault 3 of the first run: the accents lit
            // the white grout).
            var blocked = new HashSet<Renderer>(accentBlocked);
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r != null && !blocked.Contains(r)) r.renderingLayerMask |= AccentLayer;
            }
            if (remake != null) remake.SetProfileGame(roomAir, gameTier);
            ApplyAll();
        }

        private void OnDestroy()
        {
            if (remake != null) remake.OnChanged -= ApplyOverlays;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!GameInput.GetKeyDown(CycleKey)) return;
            // F3: AK all on, each switch off in turn, B-game, then around.
            Cycle((cycle + 1) % (SwitchCount + 2));
        }

        private void LateUpdate()
        {
            if (IsOn(ArkhamSwitch.Rim)) PlaceRims(Camera.main);
        }

        /// <summary>0 = AK all on, 1..8 = that switch off alone, 9 = B-game (all eight off).</summary>
        public void Cycle(int step)
        {
            cycle = step;
            for (int i = 0; i < SwitchCount; i++) on[i] = step == 0 || (step <= SwitchCount && i != step - 1);
            Current = step <= SwitchCount ? Profile.AK : Profile.BGame;
            Label = step == 0 ? "AK all on" : step <= SwitchCount ? $"AK, switch {step} off ({Names[step - 1]})" : "B-game (the control)";
            ApplyAll();
        }

        public void SetProfileAK() => Cycle(0);
        public void SetProfileGame() => Cycle(SwitchCount + 1);

        /// <summary>B-game with only this switch on (the cost of one switch).</summary>
        public void SetAlone(ArkhamSwitch s)
        {
            for (int i = 0; i < SwitchCount; i++) on[i] = i == (int)s;
            Current = Profile.Alone;
            Label = $"B-game + {Names[(int)s]} only";
            ApplyAll();
        }

        public void Set(ArkhamSwitch s, bool value)
        {
            on[(int)s] = value;
            ApplyAll();
        }

        public string Describe()
        {
            var parts = new char[SwitchCount];
            for (int i = 0; i < SwitchCount; i++) parts[i] = on[i] ? '1' : '0';
            return new string(parts);
        }

        public void ApplyAll()
        {
            // 5: the suits sit in the B-game tier slot, so RemakeDirector's own loop puts them on (and keeps them on).
            if (remake != null)
            {
                int t = Array.IndexOf(RemakeDirector.TierSizes, gameTier);
                bool suits = IsOn(ArkhamSwitch.Suits);
                if (t >= 0 && t < remake.lucyTiers.Length && lucyGame != null) remake.lucyTiers[t] = suits && lucySuit != null ? lucySuit : lucyGame;
                if (t >= 0 && t < remake.guardTiers.Length && guardGame != null) remake.guardTiers[t] = suits && guardSuit != null ? guardSuit : guardGame;
                remake.ApplyAll(); // calls ApplyOverlays through OnChanged
            }
            else ApplyOverlays();
        }

        /// <summary>Everything the RemakeDirector does not own; runs after each of its passes (it resets the floor material).</summary>
        private void ApplyOverlays()
        {
            // 1, 6, 8: the three volumes over the B-game grade (priority 20, 21, 22).
            if (gradeVolume != null) gradeVolume.weight = IsOn(ArkhamSwitch.Grade) ? 1f : 0f;
            if (practicalVolume != null) practicalVolume.weight = IsOn(ArkhamSwitch.Practicals) ? 1f : 0f;
            if (lensVolume != null) lensVolume.weight = IsOn(ArkhamSwitch.Lens) ? 1f : 0f;

            // 2: the rims, and the rim layer on the figures.
            bool rim = IsOn(ArkhamSwitch.Rim);
            FindFigures();
            foreach (GameObject f in figures)
            {
                foreach (Renderer r in f.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(r is SkinnedMeshRenderer)) continue;
                    r.renderingLayerMask = rim ? (r.renderingLayerMask | RimLayer) : (r.renderingLayerMask & ~RimLayer);
                }
            }
            foreach (Light l in rims) if (l != null) l.gameObject.SetActive(rim);
            if (rim) PlaceRims(Camera.main);

            // 3: the wet floor (after RemakeDirector put the B-game granite back).
            bool wet = IsOn(ArkhamSwitch.Wet);
            if (wet && wetMaterial != null) foreach (Renderer r in wetRenderers) if (r != null) r.sharedMaterial = wetMaterial;
            foreach (GameObject g in wetObjects) if (g != null) g.SetActive(wet);

            // 4: the planar reflection.
            if (planar != null) planar.SetOn(IsOn(ArkhamSwitch.Planar));

            // 6: the practicals and the flares.
            bool practicals = IsOn(ArkhamSwitch.Practicals);
            foreach (GameObject g in practicalObjects) if (g != null) g.SetActive(practicals);
            foreach (LensFlareComponentSRP f in flares) if (f != null) f.enabled = practicals;

            // 7: the leak.
            bool leak = IsOn(ArkhamSwitch.Leak);
            foreach (GameObject g in leakObjects) if (g != null) g.SetActive(leak);

            RunTelemetry.Event("arkham_switches", Describe());
            OnChanged?.Invoke();
        }

        private void FindFigures()
        {
            figures.Clear();
            foreach (string n in figureRoots)
            {
                GameObject g = GameObject.Find(n);
                if (g != null) figures.Add(g);
            }
        }

        /// <summary>Each rim spot behind its figure as seen from this camera, high and to one side, aimed at the chest.</summary>
        public void PlaceRims(Camera cam)
        {
            if (cam == null) return;
            if (figures.Count == 0) FindFigures();
            for (int i = 0; i < rims.Length; i++)
            {
                Light l = rims[i];
                if (l == null) continue;
                if (i >= figures.Count || figures[i] == null || !figures[i].activeInHierarchy) { l.enabled = false; continue; }
                l.enabled = true;
                Vector3 p = figures[i].transform.position;
                Vector3 back = p - cam.transform.position;
                back.y = 0f;
                back = back.sqrMagnitude > 1e-4f ? back.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, back) * (i == 0 ? 1f : -1f);
                Vector3 at = p + back * rimOffset.x + Vector3.up * rimOffset.y + side * rimOffset.z;
                l.transform.SetPositionAndRotation(at, Quaternion.LookRotation(p + Vector3.up * rimAimHeight - at));
            }
        }

        /// <summary>For the bot's hand renders: the rims and the planar mirror for this camera and this target size.</summary>
        public void PrepareRender(Camera cam, int width, int height)
        {
            if (IsOn(ArkhamSwitch.Rim)) PlaceRims(cam);
            if (planar != null && IsOn(ArkhamSwitch.Planar)) planar.RenderFor(cam, width, height);
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(GlorifiedBody.Automation.BotArgs.Bot)) return;
            GUI.Label(new Rect(12f, 8f, 1400f, 24f), $"ARKHAM LOOK (T38)  {Label}  switches {Describe()}  (F3 cycles)");
        }
    }
}
