using GlorifiedBody.Combat;

namespace GlorifiedBody.Gore.ThatIndividual
{
    /// <summary>Which gore backends run. SkinGore paints skin wounds; ThatIndividual sprays blood and stains surfaces.</summary>
    public enum GoreBackend
    {
        SkinGore,
        ThatIndividual,
        Both
    }

    /// <summary>How hard a hit reads. Picks the effect set.</summary>
    public enum GoreHitWeight
    {
        None,
        Light,
        Heavy,
        Fatal,
        FatalHeavy
    }

    /// <summary>
    /// Pure hit classification for the ThatIndividual driver. No scene access, so EditMode tests can call it.
    /// </summary>
    public static class ThatIndividualGoreRules
    {
        /// <summary>High-tier attacks: L3 punch/finisher/lunge, divekick, throw, slam, blasts. Debug is the bot's kill hit.</summary>
        public static bool IsHeavyType(DamageType type)
        {
            switch (type)
            {
                case DamageType.TimeDilationMelee:
                case DamageType.Divekick:
                case DamageType.Throw:
                case DamageType.Slam:
                case DamageType.SonicBoom:
                case DamageType.Explosion:
                case DamageType.Grenade:
                case DamageType.EnemyRocket:
                case DamageType.PsychicTelekinesis: // a slam, a throw, a returned round (THE GLORIFIED BODY)
                case DamageType.FallingHazard:
                case DamageType.PropImpact:
                case DamageType.Debug:
                    return true;
                default:
                    return false;
            }
        }

        public static GoreHitWeight Classify(DamageType type, float damage, bool fatal, float heavyDamageThreshold)
        {
            if (type == DamageType.Unknown)
            {
                return GoreHitWeight.None;
            }

            // Audio Pulse is a shove. It only bleeds when it kills.
            if (type == DamageType.AudioPulse && !fatal)
            {
                return GoreHitWeight.None;
            }

            // THE GLORIFIED BODY: a burn does not bleed (its ticks would spray every half second); a death by fire
            // is a plain death. The gas does not bleed. A death by the short is the gruesome one: the full burst.
            if (type == DamageType.PsychicFire || type == DamageType.EnemyGas)
            {
                return fatal ? GoreHitWeight.Fatal : GoreHitWeight.None;
            }
            if (type == DamageType.ShortShock && fatal)
            {
                return GoreHitWeight.FatalHeavy;
            }

            bool heavy = IsHeavyType(type) || damage >= heavyDamageThreshold;
            if (fatal)
            {
                return heavy ? GoreHitWeight.FatalHeavy : GoreHitWeight.Fatal;
            }

            return heavy ? GoreHitWeight.Heavy : GoreHitWeight.Light;
        }

        /// <summary>Low tier drops one step: no fatal-heavy extras, heavy reads as light.</summary>
        public static GoreHitWeight ApplyTier(GoreHitWeight weight, GoreTier tier)
        {
            if (tier == GoreTier.Off)
            {
                return GoreHitWeight.None;
            }

            if (tier == GoreTier.Low)
            {
                switch (weight)
                {
                    case GoreHitWeight.FatalHeavy: return GoreHitWeight.Fatal;
                    case GoreHitWeight.Heavy: return GoreHitWeight.Light;
                }
            }

            return weight;
        }

        /// <summary>
        /// Rotation for a vendor stain quad on a surface. After the vendor's 90 degree X start rotation, the lit
        /// side of the quad faces local -Y, so -Y goes along the surface normal. With +Y up the stain renders black.
        /// </summary>
        public static UnityEngine.Quaternion StainRotation(UnityEngine.Vector3 surfaceNormal, float spinDegrees)
        {
            return UnityEngine.Quaternion.FromToRotation(UnityEngine.Vector3.down, surfaceNormal) *
                   UnityEngine.Quaternion.Euler(0f, spinDegrees, 0f);
        }

        /// <summary>
        /// Takes the next slot of a fixed ring of <paramref name="cap"/> live slots. When the ring is full, the
        /// slot it returns holds the oldest entry, and the caller releases that entry first. A head left past a
        /// shrunk cap (Low tier) wraps back into range.
        /// </summary>
        public static int TakeSlot(ref int head, int cap)
        {
            if (cap < 1)
            {
                cap = 1;
            }

            int index = ((head % cap) + cap) % cap;
            head = (index + 1) % cap;
            return index;
        }

        /// <summary>Index the next <see cref="TakeSlot"/> returns, without moving the head.</summary>
        public static int PeekSlot(int head, int cap)
        {
            if (cap < 1)
            {
                cap = 1;
            }

            return ((head % cap) + cap) % cap;
        }

        /// <summary>Live-slot cap for a ring of <paramref name="length"/> slots. Low tier keeps a share, never less than 1.</summary>
        public static int CapFor(int length, GoreTier tier, float lowTierBudget)
        {
            if (length < 1)
            {
                return 1;
            }

            float budget = tier == GoreTier.Low ? lowTierBudget : 1f;
            int cap = (int)System.Math.Ceiling(length * budget);
            return cap < 1 ? 1 : cap > length ? length : cap;
        }

        public static bool RunsThatIndividual(GoreBackend backend) => backend != GoreBackend.SkinGore;
        public static bool RunsSkinGore(GoreBackend backend) => backend != GoreBackend.ThatIndividual;
    }
}
