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
            Blast(centre, radius); // crates, boxes, debris fly too (2026-11-15)
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
            if (piece == null || piece.IsBroken) return Push(collider, direction.normalized * ShotPush); // a crate, a box, a piece of debris
            piece.Damage(damage, type, point, direction);
            return true;
        }

        // Bruno, 2026-11-15: "check if grenades and bullets ... can also interact with these physic objects as well. there will
        // be book shelves etc so it can be plausible that bots will hit it". A shot pushes the movable thing it hits, an
        // explosion pushes every movable thing in its radius. Numbers PROPOSED.
        public const float ShotPush = 4f;                          // m/s along the shot (2 moved a crate 0.09 m: not seen)
        public const float BlastPushNear = 8f, BlastPushFar = 3f;  // m/s away from an explosion's centre

        /// <summary>
        /// Gives a movable thing this velocity: a body that is not kinematic, or a light prop with no body yet (it takes one, as
        /// in a lift). Not a person, not a thing in her hold, not a heavy prop, not a thing she rides. False: nothing moved.
        /// </summary>
        public static bool Push(Collider collider, Vector3 velocity)
        {
            if (collider == null || collider.GetComponentInParent<GlorifiedBody.Core.Health>() != null || collider.GetComponentInParent<IRagdollBody>() != null) return false;
            LockOnTargetable prop = collider.GetComponentInParent<LockOnTargetable>();
            if (prop != null && (prop.IsHeavy || !prop.IsProp || prop.GetComponent<TelekinesisLift>() != null)) return false;
            GlorifiedBody.Interaction.Rideable ride = collider.GetComponentInParent<GlorifiedBody.Interaction.Rideable>();
            if (ride != null && ride.Riding) return false;
            Rigidbody body = collider.attachedRigidbody;
            if (body == null && prop != null)
            {
                body = prop.gameObject.AddComponent<Rigidbody>();
                body.mass = 30f;
            }
            if (body == null || body.isKinematic) return false;
            body.WakeUp();
            body.linearVelocity += velocity;
            return true;
        }

        /// <summary>An explosion pushes the movable things in its radius, away from the centre and up. Returns how many.</summary>
        public static int Blast(Vector3 centre, float radius)
        {
            int pushed = 0;
            var done = new HashSet<Rigidbody>();
            foreach (Collider c in Physics.OverlapSphere(centre, radius, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
            {
                if (c.GetComponentInParent<Greeble>() != null) continue; // the splash wakes them already
                if (c.attachedRigidbody != null && !done.Add(c.attachedRigidbody)) continue;
                Vector3 away = c.bounds.center - centre;
                float distance = away.magnitude;
                Vector3 dir = distance > 0.01f ? away / distance : Vector3.up;
                float speed = Mathf.Lerp(BlastPushNear, BlastPushFar, Mathf.Clamp01(distance / radius));
                if (Push(c, (dir + Vector3.up * 0.5f).normalized * speed)) pushed++;
                if (c.attachedRigidbody != null) done.Add(c.attachedRigidbody);
            }
            return pushed;
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
