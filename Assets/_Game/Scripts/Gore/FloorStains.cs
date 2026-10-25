using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Gore
{
    /// <summary>
    /// The blood on the floor stays (Bruno, 2026-10-24: "blood pools disappear, and too quickly"). The driver's
    /// floor stains were pooled particles that lived 6 s in a ring of 24. Each one is now a flat quad with a stain
    /// material, laid once, which stays for the room. The body pools follow the same rule (BodyBloodPool). The
    /// oldest goes above MaxStains, a guard for a long fight, PROPOSED.
    /// </summary>
    public static class FloorStains
    {
        public const int MaxStains = 300;
        private const float Lift = 0.008f; // above the floor; each stain a little higher than the last few

        private static readonly List<GameObject> stains = new List<GameObject>();

        public static int Count { get { stains.RemoveAll(s => s == null); return stains.Count; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => stains.Clear();

        /// <summary>Lays a stain of the size (metres) on the surface, turned at random.</summary>
        public static GameObject Lay(Material material, Vector3 point, Vector3 normal, float size)
        {
            if (material == null) return null;
            stains.RemoveAll(s => s == null);
            var stain = GameObject.CreatePrimitive(PrimitiveType.Quad);
            stain.name = "FloorStain";
            Object.Destroy(stain.GetComponent<Collider>());
            Renderer renderer = stain.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            stain.transform.SetPositionAndRotation(point + normal * (Lift + 0.0015f * (stains.Count % 5)),
                Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            stain.transform.localScale = new Vector3(size, size * Random.Range(0.75f, 1f), 1f);
            stains.Add(stain);
            while (stains.Count > MaxStains)
            {
                if (stains[0] != null) Object.Destroy(stains[0]);
                stains.RemoveAt(0);
            }
            RunTelemetry.Event("gore_floor_stain", $"{size:0.00}m");
            return stain;
        }
    }
}
