using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Interaction;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Map
{
    /// <summary>
    /// What she knows of the map (Bruno, 2026-10-31: "the map will start hidden, and being revealed room-by-room unless
    /// the player picks up a floor plan item"). A room shows when she is in it or was in it (WorldState keeps every
    /// room she left, and the saves keep WorldState), or when she carries the floor plan of its floor (a key item
    /// with ItemDefinition.mapFloor). So the map needs no save data of its own, and the main menu clears it with the
    /// rest of the game.
    /// </summary>
    public static class MapKnowledge
    {
        /// <summary>She knows the room: she is in it, she was in it, or she has the plan of its floor.</summary>
        public static bool Knows(MapRoom room, ItemInventory items)
        {
            if (room == null) return false;
            return room.scene == SceneManager.GetActiveScene().name || WorldState.Visited(room.scene) || HasPlan(items, room.floor);
        }

        /// <summary>She carries the floor plan of the floor.</summary>
        public static bool HasPlan(ItemInventory items, string floor)
        {
            if (items == null || string.IsNullOrEmpty(floor)) return false;
            foreach (ItemDefinition item in items.Critical)
                if (item != null && item.kind == ItemKind.Map && item.mapFloor == floor) return true;
            return false;
        }

        /// <summary>
        /// The key place is still there: an item not taken, a door (open or not). In this room the object tells; in a
        /// room she left, the room's state does (its taken items). In a room she never saw, everything is still there.
        /// </summary>
        public static bool IsThere(MapRoom room, MapMark mark)
        {
            if (mark.kind != MapMarkKind.Item && mark.kind != MapMarkKind.Log) return true;
            Scene here = SceneManager.GetActiveScene();
            if (room.scene == here.name)
            {
                GameObject obj = ScenePaths.Find(here, mark.path);
                if (obj == null || !obj.activeInHierarchy) return false;
                return !obj.TryGetComponent(out ItemPickup pickup) || !pickup.Taken;
            }
            return !(WorldState.Rooms.TryGetValue(room.scene, out RoomState state) && state.taken.Contains(mark.path));
        }

        /// <summary>The locked door is open now (this room: the door; a room she left: its state).</summary>
        public static bool IsOpen(MapRoom room, MapMark mark)
        {
            if (mark.kind != MapMarkKind.LockedDoor) return false;
            Scene here = SceneManager.GetActiveScene();
            if (room.scene == here.name)
            {
                GameObject obj = ScenePaths.Find(here, mark.path);
                return obj != null && obj.TryGetComponent(out LockedDoor door) && door.IsOpen;
            }
            return WorldState.Rooms.TryGetValue(room.scene, out RoomState state) && state.opened.Contains(mark.path);
        }
    }
}
