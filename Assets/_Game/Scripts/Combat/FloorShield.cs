using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Combat
{
    /// <summary>
    /// The floor shield (2026-10-31, the design sweep's rank 15; design §2.3: "It rips chunks out of the floor as a
    /// shield"; Bruno, 2026-10-20: "RMB hold = the floor shield, the defensive full charge"). With mk_orzabal selected, a
    /// held defend key fills the circle; at a full circle a slab of the floor in front of her tears loose, stands up and
    /// hangs between her and her target, and it turns with her (it is a held prop: TelekinesisLift). Enemy bullets stop
    /// on it and wear it down; at 0 it shatters. The attack key throws it like a crate (a heavy target too), a full
    /// circle slams it; either breaks it. A dark scar stays on the floor where it tore out. One at a time. Numbers
    /// PROPOSED, placeholder look (the floor's own material on a box).
    /// </summary>
    public sealed class FloorShield : MonoBehaviour
    {
        public const float MaxHp = 80f;            // four rifle rounds of 20, PROPOSED
        public const float Width = 1.0f, Tall = 1.2f, Thick = 0.16f;
        public const float Ahead = 0.9f;           // m in front of her
        public const float HangHeight = 1.1f;      // its centre over the floor
        public const float RiseSeconds = 0.35f;

        public static FloorShield Current { get; private set; }

        public float Hp { get; private set; } = MaxHp;
        public int Absorbed { get; private set; }
        public GameObject Holder { get; private set; }
        public GameObject Scar { get; private set; }

        private float born;
        private TelekinesisLift lift;

        /// <summary>
        /// Tears the slab out of the floor in front of her: it lies in the floor's place, and the lift raises it. The
        /// caller applies the lift (PsychicPowers). Null when no floor lies there (a stair, a hole).
        /// </summary>
        public static FloorShield Tear(GameObject lucy)
        {
            if (lucy == null) return null;
            Vector3 forward = Vector3.ProjectOnPlane(lucy.transform.forward, Vector3.up).normalized;
            Vector3 spot = lucy.transform.position + forward * Ahead;
            if (!Physics.Raycast(spot + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor, 1f, ~0, QueryTriggerInteraction.Ignore)) return null;
            if (floor.collider.GetComponentInParent<GlorifiedBody.Core.Health>() != null) return null; // a body, not the floor
            Renderer floorRenderer = floor.collider.GetComponent<Renderer>();

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "FloorShield";
            slab.layer = LayerMask.NameToLayer("Default");
            slab.transform.position = new Vector3(spot.x, floor.point.y, spot.z);
            slab.transform.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(90f, 0f, 0f); // flat, in the floor
            slab.transform.localScale = new Vector3(Width, Tall, Thick);
            Renderer renderer = slab.GetComponent<Renderer>();
            if (floorRenderer != null && floorRenderer.sharedMaterial != null) renderer.sharedMaterial = floorRenderer.sharedMaterial;
            CharacterController body = lucy.GetComponent<CharacterController>();
            if (body != null) Physics.IgnoreCollision(body, slab.GetComponent<Collider>()); // she walks with it in front of her
            LockOnTargetable target = slab.AddComponent<LockOnTargetable>();
            target.SetProp(true);
            target.SetSelectable(false); // the switch never picks her own shield
            FloorShield shield = slab.AddComponent<FloorShield>();
            shield.Holder = lucy;
            shield.born = Time.time;
            shield.Scar = MakeScar(slab.transform.position, forward, floor.normal);
            Current = shield;
            Sfx.Play(Sfx.Slam, slab.transform.position, 0.8f);
            RunTelemetry.Event("floor_shield", $"{spot.x:0.0},{spot.z:0.0}");
            return shield;
        }

        /// <summary>The lift that holds it (PsychicPowers sets it).</summary>
        public void HeldBy(TelekinesisLift by) => lift = by;

        /// <summary>A bullet or a blast stops on it. True when it holds; at 0 HP it shatters.</summary>
        public bool Absorb(float damage, Vector3 point, Vector3 direction)
        {
            if (Hp <= 0f) return false;
            Hp -= damage;
            Absorbed++;
            RunTelemetry.Event("floor_shield_hit", $"{damage:0}:{Hp:0}");
            if (Hp > 0f) return true;
            Shatter();
            return false;
        }

        /// <summary>It breaks apart: pieces of its material fly and vanish.</summary>
        public void Shatter()
        {
            RunTelemetry.Event("floor_shield_broken", $"{Absorbed}");
            Material material = GetComponent<Renderer>().sharedMaterial;
            for (int i = 0; i < 8; i++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "PropPiece";
                Destroy(piece.GetComponent<Collider>());
                piece.transform.position = transform.position + Random.insideUnitSphere * 0.45f;
                piece.transform.rotation = Random.rotation;
                piece.transform.localScale = new Vector3(0.35f, 0.08f, 0.25f);
                if (material != null) piece.GetComponent<Renderer>().sharedMaterial = material;
                piece.AddComponent<PropPiece>().Velocity = (Random.insideUnitSphere + Vector3.up) * 2.5f;
            }
            Destroy(gameObject);
        }

        private void LateUpdate()
        {
            // The lift let it go without a break (another hit dropped it): it lies there, and a new shield may rise.
            if (lift == null && Current == this && Time.time - born > RiseSeconds + 0.5f) Current = null;
            // While it hangs it stands up from the floor, then faces the way she faces. A thrown slab keeps its turn.
            if (lift == null || !lift.CanLaunch || Holder == null) return;
            float stand = Mathf.SmoothStep(0f, 1f, (Time.time - born) / RiseSeconds);
            Quaternion yaw = Quaternion.Euler(0f, Holder.transform.eulerAngles.y, 0f);
            transform.rotation = yaw * Quaternion.Euler(90f * (1f - stand), 0f, 0f);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>The hole it leaves: a dark plate on the floor, the slab's size. It stays for the room.</summary>
        private static GameObject MakeScar(Vector3 at, Vector3 forward, Vector3 normal)
        {
            var scar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            scar.name = "FloorShield_Scar";
            Destroy(scar.GetComponent<Collider>());
            scar.transform.position = at + normal * 0.004f;
            scar.transform.rotation = Quaternion.LookRotation(forward, normal);
            scar.transform.localScale = new Vector3(Width, 0.006f, Tall);
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
            {
                var material = new Material(unlit);
                material.SetColor("_BaseColor", new Color(0.03f, 0.03f, 0.035f));
                scar.GetComponent<Renderer>().sharedMaterial = material;
            }
            scar.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return scar;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Current = null;
    }
}
