using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GlorifiedBody.Rendering
{
    /// <summary>
    /// The dark of a room with its lights out (DEMO_PLAN §10.1, the dark room). The world has one sun and a flat ambient
    /// light, so a dark renderer leaves the sun's rendering layer (LitLayer) for DarkLayer and takes a near-black ambient
    /// probe of its own. Only the lights on DarkLayer (the monitors) reach it then. The URP assets have rendering layers
    /// on (m_SupportsLightLayers). The sun of a room with a dark room must light LitLayer only.
    /// The greybox of the demo is unlit (DemoRoom.Grey), and light does not reach an unlit material. A dark unlit renderer
    /// takes DarkFactor of its colour, plus a glow from each light on DarkLayer near it (a fake of the monitors' light for
    /// the greybox). A renderer with Emissive keeps its own colour.
    /// </summary>
    public static class Darkness
    {
        public const uint LitLayer = 1u, DarkLayer = 2u;
        /// <summary>What is left of the ambient light in the dark: shapes, no colours.</summary>
        public static readonly Color DarkAmbient = new Color(0.025f, 0.025f, 0.03f);

        /// <summary>What an unlit renderer keeps of its colour in the dark, with no light near it.</summary>
        public const float DarkFactor = 0.2f;
        public const float MaxFactor = 0.5f;

        private static readonly Dictionary<Renderer, (uint mask, LightProbeUsage usage)> saved = new Dictionary<Renderer, (uint, LightProbeUsage)>();
        private static readonly Dictionary<Renderer, Color> unlit = new Dictionary<Renderer, Color>();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock block;
        private static readonly List<SphericalHarmonicsL2> darkProbe = new List<SphericalHarmonicsL2>(1);

        public static bool IsDark(Renderer r) => r != null && saved.ContainsKey(r);

        /// <summary>Puts the renderer in the dark, or gives it its light back.</summary>
        public static void Set(Renderer r, bool dark)
        {
            if (r == null || dark == saved.ContainsKey(r)) return;
            if (dark)
            {
                saved[r] = (r.renderingLayerMask, r.lightProbeUsage);
                r.renderingLayerMask = DarkLayer;
                if (darkProbe.Count == 0)
                {
                    var sh = new SphericalHarmonicsL2();
                    sh.AddAmbientLight(DarkAmbient);
                    darkProbe.Add(sh);
                }
                block ??= new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.CopySHCoefficientArraysFrom(darkProbe);
                r.SetPropertyBlock(block);
                r.lightProbeUsage = LightProbeUsage.CustomProvided;
                if (IsUnlit(r))
                {
                    unlit[r] = r.sharedMaterial.GetColor(BaseColor);
                    Tint(r, FactorAt(r.bounds.center, DarkLights()));
                }
            }
            else
            {
                (uint mask, LightProbeUsage usage) = saved[r];
                saved.Remove(r);
                r.renderingLayerMask = mask;
                r.lightProbeUsage = usage;
                if (unlit.TryGetValue(r, out Color original))
                {
                    unlit.Remove(r);
                    block ??= new MaterialPropertyBlock();
                    r.GetPropertyBlock(block);
                    block.SetColor(BaseColor, original);
                    r.SetPropertyBlock(block);
                }
            }
        }

        /// <summary>A light on DarkLayer came on or went off (the monitors): the unlit dark renderers take their glow again.</summary>
        public static void RefreshGlow()
        {
            List<Light> lights = DarkLights();
            var gone = new List<Renderer>();
            foreach (KeyValuePair<Renderer, Color> pair in unlit)
            {
                if (pair.Key == null) { gone.Add(pair.Key); continue; }
                Tint(pair.Key, FactorAt(pair.Key.bounds.center, lights));
            }
            foreach (Renderer r in gone) unlit.Remove(r);
        }

        /// <summary>The share of its colour an unlit dark renderer shows at a point (the tests).</summary>
        public static float FactorAt(Vector3 point, List<Light> lights)
        {
            float f = DarkFactor;
            foreach (Light l in lights)
            {
                float d = Vector3.Distance(point, l.transform.position);
                if (d < l.range) f += l.intensity * 0.05f * (1f - d / l.range);
            }
            return Mathf.Min(MaxFactor, f);
        }

        private static List<Light> DarkLights()
        {
            var list = new List<Light>();
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.isActiveAndEnabled && l.type != LightType.Directional && ((uint)l.renderingLayerMask & DarkLayer) != 0) list.Add(l);
            return list;
        }

        private static bool IsUnlit(Renderer r) =>
            r.GetComponent<Emissive>() == null && r.sharedMaterial != null && r.sharedMaterial.shader != null
            && r.sharedMaterial.shader.name.Contains("Unlit") && r.sharedMaterial.HasProperty(BaseColor);

        private static void Tint(Renderer r, float factor)
        {
            if (!unlit.TryGetValue(r, out Color original)) return;
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            Color c = original * factor;
            c.a = original.a;
            block.SetColor(BaseColor, c);
            r.SetPropertyBlock(block);
        }

        /// <summary>Every renderer under the root (a figure, a room part).</summary>
        public static void Set(GameObject root, bool dark)
        {
            if (root == null) return;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) Set(r, dark);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            saved.Clear();
            unlit.Clear();
        }
    }
}
