using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Cameras;

namespace GlorifiedBody.Rooms
{
    /// <summary>
    /// What a fixed camera of a room can see (Bruno, 2026-11-07: "cull rendering to what the player can see; this is easy
    /// due to the fixed camera angles", and "in some areas the walls are not fully built, so i can see through"). A grid of
    /// rays from the camera pose:
    /// - a room the ray crosses, or whose floor, wall or lintel it hits, is seen;
    /// - a ray that leaves the camera's room outside a doorway and hits something there is a gap.
    /// Only the floors, walls and lintels of the rooms stop a ray for the seen rooms: a door leaf opens and a barricade
    /// breaks, and the room behind them must still be drawn. The ceiling of a room (DemoRoom draws it, with no collider)
    /// stops a ray at the room's height: in the camera's room, and in the room behind a doorway.
    /// </summary>
    public static class RoomSight
    {
        public const int Columns = 96, Rows = 54;
        public const float Aspect = 16f / 9f, Reach = 60f;
        /// <summary>A hit this far past the room line still belongs to the room (half a wall and a little).</summary>
        public const float WallMargin = 0.2f;
        /// <summary>The top of a doorway over its floor, with room for the tallest door of the demo.</summary>
        public const float DoorTop = 2.9f;

        public struct Gap
        {
            public Vector3 exit;    // where the ray leaves the camera's room
            public Vector3 hit;     // what it hits outside
            public string what;     // the name of the thing hit, and its room
        }

        public sealed class Result
        {
            public readonly HashSet<Room> Seen = new HashSet<Room>();
            public readonly List<Gap> Gaps = new List<Gap>();
            public int Rays;
        }

        private static readonly RaycastHit[] hits = new RaycastHit[64];

        /// <summary>A floor, wall or lintel of a room (DemoRoom builds them under the room's "[Room]").</summary>
        public static bool IsStructure(Collider c)
        {
            Transform parent = c.transform.parent;
            if (parent == null || parent.name != "[Room]") return false;
            string n = c.name;
            return n.StartsWith("Floor") || n.EndsWith("_Floor") || n.StartsWith("Wall_") || n.StartsWith("Lintel_");
        }

        private static int SolidMask() => ~LayerMask.GetMask("Player", "Enemy", "BodyPart", "Debris", "Ignore Raycast", "UI");

        /// <summary>The ceiling of the room: its floor plus its height.</summary>
        public static float CeilingY(Room r) => r.BaseY + r.height;

        /// <summary>Scans the camera: own is the room it belongs to, rooms are all the rooms of the scene.</summary>
        public static Result Scan(FixedCamera camera, Room own, IReadOnlyList<Room> rooms, int columns = Columns, int rows = Rows, List<string> trace = null)
        {
            var result = new Result();
            result.Seen.Add(own);
            float yaw = camera.followPlayerYaw ? camera.followMaxYaw : 0f;
            foreach (float turn in yaw > 0f ? new[] { -yaw, 0f, yaw } : new[] { 0f })
                ScanPose(camera.transform.position, Quaternion.Euler(0f, turn, 0f) * camera.transform.rotation, camera.fieldOfView, own, rooms, columns, rows, result, trace);
            return result;
        }

        private static void ScanPose(Vector3 origin, Quaternion rotation, float fov, Room own, IReadOnlyList<Room> rooms, int columns, int rows, Result result, List<string> trace)
        {
            float tanV = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), tanH = tanV * Aspect;
            int mask = SolidMask();
            for (int i = 0; i < columns; i++)
                for (int j = 0; j < rows; j++)
                {
                    float x = (i + 0.5f) / columns * 2f - 1f, y = (j + 0.5f) / rows * 2f - 1f;
                    Vector3 dir = (rotation * new Vector3(x * tanH, y * tanV, 1f)).normalized;
                    result.Rays++;

                    // Where the ray stops: the ceiling of her room, or, past its side, the ceiling of the room behind.
                    float leave = LeaveAt(own, origin, dir);
                    float ownCeiling = CeilingAt(own, origin, dir);
                    bool inside = ownCeiling <= leave;
                    float stop = inside ? ownCeiling : Reach;
                    if (!inside)
                    {
                        Room beyond = RoomPast(own, origin + dir * (leave + 0.3f), rooms);
                        if (beyond != null) stop = Mathf.Max(leave, CeilingAt(beyond, origin, dir));
                    }

                    int n = Physics.RaycastNonAlloc(origin, dir, hits, Mathf.Min(stop, Reach), mask, QueryTriggerInteraction.Ignore);
                    float solid = float.MaxValue, structure = float.MaxValue;
                    Collider solidHit = null, structureHit = null;
                    for (int k = 0; k < n; k++)
                    {
                        RaycastHit h = hits[k];
                        if (h.distance < solid) { solid = h.distance; solidHit = h.collider; }
                        if (h.distance < structure && IsStructure(h.collider)) { structure = h.distance; structureHit = h.collider; }
                    }
                    float far = Mathf.Min(stop, structureHit != null ? structure : Reach);
                    if (structureHit != null)
                    {
                        Room owner = structureHit.GetComponentInParent<Room>();
                        if (owner != null && result.Seen.Add(owner))
                            trace?.Add($"ray {i},{j}: hits {structureHit.name} of {owner.id} at {structure:0.00} (stop {stop:0.00}, {n} hits)");
                    }
                    foreach (Room r in rooms)
                        if (!result.Seen.Contains(r) && Crosses(r, origin, dir, far))
                        {
                            result.Seen.Add(r);
                            trace?.Add($"ray {i},{j}: crosses {r.id} before {far:0.00} (structure {(structureHit != null ? structureHit.name + " of " + structureHit.GetComponentInParent<Room>()?.id : "none")} at {structure:0.00}, stop {stop:0.00}, {n} hits)");
                        }
                    if (!inside && solidHit != null) CheckGap(own, origin, dir, solid, leave, solidHit, result);
                }
        }

