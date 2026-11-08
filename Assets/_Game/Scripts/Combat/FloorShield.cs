using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Combat
{
    /// <summary>
    /// The floor shield (2026-10-31, the design sweep's rank 15; design §2.3: "It rips chunks out of the floor as a
    /// shield"; Bruno, 2026-10-20: "RMB hold = the floor shield, the defensive full charge"). With mk_orzabal selected, a
    /// held defend key fills the circle; at a full circle a slab of the floor in front of her tears loose on its near
    /// edge and bends up into a standing wall, a makeshift barricade (Bruno, 2026-11-08: "it should be fixed in place ...
    /// she's pulling chunks of the floor and bending them up"). It does not depend on her hold: it stays where it rose
    /// until the bullets break it. Enemy bullets stop on it and wear it down; at 0 it shatters. Walkers go around it (it
    /// carves the NavMesh). A dark scar stays on the floor where it tore out. Numbers PROPOSED, placeholder look (the
    /// floor's own material on a box).
    /// </summary>
    public sealed class FloorShield : MonoBehaviour
    {
        public const float MaxHp = 80f;            // four rifle rounds of 20, PROPOSED
        public const float Width = 1.2f, Tall = 1.6f, Thick = 0.16f; // standing: over her chest (1.2 m), PROPOSED
        public const float Near = 0.5f;            // m in front of her: the edge it bends up on
        public const float RiseSeconds = 0.35f;

        private static readonly List<FloorShield> all = new List<FloorShield>();
        /// <summary>The slabs that stand now (each stays until it breaks).</summary>
        public static IReadOnlyList<FloorShield> All => all;
        /// <summary>The last slab she raised, while it stands.</summary>
        public static FloorShield Current => all.Count > 0 ? all[all.Count - 1] : null;

        public float Hp { get; private set; } = MaxHp;
        public int Absorbed { get; private set; }
        public GameObject Scar { get; private set; }
        /// <summary>The slab itself (this root is the hinge on the floor).</summary>
        public Transform Slab { get; private set; }
        public bool Standing => Time.time - born >= RiseSeconds;

        private float born;
        private Quaternion flat;

        /// <summary>Tears the slab out of the floor in front of her and starts to bend it up. Null when no floor lies there (a stair, a hole).</summary>
        public static FloorShield Tear(GameObject lucy)
        {
            if (lucy == null) return null;
            Vector3 forward = Vector3.ProjectOnPlane(lucy.transform.forward, Vector3.up).normalized;
            Vector3 hinge = lucy.transform.position + forward * Near;
            Vector3 middle = hinge + forward * Tall * 0.5f;
            if (!Physics.Raycast(middle + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor, 1f, ~0, QueryTriggerInteraction.Ignore)) return null;
            if (floor.collider.GetComponentInParent<GlorifiedBody.Core.Health>() != null) return null; // a body, not the floor
            Renderer floorRenderer = floor.collider.GetComponent<Renderer>();

            var root = new GameObject("FloorShield");
            root.transform.SetPositionAndRotation(new Vector3(hinge.x, floor.point.y, hinge.z), Quaternion.LookRotation(forward));
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            slab.layer = LayerMask.NameToLayer("Default");
            slab.transform.SetParent(root.transform, false);
            slab.transform.localPosition = new Vector3(0f, -Thick * 0.5f, Tall * 0.5f); // flat, its top at the floor
            slab.transform.localScale = new Vector3(Width, Thick, Tall);
            Renderer renderer = slab.GetComponent<Renderer>();
            if (floorRenderer != null && floorRenderer.sharedMaterial != null) renderer.sharedMaterial = floorRenderer.sharedMaterial;
            NavMeshObstacle obstacle = slab.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one; // the slab's own scale gives the box
            obstacle.carving = true;     // once it stands still

            FloorShield shield = root.AddComponent<FloorShield>();
            shield.Slab = slab.transform;
            shield.flat = root.transform.rotation;
            shield.born = Time.time;
            shield.Scar = ScarAt(new Vector3(middle.x, floor.point.y, middle.z) + floor.normal * 0.004f, Quaternion.LookRotation(forward, floor.normal));
            all.Add(shield);
            Sfx.Play(Sfx.Slam, middle, 0.8f);
            RunTelemetry.Event("floor_shield", $"{middle.x:0.0},{middle.z:0.0}");
            return shield;
        }

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
            Material material = Slab != null ? Slab.GetComponent<Renderer>().sharedMaterial : null;
            Vector3 centre = Slab != null ? Slab.position : transform.position;
            for (int i = 0; i < 8; i++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "PropPiece";
                Destroy(piece.GetComponent<Collider>());
                piece.transform.position = centre + Random.insideUnitSphere * 0.45f;
                piece.transform.rotation = Random.rotation;
                piece.transform.localScale = new Vector3(0.35f, 0.08f, 0.25f);
                if (material != null) piece.GetComponent<Renderer>().sharedMaterial = material;
                piece.AddComponent<PropPiece>().Velocity = (Random.insideUnitSphere + Vector3.up) * 2.5f;
            }
            all.Remove(this);
            Destroy(gameObject);
        }

        private void Update()
        {
            // It bends up on its near edge, then stands where it rose.
            float stand = Mathf.SmoothStep(0f, 1f, (Time.time - born) / RiseSeconds);
            transform.rotation = flat * Quaternion.Euler(-90f * stand, 0f, 0f);
            if (stand >= 1f) enabled = false;
        }

        private void OnDestroy() => all.Remove(this);

        /// <summary>The hole it leaves: a dark plate on the floor, the slab's size. The room keeps it (FloorScar, the saves).</summary>
        public static GameObject ScarAt(Vector3 at, Quaternion rotation)
        {
            var scar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            scar.name = "FloorShield_Scar";
            Destroy(scar.GetComponent<Collider>());
            scar.transform.SetPositionAndRotation(at, rotation);
            scar.transform.localScale = new Vector3(Width, 0.006f, Tall);
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
            {
                var material = new Material(unlit);
                material.SetColor("_BaseColor", new Color(0.03f, 0.03f, 0.035f));
                scar.GetComponent<Renderer>().sharedMaterial = material;
            }
            scar.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            scar.AddComponent<FloorScar>();
            return scar;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => all.Clear();
    }
}
