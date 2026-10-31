#if UNITY_EDITOR
using System.Collections.Generic;
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
            // The demo (2026-10-31): each room in plan coordinates.
            new Place("Demo_W1", Demo.DemoItems.Floor2, "RECOVERY", 0f, 0f),
            new Place("Demo_W2", Demo.DemoItems.Floor2, "NORTH CORRIDOR", 0f, 0f),
            new Place("Demo_W3", Demo.DemoItems.Floor2, "PATIENTS", 0f, 0f),
            new Place("Demo_W4", Demo.DemoItems.Floor2, "SHOWERS", 0f, 0f),
            new Place("Demo_W5", Demo.DemoItems.Floor2, "SOUTH CORRIDOR", 0f, 0f),
            new Place("Demo_W6", Demo.DemoItems.Floor2, "LOCKERS", 0f, 0f),
            new Place("Demo_W7", Demo.DemoItems.Floor2, "THERAPY GYM", 0f, 0f),
            new Place("Demo_W8", Demo.DemoItems.Floor2, "INDUCTION", 0f, 0f),
            new Place("Demo_W9", Demo.DemoItems.Floor2, "NURSES' STATION", 0f, 0f),
            new Place("Demo_W10", Demo.DemoItems.Floor2, "RESEARCH", 0f, 0f),
            new Place("Demo_W11", Demo.DemoItems.Floor2, "MAPPING LAB", 0f, 0f),
            new Place("Demo_W12", Demo.DemoItems.Floor2, "STAIRWELL", 0f, 0f),
            new Place("Demo_G1", Demo.DemoItems.Floor1, "LANDING", 0f, 0f),
            new Place("Demo_G2", Demo.DemoItems.Floor1, "CONCOURSE", 0f, 0f),
            new Place("Demo_G3", Demo.DemoItems.Floor1, "DONOR WALL", 0f, 0f),
            new Place("Demo_G4", Demo.DemoItems.Floor1, "CHAPEL", 0f, 0f),
            new Place("Demo_G5", Demo.DemoItems.Floor1, "RECEPTION", 0f, 0f),
            new Place("Demo_G6", Demo.DemoItems.Floor1, "BATHROOMS", 0f, 0f),
            new Place("Demo_G7", Demo.DemoItems.Floor1, "FAMILY ROOMS", 0f, 0f),
            new Place("Demo_G8", Demo.DemoItems.Floor1, "OFFICES", 0f, 0f),
            new Place("Demo_G9", Demo.DemoItems.Floor1, "SECURITY", 0f, 0f),
            new Place("Demo_U1", Demo.DemoItems.Floor1, "SECTOR C", 0f, 0f),
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

        private static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);
    }
}
#endif
