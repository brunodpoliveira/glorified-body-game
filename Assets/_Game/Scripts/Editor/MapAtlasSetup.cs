#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Interaction;
using GlorifiedBody.Map;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The map's atlas (Bruno, 2026-10-31): the layout below puts each room scene on a floor, and the bake opens each
    /// scene and reads its floor pieces (objects named "...Floor"), its exits, and its key places (locked doors, the
    /// items and logs on the floor at the start, save machines). A carried item (a hidden pickup) is not a key place.
    /// Run it after a room builder changes a room. Batch: tools/unity.sh exec GlorifiedBody.Editor.MapAtlasSetup.BuildAtlas
    /// The test floors are PLACEHOLDERS: the demo's floors come with its rooms.
    /// </summary>
    public static class MapAtlasSetup
    {
        public const string AtlasFolder = "Assets/_Game/Resources/Map";
        public const string AtlasPath = AtlasFolder + "/map_atlas.asset";
        public const string TrainingFloor = "training";
        public const string TestFloor1 = "test_1";
        public const string TestFloor2 = "test_2";
        public const string TestFloor3 = "test_3";

        private struct Place
        {
            public string Scene, Floor, Title;
            public Vector2 Offset;
            public Place(string scene, string floor, string title, float x, float z) { Scene = scene; Floor = floor; Title = title; Offset = new Vector2(x, z); }
        }

        private static readonly (string id, string title)[] Floors =
        {
            (TrainingFloor, "TRAINING"),
            (TestFloor1, "TEST FLOOR 1"),
            (TestFloor2, "TEST FLOOR 2"),
            (TestFloor3, "TEST FLOOR 3"),
            (Demo.DemoItems.Floor2, "THE WARD"),
            (Demo.DemoItems.Floor1, "THE PUBLIC FLOOR"),
            (Demo.DemoItems.FloorRoad, "THE ROAD"),
            (Demo.DemoItems.FloorBrain, "THE BRAIN"),
        };

        /// <summary>
        /// Where each room lies on its floor (floor metres = world x, z + offset). TRAVEL A's east corridor and TRAVEL B's
        /// west corridor are the same piece of floor. KIT TEST lies east of TRAVEL B, the LAB east of the WARD, the SCREENS east of the LOCKERS.
        /// </summary>
        private static readonly Place[] Layout =
        {
            new Place("TrainingRoom", TrainingFloor, "TRAINING ROOM", 0f, 0f),
            new Place("Tutorial", TrainingFloor, "THERAPY GYM", 30f, 0f),
            new Place(TravelTestSetup.SceneA, TestFloor1, "TRAVEL A", 0f, 0f),
            new Place(TravelTestSetup.SceneB, TestFloor1, "TRAVEL B", 13.5f, 0f),
            new Place("KitTest", TestFloor1, "KIT TEST", 31f, 0f),
            new Place("WardTest", TestFloor2, "WARD", 0f, 0f),
            new Place("LabTest", TestFloor2, "LAB", 22.5f, 0f),
            new Place("OutfitTest", TestFloor3, "LOCKERS", 0f, 0f),
            new Place("ScreenTest", TestFloor3, "SCREENS", 16f, -13.8f), // the gym (z 10.3 to 17.3) east of the lockers' hall
        };

        /// <summary>The short names of the demo's rooms on the map (the world scene, 2026-11-02).</summary>
        private static readonly Dictionary<string, string> DemoTitles = new Dictionary<string, string>
        {
            { "W1", "RECOVERY" }, { "W1B", "TREATMENT" }, { "W2", "NORTH CORRIDOR" }, { "W3", "PATIENTS" }, { "W4", "SHOWERS" }, { "W5", "SOUTH CORRIDOR" },
            { "W6", "LOCKERS" }, { "W7", "THERAPY GYM" }, { "W8", "INDUCTION" }, { "W9", "NURSES' STATION" }, { "W10", "RESEARCH" },
            { "W11", "MAPPING LAB" }, { "W12", "STAIRWELL" }, { Demo.DemoWorld.Stair, "STAIRS" },
            { "G1", "LANDING" }, { "G2", "CONCOURSE" }, { "G3", "DONOR WALL" }, { "G4", "CHAPEL" }, { "G5", "RECEPTION" },
            { "G6", "BATHROOMS" }, { "G7", "FAMILY ROOMS" }, { "G8", "OFFICES" }, { "G9", "SECURITY" }, { "U1", "SECTOR C" },
            // The floorplan v2 (2026-11-05).
            { "W13", "SECURITY POST" }, { "W14", "DAY ROOM" }, { "W15", "BREAK ROOM" }, { "W16", "COLD ROOM" }, { "W17", "STORAGE" },
            { "G9b", "COMMS" }, { "G10", "CAFETERIA" }, { "G11", "BREAK ROOM" }, { "G12", "CONFERENCE" }, { "G13", "ADMISSIONS" },
            { "G14", "BUILDERS' STORE" }, { "G15", "STAFF TOILETS" }, { "G16", "RISER ROOM" },
            { "X1", "CAR PARK" }, { "R1", "THE SHOP" }, { "R2", "SERVICE BAY" }, { "R3", "TOLL YARD" },
            { "B1", "THE BRAIN" }, { "B2", "BUILDERS' DOOR" }, // 2026-11-09
            { "G18", "THE DINING HALL" }, { "G19", "THE KITCHEN" }, // 2026-11-10
        };

        [MenuItem("GlorifiedBody/Build Map Atlas")]
        public static void BuildAtlas()
        {
            // The rooms first: opening a scene unloads the assets in memory, the atlas with them (as the catalog, 2026-10-26).
            var rooms = new List<MapRoom>();
            foreach (Place place in Layout)
            {
                string path = MenuLoopSetup.ScenesFolder + "/" + (place.Scene.StartsWith("Demo_") ? "Demo/" : "") + place.Scene + ".unity";
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[MapAtlasSetup] no scene {path}: the room is not on the map.");
                    continue;
                }
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                MapRoom room = Read(scene, place);
                rooms.Add(room);
                Debug.Log($"[MapAtlasSetup] {place.Scene}: {room.areas.Count} areas, {room.exits.Count} exits, {room.marks.Count} marks, bounds {room.Bounds}");
            }
            if (File.Exists(Demo.DemoWorld.ScenePath)) rooms.AddRange(ReadWorld());
            else Debug.LogWarning($"[MapAtlasSetup] no scene {Demo.DemoWorld.ScenePath}: the demo is not on the map.");
            Directory.CreateDirectory(AtlasFolder);
            var atlas = AssetDatabase.LoadAssetAtPath<MapAtlas>(AtlasPath);
            if (atlas == null)
            {
                atlas = ScriptableObject.CreateInstance<MapAtlas>();
                AssetDatabase.CreateAsset(atlas, AtlasPath);
            }
            atlas.floors.Clear();
            foreach (var floor in Floors) atlas.floors.Add(new MapFloor { id = floor.id, title = floor.title });
            atlas.rooms.Clear();
            atlas.rooms.AddRange(rooms);
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MapAtlasSetup] {AtlasPath}: {atlas.floors.Count} floors, {atlas.rooms.Count} rooms.");
        }

        private static MapRoom Read(Scene scene, Place place)
        {
            var room = new MapRoom { scene = place.Scene, floor = place.Floor, title = place.Title, offset = place.Offset };
            foreach (GameObject top in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in top.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.gameObject.activeInHierarchy || !renderer.name.EndsWith("Floor", System.StringComparison.OrdinalIgnoreCase)) continue;
                    Bounds b = renderer.bounds;
                    room.areas.Add(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z));
                }
                foreach (RoomExit exit in top.GetComponentsInChildren<RoomExit>())
                    room.exits.Add(new MapExit { path = ScenePaths.Of(exit.transform), position = Flat(exit.transform.position), targetScene = exit.targetScene });
                foreach (LockedDoor door in top.GetComponentsInChildren<LockedDoor>())
                {
                    Transform at = door.leaf != null ? door.leaf : door.transform;
                    room.marks.Add(new MapMark { kind = MapMarkKind.LockedDoor, path = ScenePaths.Of(door.transform), position = Flat(at.position), label = door.keyName });
                }
                foreach (ItemPickup pickup in top.GetComponentsInChildren<ItemPickup>())
                {
                    if (!pickup.gameObject.activeInHierarchy || pickup.item == null) continue; // a carried item waits hidden
                    MapMarkKind kind = pickup.item.kind == ItemKind.Log || pickup.item.kind == ItemKind.AudioLog ? MapMarkKind.Log : MapMarkKind.Item;
                    room.marks.Add(new MapMark { kind = kind, path = ScenePaths.Of(pickup.transform), position = Flat(pickup.transform.position), label = pickup.item.displayName });
                }
                foreach (SavePoint save in top.GetComponentsInChildren<SavePoint>())
                    room.marks.Add(new MapMark { kind = MapMarkKind.Save, path = ScenePaths.Of(save.transform), position = Flat(save.transform.position), label = "SAVE" });
            }
            if (room.areas.Count == 0) Debug.LogWarning($"[MapAtlasSetup] {place.Scene}: no floor piece (an object named ...Floor with a renderer).");
            return room;
        }

        /// <summary>
        /// The rooms of the demo's world scene (2026-11-02): one map room per Room root, in world coordinates (floor 1
        /// lies to the south-east of the ward; the map shows one floor at a time). The doorways from the room specs are
        /// the marks of the ways out (a doorway has no exit object any more).
        /// </summary>
        private static List<MapRoom> ReadWorld()
        {
            Scene scene = EditorSceneManager.OpenScene(Demo.DemoWorld.ScenePath, OpenSceneMode.Single);
            var specs = Demo.DemoWard.Rooms().Concat(Demo.DemoPublic.Rooms()).ToDictionary(r => r.Id);
            var rooms = new List<MapRoom>();
            foreach (GlorifiedBody.Rooms.Room r in Object.FindObjectsByType<GlorifiedBody.Rooms.Room>(FindObjectsSortMode.None).OrderBy(r => r.transform.GetSiblingIndex()))
            {
                var room = new MapRoom { scene = r.id, world = scene.name, floor = r.floor, title = DemoTitles.TryGetValue(r.id, out string t) ? t : r.title };
                foreach (Renderer renderer in r.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.name.EndsWith("Floor", System.StringComparison.OrdinalIgnoreCase)) continue;
                    Bounds b = renderer.bounds;
                    room.areas.Add(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z));
                }
                if (specs.TryGetValue(r.id, out Demo.RoomSpec spec))
                {
                    Vector3 shift = r.transform.position;
                    foreach (Demo.Door d in spec.Doors)
                    {
                        if (string.IsNullOrEmpty(d.To)) continue;
                        Vector3 mouth = d.Side == 'N' ? new Vector3(d.At, 0f, spec.Z1) : d.Side == 'S' ? new Vector3(d.At, 0f, spec.Z0) : d.Side == 'E' ? new Vector3(spec.X1, 0f, d.At) : new Vector3(spec.X0, 0f, d.At);
                        room.exits.Add(new MapExit { path = r.id + "/" + d.To, position = Flat(mouth + shift), targetScene = d.To });
                    }
                }
                foreach (LockedDoor door in r.GetComponentsInChildren<LockedDoor>(true))
                {
                    Transform at = door.leaf != null ? door.leaf : door.transform;
                    room.marks.Add(new MapMark { kind = MapMarkKind.LockedDoor, path = ScenePaths.Of(door.transform), position = Flat(at.position), label = door.keyName });
                }
                foreach (ItemPickup pickup in r.GetComponentsInChildren<ItemPickup>())
                {
                    if (!pickup.gameObject.activeInHierarchy || pickup.item == null) continue;
                    MapMarkKind kind = pickup.item.kind == ItemKind.Log || pickup.item.kind == ItemKind.AudioLog ? MapMarkKind.Log : MapMarkKind.Item;
                    room.marks.Add(new MapMark { kind = kind, path = ScenePaths.Of(pickup.transform), position = Flat(pickup.transform.position), label = pickup.item.displayName });
                }
                foreach (SavePoint save in r.GetComponentsInChildren<SavePoint>())
                    room.marks.Add(new MapMark { kind = MapMarkKind.Save, path = ScenePaths.Of(save.transform), position = Flat(save.transform.position), label = "SAVE" });
                rooms.Add(room);
                Debug.Log($"[MapAtlasSetup] {scene.name}/{r.id}: {room.areas.Count} areas, {room.exits.Count} doorways, {room.marks.Count} marks");
            }
            return rooms;
        }

        private static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);
    }
}
#endif
