using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using GlorifiedBody.Cameras;
using GlorifiedBody.Rendering;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The camera coverage of a room (Bruno, 2026-10-22: "needs a new camera angle to cover this section"):
    /// for every floor point on a 0.5 m grid of every RoomFace floor, the camera its zone picks must frame the
    /// head and the hip with no occluder on the line of sight. A point under NearCamera metres of its camera is
    /// skipped: she fills the frame there, cut at the waist as in 1999. A point inside a planter, a column or a
    /// bed is skipped too. The tests of every room with fixed cameras call it.
    /// </summary>
    public static class CameraCoverage
    {
        public const float NearCamera = 3f;      // the look rooms (reference sets)
        public const float GameRoomNear = 1.2f;  // a game room: the player is never beside the lens
        public const float Aspect = 16f / 9f;

        public sealed class Report
        {
            public int Total;
            public readonly List<string> Uncovered = new List<string>();
            public float Share => Total > 0 ? (float)Uncovered.Count / Total : 0f;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append($"{Uncovered.Count} of {Total} floor points out of their camera's frame:");
                foreach (string line in Uncovered.Take(25)) sb.Append("\n  ").Append(line);
                return sb.ToString();
            }
        }

        public static Report Check(FixedCameraDirector director) => Check(director, NearCamera, false);

        /// <summary>The check with the room's own near distance: a game room uses 1.2 m (Lucy beside the lens is out of frame).</summary>
        /// <param name="targetsOcclude">A game room: the enemies and the targets on the Enemy layer hide her too.</param>
        public static Report Check(FixedCameraDirector director, float nearCamera, bool targetsOcclude)
        {
            var report = new Report();
            int roomLayers = 1 << LayerMask.NameToLayer("Default");
            CameraZone[] zones = Object.FindObjectsByType<CameraZone>(FindObjectsSortMode.None);
            // The floors: the RoomFace floors of the look rooms; a room of the kit (boxes, 2026-10-26) has none, so its
            // box colliders named "...Floor" stand in. Each floor is a centre and a size on the ground.
            // A box floor carries its collider: a point stands on its top (the floors of the world scene lie at two
            // heights, the stair's ramp slopes, 2026-11-02).
            var floors = new List<(Vector3 centre, float width, float depth, BoxCollider box)>();
            foreach (RoomFace face in Object.FindObjectsByType<RoomFace>(FindObjectsSortMode.None).Where(f => f.name.EndsWith("Floor")))
                floors.Add((face.transform.position, face.width, face.height, null)); // a floor face: local x along world x, local y along world z
            // Both kinds: the greybox tutorial has box floors and the plain closet of the old builder (2026-11-06).
                foreach (BoxCollider box in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(b => b.name.EndsWith("Floor") && !b.isTrigger))
                    floors.Add((new Vector3(box.bounds.center.x, box.bounds.max.y, box.bounds.center.z), box.bounds.size.x, box.bounds.size.z, box));
            foreach ((Vector3 c, float width, float depth, BoxCollider floorBox) in floors)
            {
                for (float dx = -width * 0.5f + 0.45f; dx <= width * 0.5f - 0.45f; dx += 0.5f)
                {
                    for (float dz = -depth * 0.5f + 0.45f; dz <= depth * 0.5f - 0.45f; dz += 0.5f)
                    {
                        Vector3 p = new Vector3(c.x + dx, floorBox != null ? c.y : 0f, c.z + dz);
                        if (floorBox != null)
                        {
                            if (!floorBox.Raycast(new Ray(p + Vector3.up * 0.5f, Vector3.down), out RaycastHit top, 20f)) continue;
                            p.y = top.point.y;
                        }
                        // The camera zones are trigger boxes on the same layer: both queries ignore triggers.
                        int solid = targetsOcclude ? roomLayers | LayerMask.GetMask("Enemy") : roomLayers;
                        if (Physics.CheckBox(p + Vector3.up * 0.9f, new Vector3(0.2f, 0.8f, 0.2f), Quaternion.identity, solid, QueryTriggerInteraction.Ignore)) continue;
                        report.Total++;
                        FixedCamera cam = CameraFor(p, zones, director);
                        if (cam == null) { report.Uncovered.Add($"{p.x:0.0},{p.z:0.0} no camera"); continue; }
                        Vector3 camPos = cam.transform.position;
                        if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(camPos.x, camPos.z)) < nearCamera) continue;
                        Vector3 head = p + Vector3.up * 1.6f, hip = p + Vector3.up * 0.9f;
                        bool inFrame = InFrame(cam, head, 0.05f) && InFrame(cam, hip, 0f);
                        bool hidden = Physics.Linecast(camPos, head, solid, QueryTriggerInteraction.Ignore);
                        if (!inFrame || hidden) report.Uncovered.Add($"{p.x:0.0},{p.z:0.0} {cam.cameraName} {(inFrame ? "hidden" : "out of frame")}");
                    }
                }
            }
            return report;
        }

        private static FixedCamera CameraFor(Vector3 p, CameraZone[] zones, FixedCameraDirector director)
        {
            CameraZone best = null;
            foreach (CameraZone zone in zones)
            {
                if (zone.fixedCamera == null || !zone.Contains(p + Vector3.up)) continue;
                if (best == null || zone.priority >= best.priority) best = zone;
            }
            return best != null ? best.fixedCamera : director != null ? director.DefaultCamera : null;
        }

        /// <summary>
        /// One floor point from one camera pose, the rule of Check: under the near distance it passes; else the head and
        /// the hip are in frame and nothing solid lies on the line to the head. A room builder scores its camera poses with it.
        /// </summary>
        public static bool Sees(Vector3 camPos, Quaternion rotation, float fieldOfView, Vector3 p, float nearCamera, int solid)
        {
            if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(camPos.x, camPos.z)) < nearCamera) return true;
            Vector3 head = p + Vector3.up * 1.6f, hip = p + Vector3.up * 0.9f;
            return InFrame(camPos, rotation, fieldOfView, head, 0.05f) && InFrame(camPos, rotation, fieldOfView, hip, 0f)
                && !Physics.Linecast(camPos, head, solid, QueryTriggerInteraction.Ignore);
        }

        private const float ExitWidthOnScreen = 0.06f; // of the frame's -1..1: a doorway seen edge on is no doorway
        private const float ExitStandOff = 0.7f;       // m in from the doorway: where she stands as she goes to it

        /// <summary>
        /// The exit rule (Bruno, 2026-11-05: "although i get good visuals on the room i also need to see the exits; in some
        /// rooms the doors/exits are in a blind spot"). From this pose the doorway is in frame from foot to head height and
        /// edge to edge, it does not show edge on, and nothing solid hides it. The camera stands on the room's side of it.
        /// </summary>
        public static bool SeesExit(Vector3 camPos, Quaternion rotation, float fieldOfView, Vector3 mouth, Vector3 outward, float width, int solid)
        {
            if (Vector3.Dot(camPos - mouth, outward) >= 0f) return false;
            Vector3 middle = mouth + Vector3.up * 1.2f;
            Vector3 side = new Vector3(outward.z, 0f, -outward.x) * (width * 0.5f - 0.1f);
            foreach (Vector3 p in new[] { middle + side, middle - side, mouth + Vector3.up * 0.3f, mouth + Vector3.up * 1.8f })
                if (!InFrame(camPos, rotation, fieldOfView, p, 0f)) return false;
            if (Mathf.Abs(ScreenX(camPos, rotation, fieldOfView, middle + side) - ScreenX(camPos, rotation, fieldOfView, middle - side)) < ExitWidthOnScreen) return false;
            return !Physics.Linecast(camPos, middle - outward * 0.3f, solid, QueryTriggerInteraction.Ignore); // short of the leaf of a shut door
        }

        /// <summary>Every exit of every room whose camera, the one for the floor in front of it, does not show it. Empty: the rule holds.</summary>
        public static List<string> ExitsOutOfView(FixedCameraDirector director)
        {
            var missing = new List<string>();
            int solid = 1 << LayerMask.NameToLayer("Default");
            CameraZone[] zones = Object.FindObjectsByType<CameraZone>(FindObjectsSortMode.None);
            foreach (GlorifiedBody.Rooms.Room room in GlorifiedBody.Rooms.Room.All)
                foreach (GlorifiedBody.Rooms.Room.Exit exit in room.exits)
                {
                    Vector3 mouth = room.transform.position + exit.mouth;
                    FixedCamera cam = CameraFor(mouth - exit.outward * ExitStandOff, zones, director);
                    if (cam == null) { missing.Add($"{room.id} to {exit.to}: no camera"); continue; }
                    if (!SeesExit(cam.transform.position, cam.transform.rotation, cam.fieldOfView, mouth, exit.outward, exit.width, solid))
                        missing.Add($"{room.id} to {exit.to}: {cam.cameraName}");
                }
            return missing;
        }

        private static float ScreenX(Vector3 camPos, Quaternion rotation, float fieldOfView, Vector3 point)
        {
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(camPos, rotation, Vector3.one).inverse;
            Vector4 clip = Matrix4x4.Perspective(fieldOfView, Aspect, 0.1f, 100f) * view * new Vector4(point.x, point.y, point.z, 1f);
            return clip.w > 0.01f ? clip.x / clip.w : 0f;
        }

        public static bool InFrame(FixedCamera cam, Vector3 point, float margin) =>
            InFrame(cam.transform.position, cam.transform.rotation, cam.fieldOfView, point, margin);

        public static bool InFrame(Vector3 camPos, Quaternion rotation, float fieldOfView, Vector3 point, float margin)
        {
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(camPos, rotation, Vector3.one).inverse;
            Matrix4x4 projection = Matrix4x4.Perspective(fieldOfView, Aspect, 0.1f, 100f);
            Vector4 clip = projection * view * new Vector4(point.x, point.y, point.z, 1f);
            if (clip.w <= 0.01f) return false;
            float x = clip.x / clip.w, y = clip.y / clip.w;
            return Mathf.Abs(x) <= 1f - margin && Mathf.Abs(y) <= 1f - margin;
        }
    }
}
