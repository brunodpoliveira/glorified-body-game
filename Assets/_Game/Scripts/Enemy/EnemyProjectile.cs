using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.UI;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// A straight bullet. It moves by itself and checks the path with a ray each frame. On Lucy it names the
    /// killer for the death screen, then damages her. On anything else it dies. Lifetime 3 s.
    /// </summary>
    public sealed class EnemyProjectile : MonoBehaviour
    {
        private float speed;
        private float damage;
        private GameObject attacker;
        private string killerId;
        private float life = 3f;
        private CharacterController playerController;
        private const float Radius = 0.075f;

        public static EnemyProjectile Launch(Vector3 origin, Vector3 direction, float speed, float damage, GameObject attacker, string killerId)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = "EnemyProjectile";
            var collider = obj.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider); // at once: the ray must never see the bullet itself
            obj.transform.position = origin;
            obj.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            obj.transform.localScale = Vector3.one * 0.15f;
            var renderer = obj.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (renderer != null && shader != null) renderer.material = new Material(shader) { color = new Color(1f, 0.6f, 0.1f) };

            var projectile = obj.AddComponent<EnemyProjectile>();
            projectile.speed = speed;
            projectile.damage = damage;
            projectile.attacker = attacker;
            projectile.killerId = killerId;
            return projectile;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            life -= dt;
            if (life <= 0f)
            {
                RunTelemetry.Event("projectile_expired", killerId);
                Destroy(gameObject);
                return;
            }

            float step = speed * dt;
            Vector3 origin = transform.position;
            Vector3 next = origin + transform.forward * step;

            // Lucy first, by geometry: a ray does not register her CharacterController in every frame rate.
            if (CrossesPlayer(origin, next, out Vector3 playerPoint))
            {
                ImpactPlayer(playerPoint);
                return;
            }

            if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, step + 0.1f, ~0, QueryTriggerInteraction.Ignore))
            {
                bool self = attacker != null && (hit.transform == attacker.transform || hit.transform.IsChildOf(attacker.transform));
                bool player = hit.transform.CompareTag("Player") || (hit.transform.root != null && hit.transform.root.CompareTag("Player"));
                if (!self && !player)
                {
                    Impact(hit);
                    return;
                }
            }

            transform.position = next;
        }

        /// <summary>True when the bullet segment passes within the player's capsule (plus the bullet radius).</summary>
        private bool CrossesPlayer(Vector3 from, Vector3 to, out Vector3 point)
        {
            point = to;
            if (playerController == null)
            {
                GameObject playerObject = GameObject.FindWithTag("Player");
                playerController = playerObject != null ? playerObject.GetComponent<CharacterController>() : null;
                if (playerController == null) return false;
            }

            Transform t = playerController.transform;
            Vector3 center = t.TransformPoint(playerController.center);
            float half = Mathf.Max(0f, playerController.height * 0.5f - playerController.radius);
            Vector3 a = center + Vector3.up * half;
            Vector3 b = center - Vector3.up * half;
            float distance = SegmentDistance(from, to, a, b, out Vector3 onBullet);
            if (distance > playerController.radius + Radius) return false;
            point = onBullet;
            return true;
        }

        /// <summary>Closest distance between segments p0-p1 and q0-q1, and the closest point on the first.</summary>
        private static float SegmentDistance(Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1, out Vector3 onP)
        {
            Vector3 d1 = p1 - p0, d2 = q1 - q0, r = p0 - q0;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-6f && e <= 1e-6f) { onP = p0; return r.magnitude; }
            if (a <= 1e-6f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-6f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom != 0f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            onP = p0 + d1 * s;
            Vector3 onQ = q0 + d2 * t;
            return Vector3.Distance(onP, onQ);
        }

        private void ImpactPlayer(Vector3 point)
        {
            Health health = playerController.GetComponentInChildren<Health>() ?? playerController.GetComponentInParent<Health>();
            RunTelemetry.Event("projectile_impact", $"{killerId}>{playerController.name}@{point.x:0.0},{point.y:0.0},{point.z:0.0}");
            if (health != null && !health.IsDead)
            {
                DeathMenuUI.LastKillerId = killerId;
                health.TakeDamage(damage);
                HitEventBus.Raise(new HitInfo
                {
                    Attacker = attacker,
                    Victim = health.gameObject,
                    HitCollider = playerController,
                    HitTransform = playerController.transform,
                    HitPoint = point,
                    HitNormal = -transform.forward,
                    DamageAmount = damage,
                    DamageType = DamageType.EnemyProjectile,
                    WasFatal = health.IsDead
                });
                RunTelemetry.Event("player_hit", $"{killerId}:{damage:0}");
            }

            Destroy(gameObject);
        }

        private void Impact(RaycastHit hit)
        {
            Transform root = hit.transform.root;
            RunTelemetry.Event("projectile_impact", $"{killerId}>{hit.transform.name}@{hit.point.x:0.0},{hit.point.y:0.0},{hit.point.z:0.0}");
            bool isPlayer = hit.transform.CompareTag("Player") || (root != null && root.CompareTag("Player"));
            if (isPlayer)
            {
                Health health = hit.transform.GetComponentInParent<Health>() ?? root.GetComponentInChildren<Health>();
                if (health != null && !health.IsDead)
                {
                    DeathMenuUI.LastKillerId = killerId;
                    health.TakeDamage(damage);
                    HitEventBus.Raise(new HitInfo
                    {
                        Attacker = attacker,
                        Victim = health.gameObject,
                        HitCollider = hit.collider,
                        HitTransform = hit.transform,
                        HitPoint = hit.point,
                        HitNormal = hit.normal,
                        DamageAmount = damage,
                        DamageType = DamageType.EnemyProjectile,
                        WasFatal = health.IsDead
                    });
                    RunTelemetry.Event("player_hit", $"{killerId}:{damage:0}");
                }
            }

            Destroy(gameObject);
        }
    }
}
