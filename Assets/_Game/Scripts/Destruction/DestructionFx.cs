using GlorifiedBody.Combat;
using GlorifiedBody.Enemy;
using UnityEngine;

namespace GlorifiedBody.Destruction
{
    /// <summary>
    /// The break effects: a dust or glint burst with chips, and a sound at the point. Particle systems made in
    /// code (no vendor prefab), destroyed after they end. Placeholder sounds (tools/destruction_sfx.py).
    /// </summary>
    public static class DestructionFx
    {
        private const string SettingsPath = "Destruction/destruction_fx";
        private static DestructionFxSettings settings;
        private static bool loaded;

        public static int Bursts { get; private set; }

        private static DestructionFxSettings Settings
        {
            get
            {
                if (!loaded)
                {
                    settings = Resources.Load<DestructionFxSettings>(SettingsPath);
                    loaded = true;
                }
                return settings;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            settings = null;
            loaded = false;
            Bursts = 0;
        }

        public static void Break(Breakable.Kind kind, Vector3 at, float size)
        {
            DestructionFxSettings s = Settings;
            Play(s != null ? Clip(s, kind) : null, at, 1f);
            float scale = Mathf.Clamp(size, 0.4f, 2.5f);
            switch (kind)
            {
                case Breakable.Kind.Glass:
                    Burst(at, Glint, 26, 0.04f * scale, 3.5f, 0.9f, true);
                    break;
                case Breakable.Kind.Metal:
                    Burst(at, Spark, 22, 0.03f, 5f, 0.5f, true);
                    Burst(at, Grey, 10, 0.25f * scale, 0.8f, 1.2f, false);
                    break;
                case Breakable.Kind.Plaster:
                    Burst(at, PlasterDust, 18, 0.45f * scale, 0.9f, 1.8f, false);
                    break;
                default:
                    Burst(at, WoodDust, 14, 0.35f * scale, 1f, 1.4f, false);
                    break;
            }
        }

        /// <summary>A hit that does not break it: a small puff and the hit sound of its surface (the old knock as a fallback).</summary>
        public static void Chip(Breakable.Kind kind, Vector3 at, Vector3 direction)
        {
            DestructionFxSettings s = Settings;
            if (!ShotEffects.SurfaceSound(Surfaces.Of(kind), at)) Play(s != null ? s.chip : null, at, 0.6f);
            Color colour = kind == Breakable.Kind.Plaster ? PlasterDust : kind == Breakable.Kind.Metal ? Grey : WoodDust;
            Burst(at - direction * 0.05f, colour, 6, 0.15f, 0.8f, 0.6f, false);
        }

        public static void Sparks(Vector3 at)
        {
            DestructionFxSettings s = Settings;
            Play(s != null ? s.sparks : null, at, 1f);
            Burst(at, Spark, 30, 0.035f, 4f, 0.7f, true);
        }

        private static readonly Color WoodDust = new Color(0.45f, 0.38f, 0.3f, 0.7f);
        private static readonly Color PlasterDust = new Color(0.78f, 0.76f, 0.72f, 0.75f);
        private static readonly Color Grey = new Color(0.4f, 0.4f, 0.42f, 0.6f);
        private static readonly Color Glint = new Color(0.8f, 0.92f, 1f, 1f);
        private static readonly Color Spark = new Color(1f, 0.75f, 0.35f, 1f);

        private static AudioClip Clip(DestructionFxSettings s, Breakable.Kind kind)
        {
            switch (kind)
            {
                case Breakable.Kind.Metal: return s.metalBreak;
                case Breakable.Kind.Glass: return s.glassBreak;
                case Breakable.Kind.Plaster: return s.plasterBreak;
                case Breakable.Kind.Plastic: return s.plasticBreak;
                default: return s.woodBreak;
            }
        }

        private static void Play(AudioClip clip, Vector3 at, float volume)
        {
            if (clip == null) return;
            DestructionFxSettings s = Settings;
            AudioSource.PlayClipAtPoint(clip, at, volume * (s != null ? s.volume : 1f));
        }

        private static void Burst(Vector3 at, Color colour, int count, float size, float speed, float life, bool glow)
        {
            DestructionFxSettings s = Settings;
            Material material = s != null ? (glow ? s.glow : s.dust) : null;
            if (material == null) return;
            Bursts++;
            var obj = new GameObject(glow ? "BreakGlints" : "BreakDust");
            obj.transform.position = at;
            var ps = obj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = colour;
            main.gravityModifier = glow ? 1f : -0.02f;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = glow ? 0.05f : 0.2f;
            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(glow ? 0.8f : 0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (glow)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.04f;
                renderer.lengthScale = 1f;
            }
            ps.Play();
            Object.Destroy(obj, life + 0.5f);
        }
    }
}
