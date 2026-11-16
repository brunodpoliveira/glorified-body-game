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
        public bool ExitShot = true;  // false: the exit rule skips it (the drop of the Hold, 2026-11-16: the shots Bruno asked for there)
        public Door(char side, float at, string to, float width = 1.6f, string flag = "", bool fire = false, float height = DemoRoom.DoorHeight, int pocket = 1, bool jammed = false, string leverFlag = "", string jamFlag = "", string leverRoom = "", bool unbreakable = false, bool exitShot = true) { Side = side; At = at; To = to; Width = width; Flag = flag; Fire = fire; Height = height; Pocket = pocket; Jammed = jammed; LeverFlag = leverFlag; JamFlag = jamFlag; LeverRoom = leverRoom; Unbreakable = unbreakable; ExitShot = exitShot; }
    }

    /// <summary>One room of the demo: its id, its title, its floor, its place on the floor plan, its doors, its dressing.</summary>
    public sealed class RoomSpec
    {
        public string Id, Title, Floor, Music;
        public float X0, Z0, X1, Z1, Height = 3f;
        /// <summary>The room's builder places every camera (DemoRoom.ExtraCamera), and the cells place none (the dining hall, 2026-11-16).</summary>
        public bool AuthoredCameras;
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
            component.exits = spec.Doors.ConvertAll(d => new Room.Exit { mouth = room.Mouth(d), outward = Outward(d.Side), width = d.Width, to = d.To, noShot = !d.ExitShot }).ToArray();
            if (!spec.AuthoredCameras) room.CamerasOnCells();
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
        /// <param name="zoneFlag">The zone shows only once this flag is up (a FlagGate): the close-up of the drop after the glass door opens.</param>
        public void ExtraCamera(string tag, Vector3 cam, Vector3 look, float fov, Vector3 zoneCentre, Vector3 zoneSize, string zoneFlag = "")
        {
            extraCameras.Add(() =>
            {
                GameObject root = MenuLoopSetup.EnsureChild(Root, "[Cameras]");
                FixedCamera fc = MenuLoopSetup.EnsureFixedCamera(root, $"Cam_{Spec.Id}_{tag}", cam, look, fov);
                Cameras.Add(fc);
                GameObject zoneRoot = root;
                if (!string.IsNullOrEmpty(zoneFlag))
                {
                    zoneRoot = MenuLoopSetup.EnsureChild(root, $"Gate_{Spec.Id}_{tag}");
                    FlagGate gate = MenuLoopSetup.EnsureComponent<FlagGate>(zoneRoot);
                    gate.flag = zoneFlag;
                    gate.showWhenRaised = true;
                }
                MenuLoopSetup.EnsureCameraZone(zoneRoot, $"Zone_{Spec.Id}_{tag}", zoneCentre, zoneSize, fc, 2);
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
                CellCameras.Add((cell, fc));
                MenuLoopSetup.EnsureCameraZone(root, $"Zone_{s.Id}_{tag}", new Vector3(cell.center.x, 1.5f, cell.center.y), new Vector3(cell.width, 3f, cell.height), fc, 1);
                if (missed > 0f) Debug.Log($"[DemoRoom] {s.Id} {fc.cameraName}: {missed:P0} of its cell out of frame");
            }
            MenuLoopSetup.EnsureShotPoints(Cameras.ToArray());
        }

        private static readonly float[] Lenses = { 62f, 72f, 84f };

        /// <summary>The camera of each cell and its cell (plan metres): the 180 pass of the world moves them (DemoWorld.ScreenDirections).</summary>
        internal readonly List<(Rect cell, FixedCamera cam)> CellCameras = new List<(Rect, FixedCamera)>();

        /// <summary>The camera pose that sees the most of a cell's floor points, and the share it misses.</summary>
        private (Vector3 cam, Vector3 look, float fov, float missed) BestPose(Rect cell)
        {
            Pose best = Search(cell, Vector3.zero, null, null);
            return (best.cam, best.look, best.fov, best.missed);
        }

        private struct Pose
        {
            public Vector3 cam, look;
            public float fov, missed;
            public int exits, points, flips;
        }

        /// <summary>
        /// The 180 rule on a cell camera (Bruno, 2026-11-16: "check the entire demo for 180 violations, and add that as a rule
        /// for future camera additions"): a pose with fewer cuts that flip her screen direction, which sees every exit and at
        /// least every floor point the camera sees now. flipsOf counts the flips of a pose (world space). Returns the flips
        /// after; the camera moves only for fewer.
        /// </summary>
        /// <param name="headings">The flat forward of each camera on the other side of its cuts: an aim along it is a pose that cannot flip with it.</param>
        internal int Repose(Rect cell, FixedCamera camera, Func<Vector3, Quaternion, int> flipsOf, IEnumerable<Vector3> headings)
        {
            Vector3 o = Root.transform.position;
            (List<Vector3> points, List<Door> exits, List<Vector3> doorways) = CellTargets(cell, o, true);
            int solid = 1 << LayerMask.NameToLayer("Default");
            Transform t = camera.transform;
            (int exitsNow, int pointsNow) = Score(points, exits, doorways, o, t.position, t.rotation, camera.fieldOfView, solid);
            int flipsNow = flipsOf(t.position, t.rotation);
            Pose best = Search(cell, o, flipsOf, (exitsNow, pointsNow), headings);
            if (best.flips >= flipsNow)
            {
                Debug.Log($"[Rule180] {camera.name}: no better pose ({flipsNow} flips; the best that sees {exitsNow} exits and {pointsNow} points: {(best.flips == int.MaxValue ? "none" : best.flips + " flips")})");
                return flipsNow;
            }
            Place(camera, best, o);
            return best.flips;
        }

        /// <summary>
        /// A cell camera that no pose frees of a flip (the 180 pass): its cell splits in two, as a cell with a blind spot does.
        /// Both ways to cut it are tried; each half takes the pose with the fewest flips on the cuts of its part that sees all
        /// its floor points and exits (cuts: the old camera's cuts, with the camera on the other side). The way with fewer
        /// flips than now wins. The camera keeps the first half, a new camera takes the second. Else nothing changes.
        /// </summary>
        internal bool Split(Rect cell, FixedCamera camera, IList<(Vector3 at, Vector3 walk, FixedCamera other)> cuts, int flipsNow, IEnumerable<Vector3> headings)
        {
            if (Mathf.Max(cell.width, cell.height) < 3f) return false;
            Vector3 o = Root.transform.position;
            int solid = 1 << LayerMask.NameToLayer("Default");
            Pose PoseFor(Rect half)
            {
                (List<Vector3> points, List<Door> exits, List<Vector3> doorways) = CellTargets(half, o, true);
                Rect area = new Rect(half.x + o.x - 0.3f, half.y + o.z - 0.3f, half.width + 0.6f, half.height + 0.6f);
                var mine = cuts.Where(c => area.Contains(new Vector2(c.at.x, c.at.z))).ToList();
                int FlipsOf(Vector3 pos, Quaternion rot) => mine.Count(c => ScreenDirection.Flips(pos, rot, c.other.transform.position, c.other.transform.rotation, c.at, c.walk));
                return Search(half, o, FlipsOf, (exits.Count + doorways.Count, points.Count), headings);
            }
            (Rect a, Rect b, Pose pa, Pose pb)? best = null;
            foreach (bool alongX in new[] { cell.width >= cell.height, cell.width < cell.height })
            {
                Rect a = alongX ? new Rect(cell.x, cell.y, cell.width * 0.5f, cell.height) : new Rect(cell.x, cell.y, cell.width, cell.height * 0.5f);
                Rect b = alongX ? new Rect(cell.center.x, cell.y, cell.width * 0.5f, cell.height) : new Rect(cell.x, cell.center.y, cell.width, cell.height * 0.5f);
                if (Mathf.Min(a.width, a.height) < 1.5f) continue;
                Pose pa = PoseFor(a), pb = PoseFor(b);
                if (pa.flips == int.MaxValue || pb.flips == int.MaxValue) continue; // a half with a blind spot
                if (pa.flips + pb.flips < flipsNow && (best == null || pa.flips + pb.flips < best.Value.pa.flips + best.Value.pb.flips)) best = (a, b, pa, pb);
            }
            CameraZone zone = Root.GetComponentsInChildren<CameraZone>(true).FirstOrDefault(z => z.fixedCamera == camera && z.priority == 1);
            if (best == null || zone == null)
            {
                Debug.Log($"[Rule180] {camera.name}: no split (no way to cut the cell gives two halves with no blind spot and fewer flips)");
                return false;
            }
            (Rect ra, Rect rb, Pose posea, Pose poseb) = best.Value;
            Place(camera, posea, o);
            zone.transform.position = new Vector3(ra.center.x, 1.5f, ra.center.y) + o;
            zone.GetComponent<BoxCollider>().size = new Vector3(ra.width, 3f, ra.height);
            EditorUtility.SetDirty(zone.transform);
            GameObject root = MenuLoopSetup.EnsureChild(Root, "[Cameras]");
            int n = Cameras.Count;
            while (root.transform.Find($"Cam_{Spec.Id}_{n:00}") != null) n++;
            FixedCamera fc = MenuLoopSetup.EnsureFixedCamera(root, $"Cam_{Spec.Id}_{n:00}", poseb.cam + o, poseb.look + o, poseb.fov);
            Cameras.Add(fc);
            MenuLoopSetup.EnsureCameraZone(root, $"Zone_{Spec.Id}_{n:00}", new Vector3(rb.center.x, 1.5f, rb.center.y) + o, new Vector3(rb.width, 3f, rb.height), fc, 1);
            var shot = new GameObject("ShotPoint_" + fc.cameraName);
            shot.transform.SetParent(Root.transform, false);
            shot.transform.SetPositionAndRotation(fc.transform.position, fc.transform.rotation);
            var point = shot.AddComponent<GlorifiedBody.Level.Build.ShotPoint>();
            point.shotName = fc.cameraName;
            point.fieldOfView = fc.fieldOfView;
            CellCameras.Remove((cell, camera));
            CellCameras.Add((ra, camera));
            CellCameras.Add((rb, fc));
            Debug.Log($"[Rule180] {camera.name}: its cell splits, {fc.name} takes the second half ({posea.flips} and {poseb.flips} flips on the old cuts)");
            return true;
        }

        /// <summary>A camera to a pose of the search (plan metres; o is where the room stands), its shot point with it.</summary>
        private void Place(FixedCamera camera, Pose pose, Vector3 o)
        {
            Transform t = camera.transform;
            t.position = pose.cam + o;
            t.LookAt(pose.look + o);
            camera.fieldOfView = pose.fov;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(t);
            foreach (GlorifiedBody.Level.Build.ShotPoint shot in Root.GetComponentsInChildren<GlorifiedBody.Level.Build.ShotPoint>(true))
                if (shot.shotName == camera.cameraName)
                {
                    shot.transform.SetPositionAndRotation(t.position, t.rotation);
                    shot.fieldOfView = camera.fieldOfView;
                }
        }

        /// <summary>
        /// The floor points of a cell (the coverage test's grid: 0.45 m in from the floor's edge, 0.5 m apart, the cell grown by
        /// 0.3 m, a point on the edge between two zones can fall to either camera) and its exits, in plan metres; o is where
        /// the room stands in the world.
        /// </summary>
        /// <param name="withDoorways">The 180 pass: also a floor point 0.1 m in from each doorway of the cell, where she stands as she
        /// comes in (DemoTests.ADoorway_ChangesTheRoom_AsSheStepsThrough_InFrame_WithNoLoad); a new pose must keep them.</param>
        private (List<Vector3> points, List<Door> exits, List<Vector3> doorways) CellTargets(Rect cell, Vector3 o, bool withDoorways = false)
        {
            RoomSpec s = Spec;
            int solid = 1 << LayerMask.NameToLayer("Default");
            var points = new List<Vector3>();
            for (float x = s.X0 + 0.45f; x <= s.X1 - 0.45f; x += 0.5f)
                for (float z = s.Z0 + 0.45f; z <= s.Z1 - 0.45f; z += 0.5f)
                {
                    if (x < cell.xMin - 0.3f || x > cell.xMax + 0.3f || z < cell.yMin - 0.3f || z > cell.yMax + 0.3f) continue;
                    var p = new Vector3(x, 0f, z);
                    if (Physics.CheckBox(p + o + Vector3.up * 0.9f, new Vector3(0.2f, 0.8f, 0.2f), Quaternion.identity, solid, QueryTriggerInteraction.Ignore)) continue;
                    points.Add(p);
                }
            // The exits of the cell (Bruno, 2026-11-05: "i also need to see the exits"): where she stands as she goes to them.
            List<Door> exits = s.Doors.FindAll(d =>
            {
                if (!d.ExitShot) return false; // the exit rule skips it
                Vector3 p = InsideDoor(d, 0.7f);
                return p.x >= cell.xMin - 0.3f && p.x <= cell.xMax + 0.3f && p.z >= cell.yMin - 0.3f && p.z <= cell.yMax + 0.3f;
            });
            var doorways = new List<Vector3>();
            if (withDoorways)
                foreach (Door d in s.Doors)
                {
                    Vector3 p = InsideDoor(d, 0.1f);
                    if (p.x >= cell.xMin - 0.3f && p.x <= cell.xMax + 0.3f && p.z >= cell.yMin - 0.3f && p.z <= cell.yMax + 0.3f) doorways.Add(new Vector3(p.x, 0f, p.z));
                }
            return (points, exits, doorways);
        }

        /// <summary>The exits (and the doorway points) and the floor points a pose sees (the pose in world space).</summary>
        private (int exits, int points) Score(List<Vector3> points, List<Door> exits, List<Vector3> doorways, Vector3 o, Vector3 cam, Quaternion rotation, float fov, int solid)
        {
            int seen = 0, doors = 0;
            // A doorway point counts only with her chest in frame and in sight (no near pass: the doorway test looks at the screen).
            foreach (Vector3 p in doorways)
            {
                Vector3 chest = p + o + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight;
                if (CameraCoverage.InFrame(cam, rotation, fov, chest, 0f) && !Physics.Linecast(cam, chest, solid, QueryTriggerInteraction.Ignore)) doors++;
            }
            foreach (Vector3 p in points) if (CameraCoverage.Sees(cam, rotation, fov, p + o, CameraCoverage.GameRoomNear, solid)) seen++;
            foreach (Door d in exits) if (CameraCoverage.SeesExit(cam, rotation, fov, Mouth(d) + o, Outward(d.Side), d.Width, solid)) doors++;
            return (doors, seen);
        }

        /// <summary>
        /// The camera poses of a cell (its corners and edge middles, and 2 m past an inner edge; two aims and one for each exit;
        /// three lenses). With no flipsOf: the pose that sees the most (the exits first). With flipsOf (the 180 pass): more
        /// places and aims, only the poses that see at least keep, and the fewest flips first.
        /// </summary>
        private Pose Search(Rect cell, Vector3 o, Func<Vector3, Quaternion, int> flipsOf, (int exits, int points)? keep, IEnumerable<Vector3> headings = null)
        {
            RoomSpec s = Spec;
            int solid = 1 << LayerMask.NameToLayer("Default");
            (List<Vector3> points, List<Door> exits, List<Vector3> doorways) = CellTargets(cell, o, flipsOf != null);
            float camY = Mathf.Min(s.Height - 0.25f, 3.4f);
            bool wide = flipsOf != null;
            float[] xs = Places(cell.xMin, cell.xMax, s.X0, s.X1, wide), zs = Places(cell.yMin, cell.yMax, s.Z0, s.Z1, wide);
            if (wide) // the 180 pass: also every metre of the room
            {
                xs = xs.Concat(Enumerable.Range(0, Mathf.FloorToInt(s.Width)).Select(i => Mathf.Clamp(s.X0 + 0.5f + i, s.X0 + 0.35f, s.X1 - 0.35f))).Distinct().ToArray();
                zs = zs.Concat(Enumerable.Range(0, Mathf.FloorToInt(s.Depth)).Select(i => Mathf.Clamp(s.Z0 + 0.5f + i, s.Z0 + 0.35f, s.Z1 - 0.35f))).Distinct().ToArray();
            }
            Vector3 middle = new Vector3(cell.center.x, 0.7f, cell.center.y);
            var best = new Pose { cam = new Vector3(xs[0], camY, zs[0]), look = middle, fov = Lenses[0], exits = -1, points = -1, flips = int.MaxValue };
            foreach (float x in xs)
            {
                foreach (float z in zs)
                {
                    var cam = new Vector3(x, camY, z);
                    if (Mathf.Abs(x - cell.center.x) < 0.5f && Mathf.Abs(z - cell.center.y) < 0.5f) continue; // the middle sees nothing behind it
                    if (Physics.CheckSphere(cam + o, 0.2f, solid, QueryTriggerInteraction.Ignore)) continue;     // inside a column or a lintel
                    if (OverATallThing(cam + o, solid)) continue; // the H1 frame under Cam_H1_04 hid the filter box and her (Bruno, 2026-11-14)
                    Vector3 away = new Vector3(middle.x - x, 0f, middle.z - z);
                    var aims = new List<Vector3> { middle, middle + away.normalized * Mathf.Min(cell.width, cell.height) * 0.25f };
                    if (wide)
                    {
                        aims.AddRange(new[] { middle + Vector3.right * 1.5f, middle + Vector3.left * 1.5f, middle + Vector3.forward * 1.5f, middle + Vector3.back * 1.5f });
                        float reach = Mathf.Max(2f, new Vector2(middle.x - x, middle.z - z).magnitude);
                        if (headings != null) foreach (Vector3 h in headings) aims.Add(new Vector3(x + h.x * reach, 0.7f, z + h.z * reach));
                    }
                    foreach (Door d in exits) aims.Add(Vector3.Lerp(middle, Mouth(d) + Vector3.up, 0.5f));
                    foreach (Vector3 look in aims)
                    {
                        Quaternion rotation = Quaternion.LookRotation(look - cam);
                        int flips = wide ? flipsOf(cam + o, rotation) : 0;
                        if (flips > best.flips) continue;
                        foreach (float fov in Lenses)
                        {
                            (int doors, int seen) = Score(points, exits, doorways, o, cam + o, rotation, fov, solid);
                            if (keep.HasValue && (doors < keep.Value.exits || seen < keep.Value.points)) continue;
                            // Equal counts keep the narrower lens (the earlier one).
                            bool better = flips < best.flips || (flips == best.flips && (doors > best.exits || (doors == best.exits && seen > best.points)));
                            if (better) best = new Pose { cam = cam, look = look, fov = fov, exits = doors, points = seen, flips = flips };
                        }
                    }
                }
            }
            best.missed = best.exits < exits.Count + doorways.Count ? 1f : points.Count == 0 ? 0f : 1f - (float)Mathf.Max(0, best.points) / points.Count;
            return best;
        }

        /// <summary>
        /// A thing taller than TallThing stands under the camera. A camera over it looks down along it, and it hides what is
        /// next to it (Bruno, 2026-11-14: the filter box of the H1 frame "dissapears depending on view").
        /// </summary>
        public static bool OverATallThing(Vector3 cam, int solid)
        {
            if (cam.y <= TallThing) return false;
            var half = new Vector3(0.15f, (cam.y - TallThing) * 0.5f, 0.15f); // 0.15 m: clear of a wall 0.35 m away
            return Physics.CheckBox(new Vector3(cam.x, (cam.y + TallThing) * 0.5f, cam.z), half, Quaternion.identity, solid, QueryTriggerInteraction.Ignore);
        }

        public const float TallThing = 1.4f;

        /// <summary>
        /// Camera places along one axis of a cell: its two edges, its middle, and 2 m past each edge, all inside the room's walls.
        /// Wide (the 180 pass): also the quarters and 1 m past each edge.
        /// </summary>
        private static float[] Places(float a, float b, float room0, float room1, bool wide = false)
        {
            var places = new List<float>();
            var at = new List<float> { a, (a + b) * 0.5f, b, a - 2f, b + 2f };
            if (wide) at.AddRange(new[] { a + (b - a) * 0.25f, a + (b - a) * 0.75f, a - 1f, b + 1f });
            foreach (float v in at)
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
            PropAssets.OutOfTheBake(obj, Vector3.zero, Vector3.one); // a moved planter leaves no hole behind
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

        /// <summary>A pane she sees through (2026-11-14: the booth of H4 was an opaque colour, so nobody saw the nurse in it):
        /// the unlit colour with an alpha, blended, no depth write.</summary>
        public static Material Glass(string id, Color c, float alpha)
        {
            c.a = alpha;
            Material m = ItemAssets.UnlitMaterial("demo_" + id, c);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

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
