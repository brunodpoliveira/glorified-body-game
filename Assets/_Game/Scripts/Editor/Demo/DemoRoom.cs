#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Automation;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Destruction;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Rooms;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>A doorway of a demo room: the wall it is in, its place along that wall (world metres), the room behind it.</summary>
    public sealed class Door
    {
        public char Side;        // 'N' (+z), 'S' (-z), 'E' (+x), 'W' (-x)
        public float At;         // the world x (N, S) or z (E, W) of the doorway's middle
        public string To;        // the room id behind it, or "" for a fire door that a flag shuts
        public float Width = 1.6f;
        public string Flag = ""; // "": always open; with To "": the leaf shows when the flag goes up (code silver)
        public bool Fire;        // a fire door at a chokepoint (2026-11-03): open at the start, a lever jams it, the soldiers breach it
        public float Height = DemoRoom.DoorHeight; // the power armour (2.6 m) needs a taller doorway
        public int Pocket = 1;   // a fire door's pocket: +1 or -1 along the wall (the room that lists the doorway first builds it)
        public bool Jammed;      // a fire door shut at the start
        public string LeverFlag = ""; // her levers do nothing until this flag goes up
        public string JamFlag = "";   // a fire door shuts and jams when this flag goes up (code silver)
        public string LeverRoom = ""; // a fire door with one lever: on the face of this room. Empty: both faces. "none": no lever
        public bool Unbreakable;      // a fire door no kick breaks (the service exit of G19: a push bar on one side)
        public Door(char side, float at, string to, float width = 1.6f, string flag = "", bool fire = false, float height = DemoRoom.DoorHeight, int pocket = 1, bool jammed = false, string leverFlag = "", string jamFlag = "", string leverRoom = "", bool unbreakable = false) { Side = side; At = at; To = to; Width = width; Flag = flag; Fire = fire; Height = height; Pocket = pocket; Jammed = jammed; LeverFlag = leverFlag; JamFlag = jamFlag; LeverRoom = leverRoom; Unbreakable = unbreakable; }
    }

    /// <summary>One room of the demo: its id, its title, its floor, its place on the floor plan, its doors, its dressing.</summary>
    public sealed class RoomSpec
    {
        public string Id, Title, Floor, Music;
        public float X0, Z0, X1, Z1, Height = 3f;
        public readonly List<Door> Doors = new List<Door>();
        public Action<DemoRoom> Dress;
        public float Width => X1 - X0;
        public float Depth => Z1 - Z0;
        public Vector3 Centre => new Vector3((X0 + X1) * 0.5f, 0f, (Z0 + Z1) * 0.5f);
    }

    /// <summary>
    /// The walls, lintels and fire doors of the world, built once (2026-11-02, the world scene: two rooms share a wall
    /// line, and each used to build its own copy into the other's floor). A wall stands on the room line, half in each
    /// room, as tall as the taller room on that line.
    /// </summary>
    public sealed class WallLedger
    {
        private readonly Dictionary<string, List<(float a, float b)>> built = new Dictionary<string, List<(float a, float b)>>();
        private readonly HashSet<string> once = new HashSet<string>();
        private readonly List<RoomSpec> specs;

        public WallLedger(List<RoomSpec> allSpecs) { specs = allSpecs; }

        private static string Line(string floor, bool alongX, float at) => $"{floor}|{(alongX ? "z" : "x")}|{at:0.00}";

        /// <summary>The parts of a..b on the line that no room built yet; they count as built now.</summary>
        public List<(float a, float b)> Claim(string floor, bool alongX, float at, float a, float b)
        {
            string key = Line(floor, alongX, at);
            if (!built.TryGetValue(key, out List<(float a, float b)> done)) built[key] = done = new List<(float a, float b)>();
            var free = new List<(float a, float b)> { (a, b) };
            foreach ((float da, float db) in done)
            {
                var next = new List<(float a, float b)>();
                foreach ((float fa, float fb) in free)
                {
                    if (db <= fa || da >= fb) { next.Add((fa, fb)); continue; }
                    if (da > fa) next.Add((fa, da));
                    if (db < fb) next.Add((db, fb));
                }
                free = next;
            }
            free.RemoveAll(p => p.b - p.a < 0.05f);
            done.AddRange(free);
            return free;
        }

        /// <summary>True the first time for the key (a lintel, a fire door: two rooms list the same doorway).</summary>
        public bool Once(string key) => once.Add(key);

        /// <summary>The tallest room with a side on the line over a..b (the wall is as tall as that room).</summary>
        public float HeightOn(string floor, bool alongX, float at, float a, float b, float own)
        {
            float h = own;
            foreach (RoomSpec s in specs)
            {
                if (s.Floor != floor) continue;
                bool onLine = alongX ? Mathf.Abs(s.Z0 - at) < 0.01f || Mathf.Abs(s.Z1 - at) < 0.01f : Mathf.Abs(s.X0 - at) < 0.01f || Mathf.Abs(s.X1 - at) < 0.01f;
                if (!onLine) continue;
                float s0 = alongX ? s.X0 : s.Z0, s1 = alongX ? s.X1 : s.Z1;
                if (s1 > a && s0 < b) h = Mathf.Max(h, s.Height);
            }
            return h;
        }
    }

    /// <summary>
    /// The greybox of a demo room (2026-10-31, Bruno: "create the floor 2 and 1 of the demo in greybox"; the demo plan
    /// §3, §4, §7, round 18: "build the first play-through all in greybox"). Since 2026-11-02 (Bruno: "i want all the map
    /// to be built and loaded up in one go") a room is a root of the world scene (DemoWorld), not a scene: its Room
    /// component, the floor, its walls with their doorways (the ledger builds a shared wall once), its cameras on a grid
    /// of cells, the music key, then the room's own dressing (DemoWard, DemoPublic). A doorway opens straight into the
    /// next room. The rooms are black boxes with grey lines: the people read on them (T22). The builder makes each room
    /// at the plan coordinates with the other rooms off, and DemoWorld moves the rooms of floor 1 to their place after.
    /// </summary>
    public sealed class DemoRoom
    {
        public const string Folder = MenuLoopSetup.ScenesFolder + "/Demo";
        public const float Wall = 0.3f, CellMax = 9f, DoorHeight = 2.3f;
        public const float WallShade = 0.22f; // lighter than the dark clear colour (0.12 was the same tone: the floors seemed to float)

        /// <summary>Lucy in the world scene (DemoWorld makes her before the rooms): the W1 dressing places her.</summary>
        public static GameObject Lucy;

        public readonly RoomSpec Spec;
        public readonly GameObject Root, Room, Props, Figures, Story;
        public readonly CharacterCatalog Catalog;
        public readonly List<FixedCamera> Cameras = new List<FixedCamera>();
        private readonly WallLedger ledger;

        private DemoRoom(RoomSpec spec, GameObject root, CharacterCatalog catalog, WallLedger walls)
        {
            Spec = spec;
            Root = root;
            Catalog = catalog;
            ledger = walls;
            Room = MenuLoopSetup.EnsureChild(root, "[Room]");
            Props = MenuLoopSetup.EnsureChild(root, "[Props]");
            Figures = MenuLoopSetup.EnsureChild(root, "[Figures]");
            Story = MenuLoopSetup.EnsureChild(root, "[Story]");
        }

        /// <summary>Builds the room under its root (at the world origin, the other rooms off): the shell, the dressing, the cameras, the music.</summary>
        public static DemoRoom Build(RoomSpec spec, GameObject root, CharacterCatalog catalog, WallLedger walls)
        {
            var room = new DemoRoom(spec, root, catalog, walls);
            Room component = MenuLoopSetup.EnsureComponent<Room>(root);
            component.id = spec.Id;
            component.title = spec.Title;
            component.floor = spec.Floor;
            component.rect = new Vector4(spec.X0, spec.Z0, spec.X1, spec.Z1);
            component.height = spec.Height;
            room.Shell();
            spec.Dress?.Invoke(room);
            Physics.SyncTransforms();
            component.exits = spec.Doors.ConvertAll(d => new Room.Exit { mouth = room.Mouth(d), outward = Outward(d.Side), width = d.Width, to = d.To }).ToArray();
            room.CamerasOnCells();
            foreach (Action add in room.extraCameras) add();
            GameObject music = MenuLoopSetup.EnsureChild(root, "[Music]");
            Audio.RoomMusic key = MenuLoopSetup.EnsureComponent<Audio.RoomMusic>(music);
            key.Key = spec.Music;
            EditorUtility.SetDirty(key);
            Debug.Log($"[DemoRoom] built {spec.Id}: {spec.Title}, {spec.Width:0} x {spec.Depth:0} m, {spec.Doors.Count} doors, {room.Cameras.Count} cameras");
            return room;
        }

        // ── The shell ─────────────────────────────────────────────────

        private void Shell()
        {
            RoomSpec s = Spec;
            Box(Room, "Floor", new Vector3(s.Centre.x, -0.05f, s.Centre.z), new Vector3(s.Width, 0.1f, s.Depth), Grey(0.42f));
            Ceiling(Room, new Vector3(s.Centre.x, s.Height, s.Centre.z), new Vector3(s.Width + Wall, s.Depth + Wall));
            WallSide('N', s.X0, s.X1, s.Z1);
            WallSide('S', s.X0, s.X1, s.Z0);
            WallSide('E', s.Z0, s.Z1, s.X1);
            WallSide('W', s.Z0, s.Z1, s.X0);
            foreach (Door d in s.Doors) Doorway(d);
        }

        /// <summary>
        /// The ceiling of a room, its underside at the room's height (Bruno, 2026-11-07: "in some areas the walls are not fully
        /// built, so i can see through"): with no ceiling, the top of a frame looked over the far walls into the other rooms,
        /// and from floor 1 up into floor 2. It has the tone of the dark clear colour, no collider and no shadow: the sun still
        /// lights the floor, and nothing in the game hits it. RoomSight stops its rays at this height.
        /// </summary>
        public static GameObject Ceiling(GameObject shell, Vector3 underside, Vector2 plan)
        {
            GameObject ceiling = RoomKit.Box(shell, "Ceiling", underside + Vector3.up * 0.05f, new Vector3(plan.x, 0.1f, plan.y));
            ceiling.GetComponent<Renderer>().sharedMaterial = Grey(0.12f);
            ceiling.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Collider c = ceiling.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.DestroyImmediate(c);
            return ceiling;
        }

        /// <summary>One wall with a gap at each doorway on it. along0..along1 run along the wall; at is the wall's line.</summary>
        private void WallSide(char side, float along0, float along1, float at)
        {
            bool alongX = side == 'N' || side == 'S';
            var gaps = new List<(float a, float b)>();
            var tops = new Dictionary<float, float>();
            foreach (Door d in Spec.Doors)
                if (d.Side == side)
                {
                    gaps.Add((d.At - d.Width * 0.5f, d.At + d.Width * 0.5f));
                    tops[d.At - d.Width * 0.5f] = d.Height;
                }
            gaps.Sort((p, q) => p.a.CompareTo(q.a));
            float from = along0 - Wall * 0.5f;
            int piece = 0;
            var pieces = new List<(float a, float b)>();
            foreach ((float a, float b) in gaps)
            {
                pieces.Add((from, a));
                from = b;
            }
            pieces.Add((from, along1 + Wall * 0.5f));
            foreach ((float a, float b) in pieces)
                foreach ((float fa, float fb) in ledger.Claim(Spec.Floor, alongX, at, a, b))
                    WallPiece(side, fa, fb, at, ledger.HeightOn(Spec.Floor, alongX, at, fa, fb, Spec.Height), piece++);
            // Over each doorway: the lintel (once for the two rooms of the doorway).
            foreach ((float a, float b) in gaps)
                if (ledger.Once($"lintel|{Spec.Floor}|{(alongX ? "z" : "x")}|{at:0.00}|{a:0.00}"))
                    Lintel(side, a, b, at, ledger.HeightOn(Spec.Floor, alongX, at, a, b, Spec.Height), tops[a]);
        }

        private void WallPiece(char side, float a, float b, float at, float h, int index)
        {
            if (b - a < 0.05f) return;
            float mid = (a + b) * 0.5f, len = b - a;
            Vector3 pos = side == 'N' || side == 'S' ? new Vector3(mid, h * 0.5f, at) : new Vector3(at, h * 0.5f, mid);
            Vector3 size = side == 'N' || side == 'S' ? new Vector3(len, h, Wall) : new Vector3(Wall, h, len);
            Box(Room, $"Wall_{side}_{index}", pos, size, Grey(WallShade));
        }

        private void Lintel(char side, float a, float b, float at, float h, float top)
        {
            if (h - top < 0.05f) return;
            Vector3 pos = side == 'N' || side == 'S' ? new Vector3((a + b) * 0.5f, (h + top) * 0.5f, at) : new Vector3(at, (h + top) * 0.5f, (a + b) * 0.5f);
            Vector3 size = side == 'N' || side == 'S' ? new Vector3(b - a, h - top, Wall) : new Vector3(Wall, h - top, b - a);
            Box(Room, $"Lintel_{side}_{a:0.0}", pos, size, Grey(WallShade));
        }

        /// <summary>
        /// A doorway opens into the next room. A fire door (no room named, a flag) gets its steel leaf, shown when the flag
        /// goes up (code silver): the two rooms list the same doorway, the ledger builds the leaf once.
        /// </summary>
        private void Doorway(Door d)
        {
            if (d.Fire && !string.IsNullOrEmpty(d.To))
            {
                // A fire door at a chokepoint: the two rooms list the same doorway, the ledger builds it once (in the first room).
                Vector3 at = Mouth(d);
                if (ledger.Once($"door|{Spec.Floor}|{at.x:0.0}|{at.z:0.0}"))
                    GlorifiedBody.Editor.Destruction.BreakableAssets.Door(Props, "FireDoor_" + Spec.Id + "_" + d.To, at, Outward(d.Side), d.Width, d.Height, Wall, d.Pocket,
                        Grey(0.34f), Grey(0.5f), Colour("fire_band", new Color(0.55f, 0.12f, 0.1f)), Grey(0.12f), Colour("fire_lever", new Color(0.85f, 0.15f, 0.1f)),
                        Colour("jam_bar", new Color(0.8f, 0.66f, 0.18f)), d.Jammed, d.LeverFlag, d.JamFlag,
                        string.IsNullOrEmpty(d.LeverRoom) ? 0 : d.LeverRoom == "none" ? 2 : d.LeverRoom == Spec.Id ? -1 : 1, // -1: the face inside this room, 2: no lever
                        canBreak: !d.Unbreakable);
                return;
            }
            if (!string.IsNullOrEmpty(d.To) || string.IsNullOrEmpty(d.Flag)) return;
            Vector3 mouth = Mouth(d);
            if (!ledger.Once($"fire|{Spec.Floor}|{mouth.x:0.0}|{mouth.z:0.0}")) return;
            Vector3 outward = Outward(d.Side);
            Vector3 across = new Vector3(outward.z, 0f, -outward.x);
            string tag = "Fire_" + d.Side + "_" + d.At.ToString("0");
            GameObject holder = MenuLoopSetup.EnsureChild(Story, "Gate_" + tag);
            FlagGate gate = MenuLoopSetup.EnsureComponent<FlagGate>(holder);
            gate.flag = d.Flag;
            gate.showWhenRaised = true;
            Carve(Box(holder, "Door_" + tag, mouth + Vector3.up * DoorHeight * 0.5f, Abs(across * d.Width + Vector3.up * DoorHeight + outward * 0.1f), Grey(0.22f)));
        }

        /// <summary>
        /// A door leaf stays out of the NavMesh bake and carves it while it stands in its doorway: a fire door that a flag
        /// shows, a locked door that a key slides away. The chasers then find the way through an open door only.
        /// </summary>
        public static void Carve(GameObject leaf)
        {
            MenuLoopSetup.EnsureComponent<Unity.AI.Navigation.NavMeshModifier>(leaf).ignoreFromBuild = true;
            UnityEngine.AI.NavMeshObstacle obstacle = MenuLoopSetup.EnsureComponent<UnityEngine.AI.NavMeshObstacle>(leaf);
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one; // the leaf's own scale gives the box
            obstacle.carving = true;
        }

        public Vector3 Mouth(Door d)
        {
            switch (d.Side)
            {
                case 'N': return new Vector3(d.At, 0f, Spec.Z1);
                case 'S': return new Vector3(d.At, 0f, Spec.Z0);
                case 'E': return new Vector3(Spec.X1, 0f, d.At);
                default: return new Vector3(Spec.X0, 0f, d.At);
            }
        }

        public static Vector3 Outward(char side) => side == 'N' ? Vector3.forward : side == 'S' ? Vector3.back : side == 'E' ? Vector3.right : Vector3.left;

        public Vector3 InsideDoor(Door d, float metres) => Mouth(d) - Outward(d.Side) * metres;

        public float InwardYaw(Door d) => Quaternion.LookRotation(-Outward(d.Side)).eulerAngles.y;

        public Door DoorTo(string id) => Spec.Doors.Find(d => d.To == id);

        // ── The cameras ───────────────────────────────────────────────

        /// <summary>
        /// A camera for each cell of the room (cells of 9 m or less) and a zone on the cell. Each cell tries camera poses (its
        /// corners and edge middles, and 2 m past an inner edge; two aims; three lenses) and keeps the pose that sees the most
        /// of its floor by the coverage rule (CameraCoverage.Sees). A cell that misses any point splits in two along its
        /// long side, four times at most (Bruno, 2026-10-31: "I prefer no hiding/blind spots"). The camera coverage test
        /// checks every floor point (DemoTests). The zones end at the room's walls: in a doorway the room she came from
        /// keeps its camera until she is through (the zone of the next room starts at the wall line).
        /// </summary>
        private readonly List<Action> extraCameras = new List<Action>();

        /// <summary>
        /// One more camera with its zone, over the cells of the room (a beat the cell's camera shows badly: the W1 door,
        /// 2026-11-05). The zone takes priority over the cells, and its floor points must be in this camera's frame.
        /// </summary>
        public void ExtraCamera(string tag, Vector3 cam, Vector3 look, float fov, Vector3 zoneCentre, Vector3 zoneSize)
        {
            extraCameras.Add(() =>
            {
                GameObject root = MenuLoopSetup.EnsureChild(Root, "[Cameras]");
                FixedCamera fc = MenuLoopSetup.EnsureFixedCamera(root, $"Cam_{Spec.Id}_{tag}", cam, look, fov);
                Cameras.Add(fc);
                MenuLoopSetup.EnsureCameraZone(root, $"Zone_{Spec.Id}_{tag}", zoneCentre, zoneSize, fc, 2);
                MenuLoopSetup.EnsureShotPoints(new[] { fc });
            });
        }

        private void CamerasOnCells()
        {
            GameObject root = MenuLoopSetup.EnsureChild(Root, "[Cameras]");
            RoomSpec s = Spec;
            int nx = Mathf.Max(1, Mathf.CeilToInt(s.Width / CellMax)), nz = Mathf.Max(1, Mathf.CeilToInt(s.Depth / CellMax));
            float cw = s.Width / nx, cd = s.Depth / nz;
            var cells = new Queue<(Rect cell, int splits)>();
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    cells.Enqueue((new Rect(s.X0 + i * cw, s.Z0 + j * cd, cw, cd), 0));
            while (cells.Count > 0)
            {
                (Rect cell, int splits) = cells.Dequeue();
                (Vector3 cam, Vector3 look, float fov, float missed) = BestPose(cell);
                if (missed > 0f && splits < 4 && Mathf.Max(cell.width, cell.height) > 1.5f)
                {
                    bool alongX = cell.width >= cell.height;
                    Rect a = alongX ? new Rect(cell.x, cell.y, cell.width * 0.5f, cell.height) : new Rect(cell.x, cell.y, cell.width, cell.height * 0.5f);
                    Rect b = alongX ? new Rect(cell.center.x, cell.y, cell.width * 0.5f, cell.height) : new Rect(cell.x, cell.center.y, cell.width, cell.height * 0.5f);
                    cells.Enqueue((a, splits + 1));
                    cells.Enqueue((b, splits + 1));
                    continue;
                }
                string tag = Cameras.Count.ToString("00");
                FixedCamera fc = MenuLoopSetup.EnsureFixedCamera(root, $"Cam_{s.Id}_{tag}", cam, look, fov);
                Cameras.Add(fc);
                MenuLoopSetup.EnsureCameraZone(root, $"Zone_{s.Id}_{tag}", new Vector3(cell.center.x, 1.5f, cell.center.y), new Vector3(cell.width, 3f, cell.height), fc, 1);
                if (missed > 0f) Debug.Log($"[DemoRoom] {s.Id} {fc.cameraName}: {missed:P0} of its cell out of frame");
            }
            MenuLoopSetup.EnsureShotPoints(Cameras.ToArray());
        }

        private static readonly float[] Lenses = { 62f, 72f, 84f };

        /// <summary>The camera pose that sees the most of a cell's floor points, and the share it misses.</summary>
        private (Vector3 cam, Vector3 look, float fov, float missed) BestPose(Rect cell)
        {
            RoomSpec s = Spec;
            int solid = 1 << LayerMask.NameToLayer("Default");
            // The test's grid (CameraCoverage.Check: 0.45 m in from the floor's edge, 0.5 m apart), the cell grown by 0.3 m:
            // a point on the edge between two zones can fall to either camera.
            var points = new List<Vector3>();
            for (float x = s.X0 + 0.45f; x <= s.X1 - 0.45f; x += 0.5f)
                for (float z = s.Z0 + 0.45f; z <= s.Z1 - 0.45f; z += 0.5f)
                {
                    if (x < cell.xMin - 0.3f || x > cell.xMax + 0.3f || z < cell.yMin - 0.3f || z > cell.yMax + 0.3f) continue;
                    var p = new Vector3(x, 0f, z);
                    if (Physics.CheckBox(p + Vector3.up * 0.9f, new Vector3(0.2f, 0.8f, 0.2f), Quaternion.identity, solid, QueryTriggerInteraction.Ignore)) continue;
                    points.Add(p);
                }
            // The exits of the cell (Bruno, 2026-11-05: "i also need to see the exits"): where she stands as she goes to them.
            List<Door> exits = s.Doors.FindAll(d =>
            {
                Vector3 p = InsideDoor(d, 0.7f);
                return p.x >= cell.xMin - 0.3f && p.x <= cell.xMax + 0.3f && p.z >= cell.yMin - 0.3f && p.z <= cell.yMax + 0.3f;
            });
            const int ExitWeight = 100000; // an exit counts before any floor point
            float camY = Mathf.Min(s.Height - 0.25f, 3.4f);
            float[] xs = Places(cell.xMin, cell.xMax, s.X0, s.X1), zs = Places(cell.yMin, cell.yMax, s.Z0, s.Z1);
            Vector3 middle = new Vector3(cell.center.x, 0.7f, cell.center.y);
            (Vector3 cam, Vector3 look, float fov, int seen) best = (new Vector3(xs[0], camY, zs[0]), middle, Lenses[0], -1);
            foreach (float x in xs)
            {
                foreach (float z in zs)
                {
                    var cam = new Vector3(x, camY, z);
                    if (Mathf.Abs(x - cell.center.x) < 0.5f && Mathf.Abs(z - cell.center.y) < 0.5f) continue; // the middle sees nothing behind it
                    if (Physics.CheckSphere(cam, 0.2f, solid, QueryTriggerInteraction.Ignore)) continue;     // inside a column or a lintel
                    Vector3 away = new Vector3(middle.x - x, 0f, middle.z - z);
                    var aims = new List<Vector3> { middle, middle + away.normalized * Mathf.Min(cell.width, cell.height) * 0.25f };
                    foreach (Door d in exits) aims.Add(Vector3.Lerp(middle, Mouth(d) + Vector3.up, 0.5f));
                    foreach (Vector3 look in aims)
                    {
                        Quaternion rotation = Quaternion.LookRotation(look - cam);
                        foreach (float fov in Lenses)
                        {
                            int seen = 0;
                            foreach (Vector3 p in points) if (CameraCoverage.Sees(cam, rotation, fov, p, CameraCoverage.GameRoomNear, solid)) seen++;
                            foreach (Door d in exits) if (CameraCoverage.SeesExit(cam, rotation, fov, Mouth(d), Outward(d.Side), d.Width, solid)) seen += ExitWeight;
                            if (seen > best.seen) best = (cam, look, fov, seen); // equal counts keep the narrower lens (the earlier one)
                        }
                    }
                }
            }
            int exitsSeen = best.seen / ExitWeight, pointsSeen = best.seen % ExitWeight;
            float missed = exitsSeen < exits.Count ? 1f : points.Count == 0 ? 0f : 1f - (float)pointsSeen / points.Count;
            return (best.cam, best.look, best.fov, missed);
        }

        /// <summary>Camera places along one axis of a cell: its two edges, its middle, and 2 m past each edge, all inside the room's walls.</summary>
        private static float[] Places(float a, float b, float room0, float room1)
        {
            var places = new List<float>();
            foreach (float v in new[] { a, (a + b) * 0.5f, b, a - 2f, b + 2f })
            {
                float inside = Mathf.Clamp(v, room0 + 0.35f, room1 - 0.35f);
                if (!places.Exists(q => Mathf.Abs(q - inside) < 0.3f)) places.Add(inside);
            }
            return places.ToArray();
        }

        // ── Helpers for the dressing ──────────────────────────────────

        /// <summary>A static box (a prop, a counter, a bed) on the Default layer: it blocks, the NavMesh reads it.</summary>
        public GameObject Box(GameObject parent, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject obj = RoomKit.Box(parent, name, position, size);
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        /// <summary>A prop on the floor: its foot at the point, its size in metres.</summary>
        public GameObject Prop(string name, Vector3 floorPoint, Vector3 size, float shade = 0.3f)
        {
            GameObject obj = BreakableType(name, out Breakable.Kind kind, out float health)
                ? GlorifiedBody.Editor.Destruction.BreakableAssets.PropBox(Props, name, Spec.Id + "_" + name, floorPoint, size, kind, health, Grey(shade), Grey(Mathf.Min(1f, shade + 0.25f))).gameObject
                : Box(Props, name, floorPoint + Vector3.up * size.y * 0.5f, size, Grey(shade));
            Examine(obj, name, size);
            return obj;
        }

        /// <summary>
        /// A planter that mk_orzabal moves (DEMO_PLAN §10.5, CANON round 43): a lane, or a wall against the pinch, or a stop in a
        /// doorway against guards (DoorStop). A prop with a Rigidbody: the mouse carry lifts it, the NavMesh goes around it where it
        /// rests, the save keeps its place (MovableProp). Not breakable: a thrown one stays whole.
        /// </summary>
        public GameObject Planter(string name, Vector3 floorPoint, Vector3 size, float mass = 60f)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(Props.transform, false);
            obj.transform.position = floorPoint + Vector3.up * size.y * 0.5f;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = Colour("planter", new Color(0.36f, 0.33f, 0.3f));
            LockOnTargetable target = MenuLoopSetup.EnsureComponent<LockOnTargetable>(obj);
            var so = new SerializedObject(target);
            so.FindProperty("fallbackHeight").floatValue = 0f; // the lock point is the centre
            so.FindProperty("isProp").boolValue = true;
            so.FindProperty("heavy").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            Rigidbody body = MenuLoopSetup.EnsureComponent<Rigidbody>(obj);
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            UnityEngine.AI.NavMeshObstacle obstacle = MenuLoopSetup.EnsureComponent<UnityEngine.AI.NavMeshObstacle>(obj);
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            MenuLoopSetup.EnsureComponent<MovableProp>(obj);
            MenuLoopSetup.EnsureComponent<DoorStop>(obj);
            Examine(obj, name, size);
            return obj;
        }

        /// <summary>
        /// A container with items inside (Bruno, 2026-11-08: "cupboards already cleaned up of items"): the prop, and the items
        /// as hidden pickups under it. The Interact key asks to take each one, then says "Nothing else of note here."
        /// </summary>
        public Examinable Container(string name, Vector3 floorPoint, Vector3 size, float shade, params ItemDefinition[] items)
        {
            Examinable look = Prop(name, floorPoint, size, shade).GetComponent<Examinable>();
            look.contents.Clear();
            for (int i = 0; i < items.Length; i++)
            {
                GameObject obj = ItemAssets.Pickup(look.gameObject, name + "_Item_" + i, items[i], PrimitiveType.Cube, floorPoint + Vector3.up * size.y * 0.5f, Vector3.one * 0.1f, Quaternion.identity);
                obj.SetActive(false); // inside: only the container's question reaches it
                look.contents.Add(obj.GetComponent<ItemPickup>());
            }
            EditorUtility.SetDirty(look);
            return look;
        }

        /// <summary>
        /// The common interaction on a prop (Examinable): her comment by the prop's type, from the writer's
        /// game/examine.csv (type,line) when it has the type, else the placeholder below. The structure gets none.
        /// </summary>
        private static void Examine(GameObject obj, string name, Vector3 size)
        {
            string type = PropType(name);
            if (NotExamined.Contains(type)) return;
            Examinable look = MenuLoopSetup.EnsureComponent<Examinable>(obj);
            look.thing = "the " + type.Replace('_', ' ').ToLowerInvariant();
            look.comment = ExamineLine(type);
            look.range = Mathf.Max(size.x, size.z) * 0.5f + 1.0f;
            EditorUtility.SetDirty(look);
        }

        /// <summary>The type of a prop: its name, or the name before its last "_tag" ("Desk_Nurse" is a Desk).</summary>
        public static string PropType(string name)
        {
            WriterLines();
            for (string type = name; ; type = type.Substring(0, type.LastIndexOf('_')))
            {
                if (ExamineLines.ContainsKey(type) || writerLines.ContainsKey(type) || NotExamined.Contains(type)) return type;
                if (type.LastIndexOf('_') <= 0) return name; // a type with no line: the whole name
            }
        }

        private static readonly HashSet<string> NotExamined = new HashSet<string>
        {
            "Wall", "Stall_Wall", "Handrail", "Bars_Rail", "Stairs", "Sheeting", "Ramp", "Step", "Lintel", "Strap",
        };

        // PLACEHOLDER lines (Bruno, 2026-11-08: "nothing else important here or nothing of note here something like that").
        // The writer's game/examine.csv replaces any of them by type.
        private static readonly Dictionary<string, string> ExamineLines = new Dictionary<string, string>
        {
            { "Desk", "A desk. Nothing of note here." },
            { "Reception_Desk", "The reception desk. Nothing of note here." },
            { "Table", "A table. Nothing of note here." },
            { "Low_Table", "A low table. Nothing of note here." },
            { "Chair", "A chair. Nothing of note here." },
            { "Small_Chair", "A small chair. Nothing of note here." },
            { "Bed", "A bed. Nothing of note here." },
            { "Bunk", "A bunk. Nothing of note here." },
            { "Bench", "A bench. Nothing of note here." },
            { "Counter", "A counter. Nothing of note here." },
            { "Sink", "A sink. Nothing of note here." },
            { "Shelf", "A shelf. Nothing of note here." },
            { "Shelves", "Shelves. Nothing of note here." },
            { "Rack", "A rack. Nothing of note here." },
            { "Lockers", "Lockers. Nothing of note in them." },
            { "Cabinet", "A cabinet. Nothing of note in it." },
            { "Filing", "A filing cabinet. Nothing of note in it." },
            { "Fridge", "A fridge. Nothing of note in it." },
            { "Freezer", "A freezer. Nothing of note in it." },
            { "Box", "A box. Nothing of note in it." },
            { "Crate", "A crate. Nothing of note in it." },
            { "Cart", "A cart. Nothing of note here." },
            { "Gurney", "A gurney. Nothing of note here." },
            { "Copier", "A copier. Nothing of note here." },
            { "Printer", "A printer. Nothing of note here." },
            { "Monitor", "A monitor. Nothing of note here." },
            { "Radio", "A radio. Nothing of note here." },
            { "Kettle", "A kettle. Nothing of note here." },
            { "Cup", "A cup. Nothing of note here." },
            { "Planter", "A planter. Nothing of note here." },
            { "Lectern", "A lectern. Nothing of note here." },
            { "Machine", "A machine. Nothing of note here." },
        };

        private static Dictionary<string, string> writerLines;

        private static Dictionary<string, string> WriterLines()
        {
            if (writerLines != null) return writerLines;
            writerLines = new Dictionary<string, string>();
            string path = System.IO.Path.GetFullPath(DemoScripts.WriterGame + "/examine.csv");
            if (System.IO.File.Exists(path))
                foreach (string[] row in DeathScreenSetup.ReadCsv(System.IO.File.ReadAllText(path)).Skip(1))
                    if (row.Length >= 2 && !string.IsNullOrWhiteSpace(row[0])) writerLines[row[0].Trim()] = row[1].Trim();
            return writerLines;
        }

        /// <summary>Her comment on a prop of this type: the writer's row, else the placeholder, else "Nothing of note here."</summary>
        public static string ExamineLine(string type)
        {
            if (WriterLines().TryGetValue(type, out string line)) return line;
            return ExamineLines.TryGetValue(type, out line) ? line : Examinable.DefaultComment;
        }

        // The props that break, by type (Bruno, 2026-11-06: "add some prop types"): the name, or the name before its "_tag".
        // Whatever stands chest high among them is cover (BreakableAssets.PropBox). Counters, racks, fridges, lockers and walls
        // do not break. Health PROPOSED.
        private static readonly Dictionary<string, (Breakable.Kind kind, float health)> BreakableTypes = new Dictionary<string, (Breakable.Kind, float)>
        {
            { "Planter", (Breakable.Kind.Plaster, 60f) },
            { "Desk", (Breakable.Kind.Wood, 70f) },
            { "Reception_Desk", (Breakable.Kind.Wood, 140f) },
            { "Lectern", (Breakable.Kind.Wood, 40f) },
            { "Low_Table", (Breakable.Kind.Wood, 30f) },
            { "Cart", (Breakable.Kind.Metal, 50f) },
            { "Laundry_Cart", (Breakable.Kind.Metal, 50f) },
            { "Dressing_Cart", (Breakable.Kind.Metal, 50f) },
            { "Monitor_Cart", (Breakable.Kind.Metal, 50f) },
            { "Gurney", (Breakable.Kind.Metal, 40f) },
            { "Filing", (Breakable.Kind.Metal, 70f) },
            { "Cabinet", (Breakable.Kind.Metal, 60f) },
            { "Copier", (Breakable.Kind.Plastic, 50f) },
            { "Box", (Breakable.Kind.Wood, 20f) },
            { "Bin", (Breakable.Kind.Plastic, 15f) },
        };

        private static bool BreakableType(string name, out Breakable.Kind kind, out float health)
        {
            kind = Breakable.Kind.Wood;
            health = 0f;
            string type = name;
            if (!BreakableTypes.ContainsKey(type))
            {
                int tag = name.LastIndexOf('_');
                if (tag <= 0) return false;
                type = name.Substring(0, tag);
                if (!BreakableTypes.ContainsKey(type)) return false;
            }
            (kind, health) = BreakableTypes[type];
            return true;
        }

        public static Material Grey(float v) => ItemAssets.UnlitMaterial("demo_grey_" + Mathf.RoundToInt(v * 100f), new Color(v, v, v * 1.05f));

        public static Material Colour(string id, Color c) => ItemAssets.UnlitMaterial("demo_" + id, c);

        public static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>A figure (an EnemySpawn marker) under [Figures], or under a parent (a group that a flag shows).</summary>
        public EnemySpawn Figure(string name, string kind, Vector3 at, float yaw, int variant = -1, GameObject parent = null) =>
            RoomKit.Spawn(parent != null ? parent : Figures, name, kind, at, yaw, variant);

        /// <summary>A group under [Figures] that shows when the flag goes up (FlagGate): its figures spawn then.</summary>
        public GameObject Group(string name, string flag, bool showWhenRaised = true)
        {
            GameObject group = MenuLoopSetup.EnsureChild(Figures, name);
            FlagGate gate = MenuLoopSetup.EnsureComponent<FlagGate>(group);
            gate.flag = flag;
            gate.showWhenRaised = showWhenRaised;
            gate.waitUnseen = true; // the figures do not pop up in her view (the world scene)
            return group;
        }

        /// <summary>An item to take: a small unlit box at the point.</summary>
        public ItemPickup Item(string name, ItemDefinition item, Vector3 at, Vector3 size)
        {
            GameObject obj = ItemAssets.Pickup(Props, name, item, PrimitiveType.Cube, at, size, Quaternion.identity);
            return obj.GetComponent<ItemPickup>();
        }

        /// <summary>A capsule on the floor (MK is ammunition, Bruno, 2026-11-06: the room builder places the clips).</summary>
        public ItemPickup Capsule(string name, DrugType type, Vector3 at)
        {
            GameObject obj = ItemAssets.Pickup(Props, name, ItemAssets.Drug(type), PrimitiveType.Capsule, at, Vector3.one * 0.3f, Quaternion.Euler(0f, 0f, 90f));
            return obj.GetComponent<ItemPickup>();
        }

        /// <summary>A scan on a thing: a point the scan reaches, with its picture or clip and caption (PLACEHOLDER pictures).</summary>
        public Scannable Scan(string name, Vector3 at, string scanId, string caption, string raiseFlag = "")
        {
            GameObject obj = MenuLoopSetup.EnsureChild(Story, name);
            obj.transform.position = at;
            Scannable scan = MenuLoopSetup.EnsureComponent<Scannable>(obj);
            scan.scanId = scanId;
            scan.caption = caption;
            scan.image = TutorialSetup.EnsureScanImage();
            scan.range = Scannable.DefaultRange;
            scan.raiseFlag = raiseFlag;
            return scan;
        }

        public GameObject Sign(string name, string words, Vector3 point, Vector3 facing, Vector2 size, int fontSize = 56) =>
            RoomKit.Sign(Props, name, words, point, facing, size, fontSize);

        /// <summary>A sequence of steps under [Story], with a trigger box, or run when she first enters the room (trigger size zero).</summary>
        public ScriptedSequence Beat(string name, string doneFlag, Vector3 triggerAt, Vector3 triggerSize, params ScriptStep[] steps)
        {
            ScriptedSequence seq = RoomKit.Sequence(name, doneFlag, steps);
            seq.transform.SetParent(Story.transform, true);
            if (triggerSize == Vector3.zero) seq.runOnStart = true;
            else RoomKit.Trigger("Trigger_" + name, triggerAt, triggerSize, seq).transform.SetParent(Story.transform, true);
            return seq;
        }

        /// <summary>A locker with the guard uniform in it (OutfitLocker, as in OUTFIT TEST), its front to -z at the point.</summary>
        public GameObject Locker(string name, Vector3 floorPoint)
        {
            GameObject locker = MenuLoopSetup.EnsureChild(Props, name);
            Material black = Grey(0.07f);
            Box(locker, "Back", floorPoint + new Vector3(0f, 0.95f, 0.25f), new Vector3(0.9f, 1.9f, 0.05f), black);
            Box(locker, "Side_W", floorPoint + new Vector3(-0.43f, 0.95f, 0f), new Vector3(0.04f, 1.9f, 0.55f), black);
            Box(locker, "Side_E", floorPoint + new Vector3(0.43f, 0.95f, 0f), new Vector3(0.04f, 1.9f, 0.55f), black);
            GameObject uniform = MenuLoopSetup.EnsureChild(locker, "Uniform_Hanger");
            foreach ((string part, Vector3 at, Vector3 size, float shade) in new[]
            {
                ("Torso", new Vector3(0f, 1.3f, 0.05f), new Vector3(0.48f, 0.62f, 0.14f), 0.42f),
                ("Legs", new Vector3(0f, 0.68f, 0.05f), new Vector3(0.38f, 0.6f, 0.12f), 0.42f),
                ("Band", new Vector3(0f, 1.38f, -0.025f), new Vector3(0.49f, 0.05f, 0.01f), 0.95f),
            })
            {
                GameObject piece = MenuLoopSetup.EnsureChildPrimitive(uniform, part, PrimitiveType.Cube, floorPoint + at, size);
                UnityEngine.Object.DestroyImmediate(piece.GetComponent<Collider>());
                piece.GetComponent<Renderer>().sharedMaterial = Colour("uniform_" + part.ToLowerInvariant(), new Color(shade * 0.8f, shade * 0.95f, shade * 1.2f));
            }
            OutfitLocker component = MenuLoopSetup.EnsureComponent<OutfitLocker>(locker);
            component.outfitId = OutfitChange.GuardId;
            component.thing = "the guard uniform";
            component.shown = uniform;
            component.range = 1.6f;
            GameObject point = MenuLoopSetup.EnsureChild(locker, "Point");
            point.transform.position = floorPoint + new Vector3(0f, 1f, -0.45f);
            component.point = point.transform;
            return locker;
        }

        /// <summary>
        /// A wall of CRTs (InWorldScreen, one screen of many faces) standing on the floor point, its screens showing to the
        /// facing. With no clip it runs its lines as the greybox card (the plan: "in greybox, a card with the subtitles").
        /// </summary>
        public GameObject CrtWall(string name, Vector3 floorPoint, int cols, int rows, List<TapeLine> lines, float faceZ = 0.24f, Vector3? facing = null, bool interactive = false)
        {
            Vector3 face = facing ?? Vector3.back;
            Quaternion turn = Quaternion.LookRotation(-face);
            GameObject screen = MenuLoopSetup.EnsureChild(Props, name);
            screen.transform.SetPositionAndRotation(floorPoint, turn);
            var faces = new List<Renderer>();
            float w = 0.55f, h = 0.48f;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    Vector3 local = new Vector3((col - (cols - 1) * 0.5f) * w, 0.35f + row * h, 0f);
                    // Box takes a local position under the screen, which stands at the floor point, turned (2026-11-03: the boxes stood at twice their place).
                    Box(screen, $"Crt_{row}_{col}", local, new Vector3(0.5f, 0.42f, 0.46f), Grey(0.1f)).transform.localRotation = Quaternion.identity;
                    GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = $"CrtFace_{row}_{col}";
                    UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                    q.transform.SetParent(screen.transform, true);
                    q.transform.SetPositionAndRotation(floorPoint + turn * (local + new Vector3(0f, 0.01f, -faceZ)), turn);
                    q.transform.localScale = new Vector3(0.4f, 0.32f, 1f);
                    Renderer renderer = q.GetComponent<Renderer>();
                    renderer.sharedMaterial = Colour("crt_idle", new Color(0.05f, 0.16f, 0.08f));
                    faces.Add(renderer);
                }
            }
            InWorldScreen component = MenuLoopSetup.EnsureComponent<InWorldScreen>(screen);
            component.faces = faces.ToArray();
            component.lines = lines;
            component.interactive = interactive;
            component.loop = false;
            component.thing = "the monitors";
            EditorUtility.SetDirty(component);
            return screen;
        }

        /// <summary>One CRT on a table at the floor point, its screen to the yaw: a screen she turns on with Interact.</summary>
        public GameObject Crt(string name, Vector3 floorPoint, float yaw, List<TapeLine> lines, bool interactive, bool loop)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Prop(name + "_Table", floorPoint, new Vector3(0.7f, 0.75f, 0.6f), 0.25f);
            GameObject screen = CrtWall(name, floorPoint + Vector3.up * 0.42f, 1, 1, lines, 0.24f, turn * Vector3.forward, interactive);
            InWorldScreen component = screen.GetComponent<InWorldScreen>();
            component.loop = loop;
            component.thing = "the screen";
            GameObject point = MenuLoopSetup.EnsureChild(screen, "Point");
            point.transform.position = floorPoint + turn * Vector3.forward * 0.6f + Vector3.up;
            component.point = point.transform;
            component.range = 1.6f;
            EditorUtility.SetDirty(component);
            return screen;
        }

        /// <summary>A steel leaf in the doorway, locked: the key opens it (LockedDoor), and the way on behind it opens.</summary>
        public GameObject LockedLeaf(Door d, string keyId, string keyName)
        {
            Vector3 outward = Outward(d.Side);
            Vector3 across = new Vector3(outward.z, 0f, -outward.x);
            GameObject door = MenuLoopSetup.EnsureChild(Props, "Door_" + d.To);
            door.transform.position = Mouth(d);
            // Box takes a local position: the door stands at the doorway already (2026-11-03: the leaf stood at twice its place).
            GameObject leaf = Box(door, "Leaf", Vector3.up * d.Height * 0.5f, Abs(across * d.Width + Vector3.up * d.Height + outward * 0.1f), Grey(0.25f));
            // The leaf slides: not static (2026-11-07, Bruno: "door won't appear open but you can go pass throught it": static
            // batching drew the leaf at its first place, and only its collider went).
            leaf.isStatic = false;
            Carve(leaf);
            LockedDoor locked = MenuLoopSetup.EnsureComponent<LockedDoor>(door);
            locked.keyItemId = keyId;
            locked.keyName = keyName;
            locked.leaf = leaf.transform;
            locked.openOffset = across * (d.Width + 0.1f);
            locked.point = leaf.transform;
            locked.range = 1.8f;
            EditorUtility.SetDirty(locked);
            return door;
        }

        /// <summary>
        /// The site code on a card door (DEMO_PLAN §10.2): after the rotation flag the card works only once a terminal
        /// enrols it. The reader on the wall beside the door turns from red to green.
        /// </summary>
        public LockedDoor CodeLock(GameObject door, Vector3 readerAt, Vector3 facing, string rotatedFlag, string enrolledFlag)
        {
            LockedDoor locked = door.GetComponent<LockedDoor>();
            GameObject reader = Box(Props, "Card_Reader_" + door.name, readerAt, Abs(new Vector3(facing.z, 0f, facing.x) * 0.12f + Vector3.up * 0.18f + facing * 0.04f),
                Colour("reader", Color.white));
            reader.isStatic = false; // its colour changes in play
            locked.rotatedFlag = rotatedFlag;
            locked.enrolledFlag = enrolledFlag;
            locked.reader = reader.GetComponent<Renderer>();
            EditorUtility.SetDirty(locked);
            return locked;
        }

        /// <summary>A security terminal on a desk: the screen faces the room, the keypad enrols the card (CodeTerminal).</summary>
        public CodeTerminal Terminal(string name, Vector3 floorPoint, Vector3 facing, string cardId, string code, string rotatedFlag, string enrolledFlag)
        {
            Quaternion turn = Quaternion.LookRotation(facing);
            GameObject desk = Box(Props, name + "_Desk", floorPoint + Vector3.up * 0.375f, Abs(turn * new Vector3(1.2f, 0.75f, 0.7f)), Grey(0.25f));
            GameObject screen = Box(Props, name, floorPoint + Vector3.up * 1.0f - facing * 0.15f, Abs(turn * new Vector3(0.5f, 0.38f, 0.08f)), Colour("terminal_screen", new Color(0.1f, 0.45f, 0.2f)));
            CodeTerminal terminal = MenuLoopSetup.EnsureComponent<CodeTerminal>(screen);
            terminal.cardItemId = cardId;
            terminal.code = code;
            terminal.rotatedFlag = rotatedFlag;
            terminal.enrolledFlag = enrolledFlag;
            GameObject point = MenuLoopSetup.EnsureChild(screen, "Point");
            point.transform.position = floorPoint + facing * 0.55f + Vector3.up;
            terminal.point = point.transform;
            terminal.range = 1.4f;
            EditorUtility.SetDirty(terminal);
            return terminal;
        }

        /// <summary>A switch on a wall: Interact asks, a yes raises the story flag (FlagSwitch).</summary>
        public FlagSwitch Switch(string name, Vector3 at, string flag, string question, string hint, string done)
        {
            GameObject obj = Box(Props, name, at, new Vector3(0.25f, 0.35f, 0.08f), Colour("switch", new Color(0.85f, 0.7f, 0.2f)));
            FlagSwitch sw = MenuLoopSetup.EnsureComponent<FlagSwitch>(obj);
            sw.flag = flag;
            sw.question = question;
            sw.hint = hint;
            sw.doneText = done;
            sw.range = 1.6f;
            EditorUtility.SetDirty(sw);
            return sw;
        }

        /// <summary>Moves Lucy (the room where the game starts, a bed).</summary>
        public void PlaceLucy(Vector3 at, float yaw)
        {
            if (Lucy != null) Lucy.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        }
    }
}
#endif
