using UnityEngine;
using UnityEngine.Rendering;

namespace GlorifiedBody.Rendering
{
    /// <summary>
    /// Switch 1 of profile B: two bakes of the same room in one scene. The scene carries the high bake (B: 40 texels a
    /// metre, four bounces, the panel as an area light, directional). This component carries the low one (A's numbers:
    /// 10 texels, two bounces, no area light, non-directional): its lightmaps, the index and the scale-offset of every
    /// lightmapped renderer, and the probe coefficients (the same probe positions in both bakes). RemakeLookSetup.Bake
    /// fills it between the two bakes.
    /// </summary>
    public sealed class RemakeBakeSwap : MonoBehaviour
    {
        public Texture2D[] lowColor = new Texture2D[0];
        public Renderer[] renderers = new Renderer[0];
        public int[] lowIndex = new int[0];
        public Vector4[] lowScaleOffset = new Vector4[0];
        public float[] lowProbes = new float[0]; // 27 floats a probe (SphericalHarmonicsL2)

        private LightmapData[] highMaps;
        private LightmapsMode highMode;
        private int[] highIndex;
        private Vector4[] highScaleOffset;
        private SphericalHarmonicsL2[] highProbes;
        private SphericalHarmonicsL2[] lowProbeArray;
        private bool captured;

        public bool HighOn { get; private set; } = true;
        public bool HasLow => lowColor != null && lowColor.Length > 0 && renderers.Length == lowIndex.Length;

        /// <summary>The state the scene loaded with is the high bake: keep it to come back to.</summary>
        public void CaptureHigh()
        {
            if (captured) return;
            captured = true;
            highMaps = LightmapSettings.lightmaps;
            highMode = LightmapSettings.lightmapsMode;
            highIndex = new int[renderers.Length];
            highScaleOffset = new Vector4[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                highIndex[i] = renderers[i].lightmapIndex;
                highScaleOffset[i] = renderers[i].lightmapScaleOffset;
            }
            LightProbes probes = LightmapSettings.lightProbes;
            highProbes = probes != null ? probes.bakedProbes : null;
            if (highProbes != null && lowProbes.Length == highProbes.Length * 27)
            {
                lowProbeArray = new SphericalHarmonicsL2[highProbes.Length];
                for (int p = 0; p < lowProbeArray.Length; p++)
                {
                    var sh = new SphericalHarmonicsL2();
                    for (int c = 0; c < 3; c++)
                    for (int k = 0; k < 9; k++) sh[c, k] = lowProbes[p * 27 + c * 9 + k];
                    lowProbeArray[p] = sh;
                }
            }
        }

        public void Use(bool high)
        {
            CaptureHigh();
            if (!high && !HasLow) high = true;
            HighOn = high;
            if (high)
            {
                LightmapSettings.lightmapsMode = highMode;
                LightmapSettings.lightmaps = highMaps;
            }
            else
            {
                var maps = new LightmapData[lowColor.Length];
                for (int i = 0; i < maps.Length; i++) maps[i] = new LightmapData { lightmapColor = lowColor[i] };
                LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
                LightmapSettings.lightmaps = maps;
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].lightmapIndex = high ? highIndex[i] : lowIndex[i];
                renderers[i].lightmapScaleOffset = high ? highScaleOffset[i] : lowScaleOffset[i];
            }
            LightProbes probes = LightmapSettings.lightProbes;
            SphericalHarmonicsL2[] wanted = high ? highProbes : lowProbeArray;
            if (probes != null && wanted != null && wanted.Length == probes.count) probes.bakedProbes = wanted;
        }
    }
}
