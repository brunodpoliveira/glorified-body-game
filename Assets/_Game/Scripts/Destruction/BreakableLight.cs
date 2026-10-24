using System.Collections;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Destruction
{
    /// <summary>
    /// A lamp that breaks (Bruno, 2026-10-24: "breakable lights if possible"). A hit or a splash on it, an
    /// explosion, or the start of the short near it: sparks, a flicker of about 0.3 s, then the light, its bounce
    /// and the glow of its panel go out, and the glass falls. The lamp's light is Realtime, so the bake leaves it
    /// out (a baked bounce would stay on the walls with no source). Its bounce is faked by one weak point light
    /// with no shadows that dies with it ("realtime with a fake bounce"). Numbers PROPOSED.
    /// </summary>
    public sealed class BreakableLight : Breakable
    {
        private const float FlickerSeconds = 0.3f;

        [SerializeField] private Light lamp;
        [SerializeField] private Light bounce;
        [SerializeField] private Renderer panel;
        [SerializeField] private Material panelOff;

        public Light Lamp => lamp;
        public Light Bounce => bounce;
        public bool IsDark { get; private set; }
        public static int TotalDark { get; private set; }

        public void BindLight(Light main, Light fill, Renderer glow, Material off)
        {
            lamp = main;
            bounce = fill;
            panel = glow;
            panelOff = off;
        }

        /// <summary>A lamp breaks from anything that reaches it, the short's surge too.</summary>
        protected override float Scale(DamageType type)
        {
            if (type == DamageType.ShortShock) return 1f;
            return base.Scale(type) > 0f ? Mathf.Max(1f, base.Scale(type)) : 1f;
        }

        protected override void OnBreak(Vector3 point)
        {
            DestructionFx.Sparks(lamp != null ? lamp.transform.position : transform.position);
            if (isActiveAndEnabled) StartCoroutine(Die());
            else Dark();
        }

        protected override void OnRestoredBroken() => Dark();

        private IEnumerator Die()
        {
            float lampOn = lamp != null ? lamp.intensity : 0f;
            float bounceOn = bounce != null ? bounce.intensity : 0f;
            float t = 0f;
            while (t < FlickerSeconds)
            {
                float k = Random.value > 0.45f ? Random.Range(0.6f, 1.4f) : 0f;
                if (lamp != null) lamp.intensity = lampOn * k;
                if (bounce != null) bounce.intensity = bounceOn * k;
                float wait = Random.Range(0.03f, 0.07f);
                t += wait;
                yield return new WaitForSeconds(wait);
            }
            Dark();
        }

        private void Dark()
        {
            if (IsDark) return;
            IsDark = true;
            TotalDark++;
            if (lamp != null) lamp.enabled = false;
            if (bounce != null) bounce.enabled = false;
            if (panel != null && panelOff != null) panel.sharedMaterial = panelOff;
            RunTelemetry.Event("light_dark", Id);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCount() => TotalDark = 0;
    }
}
