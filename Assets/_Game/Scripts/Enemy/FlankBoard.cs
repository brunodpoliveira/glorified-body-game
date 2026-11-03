using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The sides of the fight around her (2026-11-03, DEMO_PLAN 13.1). Bruno: "verify if the soldiers prefer attacking her
    /// from the flanks or behind (that'll make them look smarter by trying to flank/pinch her)"; "guards would try to face
    /// her head-on, soldiers and power armor flank her". The first flanker of a fight fixes the axis: the line from her to
    /// him (the front of the fight). Each flanker then claims a free side of that axis: a flank (the one nearest his own
    /// bearing), then the other flank, her back, the back corners, the front corners. Two soldiers so take opposite sides
    /// (the pinch), and a third goes behind her. A claim goes at a
    /// death or a retreat; with no claim left the axis goes too. The same board as the cover claims, for the sides.
    /// </summary>
    public static class FlankBoard
    {
        /// <summary>The sides in degrees from the axis (+ to her left as seen from the front), in their order of preference.</summary>
        public static readonly float[] Slots = { 90f, -90f, 180f, 135f, -135f, 45f, -45f };
        /// <summary>The order of the claims: the flanks, her back, the back corners, the front corners (indexes into Slots).</summary>
        private static readonly int[][] Groups = { new[] { 0, 1 }, new[] { 2 }, new[] { 3, 4 }, new[] { 5, 6 } };

        private static Vector3? axis;
        private static readonly Dictionary<SoldierBrain, int> claims = new Dictionary<SoldierBrain, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            claims.Clear();
            axis = null;
        }

        public static int Count
        {
            get
            {
                Prune();
                return claims.Count;
            }
        }

        /// <summary>The axis of the fight (flat, from her to its first flanker), or null with no fight.</summary>
        public static Vector3? Axis => axis;

        /// <summary>The side of a flanker in degrees from the axis, or null when it has none.</summary>
        public static float? SlotOf(SoldierBrain who) => who != null && claims.TryGetValue(who, out int i) ? Slots[i] : (float?)null;

        /// <summary>The flat direction from her to the side of the flanker (it claims one now if it has none).</summary>
        public static Vector3 Claim(SoldierBrain who, Vector3 her)
        {
            Prune();
            Vector3 toHim = who.transform.position - her;
            toHim.y = 0f;
            if (toHim.sqrMagnitude < 0.01f) toHim = -who.transform.forward;
            toHim.Normalize();
            if (axis == null || claims.Count == 0) axis = toHim;
            if (!claims.TryGetValue(who, out int slot))
            {
                // The sides go in groups: the two flanks first (so the second flanker takes the side opposite the first:
                // the pinch), then her back, then the back corners, then the front corners. In a group, the side nearest
                // his bearing. (2026-11-03: a nearest-side rule gave three flankers in front of her 45, 90 and -45.)
                float bearing = Vector3.SignedAngle(axis.Value, toHim, Vector3.up);
                slot = -1;
                foreach (int[] group in Groups)
                {
                    float best = float.MaxValue;
                    foreach (int i in group)
                    {
                        if (claims.ContainsValue(i)) continue;
                        float cost = Mathf.Abs(Mathf.DeltaAngle(bearing, Slots[i]));
                        if (cost < best) { best = cost; slot = i; }
                    }
                    if (slot >= 0) break;
                }
                if (slot < 0) slot = 0; // more flankers than sides: they share the first
                claims[who] = slot;
                GlorifiedBody.DebugTools.RunTelemetry.Event("flank_claim", $"{who.name}:{Slots[slot]:0}");
            }
            return Quaternion.Euler(0f, Slots[slot], 0f) * axis.Value;
        }

        public static void Release(SoldierBrain who)
        {
            if (who == null) return;
            if (claims.Remove(who) && claims.Count == 0) axis = null;
        }

        /// <summary>The dead, the frightened and the gone leave the board.</summary>
        private static void Prune()
        {
            var gone = new List<SoldierBrain>();
            foreach (SoldierBrain b in claims.Keys)
                if (b == null || b.Current == SoldierBrain.Phase.Dead || b.Retreating) gone.Add(b);
            foreach (SoldierBrain b in gone) claims.Remove(b);
            if (claims.Count == 0) axis = null;
        }
    }
}
