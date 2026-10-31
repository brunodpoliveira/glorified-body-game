using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Map
{
    /// <summary>The kinds of key places on the map (Bruno, 2026-10-31: "locked door, items, logs"). Save machines too (PROPOSED).</summary>
    public enum MapMarkKind
    {
        LockedDoor = 0,
        Item = 1,
        Log = 2,
        Save = 3
    }

    /// <summary>A key place of a room: its object (the path in the scene, as the saves name it) and where it is (world x, z).</summary>
    [Serializable]
    public sealed class MapMark
    {
        public MapMarkKind kind;
        public string path = "";
        public Vector2 position;
        public string label = "";
    }

    /// <summary>A way out of a room (a RoomExit): where it is and the room it leads to.</summary>
    [Serializable]
    public sealed class MapExit
    {
        public string path = "";
        public Vector2 position;
        public string targetScene = "";
    }

    /// <summary>
    /// One room of the map: a scene on a floor. The areas are its floor pieces (world x, z of the scene), the offset
    /// puts the room on the floor plan (floor metres = world x, z + offset).
    /// </summary>
    [Serializable]
    public sealed class MapRoom
    {
        public string scene = "";
        public string floor = "";
        public string title = "";
        public Vector2 offset;
        public List<Rect> areas = new List<Rect>();
        public List<MapExit> exits = new List<MapExit>();
        public List<MapMark> marks = new List<MapMark>();

        /// <summary>The room on the floor plan (floor metres).</summary>
        public Rect Bounds
        {
            get
            {
                if (areas.Count == 0) return new Rect(offset, Vector2.zero);
                Rect r = areas[0];
                foreach (Rect a in areas)
                {
                    r.xMin = Mathf.Min(r.xMin, a.xMin);
                    r.yMin = Mathf.Min(r.yMin, a.yMin);
                    r.xMax = Mathf.Max(r.xMax, a.xMax);
                    r.yMax = Mathf.Max(r.yMax, a.yMax);
                }
                r.position += offset;
                return r;
            }
        }

        /// <summary>A world point of this room (x, z) on the floor plan.</summary>
        public Vector2 OnPlan(Vector2 world) => world + offset;
    }

    [Serializable]
    public sealed class MapFloor
    {
        public string id = "";
        public string title = "";
    }

    /// <summary>
    /// The map of the building (Bruno, 2026-10-31): its floors and its rooms, with the key places of each room. Editor
    /// code reads the room scenes and writes it (GlorifiedBody.Editor.MapAtlasSetup); the game reads it from
    /// Resources/Map. What she knows of it is MapKnowledge.
    /// </summary>
    [CreateAssetMenu(fileName = "map_atlas", menuName = "GlorifiedBody/Map Atlas")]
    public sealed class MapAtlas : ScriptableObject
    {
        public const string ResourcePath = "Map/map_atlas";

        public List<MapFloor> floors = new List<MapFloor>();
        public List<MapRoom> rooms = new List<MapRoom>();

        private static MapAtlas loaded;

        /// <summary>The game's atlas, or null when none is built.</summary>
        public static MapAtlas Instance => loaded != null ? loaded : (loaded = Resources.Load<MapAtlas>(ResourcePath));

        public MapRoom Room(string scene) => rooms.Find(r => r.scene == scene);

        public MapFloor Floor(string id) => floors.Find(f => f.id == id);

        public IEnumerable<MapRoom> RoomsOn(string floor)
        {
            foreach (MapRoom room in rooms)
                if (room.floor == floor) yield return room;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => loaded = null;
    }
}
