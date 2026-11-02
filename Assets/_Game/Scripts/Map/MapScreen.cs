using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Interaction;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Map
{
    /// <summary>
    /// The map (Bruno, 2026-10-31; a corner map since 2026-11-02: "it also should work in real-time i.e opening the map
    /// does not pause the game, and shows the player's location"). The Map key (M) shows it in the top left corner of
    /// the screen, and the game goes on: she walks, turns and fights with it on. It shows her floor: the rooms she knows
    /// (MapKnowledge) as their floor pieces, her room lighter, the doors out, the key places (locked doors red, open
    /// doors green, items, logs, save machines), and her place with the way she faces, moved every frame. The key
    /// places draw again when they change (a door opens, she takes an item). It stays on through a door and goes to her
    /// new floor. The arrow keys change the floor (MapPreviousFloor, MapNextFloor: A and D turn her). The Map key hides
    /// it. Under the pause, the inventory, a page, a cutscene and the death it hides, and it comes back after. North is
    /// up. One object for the whole game ("[Map]", made at the start like the sound system), so no room needs a
    /// rebuild. Placeholder look: plain panels, the legacy font.
    /// </summary>
    public sealed class MapScreen : MonoBehaviour
    {
        public static MapScreen Instance { get; private set; }
        public static event Action OnOpened;
        public static event Action OnClosed;

        public static readonly Color RoomColour = new Color(0.22f, 0.3f, 0.36f, 0.95f);
        public static readonly Color HereColour = new Color(0.38f, 0.52f, 0.6f, 0.95f);
        public static readonly Color LockedColour = new Color(0.95f, 0.22f, 0.2f);
        public static readonly Color OpenColour = new Color(0.3f, 0.9f, 0.4f);
        public static readonly Color ItemColour = new Color(1f, 0.82f, 0.2f);
        public static readonly Color LogColour = new Color(0.96f, 0.95f, 0.88f);
        public static readonly Color SaveColour = new Color(0.35f, 0.65f, 1f);
        public static readonly Color HerColour = new Color(1f, 0.35f, 0.75f);

        // The corner (1920 x 1080 reference): the top left, under the debug line of a dev build.
        public const float CornerWidth = 600f;
        public const float CornerHeight = 390f;
        private static readonly Vector2 CornerPlace = new Vector2(24f, -48f);
        private const float PlanWidth = 568f;
        private const float PlanHeight = 280f;
        private const float MaxPixelsPerMetre = 24f;
        private const float RefreshSeconds = 0.25f; // how often the key places are checked for a change
        private const float TitleMinPixels = 70f;  // a room narrower than this on screen shows no title

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
        private RectTransform herDot, herNose;
        private Text titleText;
        private Text keysText;
        private Text noteText;
        private readonly List<GameObject> drawn = new List<GameObject>();
        private int toggleFrame = -1;
        private bool wanted;
        private bool newRoom;
        private float nextRefresh;
        private int signature;
        private float scale = 1f;
        private Vector2 centre;
        private int floorIndex;
        private readonly List<string> floors = new List<string>();
        private readonly List<string> shownRooms = new List<string>();
        private readonly List<ShownMark> shownMarks = new List<ShownMark>();

        public Canvas Canvas => canvas;
        /// <summary>The player turned the map on (it can be hidden for a moment under the pause or a page).</summary>
        public bool IsOpen => wanted;
        /// <summary>On screen now.</summary>
        public bool IsShowing => root != null && root.activeSelf;
        /// <summary>The corner of the screen the map takes (reference pixels, from the top left).</summary>
        public Rect Corner => new Rect(CornerPlace.x, -CornerPlace.y, CornerWidth, CornerHeight);
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
        /// <summary>How many times the rooms and the key places were drawn (tests: a change draws again).</summary>
        public int Draws { get; private set; }

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
            if (mode != LoadSceneMode.Single) return;
            // Through a door it stays on and goes to her new floor; to the menu it goes off.
            if (scene.name == "Main_Menu") Close();
            else newRoom = true;
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            GameObject lucy = gm.IsPlaying ? GameObject.FindGameObjectWithTag("Player") : null;
            if (gm.IsPlaying && Time.frameCount != toggleFrame && GameInput.GetActionDown(InputManager.GameAction.Map, KeyCode.M))
            {
                if (wanted) Close();
                else Open();
            }
            if (wanted && lucy != null && Dead(lucy)) Close(); // the death fall (seam sweep, 2026-10-31)
            bool show = wanted && lucy != null;
            if (root.activeSelf != show) root.SetActive(show);
            if (!show) return;

            if (newRoom)
            {
                newRoom = false;
                OnHerFloor(lucy);
                Draw(lucy);
            }
            if (GameInput.GetActionDown(InputManager.GameAction.MapPreviousFloor, KeyCode.LeftArrow)) Page(-1);
            if (GameInput.GetActionDown(InputManager.GameAction.MapNextFloor, KeyCode.RightArrow)) Page(1);
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + RefreshSeconds;
                CollectFloors(lucy);
                if (Signature(lucy) != signature) Draw(lucy);
            }
            PlaceHer(lucy);
        }

        private static bool Dead(GameObject lucy)
        {
            GlorifiedBody.Core.Health health = lucy.GetComponentInChildren<GlorifiedBody.Core.Health>();
            return health != null && health.IsDead;
        }

        /// <summary>Shows the map on her floor. False when the game is not playing or she is dead.</summary>
        public bool Open()
        {
            GameManager gm = GameManager.Instance;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            if (gm == null || !gm.IsPlaying || lucy == null || Dead(lucy)) return false;
            if (wanted) return true;
            wanted = true;
            toggleFrame = Time.frameCount;
            root.SetActive(true);
            OnHerFloor(lucy);
            Draw(lucy);
            PlaceHer(lucy);
            Sfx.Ui(Sfx.UiOpen, 0.4f);
            RunTelemetry.Event("map_open", $"{ShownFloor}: {shownRooms.Count} rooms, {shownMarks.Count} marks");
            OnOpened?.Invoke();
            return true;
        }

        /// <summary>Hides the map.</summary>
        public void Close()
        {
            if (!wanted) return;
            wanted = false;
            toggleFrame = Time.frameCount;
            root.SetActive(false);
            Sfx.Ui(Sfx.UiClose, 0.4f);
            OnClosed?.Invoke();
        }

        /// <summary>The next or the previous floor she knows a room of.</summary>
        public void Page(int step)
        {
            if (!wanted || floors.Count < 2) return;
            floorIndex = ((floorIndex + step) % floors.Count + floors.Count) % floors.Count;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            Draw(lucy);
            PlaceHer(lucy);
            RunTelemetry.Event("map_floor", ShownFloor);
        }

        private void OnHerFloor(GameObject lucy)
        {
            CollectFloors(lucy);
            MapAtlas atlas = MapAtlas.Instance;
            MapRoom here = atlas != null ? atlas.Room(SceneManager.GetActiveScene().name) : null;
            floorIndex = here != null ? floors.IndexOf(here.floor) : 0;
            if (floorIndex < 0) floorIndex = 0;
        }

        private void CollectFloors(GameObject lucy)
        {
            string shown = ShownFloor;
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
            if (!string.IsNullOrEmpty(shown) && floors.Contains(shown)) floorIndex = floors.IndexOf(shown); // a new floor never moves the page
        }

        /// <summary>A number for what the map shows now: the room, the floor, the known rooms, each key place there and open.</summary>
        private int Signature(GameObject lucy)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + SceneManager.GetActiveScene().name.GetHashCode();
                hash = hash * 31 + ShownFloor.GetHashCode();
                hash = hash * 31 + floors.Count;
                MapAtlas atlas = MapAtlas.Instance;
                if (atlas == null) return hash;
                ItemInventory items = lucy != null ? lucy.GetComponent<ItemInventory>() : null;
                hash = hash * 31 + (MapKnowledge.HasPlan(items, ShownFloor) ? 1 : 0);
                foreach (MapRoom room in atlas.RoomsOn(ShownFloor))
                {
                    if (!MapKnowledge.Knows(room, items)) continue;
                    hash = hash * 31 + room.scene.GetHashCode();
                    foreach (MapMark mark in room.marks)
                        hash = hash * 31 + (MapKnowledge.IsThere(room, mark) ? 1 : 0) * 2 + (MapKnowledge.IsOpen(room, mark) ? 1 : 0);
                }
                return hash;
            }
        }

        // ── Draw ──────────────────────────────────────────────────────

        private void Draw(GameObject lucy)
        {
            foreach (GameObject obj in drawn) Destroy(obj);
            drawn.Clear();
            shownRooms.Clear();
            shownMarks.Clear();
            Draws++;
            signature = Signature(lucy);
            MapAtlas atlas = MapAtlas.Instance;
            string here = SceneManager.GetActiveScene().name;
            string keys = $"{Key(InputManager.GameAction.Map)} hide";
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
            keysText.text = (floors.Count > 1 ? $"{Key(InputManager.GameAction.MapPreviousFloor)} {Key(InputManager.GameAction.MapNextFloor)}  floor {floorIndex + 1} / {floors.Count}    " : "") + keys;

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
            scale = Mathf.Min(MaxPixelsPerMetre, PlanWidth / Mathf.Max(1f, all.width), PlanHeight / Mathf.Max(1f, all.height));
            centre = all.center;

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
                if (room.Bounds.width * scale >= TitleMinPixels)
                {
                    Text title = Label(plan.transform, "Title_" + room.scene, 12, FontStyle.Bold, new Color(0.92f, 0.95f, 0.96f), TextAnchor.MiddleCenter, new Vector2(room.Bounds.width * scale, 18f));
                    title.text = room.title;
                    title.rectTransform.anchoredPosition = Pixel(room.Bounds.center) + new Vector2(0f, room.Bounds.height * scale * 0.5f - 10f);
                    drawn.Add(title.gameObject);
                }
                foreach (MapExit exit in room.exits)
                {
                    GameObject door = Box("Exit_" + exit.path, Pixel(room.OnPlan(exit.position)), new Vector2(6f, 6f), new Color(0.95f, 0.95f, 0.95f, 0.9f));
                    door.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }
                foreach (MapMark mark in room.marks)
                {
                    if (!MapKnowledge.IsThere(room, mark)) continue;
                    bool open = MapKnowledge.IsOpen(room, mark);
                    Vector2 at = Pixel(room.OnPlan(mark.position));
                    Box("Mark_" + mark.path, at, new Vector2(10f, 10f), ColourOf(mark.kind, open)).AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                    shownMarks.Add(new ShownMark { Room = room.scene, Kind = mark.kind, Path = mark.path, Open = open, Pixel = at });
                }
            }
            herNose.SetAsLastSibling();
            herDot.SetAsLastSibling();
        }

        private Vector2 Pixel(Vector2 metres) => (metres - centre) * scale;

        /// <summary>Her place: a dot and a nose the way she faces, on her room's floor only. Every frame.</summary>
        private void PlaceHer(GameObject lucy)
        {
            HerPlace = null;
            HerPixel = null;
            MapAtlas atlas = MapAtlas.Instance;
            MapRoom hers = atlas != null ? atlas.Room(SceneManager.GetActiveScene().name) : null;
            bool on = lucy != null && hers != null && hers.floor == ShownFloor && floors.Count > 0;
            herDot.gameObject.SetActive(on);
            herNose.gameObject.SetActive(on);
            if (!on) return;
            Vector3 p = lucy.transform.position;
            HerPlace = hers.OnPlan(new Vector2(p.x, p.z));
            HerPixel = Pixel(HerPlace.Value);
            HerHeading = lucy.transform.eulerAngles.y;
            herDot.anchoredPosition = HerPixel.Value;
            herNose.anchoredPosition = HerPixel.Value;
            herNose.localRotation = Quaternion.Euler(0f, 0f, -HerHeading);
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
            canvas.sortingOrder = 40; // over the HUD, under the menus
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // The corner: a dark sheet you can see the room through at its edges.
            root = Panel(canvasObj.transform, "Root", new Vector2(0f, 1f), Vector2.zero, new Vector2(CornerWidth, CornerHeight), new Color(0.02f, 0.03f, 0.04f, 0.82f));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = CornerPlace;
            root.AddComponent<Outline>().effectColor = new Color(0.85f, 0.88f, 0.9f, 0.7f);
            var planObj = new GameObject("Plan", typeof(RectTransform));
            planObj.transform.SetParent(root.transform, false);
            plan = planObj.GetComponent<RectTransform>();
            plan.anchorMin = plan.anchorMax = new Vector2(0.5f, 0.5f);
            plan.sizeDelta = new Vector2(PlanWidth, PlanHeight);
            plan.anchoredPosition = new Vector2(0f, -4f);

            titleText = Label(root.transform, "Title", 18, FontStyle.Bold, new Color(0.7f, 0.95f, 0.95f), TextAnchor.MiddleLeft, new Vector2(CornerWidth - 24f, 26f));
            titleText.rectTransform.anchoredPosition = new Vector2(0f, CornerHeight * 0.5f - 18f);
            keysText = Label(root.transform, "Keys", 13, FontStyle.Bold, new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleRight, new Vector2(CornerWidth - 24f, 26f));
            keysText.rectTransform.anchoredPosition = new Vector2(0f, CornerHeight * 0.5f - 18f);
            noteText = Label(root.transform, "Note", 12, FontStyle.Normal, new Color(0.95f, 0.92f, 0.8f), TextAnchor.MiddleLeft, new Vector2(CornerWidth - 24f, 18f));
            noteText.rectTransform.anchoredPosition = new Vector2(0f, CornerHeight * 0.5f - 38f);

            // Her place: kept from draw to draw, moved every frame.
            herNose = Panel(plan.transform, "Her_Nose", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 16f), HerColour).GetComponent<RectTransform>();
            herNose.pivot = new Vector2(0.5f, 0f);
            herDot = Panel(plan.transform, "Her", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f), HerColour).GetComponent<RectTransform>();
            herDot.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            // The legend along the bottom.
            var legend = new (string, Color)[] { ("YOU", HerColour), ("LOCKED", LockedColour), ("OPEN", OpenColour), ("ITEM", ItemColour), ("LOG", LogColour), ("SAVE", SaveColour), ("DOOR", new Color(0.95f, 0.95f, 0.95f)) };
            float step = (CornerWidth - 24f) / legend.Length;
            for (int i = 0; i < legend.Length; i++)
            {
                float x = -CornerWidth * 0.5f + 12f + i * step + 6f;
                float y = -CornerHeight * 0.5f + 16f;
                Panel(root.transform, "Legend_" + legend[i].Item1, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(10f, 10f), legend[i].Item2);
                Text word = Label(root.transform, "LegendWord_" + legend[i].Item1, 12, FontStyle.Bold, new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleLeft, new Vector2(step - 14f, 20f));
                word.text = legend[i].Item1;
                word.rectTransform.anchoredPosition = new Vector2(x + 10f + (step - 14f) * 0.5f, y);
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