        /// <summary>A ray that hits something outside the room, past a line that is no doorway, is a gap.</summary>
        private static void CheckGap(Room own, Vector3 origin, Vector3 dir, float distance, float leave, Collider what, Result result)
        {
            Vector3 hit = origin + dir * distance;
            if (InFootprint(own, hit, WallMargin)) return;
            Vector3 exit = origin + dir * leave;
            if (InDoorway(own, exit)) return;
            Room other = what.GetComponentInParent<Room>();
            result.Gaps.Add(new Gap { exit = exit, hit = hit, what = what.name + " in " + (other != null ? other.id : "no room") });
        }

        /// <summary>The distance along the ray where it leaves the room's plan (its room lines).</summary>
        private static float LeaveAt(Room r, Vector3 origin, Vector3 dir)
        {
            Vector3 local = origin - r.transform.position;
            float leave = Reach;
            for (int axis = 0; axis < 2; axis++)
            {
                float o = axis == 0 ? local.x : local.z, d = axis == 0 ? dir.x : dir.z;
                float lo = axis == 0 ? r.rect.x : r.rect.y, hi = axis == 0 ? r.rect.z : r.rect.w;
                if (Mathf.Abs(d) < 1e-6f) continue;
                float t = d > 0f ? (hi - o) / d : (lo - o) / d;
                if (t >= 0f && t < leave) leave = t;
            }
            return leave;
        }

        /// <summary>The distance along the ray to the ceiling plane of the room (no end when the ray does not rise).</summary>
        private static float CeilingAt(Room r, Vector3 origin, Vector3 dir)
        {
            if (dir.y <= 1e-6f) return Reach;
            return Mathf.Max(0f, (CeilingY(r) - origin.y) / dir.y);
        }

        /// <summary>The room of a point just past the room's line (not the room itself), or null.</summary>
        private static Room RoomPast(Room own, Vector3 p, IReadOnlyList<Room> rooms)
        {
            foreach (Room r in rooms)
                if (r != own && InFootprint(r, p, 0f) && p.y <= CeilingY(r) + 0.5f) return r;
            return null;
        }

        private static bool InFootprint(Room r, Vector3 p, float margin)
        {
            Vector3 local = p - r.transform.position;
            return local.x >= r.rect.x - margin && local.x <= r.rect.z + margin && local.z >= r.rect.y - margin && local.z <= r.rect.w + margin
                && local.y >= -r.below - margin;
        }

        private static bool InDoorway(Room r, Vector3 p)
        {
            foreach (Room.Exit e in r.exits)
            {
                Vector3 mouth = r.transform.position + e.mouth;
                Vector3 across = new Vector3(e.outward.z, 0f, -e.outward.x);
                Vector3 d = p - mouth;
                if (Mathf.Abs(Vector3.Dot(d, e.outward)) > WallMargin + 0.05f) continue;
                if (Mathf.Abs(Vector3.Dot(d, across)) > e.width * 0.5f + 0.05f) continue;
                float up = p.y - r.BaseY;
                if (up < -r.below - 0.1f || up > DoorTop) continue;
                return true;
            }
            return false;
        }

        /// <summary>The ray crosses the room's box (its plan, from under its floor to its ceiling) before far.</summary>
        private static bool Crosses(Room r, Vector3 origin, Vector3 dir, float far)
        {
            Vector3 p = r.transform.position;
            Vector3 lo = new Vector3(p.x + r.rect.x, p.y - r.below, p.z + r.rect.y), hi = new Vector3(p.x + r.rect.z, CeilingY(r), p.z + r.rect.w);
            float t0 = 0f, t1 = far;
            for (int a = 0; a < 3; a++)
            {
                float o = origin[a], d = dir[a];
                if (Mathf.Abs(d) < 1e-6f)
                {
                    if (o < lo[a] || o > hi[a]) return false;
                    continue;
                }
                float ta = (lo[a] - o) / d, tb = (hi[a] - o) / d;
                if (ta > tb) (ta, tb) = (tb, ta);
                t0 = Mathf.Max(t0, ta);
                t1 = Mathf.Min(t1, tb);
                if (t0 > t1) return false;
            }
            return true;
        }
    }
}
