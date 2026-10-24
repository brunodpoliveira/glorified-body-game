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
    /// The grenade of the grenade launcher (Bruno, 2026-10-24). It arcs to the locked point on the floor under
    /// Lucy, lies there for a short fuse, and bursts: damage in a radius, less at the edge. A red ring on the floor
    /// shows the blast zone from the launch to the burst, so she can leave it. In the air, mk_orzabal's defend press
    /// catches it (PsychicPowers.TryCatchBullet): it flies back to the shooter, bursts there at once and hurts the
    /// enemies in the radius, twice as hard. Numbers PROPOSED.
    /// The gas grenade (Bruno, 2026-10-24: the gas grenadier; "the soldiers and guards have masks") flies the same
    /// way with a green ring, and bursts into a poison cloud (GasCloud) that hurts only her. A caught gas grenade
    /// bursts at its shooter: the masks keep the soldiers safe, but the cloud is away from her.
    /// </summary>
    public sealed class EnemyGrenade : MonoBehaviour
    {
        private const float Gravity = 9.81f;
        private const int RingPoints = 48;

        private Vector3 start;
        private Vector3 velocity;
        private float flight;
        private float flown;
        private float fuse;
        private float damage;
        private float radius;
        private GameObject attacker;
        private string killerId;
        private bool returned;
        private bool landed;
        private bool burst;
        private LineRenderer ring;
        private Vector3 target;
        private bool gas;
        private float gasSeconds;

        public static readonly Color FragColour = new Color(1f, 0.15f, 0.1f);
        public static readonly Color GasColour = new Color(0.55f, 0.9f, 0.2f);

        public static int Bursts { get; private set; }
        /// <summary>A grenade burst: the grenade and its centre (the tutorial's "leave the ring" step).</summary>
        public static event System.Action<EnemyGrenade, Vector3> OnBurst;
        public bool Catchable => !returned && !landed && !burst;
        public bool Returned => returned;
        public bool Landed => landed;
        public Vector3 Target => target;
        public float Radius => radius;
        public string KillerId => killerId;
        public bool IsGas => gas;
        /// <summary>The shooter (her, once she returned it).</summary>
        public GameObject Attacker => attacker;
        /// <summary>The floor ring of the blast zone.</summary>
        public LineRenderer Ring => ring;

        public static EnemyGrenade Launch(Vector3 origin, Vector3 floorPoint, float flightSeconds, float fuseSeconds, float damage, float radius, GameObject attacker, string killerId)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = "EnemyGrenade";
            var collider = obj.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);
            obj.transform.position = origin;
            obj.transform.localScale = Vector3.one * 0.12f;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null && shader != null) renderer.material = new Material(shader) { color = new Color(0.25f, 0.3f, 0.2f) };
            var trail = obj.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = 0.06f;
            trail.endWidth = 0f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (shader != null) trail.material = new Material(shader) { color = new Color(0.8f, 0.8f, 0.75f) };

            var grenade = obj.AddComponent<EnemyGrenade>();
            grenade.damage = damage;
            grenade.radius = radius;
            grenade.attacker = attacker;
            grenade.killerId = killerId;
            grenade.fuse = fuseSeconds;
            grenade.Aim(origin, floorPoint, flightSeconds);
            grenade.ring = grenade.MakeRing();
            return grenade;
        }

        /// <summary>A gas grenade: the same arc, a green ring, a poison cloud of the radius for the seconds.</summary>
        public static EnemyGrenade LaunchGas(Vector3 origin, Vector3 floorPoint, float flightSeconds, float fuseSeconds, float damagePerSecond, float radius, float seconds, GameObject attacker, string killerId)
        {
            EnemyGrenade grenade = Launch(origin, floorPoint, flightSeconds, fuseSeconds, damagePerSecond, radius, attacker, killerId);
            grenade.name = "EnemyGasGrenade";
            grenade.gas = true;
            grenade.gasSeconds = seconds;
            var renderer = grenade.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null) renderer.material.color = new Color(0.45f, 0.55f, 0.2f);
            grenade.PlaceRing(floorPoint, GasColour);
            return grenade;
        }

        /// <summary>A ballistic arc from the point to the target in the given time.</summary>
        private void Aim(Vector3 from, Vector3 to, float seconds)
        {
            start = from;
            target = to;
            flight = Mathf.Max(0.1f, seconds);
            flown = 0f;
            velocity = (to - from - 0.5f * Gravity * flight * flight * Vector3.down) / flight;
        }

        /// <summary>mk_orzabal caught it: back to the shooter, a burst on arrival, twice as hard on the enemies.</summary>
        public void Return(GameObject catcher)
        {
            if (!Catchable) return;
            returned = true;
            Vector3 back = transform.position - (target - transform.position).normalized * 6f;
            if (attacker != null)
            {
                Health shooter = attacker.GetComponentInParent<Health>() ?? attacker.GetComponentInChildren<Health>();
                if (shooter != null && !shooter.IsDead) back = attacker.transform.position;
            }
            back.y = Mathf.Min(back.y, transform.position.y);
            Aim(transform.position, back, 0.6f);
            fuse = 0f;
            if (!gas) damage *= 2f;
            attacker = catcher;
            var renderer = GetComponent<Renderer>();
            if (renderer != null && renderer.material != null) renderer.material.color = new Color(0.3f, 0.7f, 1f);
            PlaceRing(back, new Color(0.3f, 0.7f, 1f));
            RunTelemetry.Event("grenade_caught", killerId);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || burst) return;

            if (!landed)
            {
                flown += dt;
                float t = Mathf.Min(flown, flight);
                transform.position = start + velocity * t + 0.5f * Gravity * t * t * Vector3.down;
                if (flown >= flight)
                {
                    landed = true;
                    transform.position = target + Vector3.up * 0.06f;
                }
            }
            else
            {
                fuse -= dt;
                if (fuse <= 0f) Burst();
            }

            if (ring != null)
            {
                // The ring pulses faster as the burst comes.
                float left = landed ? fuse : flight - flown + fuse;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(30f, 10f, Mathf.Clamp01(left)));
                float width = Mathf.Lerp(0.05f, 0.1f, pulse);
                ring.startWidth = width;
                ring.endWidth = width;
            }
        }

        private void Burst()
        {
            burst = true;
            Bursts++;
            Vector3 centre = transform.position;
            OnBurst?.Invoke(this, centre);
            RunTelemetry.Event("grenade_burst", $"{killerId}{(returned ? ":returned" : "")}@{centre.x:0.0},{centre.z:0.0}");
            if (gas)
            {
                GasCloud.Spawn(centre, radius, gasSeconds, damage, killerId);
            }
            else
            {
                ShotEffects.Explosion(centre);
                Breakables.Splash(centre, radius, damage, DamageType.Grenade); // the room breaks around it
                if (returned) HurtEnemies(centre);
                else HurtPlayer(centre);
            }
            if (ring != null) Destroy(ring.gameObject);
            Destroy(gameObject);
        }

        /// <summary>Full damage at the centre, half at the edge.</summary>
        private float DamageAt(float distance) => damage * Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(distance / radius));

        private void HurtPlayer(Vector3 centre)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null) return;
            Health health = playerObject.GetComponentInChildren<Health>();
            if (health == null || health.IsDead) return;
            float distance = Flat(playerObject.transform.position, centre);
            if (distance > radius) return;
            float amount = DamageAt(distance);
            DeathMenuUI.LastKillerId = killerId;
            health.TakeDamage(amount);
            HitEventBus.Raise(new HitInfo
            {
                Attacker = attacker,
                Victim = health.gameObject,
                HitTransform = playerObject.transform,
                HitPoint = playerObject.transform.position + Vector3.up * Body.ChestHeight,
                HitNormal = (playerObject.transform.position - centre).normalized,
                DamageAmount = amount,
                DamageType = DamageType.Grenade,
                WasFatal = health.IsDead
            });
            RunTelemetry.Event("player_hit", $"{killerId}:grenade:{amount:0}");
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
                    DamageType = DamageType.Grenade, // an explosion: it cuts a limb (Bruno, 2026-10-24), Attacker is her
                    WasFatal = enemy.IsDead
                });
                enemy.GetComponentInParent<IStaggerable>()?.Stagger(1f);
                RunTelemetry.Event("grenade_returned_hit", $"{enemy.name}:{amount:0}");
            }
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private LineRenderer MakeRing()
        {
            var obj = new GameObject("GrenadeRing");
            var line = obj.AddComponent<LineRenderer>();
            line.positionCount = RingPoints;
            line.loop = true;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader != null) line.material = new Material(shader);
            ring = line;
            PlaceRing(target, FragColour);
            return line;
        }

        private void PlaceRing(Vector3 centre, Color colour)
        {
            if (ring == null) return;
            if (ring.material != null) ring.material.color = colour;
            ring.startColor = colour;
            ring.endColor = colour;
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i * Mathf.PI * 2f / RingPoints;
                ring.SetPosition(i, centre + new Vector3(Mathf.Cos(a) * radius, 0.04f, Mathf.Sin(a) * radius));
            }
        }

        private void OnDestroy()
        {
            if (ring != null) Destroy(ring.gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Bursts = 0;
    }
}
