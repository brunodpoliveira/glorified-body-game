using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Combat;

namespace GlorifiedBody.Destruction
{
    /// <summary>
    /// The ways the fight reaches the room (the study, 2026-10-24): a splash (a blast at its target, a slam, a
    /// landing, an explosion), a hit on one collider (an enemy bullet, a thrown body), the cover check on the line of
    /// a power, and the surge of the short on the lamps. The Debris layer hits the room, the other pieces and the
    /// ragdolls, not her and not the capsules. Numbers PROPOSED.
    /// </summary>
    public static class Breakables
    {
        public const string DebrisLayerName = "Debris";
        public const float GreebleReach = 1.3f; // a splash wakes items a little beyond its damage radius

        public static int DebrisLayer => LayerMask.NameToLayer(DebrisLayerName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SetUpCollisions()
        {
            int debris = DebrisLayer;
            if (debris < 0) return;
            int bodyPart = LayerMask.NameToLayer("BodyPart");
            for (int layer = 0; layer < 32; layer++)
                Physics.IgnoreLayerCollision(debris, layer, !(layer == 0 || layer == debris || layer == bodyPart));
        }

        /// <summary>Damage with a fall-off to every intact piece in the radius (but the one that took the hit), and a push to the items. Returns the number broken.</summary>
        public static int Splash(Vector3 centre, float radius, float damage, DamageType type, Breakable except = null)
        {
            if (radius <= 0f) return 0;
            int broken = 0;
            var targets = new List<Breakable>(Breakable.All);
            foreach (Breakable b in targets)
            {
                if (b == null || b.IsBroken || b == except) continue;
                Vector3 near = b.Bounds.ClosestPoint(centre);
                float distance = Vector3.Distance(near, centre);
                if (distance > radius) continue;
                Vector3 direction = near - centre;
                if (b.Damage(damage * Mathf.Lerp(1f, 0.4f, distance / radius), type, near, direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up)) broken++;
            }
            foreach (Greeble g in new List<Greeble>(Greeble.All))
            {
                if (g == null) continue;
                float distance = Vector3.Distance(g.transform.position, centre);
                if (distance <= radius * GreebleReach) g.Disturb(centre, Mathf.Lerp(5f, 2f, distance / (radius * GreebleReach)));
            }
            return broken;
        }

        /// <summary>A hit on one collider: an item wakes, a piece takes the damage. True when the collider is part of the room's breakables.</summary>
        public static bool HitCollider(Collider collider, float damage, DamageType type, Vector3 point, Vector3 direction)
        {
            if (collider == null) return false;
            Greeble item = collider.GetComponentInParent<Greeble>();
            if (item != null)
            {
                item.Disturb(point - direction * 0.5f, 3f);
                return true;
            }
            Breakable piece = collider.GetComponentInParent<Breakable>();
            if (piece == null || piece.IsBroken) return false;
            piece.Damage(damage, type, point, direction);
            return true;
        }

        /// <summary>
        /// The intact cover that stands first on the line, or null. A plain wall in front of it hides it (and the
        /// powers keep their rule for walls). The cover's box counts a little above its top, so a line that grazes
        /// the top still stops on it (her chest and his lock point are both at about 1.2 m).
        /// </summary>
        public static Breakable CoverOnLine(Vector3 from, Vector3 to, out Vector3 point)
        {
            point = to;
            Vector3 line = to - from;
            float length = line.magnitude;
            if (length < 0.01f) return null;
            var ray = new Ray(from, line / length);
            float wall = length;
            foreach (RaycastHit hit in Physics.RaycastAll(ray, length, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
            {
                Breakable piece = hit.collider.GetComponentInParent<Breakable>();
                if (piece == null && hit.distance < wall) wall = hit.distance;
            }
            Breakable best = null;
            float bestDistance = wall;
            foreach (Breakable b in Breakable.All)
            {
                if (b == null || !b.IsCover) continue;
                Bounds box = b.Bounds;
                box.Expand(new Vector3(0f, CoverGraze * 2f, 0f));
                if (box.Contains(from) || !box.IntersectRay(ray, out float distance) || distance >= bestDistance) continue;
                best = b;
                bestDistance = distance;
            }
            if (best != null) point = ray.GetPoint(bestDistance);
            return best;
        }

        public const float CoverGraze = 0.12f; // m above the top that still counts

        /// <summary>The short's surge pops the lamps in the radius (PROPOSED). Returns the number broken.</summary>
        public static int Surge(Vector3 centre, float radius)
        {
            int broken = 0;
            foreach (Breakable b in new List<Breakable>(Breakable.All))
            {
                if (!(b is BreakableLight) || b.IsBroken) continue;
                Vector3 near = b.Bounds.ClosestPoint(centre);
                if (Vector3.Distance(near, centre) > radius) continue;
                b.Break(near, Vector3.down);
                broken++;
            }
            return broken;
        }
    }
}
