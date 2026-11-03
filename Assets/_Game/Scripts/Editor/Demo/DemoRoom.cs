#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Automation;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>A doorway of a demo room: the wall it is in, its place along that wall (world metres), the room behind it.</summary>
    public sealed class Door
    {
        public char Side;        // 'N' (+z), 'S' (-z), 'E' (+x), 'W' (-x)
        public float At;         // the world x (N, S) or z (E, W) of the doorway's middle
        public string To;        // the room id behind it, or "" for a closed door (a shape only)
        public float Width = 1.6f;
        public string Flag = ""; // the exit works only with this story flag up (RoomExit.requiredFlag)
        public Door(char side, float at, string to, float width = 1.6f, string flag = "") { Side = side; At = at; To = to; Width = width; Flag = flag; }
    }

    /// <summary>One room of the demo: its id, its title, its floor, its place on the floor plan, its doors, its dressing.</summary>
    public sealed class RoomSpec
    {
        public string Id, Title, Floor, Music;
        public float X0, Z0, X1, Z1, Height = 3f;
        public readonly List<Door> Doors = new List<Door>();
        public Action<DemoRoom> Dress;
        public string Scene => "Demo_" + Id;
        public float Width => X1 - X0;
        public float Depth => Z1 - Z0;
        public Vector3 Centre => new Vector3((X0 + X1) * 0.5f, 0f, (Z0 + Z1) * 0.5f);
    }

    /// <summary>
    /// The greybox of a demo room (2026-10-31, Bruno: "create the floor 2 and 1 of the demo in greybox"; the demo plan
    /// §3, §4, §7, round 18: "build the first play-through all in greybox"). One scene per room, in plan coordinates
    /// (the map needs no offset): the floor, the walls with their doorways, a short stub out of each doorway with the
    /// exit to the next room and the entry from it, cameras on a grid of cells, the NavMesh, the music key, Lucy, the
    /// systems, then the room's own dressing (DemoWard, DemoPublic). The rooms are black boxes with grey lines: the people
    /// read on them (T22). A builder makes a fresh scene each time, so a rebuild leaves nothing old behind.
    /// </summary>
    public sealed class DemoRoom
    {
        public const string Folder = MenuLoopSetup.ScenesFolder + "/Demo";
        public const float Wall = 0.3f, StubLength = 2.2f, CellMax = 9f;
        public const float WallShade = 0.22f; // lighter than the dark clear colour (0.12 was the same tone: the floors seemed to float)

        public readonly RoomSpec Spec;
        public readonly GameObject Room, Props, Figures, Story, Exits;
        public readonly CharacterCatalog Catalog;

        private DemoRoom(RoomSpec spec, CharacterCatalog catalog)
        {
            Spec = spec;
            Catalog = catalog;
            Room = MenuLoopSetup.EnsureRoot("[Room]");
            Props = MenuLoopSetup.EnsureRoot("[Props]");
            Figures = MenuLoopSetup.EnsureRoot("[Figures]");
            Story = MenuLoopSetup.EnsureRoot("[Story]");
            Exits = MenuLoopSetup.EnsureRoot("[Exits]");
        }

        public static string PathOf(RoomSpec spec) => Folder + "/" + spec.Scene + ".unity";
        public static string NavPathOf(RoomSpec spec) => Folder + "/" + spec.Scene + "_NavMesh.asset";

        /// <summary>Builds the room's scene from its spec. Lucy stands at the first door, facing in, unless the dressing moves her.</summary>
        public static void Build(RoomSpec spec)
        {
            Directory.CreateDirectory(Folder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog();
            var room = new DemoRoom(spec, catalog);
            room.Shell();
            MenuLoopSetup.EnsureCamera(spec.Centre + new Vector3(0f, 3f, -4f), spec.Centre, 60f);
            Vector3 start = spec.Doors.Count > 0 ? room.InsideDoor(spec.Doors[0], 1.4f) : spec.Centre;
            MenuLoopSetup.EnsureLucy(catalog, start, spec.Doors.Count > 0 ? room.InwardYaw(spec.Doors[0]) : 0f);
            spec.Dress?.Invoke(room);
            RoomKit.BakeNavMesh(room.Room, NavPathOf(spec));
            room.CamerasOnCells();
            RoomKit.Music(spec.Music);
            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            EditorSceneManager.SaveScene(scene, PathOf(spec));
            Debug.Log($"[DemoRoom] built {spec.Scene}: {spec.Title}, {spec.Width:0} x {spec.Depth:0} m, {spec.Doors.Count} doors");
        }

        // ── The shell ─────────────────────────────────────────────────

        private void Shell()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.32f, 0.35f);
            var sun = new GameObject("Light");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            light.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
            RoomSpec s = Spec;
            Box(Room, "Floor", new Vector3(s.Centre.x, -0.05f, s.Centre.z), new Vector3(s.Width, 0.1f, s.Depth), Grey(0.42f));
            WallSide('N', s.X0, s.X1, s.Z1);
            WallSide('S', s.X0, s.X1, s.Z0);
            WallSide('E', s.Z0, s.Z1, s.X1);
            WallSide('W', s.Z0, s.Z1, s.X0);
            foreach (Door d in s.Doors) Doorway(d);
        }

        /// <summary>One wall with a gap at each doorway on it. along0..along1 run along the wall; at is the wall's line.</summary>
        private void WallSide(char side, float along0, float along1, float at)
        {
            var gaps = new List<(float a, float b)>();
            foreach (Door d in Spec.Doors) if (d.Side == side) gaps.Add((d.At - d.Width * 0.5f, d.At + d.Width * 0.5f));
            gaps.Sort((p, q) => p.a.CompareTo(q.a));
            float from = along0 - Wall;
            int piece = 0;
            foreach ((float a, float b) in gaps)
            {
                WallPiece(side, from, a, at, piece++);
                from = b;
            }
            WallPiece(side, from, along1 + Wall, at, piece);
            // Over each doorway: the lintel.
            foreach ((float a, float b) in gaps) Lintel(side, a, b, at);
        }

        private void WallPiece(char side, float a, float b, float at, int index)
        {
            if (b - a < 0.05f) return;
            float h = Spec.Height, mid = (a + b) * 0.5f, len = b - a;
            float off = side == 'N' || side == 'E' ? Wall * 0.5f : -Wall * 0.5f;
            Vector3 pos = side == 'N' || side == 'S' ? new Vector3(mid, h * 0.5f, at + off) : new Vector3(at + off, h * 0.5f, mid);
            Vector3 size = side == 'N' || side == 'S' ? new Vector3(len, h, Wall) : new Vector3(Wall, h, len);
            Box(Room, $"Wall_{side}_{index}", pos, size, Grey(WallShade));
        }

        private void Lintel(char side, float a, float b, float at)
        {
            float h = Spec.Height, doorH = 2.3f;
            if (h - doorH < 0.05f) return;
            float off = side == 'N' || side == 'E' ? Wall * 0.5f : -Wall * 0.5f;
            Vector3 pos = side == 'N' || side == 'S' ? new Vector3((a + b) * 0.5f, (h + doorH) * 0.5f, at + off) : new Vector3(at + off, (h + doorH) * 0.5f, (a + b) * 0.5f);
            Vector3 size = side == 'N' || side == 'S' ? new Vector3(b - a, h - doorH, Wall) : new Vector3(Wall, h - doorH, b - a);
            Box(Room, $"Lintel_{side}_{a:0.0}", pos, size, Grey(WallShade));
        }

        /// <summary>A doorway: the stub corridor out of it, the exit at its end, the entry from that room just inside.</summary>
        private void Doorway(Door d)
        {
            Vector3 outward = Outward(d.Side);
            Vector3 mouth = Mouth(d);
            Vector3 across = new Vector3(outward.z, 0f, -outward.x);
            string tag = string.IsNullOrEmpty(d.To) ? "Closed_" + d.Side + "_" + d.At.ToString("0") : d.To;
            if (string.IsNullOrEmpty(d.To))
            {
                // A closed door: a steel leaf in the gap, a shape only. With a flag it shuts when the flag goes up (the
                // fire door at code silver), and a short dead end lies behind it.
                GameObject holder = Room;
                if (!string.IsNullOrEmpty(d.Flag))
                {
                    holder = MenuLoopSetup.EnsureChild(Story, "Gate_" + tag);
                    FlagGate gate = MenuLoopSetup.EnsureComponent<FlagGate>(holder);
                    gate.flag = d.Flag;
                    gate.showWhenRaised = true;
                    Box(Room, "DeadEnd_" + tag, mouth + outward * (Wall + 0.8f) + Vector3.up * 1.2f, Abs(across * (d.Width + 0.6f) + Vector3.up * 2.4f + outward * 0.3f), Grey(WallShade));
                    Box(Room, "DeadEndFloor_" + tag, mouth + outward * (Wall * 0.5f + 0.35f) + Vector3.down * 0.05f, Abs(across * d.Width + Vector3.up * 0.1f + outward * (Wall + 0.7f)), Grey(0.3f));
                }
                Box(holder, "Door_" + tag, mouth + Vector3.up * 1.15f + outward * (Wall * 0.5f), Abs(across * d.Width + Vector3.up * 2.3f + outward * 0.1f), Grey(0.22f));
                return;
            }
            Vector3 stubMid = mouth + outward * (Wall + StubLength * 0.5f);
            // "Stub" (not "...Floor"): the camera check and the map read the room's floor only.
            Box(Room, "Stub_" + tag, stubMid + Vector3.down * 0.05f, Abs(across * (d.Width + 0.2f) + Vector3.up * 0.1f + outward * StubLength), Grey(0.3f));
            foreach (float sign in new[] { -1f, 1f })
                Box(Room, $"StubWall_{tag}_{(sign < 0 ? "a" : "b")}", stubMid + across * sign * (d.Width * 0.5f + 0.15f) + Vector3.up * 1.2f, Abs(across * 0.3f + Vector3.up * 2.4f + outward * StubLength), Grey(WallShade));
            Box(Room, "StubEnd_" + tag, mouth + outward * (Wall + StubLength + 0.15f) + Vector3.up * 1.2f, Abs(across * (d.Width + 0.6f) + Vector3.up * 2.4f + outward * 0.3f), Grey(WallShade));
            // The exit fills the doorway (from 0.15 m into the wall to 0.4 m past it): the room changes as she steps
            // through, in frame. Bruno, 2026-11-02: at the end of the stub it took "3-4 steps blindly down the doorway".
            // The stub stays as the dark corridor behind the door; the entry of this room stands 1.2 m inside.
            const float exitFrom = 0.15f, exitTo = Wall + 0.4f;
            RoomExit exit = RoomKit.Exit("Exit_To_" + d.To, mouth + outward * ((exitFrom + exitTo) * 0.5f) + Vector3.up * 1.2f,
                Abs(across * d.Width + Vector3.up * 2.4f + outward * (exitTo - exitFrom)), "Demo_" + d.To, "Entry_From_" + Spec.Id, d.Flag);
            exit.transform.SetParent(Exits.transform, true);
            RoomEntry entry = RoomKit.Entry("Entry_From_" + d.To, InsideDoor(d, 1.2f), InwardYaw(d));
            entry.transform.SetParent(Exits.transform, true);
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
        /// long side, four times at most (Bruno, 2026-10-31: "I prefer no hiding/blind spots"). The camera coverage test checks every floor point (DemoTests).
        /// </summary>
        private void CamerasOnCells()
        {
            GameObject root = LookTestSetup.ResetCameras();
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(root);
            RoomSpec s = Spec;
            Physics.SyncTransforms();
            int nx = Mathf.Max(1, Mathf.CeilToInt(s.Width / CellMax)), nz = Mathf.Max(1, Mathf.CeilToInt(s.Depth / CellMax));
            float cw = s.Width / nx, cd = s.Depth / nz;
            var cells = new Queue<(Rect cell, int splits)>();
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    cells.Enqueue((new Rect(s.X0 + i * cw, s.Z0 + j * cd, cw, cd), 0));
            var cameras = new List<FixedCamera>();
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
                string tag = cameras.Count.ToString("00");
                FixedCamera fc = MenuLoopSetup.EnsureFixedCamera(root, $"Cam_{s.Id}_{tag}", cam, look, fov);
                cameras.Add(fc);
                MenuLoopSetup.EnsureCameraZone(root, $"Zone_{s.Id}_{tag}", new Vector3(cell.center.x, 1.5f, cell.center.y), new Vector3(cell.width, 3f, cell.height), fc, 1);
                if (missed > 0f) Debug.Log($"[DemoRoom] {s.Scene} {fc.cameraName}: {missed:P0} of its cell out of frame");
            }
            director.SetDefaultCamera(cameras[0]);
            // The stubs: the camera of the cell at the doorway sees into them; a zone keeps it while she stands there.
            foreach (Door d in s.Doors)
            {
                if (string.IsNullOrEmpty(d.To)) continue;
                Vector3 mid = Mouth(d) + Outward(d.Side) * (Wall + StubLength * 0.5f);
                FixedCamera near = Nearest(cameras, Mouth(d));
                Vector3 size = d.Side == 'N' || d.Side == 'S' ? new Vector3(d.Width, 3f, StubLength + Wall) : new Vector3(StubLength + Wall, 3f, d.Width);
                MenuLoopSetup.EnsureCameraZone(root, $"Zone_Stub_{d.To}", mid + Vector3.up * 1.5f, size, near, 2);
            }
            MenuLoopSetup.EnsureShotPoints(cameras.ToArray());
        }

        private char FirstDoorSide() => Spec.Doors.Count > 0 ? Spec.Doors[0].Side : 'S';

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
                    Vector3[] aims = { middle, middle + away.normalized * Mathf.Min(cell.width, cell.height) * 0.25f };
                    foreach (Vector3 look in aims)
                    {
                        Quaternion rotation = Quaternion.LookRotation(look - cam);
                        foreach (float fov in Lenses)
                        {
                            int seen = 0;
                            foreach (Vector3 p in points) if (CameraCoverage.Sees(cam, rotation, fov, p, CameraCoverage.GameRoomNear, solid)) seen++;
                            if (seen > best.seen) best = (cam, look, fov, seen); // equal counts keep the narrower lens (the earlier one)
                        }
                    }
                }
            }
            float missed = points.Count == 0 ? 0f : 1f - (float)best.seen / points.Count;
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

        private static FixedCamera Nearest(List<FixedCamera> cameras, Vector3 p)
        {
            FixedCamera best = cameras[0];
            float bestD = float.MaxValue;
            foreach (FixedCamera c in cameras)
            {
                float d = (c.transform.position - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
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
        public GameObject Prop(string name, Vector3 floorPoint, Vector3 size, float shade = 0.3f) =>
            Box(Props, name, floorPoint + Vector3.up * size.y * 0.5f, size, Grey(shade));

        public static Material Grey(float v) => ItemAssets.UnlitMaterial("demo_grey_" + Mathf.RoundToInt(v * 100f), new Color(v, v, v * 1.05f));

        public static Material Colour(string id, Color c) => ItemAssets.UnlitMaterial("demo_" + id, c);

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

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
            return group;
        }

        /// <summary>An item to take: a small unlit box at the point.</summary>
        public ItemPickup Item(string name, ItemDefinition item, Vector3 at, Vector3 size)
        {
            GameObject obj = ItemAssets.Pickup(Props, name, item, PrimitiveType.Cube, at, size, Quaternion.identity);
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

        /// <summary>A sequence of steps under [Story], with a trigger box, or run when the room starts (trigger size zero).</summary>
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
                    Box(screen, $"Crt_{row}_{col}", floorPoint + turn * local, new Vector3(0.5f, 0.42f, 0.46f), Grey(0.1f)).transform.rotation = turn;
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

        /// <summary>A steel leaf in the doorway, locked: the key opens it (LockedDoor), and the way out behind it works then.</summary>
        public GameObject LockedLeaf(Door d, string keyId, string keyName)
        {
            Vector3 outward = Outward(d.Side);
            Vector3 across = new Vector3(outward.z, 0f, -outward.x);
            GameObject door = MenuLoopSetup.EnsureChild(Props, "Door_" + d.To);
            door.transform.position = Mouth(d);
            GameObject leaf = Box(door, "Leaf", Mouth(d) + outward * (Wall * 0.5f) + Vector3.up * 1.15f, Abs(across * d.Width + Vector3.up * 2.3f + outward * 0.1f), Grey(0.25f));
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
            GameObject lucy = GameObject.Find("Lucy");
            if (lucy != null) lucy.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        }
    }
}
#endif
