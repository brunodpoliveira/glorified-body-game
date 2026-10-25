using UnityEngine;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// The arc of one shock of the short, from her chest to the victim's chest (Bruno, 2026-10-25: "as of now it is
    /// not clear who the shock is damaging"). A jagged line of a few segments, blue white, that changes its shape
    /// every frame and goes after arcSeconds. A light at the victim's end. A PLACEHOLDER effect: Bruno, "for later:
    /// replace the placeholder fire/shock effects".
    /// </summary>
    public sealed class ShockArc : MonoBehaviour
    {
        public const float Seconds = 0.15f;
        private const int Points = 9;
        private const float Jag = 0.18f; // m, the largest side step of a point

        private static readonly Color Colour = new Color(0.75f, 0.88f, 1f);
        private static Material material;

        private LineRenderer line;
        private Vector3 from, to;
        private float endAt;

        /// <summary>The number of arcs alive now (the tests).</summary>
        public static int Alive { get; private set; }

        public static ShockArc Show(Vector3 from, Vector3 to)
        {
            var obj = new GameObject("ShockArc");
            var arc = obj.AddComponent<ShockArc>();
            arc.from = from;
            arc.to = to;
            arc.endAt = Time.time + Seconds;
            arc.line = obj.AddComponent<LineRenderer>();
            arc.line.positionCount = Points;
            arc.line.widthMultiplier = 0.035f;
            arc.line.numCapVertices = 2;
            arc.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arc.line.receiveShadows = false;
            arc.line.startColor = arc.line.endColor = Colour;
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (shader != null) material = new Material(shader) { color = Colour };
            }
            if (material != null) arc.line.sharedMaterial = material;
            var light = new GameObject("ShockArcLight").AddComponent<Light>();
            light.transform.SetParent(obj.transform, false);
            light.transform.position = to;
            light.type = LightType.Point;
            light.color = Colour;
            light.range = 3f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
            arc.Jagged();
            return arc;
        }

        private void OnEnable() => Alive++;
        private void OnDisable() => Alive--;

        private void Update()
        {
            if (Time.time >= endAt) { Destroy(gameObject); return; }
            Jagged();
        }

        /// <summary>A new jagged shape: the ends fixed, the middle points pushed to the side at random.</summary>
        private void Jagged()
        {
            Vector3 along = to - from;
            Vector3 side = Vector3.Cross(along, Vector3.up);
            if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
            side.Normalize();
            Vector3 up = Vector3.Cross(side, along).normalized;
            for (int i = 0; i < Points; i++)
            {
                float t = i / (float)(Points - 1);
                Vector3 p = from + along * t;
                if (i > 0 && i < Points - 1)
                {
                    float taper = Mathf.Sin(t * Mathf.PI); // the middle moves the most
                    p += (side * Random.Range(-1f, 1f) + up * Random.Range(-1f, 1f)) * Jag * taper;
                }
                line.SetPosition(i, p);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Alive = 0;
    }
}
