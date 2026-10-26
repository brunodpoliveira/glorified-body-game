using UnityEngine;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Destruction;
using GlorifiedBody.UI;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The power armour's rocket (Bruno, 2026-10-24: "the power armor may have frag grenades and rockets"). It flies
    /// straight to the locked point, slower than a bullet, and bursts on the first thing it meets (her, a wall, the
    /// floor) or after 4 s: damage in a radius, half at the edge. mk_orzabal's defend press catches it within the
    /// catch radius, for more MK than a bullet: it flies back to its shooter and bursts on the enemies, twice as
    /// hard. A returned rocket steers to its shooter (her telekinesis guides it): a straight one missed a running
    /// power armour in the test. The model is the vendor RPG's missile (ShotEffects.rocketMesh), else a dark capsule. Numbers PROPOSED.
    /// </summary>
    public sealed class EnemyRocket : MonoBehaviour
    {
        private const float HitRadius = 0.35f; // around her body axis

        private float speed;
        private float damage;
        private float radius;
        private GameObject attacker;
        private string killerId;
        private float life = 4f;
        private bool returned;
        private bool burst;
        private Transform player;
        private LockOnTargetable homingTarget;
        private Health homingHealth;
        private const float HomingDegreesPerSecond = 360f;

        public static int Bursts { get; private set; }
        public bool Catchable => !returned && !burst;
        public bool Returned => returned;
        public string KillerId => killerId;

        public static EnemyRocket Launch(Vector3 origin, Vector3 direction, float speed, float damage, float radius, GameObject attacker, string killerId)
        {
            var obj = new GameObject("EnemyRocket");
            obj.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction.normalized, Vector3.up));
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            ShotEffects fx = ShotEffects.Load();
            if (fx != null && fx.rocketMesh != null)
            {
                var model = new GameObject("Model");
                model.transform.SetParent(obj.transform, false);
                model.transform.localRotation = fx.rocketMeshRotation;
                model.transform.localScale = Vector3.one * fx.rocketMeshScale;
                model.AddComponent<MeshFilter>().sharedMesh = fx.rocketMesh;
                model.AddComponent<MeshRenderer>().sharedMaterial = fx.rocketMaterial;
            }
            else
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                DestroyImmediate(body.GetComponent<Collider>());
                body.transform.SetParent(obj.transform, false);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body.transform.localScale = new Vector3(0.1f, 0.18f, 0.1f);
                if (shader != null) body.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.2f, 0.22f, 0.2f) };
            }
            var trail = obj.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.startWidth = 0.12f;
            trail.endWidth = 0f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (shader != null) trail.material = new Material(shader) { color = new Color(1f, 0.55f, 0.15f) };
            trail.startColor = new Color(1f, 0.55f, 0.15f);
            trail.endColor = new Color(0.6f, 0.6f, 0.6f, 0f);

            var rocket = obj.AddComponent<EnemyRocket>();
            GlorifiedBody.Audio.Sfx.Loop(GlorifiedBody.Audio.Sfx.RocketLoop, obj, 0.6f);
            rocket.speed = speed;
            rocket.damage = damage;
            rocket.radius = radius;
            rocket.attacker = attacker;
            rocket.killerId = killerId;
            return rocket;
        }

        /// <summary>mk_orzabal caught it: back to the shooter's chest, faster, twice as hard on the enemies.</summary>
        public void Return(GameObject catcher)
        {
            if (!Catchable) return;
            returned = true;
            Vector3 direction = -transform.forward;
            if (attacker != null)
            {
                Health shooter = attacker.GetComponentInParent<Health>() ?? attacker.GetComponentInChildren<Health>();
                LockOnTargetable aim = attacker.GetComponent<LockOnTargetable>();
                Vector3 chest = aim != null ? aim.GetLockPoint() : attacker.transform.position + Vector3.up * Body.ChestHeight;
                if (shooter != null && !shooter.IsDead)
                {
                    direction = (chest - transform.position).normalized;
                    homingTarget = aim;
                    homingHealth = shooter;
                }
            }
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            speed *= 1.3f;
            damage *= 2f;
            life = 4f;
            attacker = catcher;
            RunTelemetry.Event("rocket_caught", killerId);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || burst) return;
            life -= dt;
            if (life <= 0f)
            {
                Burst(transform.position);
                return;
            }

            if (returned && homingTarget != null && homingHealth != null && !homingHealth.IsDead)
            {
                Vector3 want = homingTarget.GetLockPoint() - transform.position;
                if (want.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(want.normalized, Vector3.up), HomingDegreesPerSecond * dt);
                }
            }

            Vector3 from = transform.position;
            float step = speed * dt;
            Vector3 to = from + transform.forward * step;

            if (!returned && NearPlayer(from, to, out Vector3 at))
            {
                Burst(at);
                return;
            }
            RaycastHit[] hits = Physics.RaycastAll(from, transform.forward, step + 0.1f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Vector3 point = Vector3.zero;
            foreach (RaycastHit hit in hits)
            {
                bool self = attacker != null && (hit.transform == attacker.transform || hit.transform.IsChildOf(attacker.transform));
                bool her = hit.transform.CompareTag("Player") || (hit.transform.root != null && hit.transform.root.CompareTag("Player"));
                if (self || (returned && her)) continue;
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    point = hit.point;
                }
            }
            if (nearest < float.MaxValue)
            {
                Burst(point);
                return;
            }
            transform.position = to;
        }

        /// <summary>The step passes within HitRadius of her body axis.</summary>
        private bool NearPlayer(Vector3 from, Vector3 to, out Vector3 point)
        {
            point = to;
            if (player == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p == null) return false;
                player = p.transform;
            }
            Vector3 feet = player.position;
            for (int i = 0; i <= 4; i++)
            {
                Vector3 q = Vector3.Lerp(from, to, i / 4f);
                if (q.y < feet.y - 0.1f || q.y > feet.y + 1.8f) continue;
                Vector2 flat = new Vector2(q.x - feet.x, q.z - feet.z);
                if (flat.magnitude <= HitRadius)
                {
                    point = q;
                    return true;
                }
            }
            return false;
        }

        private void Burst(Vector3 centre)
        {
            if (burst) return;
            burst = true;
            Bursts++;
            RunTelemetry.Event("rocket_burst", $"{killerId}{(returned ? ":returned" : "")}@{centre.x:0.0},{centre.z:0.0}");
            ShotEffects.Explosion(centre);
            Breakables.Splash(centre, radius, damage, DamageType.EnemyRocket); // the room breaks around it
            if (returned) HurtEnemies(centre);
            else HurtPlayer(centre);
            Destroy(gameObject);
        }

        private float DamageAt(float distance) => damage * Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(distance / radius));

        private void HurtPlayer(Vector3 centre)
        {
            GameObject p = GameObject.FindWithTag("Player");
            Health health = p != null ? p.GetComponentInChildren<Health>() : null;
            if (health == null || health.IsDead) return;
            float distance = Flat(p.transform.position, centre);
            if (distance > radius) return;
            float amount = GlorifiedBody.Systems.DifficultyManager.EnemyDamage(DamageAt(distance));
            DeathMenuUI.LastKillerId = killerId;
            health.TakeDamage(amount);
            HitEventBus.Raise(new HitInfo
            {
                Attacker = attacker,
                Victim = health.gameObject,
                HitTransform = p.transform,
                HitPoint = p.transform.position + Vector3.up * Body.ChestHeight,
                HitNormal = (p.transform.position - centre).normalized,
                DamageAmount = amount,
                DamageType = DamageType.EnemyRocket,
                WasFatal = health.IsDead
            });
            RunTelemetry.Event("player_hit", $"{killerId}:rocket:{amount:0}");
        }

        private void HurtEnemies(Vector3 centre)
        {
            foreach (Health enemy in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (enemy == null || enemy.IsDead || enemy.CompareTag("Player")) continue;
                float distance = Flat(enemy.transform.position, centre);
                if (distance > radius) continue;
                float amount = DamageAt(distance);
                enemy.TakeDamage(amount);
                HitEventBus.Raise(new HitInfo
                {
                    Attacker = attacker,
                    Victim = enemy.gameObject,
                    HitTransform = enemy.transform,
                    HitPoint = enemy.transform.position + Vector3.up * Body.ChestHeight,
                    HitNormal = (enemy.transform.position - centre).normalized,
                    DamageAmount = amount,
                    DamageType = DamageType.EnemyRocket, // an explosion: it cuts a limb (Bruno, 2026-10-24), Attacker is her
                    WasFatal = enemy.IsDead
                });
                enemy.GetComponentInParent<IStaggerable>()?.Stagger(1f);
                RunTelemetry.Event("rocket_returned_hit", $"{enemy.name}:{amount:0}");
            }
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Bursts = 0;
    }
}
