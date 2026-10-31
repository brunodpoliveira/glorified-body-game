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
        };

        /// <summary>
        /// Where each room lies on its floor (floor metres = world x, z + offset). TRAVEL A's east corridor and TRAVEL B's
        /// west corridor are the same piece of floor. KIT TEST lies east of TRAVEL B, the LAB east of the WARD.
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
        };

        [MenuItem("GlorifiedBody/Build Map Atlas")]
        public static void BuildAtlas()
        {
            // The rooms first: opening a scene unloads the assets in memory, the atlas with them (as the catalog, 2026-10-26).
            var rooms = new List<MapRoom>();
            foreach (Place place in Layout)
            {
                string path = MenuLoopSetup.ScenesFolder + "/" + place.Scene + ".unity";
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
