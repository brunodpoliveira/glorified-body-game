using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Cameras;
using GlorifiedBody.Rooms;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The 180 rule (Bruno, 2026-11-16: "check the entire demo for 180 violations, and add that as a rule for future camera
    /// additions"): where she walks from one camera's zone into another's, in a room or through a doorway, her walk keeps its
    /// screen direction. On a grid of Step metres over every room floor, each step to the next point that she can walk (ground
    /// under both, no wall between) and that changes the camera is a cut. The cut flips when her walk goes right on one screen
    /// and left on the other, and both screens show it mostly sideways. A walk into the depth of one screen never flips.
    /// The world builder repairs the cell cameras with it (DemoWorld), DemoTests checks the whole demo with it.
    /// </summary>
    public static class ScreenDirection
    {
        public const float Step = 0.5f;
        public const float Sideways = 0.5f; // of a full sideways walk at the same depth: under it, the walk goes mostly into the depth

        public struct Cut
        {
            public Vector3 at, walk;
            public CameraZone from, to;
        }

        /// <summary>The zone the director picks at the point: the highest priority; a tie goes to the first name.</summary>
        public static CameraZone ZoneAt(IList<CameraZone> zones, Vector3 p)
        {
            CameraZone best = null;
            foreach (CameraZone z in zones)
                if (z.fixedCamera != null && z.Contains(p) && (best == null || z.priority > best.priority || (z.priority == best.priority && string.CompareOrdinal(z.name, best.name) < 0)))
                    best = z;
            return best;
        }

        /// <summary>A point she can stand on: ground under it, nothing solid at her waist.</summary>
        public static bool Ground(Vector3 p, int solid) =>
            Physics.Raycast(p + Vector3.up * 0.4f, Vector3.down, 0.7f, solid, QueryTriggerInteraction.Ignore)
            && !Physics.CheckSphere(p + Vector3.up * 0.5f, 0.15f, solid, QueryTriggerInteraction.Ignore);

        /// <summary>Every cut of the rooms' floors between two zones with two different cameras.</summary>
        public static List<Cut> Cuts(IEnumerable<Room> rooms, IList<CameraZone> zones, int solid)
        {
            var cuts = new List<Cut>();
            var near = new List<CameraZone>();
            foreach (Room room in rooms)
            {
                Vector3 o = room.transform.position;
                var area = new Bounds(new Vector3(o.x + (room.rect.x + room.rect.z) * 0.5f, o.y + 1f, o.z + (room.rect.y + room.rect.w) * 0.5f),
                    new Vector3(room.rect.z - room.rect.x + 2f, 4f, room.rect.w - room.rect.y + 2f));
                near.Clear();
                foreach (CameraZone z in zones)
                {
                    BoxCollider box = z.GetComponent<BoxCollider>();
                    Bounds b = box != null ? new Bounds(z.transform.TransformPoint(box.center), Vector3.Scale(box.size, z.transform.lossyScale)) : default;
                    b.Expand(Vector3.one * 0.1f);
                    if (box != null && (z.transform.rotation != Quaternion.identity || b.Intersects(area))) near.Add(z);
                }
                for (float x = room.rect.x + Step * 0.5f; x < room.rect.z; x += Step)
                    for (float z = room.rect.y + Step * 0.5f; z < room.rect.w; z += Step)
                    {
                        Vector3 p = new Vector3(o.x + x, o.y, o.z + z);
                        if (!Ground(p, solid)) continue;
                        CameraZone a = ZoneAt(near, p + Vector3.up);
                        if (a == null) continue;
                        foreach (Vector3 walk in new[] { Vector3.right, Vector3.forward })
                        {
                            Vector3 q = p + walk * Step;
                            if (!Ground(q, solid) || Physics.Linecast(p + Vector3.up * 0.5f, q + Vector3.up * 0.5f, solid, QueryTriggerInteraction.Ignore)) continue;
                            CameraZone b = ZoneAt(near, q + Vector3.up);
                            if (b == null || b.fixedCamera == a.fixedCamera) continue;
                            cuts.Add(new Cut { at = (p + q) * 0.5f, walk = walk, from = a, to = b });
                        }
                    }
            }
            return cuts;
        }

        /// <summary>
        /// A walk at the point on the screen of a camera pose (x right, y up), as a share of a walk across the view at the same
        /// depth: 1 is a full sideways walk; a walk straight at the lens from the side of the frame drifts sideways too, by the
        /// point's offset over its depth. Zero behind the lens.
        /// </summary>
        public static Vector2 OnScreen(Vector3 camPos, Quaternion rotation, Vector3 at, Vector3 walk)
        {
            Quaternion back = Quaternion.Inverse(rotation);
            Vector3 a = back * (at + Vector3.up - camPos), b = back * (at + Vector3.up + walk * 0.3f - camPos);
            if (a.z < 0.1f || b.z < 0.1f) return Vector2.zero;
            return (new Vector2(b.x / b.z, b.y / b.z) - new Vector2(a.x / a.z, a.y / a.z)) * (a.z / 0.3f);
        }

        /// <summary>The walk goes right on one screen and left on the other, at least half sideways on both.</summary>
        public static bool Flips(Vector3 posA, Quaternion rotA, Vector3 posB, Quaternion rotB, Vector3 at, Vector3 walk)
        {
            Vector2 sa = OnScreen(posA, rotA, at, walk), sb = OnScreen(posB, rotB, at, walk);
            return sa.x * sb.x < 0f && Mathf.Abs(sa.x) >= Sideways && Mathf.Abs(sb.x) >= Sideways;
        }

        public static bool Flips(Cut cut) =>
            Flips(cut.from.fixedCamera.transform.position, cut.from.fixedCamera.transform.rotation, cut.to.fixedCamera.transform.position, cut.to.fixedCamera.transform.rotation, cut.at, cut.walk);
    }
}
