using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.UI;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The poison cloud of a gas grenade (Bruno, 2026-10-24: the gas is poison; "the soldiers and guards have
    /// masks"). For its seconds it hurts Lucy while she stands in its radius, damagePerSecond in ticks of 0.25 s.
    /// The enemies wear masks: it never hurts them. A green ring on the floor shows its edge, and the vendor smoke
    /// (tinted green, ShotEffects.gasPrefab) fills it. The dodge's invulnerability skips a tick. Numbers PROPOSED.
    /// </summary>
    public sealed class GasCloud : MonoBehaviour
    {
        private const float Tick = 0.25f;
        private const int RingPoints = 48;

        private float radius;
        private float seconds;
        private float damagePerSecond;
        private string killerId;
        private float tickTimer;
        private Transform player;
        private Health playerHealth;
        private LineRenderer ring;

        public static int Clouds { get; private set; }
        public float Radius => radius;
        public float SecondsLeft => seconds;
        public float DamageDealt { get; private set; }

        public static GasCloud Spawn(Vector3 centre, float radius, float seconds, float damagePerSecond, string killerId)
        {
            var obj = new GameObject("GasCloud");
            obj.transform.position = centre;
            var cloud = obj.AddComponent<GasCloud>();
            cloud.radius = radius;
            cloud.seconds = seconds;
            cloud.damagePerSecond = damagePerSecond;
            cloud.killerId = killerId;
            Clouds++;

            ShotEffects fx = ShotEffects.Load();
            if (fx != null && fx.gasPrefab != null)
            {
                GameObject smoke = Instantiate(fx.gasPrefab, centre, Quaternion.identity, obj.transform);
                smoke.transform.localScale = Vector3.one * radius / 6f; // the vendor smoke spreads about 6 m: fit the ring
                Color gas = new Color(0.75f, 1f, 0.35f, 0.6f); // with the smoke texture's own alpha
                foreach (ParticleSystem ps in smoke.GetComponentsInChildren<ParticleSystem>())
                {
                    var main = ps.main;
                    main.startColor = gas;
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null && renderer.sharedMaterial != null)
                    {
                        Material tinted = renderer.material; // an instance: the vendor material stays as it is
                        foreach (string property in new[] { "_BaseColor", "_Color", "_TintColor" })
                        {
                            if (tinted.HasProperty(property)) tinted.SetColor(property, new Color(gas.r, gas.g, gas.b, tinted.GetColor(property).a));
                        }
                    }
                    ps.Play(true);
                }
            }
            if (fx != null) ShotEffects.PlayAt(fx.gasClip, centre, 0.8f);
            cloud.ring = cloud.MakeRing();
            RunTelemetry.Event("gas_cloud", $"{killerId}@{centre.x:0.0},{centre.z:0.0}");
            return cloud;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            seconds -= dt;
            if (seconds <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            if (ring != null)
            {
                float width = 0.05f + 0.03f * Mathf.Sin(Time.time * 6f);
                ring.startWidth = width;
                ring.endWidth = width;
            }

            tickTimer -= dt;
            if (tickTimer > 0f) return;
            tickTimer = Tick;
            if (player == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p == null) return;
                player = p.transform;
                playerHealth = p.GetComponentInChildren<Health>();
            }
            if (playerHealth == null || playerHealth.IsDead || !Inside(player.position)) return;
            float amount = damagePerSecond * Tick;
            if (playerHealth.IsInvulnerable) return;
            DeathMenuUI.LastKillerId = killerId;
            playerHealth.TakeDamage(amount);
            DamageDealt += amount;
            RunTelemetry.Event("player_hit", $"{killerId}:gas:{amount:0.0}");
        }

        /// <summary>In the radius on the floor, and not above the cloud.</summary>
        public bool Inside(Vector3 point)
        {
            Vector3 c = transform.position;
            if (point.y - c.y > 2.2f) return false;
            point.y = c.y;
            return Vector3.Distance(point, c) <= radius;
        }

        private LineRenderer MakeRing()
        {
            var obj = new GameObject("GasRing");
            obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>();
            line.positionCount = RingPoints;
            line.loop = true;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader != null) line.material = new Material(shader) { color = EnemyGrenade.GasColour };
            line.startColor = EnemyGrenade.GasColour;
            line.endColor = EnemyGrenade.GasColour;
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i * Mathf.PI * 2f / RingPoints;
                line.SetPosition(i, transform.position + new Vector3(Mathf.Cos(a) * radius, 0.04f, Mathf.Sin(a) * radius));
            }
            return line;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Clouds = 0;
    }
}
