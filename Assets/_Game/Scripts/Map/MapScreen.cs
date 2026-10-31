using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Interaction;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Map
{
    /// <summary>
    /// The map screen (Bruno, 2026-10-31). The Map key (M) opens it over the stopped room, on her floor: the rooms she
    /// knows (MapKnowledge) as their floor pieces, her room lighter, the doors out, the key places (locked doors red,
    /// open doors green, items, logs, save machines), and her place with the way she faces. A and D change the floor
    /// (only floors with a known room). The Map key or Back closes it. North is up. One object for the whole game
    /// ("[Map]", made at the start like the sound system), so no room needs a rebuild. Placeholder look: plain
    /// panels, the legacy font.
    /// </summary>
    public sealed class MapScreen : MonoBehaviour
    {
        public static MapScreen Instance { get; private set; }
        public static event Action OnOpened;
        public static event Action OnClosed;

        public static readonly Color RoomColour = new Color(0.22f, 0.3f, 0.36f, 1f);
        public static readonly Color HereColour = new Color(0.38f, 0.52f, 0.6f, 1f);
        public static readonly Color LockedColour = new Color(0.95f, 0.22f, 0.2f);
        public static readonly Color OpenColour = new Color(0.3f, 0.9f, 0.4f);
        public static readonly Color ItemColour = new Color(1f, 0.82f, 0.2f);
        public static readonly Color LogColour = new Color(0.96f, 0.95f, 0.88f);
        public static readonly Color SaveColour = new Color(0.35f, 0.65f, 1f);
        public static readonly Color HerColour = new Color(1f, 0.35f, 0.75f);

        private const float PlanWidth = 1560f;
        private const float PlanHeight = 720f;
        private const float MaxPixelsPerMetre = 40f;

        /// <summary>A key place as the screen shows it (tests).</summary>
        public struct ShownMark
        {
            public string Room;
            public MapMarkKind Kind;
            public string Path;
            public bool Open;
            public Vector2 Pixel;
        }

        private Canvas canvas;
        private GameObject root;
        private RectTransform plan;
        private Text titleText;
        private Text keysText;
        private Text noteText;
        private readonly List<GameObject> drawn = new List<GameObject>();
        private int openFrame = -1;
        private int floorIndex;
        private readonly List<string> floors = new List<string>();
        private readonly List<string> shownRooms = new List<string>();
        private readonly List<ShownMark> shownMarks = new List<ShownMark>();

        public Canvas Canvas => canvas;
        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>The floor on screen (its id), empty when no room is known.</summary>
        public string ShownFloor => floorIndex >= 0 && floorIndex < floors.Count ? floors[floorIndex] : "";
        /// <summary>The floors she can page through (each with a known room).</summary>
        public IReadOnlyList<string> Floors => floors;
        /// <summary>The scenes of the rooms on screen.</summary>
        public IReadOnlyList<string> ShownRooms => shownRooms;
        public IReadOnlyList<ShownMark> ShownMarks => shownMarks;
        /// <summary>Her place on the floor plan (floor metres), and on screen. Null when she is not on the shown floor.</summary>
        public Vector2? HerPlace { get; private set; }
        public Vector2? HerPixel { get; private set; }
        public float HerHeading { get; private set; }
        public string NoteShown => noteText != null ? noteText.text : "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;
            var obj = new GameObject("[Map]");
            DontDestroyOnLoad(obj);
            obj.AddComponent<MapScreen>();
        }

        private void Awake()
        {
            Instance = this;
            Build();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Hide();
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            if (!IsOpen)
            {
                if (gm.IsPlaying && Time.frameCount != openFrame && GameInput.GetActionDown(InputManager.GameAction.Map, KeyCode.M)) Open();
                return;
            }
            if (!gm.IsMap)
            {
                // Escape closed it through the GameManager.
                Hide();
                return;
            }
            if (Time.frameCount == openFrame) return;
            if (GameInput.GetActionDown(InputManager.GameAction.Map, KeyCode.M) || GameInput.GetActionDown(InputManager.GameAction.Back, KeyCode.S))
            {
                Close();
                return;
            }
            if (GameInput.GetActionDown(InputManager.GameAction.TurnLeft, KeyCode.A)) Page(-1);
            if (GameInput.GetActionDown(InputManager.GameAction.TurnRight, KeyCode.D)) Page(1);
        }

        /// <summary>Opens the map on her floor. False when the game is not playing or she is busy (a question, a scan).</summary>
        public bool Open()
        {
            GameManager gm = GameManager.Instance;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            if (gm == null || !gm.IsPlaying || lucy == null) return false;
            Interactor interactor = lucy.GetComponent<Interactor>();
            PsychicScanner scanner = lucy.GetComponent<PsychicScanner>();
            if ((interactor != null && interactor.Current != Interactor.State.Free) || (scanner != null && scanner.IsViewing)) return false;
            if (!gm.OpenMap()) return false;
            openFrame = Time.frameCount;
            root.SetActive(true);
            CollectFloors(lucy);
            MapAtlas atlas = MapAtlas.Instance;
            MapRoom here = atlas != null ? atlas.Room(SceneManager.GetActiveScene().name) : null;
            floorIndex = here != null ? floors.IndexOf(here.floor) : 0;
            if (floorIndex < 0) floorIndex = 0;
            Draw(lucy);
            RunTelemetry.Event("map_open", $"{ShownFloor}: {shownRooms.Count} rooms, {shownMarks.Count} marks");
            OnOpened?.Invoke();
            return true;
        }

        public void Close()
        {
            GameManager.Instance?.CloseMap();
            Hide();
        }

        /// <summary>The next or the previous floor she knows a room of.</summary>
        public void Page(int step)
        {
            if (!IsOpen || floors.Count < 2) return;
            floorIndex = ((floorIndex + step) % floors.Count + floors.Count) % floors.Count;
            Draw(GameObject.FindGameObjectWithTag("Player"));
            RunTelemetry.Event("map_floor", ShownFloor);
        }

        private void Hide()
        {
            if (!IsOpen) return;
            openFrame = Time.frameCount;
            root.SetActive(false);
            OnClosed?.Invoke();
        }

        private void CollectFloors(GameObject lucy)
        {
            floors.Clear();
            MapAtlas atlas = MapAtlas.Instance;
            if (atlas == null) return;
            ItemInventory items = lucy != null ? lucy.GetComponent<ItemInventory>() : null;
            foreach (MapFloor floor in atlas.floors)
            {
                foreach (MapRoom room in atlas.RoomsOn(floor.id))
                {
                    if (!MapKnowledge.Knows(room, items)) continue;
                    floors.Add(floor.id);
                    break;
                }
            }
        }

        // ── Draw ──────────────────────────────────────────────────────

        private void Draw(GameObject lucy)
        {
            foreach (GameObject obj in drawn) Destroy(obj);
            drawn.Clear();
            shownRooms.Clear();
            shownMarks.Clear();
            HerPlace = null;
            HerPixel = null;
            MapAtlas atlas = MapAtlas.Instance;
            string here = SceneManager.GetActiveScene().name;
            string keys = $"{Key(InputManager.GameAction.Map)} / {Key(InputManager.GameAction.Back)}  close";
            if (atlas == null || floors.Count == 0)
            {
                titleText.text = "MAP";
                noteText.text = "There is no map of this place.";
                keysText.text = keys;
                return;
            }
            string floorId = ShownFloor;
            MapFloor floor = atlas.Floor(floorId);
            ItemInventory items = lucy != null ? lucy.GetComponent<ItemInventory>() : null;
            bool hasPlan = MapKnowledge.HasPlan(items, floorId);
            titleText.text = "MAP  -  " + (floor != null ? floor.title : floorId);
            noteText.text = hasPlan ? "FLOOR PLAN: the whole floor shows." : atlas.Room(here) == null ? "There is no map of this room." : "";
            keysText.text = (floors.Count > 1 ? $"{Key(InputManager.GameAction.TurnLeft)} {Key(InputManager.GameAction.TurnRight)}  floor {floorIndex + 1} / {floors.Count}      " : "") + keys;

            // The scale fits the whole floor, known or not: a new room never moves the others.
            bool any = false;
            Rect all = default;
            foreach (MapRoom room in atlas.RoomsOn(floorId))
            {
                Rect b = room.Bounds;
                if (!any) { all = b; any = true; continue; }
                all.xMin = Mathf.Min(all.xMin, b.xMin);
                all.yMin = Mathf.Min(all.yMin, b.yMin);
                all.xMax = Mathf.Max(all.xMax, b.xMax);
                all.yMax = Mathf.Max(all.yMax, b.yMax);
            }
            float scale = Mathf.Min(MaxPixelsPerMetre, PlanWidth / Mathf.Max(1f, all.width), PlanHeight / Mathf.Max(1f, all.height));
            Vector2 centre = all.center;
            Vector2 Pixel(Vector2 metres) => (metres - centre) * scale;

            foreach (MapRoom room in atlas.RoomsOn(floorId))
            {
                if (!MapKnowledge.Knows(room, items)) continue;
                shownRooms.Add(room.scene);
                bool isHere = room.scene == here;
                foreach (Rect area in room.areas)
                {
                    Rect onPlan = new Rect(area.position + room.offset, area.size);
                    GameObject cell = Box("Room_" + room.scene, Pixel(onPlan.center), onPlan.size * scale, isHere ? HereColour : RoomColour);
                    cell.AddComponent<Outline>().effectColor = new Color(0.85f, 0.9f, 0.92f, 0.8f);
                }
                Text title = Label(plan.transform, "Title_" + room.scene, 18, FontStyle.Bold, new Color(0.92f, 0.95f, 0.96f), TextAnchor.MiddleCenter, new Vector2(300f, 30f));
                title.text = room.title;
                title.rectTransform.anchoredPosition = Pixel(room.Bounds.center) + new Vector2(0f, room.Bounds.height * scale * 0.5f - 20f);
                drawn.Add(title.gameObject);
                foreach (MapExit exit in room.exits)
                {
                    GameObject door = Box("Exit_" + exit.path, Pixel(room.OnPlan(exit.position)), new Vector2(10f, 10f), new Color(0.95f, 0.95f, 0.95f, 0.9f));
                    door.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }
                foreach (MapMark mark in room.marks)
                {
                    if (!MapKnowledge.IsThere(room, mark)) continue;
                    bool open = MapKnowledge.IsOpen(room, mark);
                    Vector2 at = Pixel(room.OnPlan(mark.position));
                    Box("Mark_" + mark.path, at, new Vector2(18f, 18f), ColourOf(mark.kind, open)).AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                    shownMarks.Add(new ShownMark { Room = room.scene, Kind = mark.kind, Path = mark.path, Open = open, Pixel = at });
                }
            }

            // Her place: a dot and a nose the way she faces, on her room's floor only.
            MapRoom hers = atlas.Room(here);
            if (lucy != null && hers != null && hers.floor == floorId)
            {
                Vector3 p = lucy.transform.position;
                HerPlace = hers.OnPlan(new Vector2(p.x, p.z));
                HerPixel = Pixel(HerPlace.Value);
                HerHeading = lucy.transform.eulerAngles.y;
                GameObject nose = Box("Her_Nose", HerPixel.Value, new Vector2(6f, 30f), HerColour);
                RectTransform rect = nose.GetComponent<RectTransform>();
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = HerPixel.Value;
                rect.localRotation = Quaternion.Euler(0f, 0f, -HerHeading);
                Box("Her", HerPixel.Value, new Vector2(20f, 20f), HerColour).AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            }
        }

        public static Color ColourOf(MapMarkKind kind, bool open)
        {
            switch (kind)
            {
                case MapMarkKind.LockedDoor: return open ? OpenColour : LockedColour;
                case MapMarkKind.Log: return LogColour;
                case MapMarkKind.Save: return SaveColour;
                default: return ItemColour;
            }
        }

        private GameObject Box(string name, Vector2 position, Vector2 size, Color colour)
        {
            GameObject obj = Panel(plan.transform, name, new Vector2(0.5f, 0.5f), position, size, colour);
            drawn.Add(obj);
            return obj;
        }

        private static string Key(InputManager.GameAction action)
        {
            KeyCode key = InputManager.Instance != null ? InputManager.Instance.GetKey(action) : KeyCode.None;
            return key == KeyCode.None ? action.ToString() : InputManager.GetKeyDisplayName(key);
        }

        // ── Build ─────────────────────────────────────────────────────

        private void Build()
        {
            var canvasObj = new GameObject("MapUI", typeof(RectTransform));
            canvasObj.transform.SetParent(transform, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            root = Panel(canvasObj.transform, "Root", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f), new Color(0.02f, 0.03f, 0.04f, 1f)); // opaque: the room behind does not show through
            GameObject sheet = Panel(root.transform, "Sheet", new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(PlanWidth + 120f, PlanHeight + 100f), new Color(0.07f, 0.09f, 0.11f, 1f));
            sheet.AddComponent<Outline>().effectColor = new Color(0.85f, 0.88f, 0.9f, 0.9f);
            var planObj = new GameObject("Plan", typeof(RectTransform));
            planObj.transform.SetParent(sheet.transform, false);
            plan = planObj.GetComponent<RectTransform>();
            plan.anchorMin = plan.anchorMax = new Vector2(0.5f, 0.5f);
            plan.sizeDelta = new Vector2(PlanWidth, PlanHeight);

            titleText = Label(root.transform, "Title", 34, FontStyle.Bold, new Color(0.7f, 0.95f, 0.95f), TextAnchor.MiddleLeft, new Vector2(1600f, 50f));
            titleText.rectTransform.anchoredPosition = new Vector2(0f, 485f);
            noteText = Label(root.transform, "Note", 22, FontStyle.Normal, new Color(0.95f, 0.92f, 0.8f), TextAnchor.MiddleRight, new Vector2(1600f, 40f));
            noteText.rectTransform.anchoredPosition = new Vector2(0f, 485f);
            keysText = Label(root.transform, "Keys", 20, FontStyle.Bold, new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleRight, new Vector2(1600f, 40f));
            keysText.rectTransform.anchoredPosition = new Vector2(0f, -490f);

            // The legend at the bottom left.
            var legend = new (string, Color)[] { ("YOU", HerColour), ("LOCKED", LockedColour), ("OPEN", OpenColour), ("ITEM", ItemColour), ("LOG", LogColour), ("SAVE", SaveColour), ("DOOR OUT", new Color(0.95f, 0.95f, 0.95f)) };
            for (int i = 0; i < legend.Length; i++)
            {
                float x = -800f + i * 140f;
                Panel(root.transform, "Legend_" + legend[i].Item1, new Vector2(0.5f, 0.5f), new Vector2(x, -490f), new Vector2(18f, 18f), legend[i].Item2);
                Text word = Label(root.transform, "LegendWord_" + legend[i].Item1, 18, FontStyle.Bold, new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleLeft, new Vector2(110f, 30f));
                word.text = legend[i].Item1;
                word.rectTransform.anchoredPosition = new Vector2(x + 72f, -490f);
            }
            root.SetActive(false);
        }

        private static GameObject Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color colour)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            obj.AddComponent<Image>().color = colour;
            return obj;
        }

        private static Text Label(Transform parent, string name, int size, FontStyle style, Color colour, TextAnchor anchor, Vector2 box)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = box;
            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = colour;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
