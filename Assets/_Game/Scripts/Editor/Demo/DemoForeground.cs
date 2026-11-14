#if UNITY_EDITOR
using UnityEngine;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The odd cameras (rounds 147 and 148, DEMO_PLAN §10.5b item 4; Bruno, 2026-11-14: "some rooms with this type of
    /// foreground would also be nice", his pictures of a candelabrum, a desk lamp and a piano in front of a fixed camera).
    /// Each room here gets one more camera (DemoRoom.ExtraCamera) with a thing in its foreground, and a zone that the camera
    /// frames whole. The rest of the room keeps the cameras of its cells. Plan metres, as the room builders. PROPOSED poses.
    /// </summary>
    public static class DemoForeground
    {
        private static GameObject Thing(DemoRoom r, string name, Vector3 centre, Vector3 size, Material m, bool solid)
        {
            GameObject g = r.Box(r.Props, name, centre, size, m);
            if (!solid) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        private static Material Brass => DemoRoom.Colour("brass", new Color(0.72f, 0.6f, 0.32f));
        private static Material Flame => DemoRoom.Colour("candle_flame", new Color(1f, 0.85f, 0.45f));
        private static Material Wax => DemoRoom.Colour("candle_wax", new Color(0.95f, 0.93f, 0.86f));

        /// <summary>A tall candle stand on the floor: a post, a cross bar, five candles with flames.</summary>
        private static void Candelabrum(DemoRoom r, string name, Vector3 foot, float height)
        {
            Thing(r, name + "_Foot", foot + Vector3.up * 0.04f, new Vector3(0.36f, 0.08f, 0.36f), Brass, true);
            Thing(r, name + "_Post", foot + Vector3.up * height * 0.5f, new Vector3(0.05f, height, 0.05f), Brass, true);
            Thing(r, name + "_Bar", foot + Vector3.up * (height - 0.05f), new Vector3(0.62f, 0.04f, 0.04f), Brass, false);
            for (int i = -2; i <= 2; i++)
            {
                Vector3 at = foot + new Vector3(i * 0.15f, height + 0.1f - Mathf.Abs(i) * 0.03f, 0f);
                Thing(r, $"{name}_Candle_{i + 2}", at, new Vector3(0.04f, 0.18f, 0.04f), Wax, false);
                Thing(r, $"{name}_Flame_{i + 2}", at + Vector3.up * 0.12f, new Vector3(0.025f, 0.05f, 0.025f), Flame, false);
            }
        }

        /// <summary>W14, the children's day room: low on the floor by the south wall, looking up, the toy blocks in front.</summary>
        public static void W14(DemoRoom r) =>
            r.ExtraCamera("low", new Vector3(-4.6f, 0.3f, 9.4f), new Vector3(-2.0f, 2.4f, 13.9f), 64f, new Vector3(-3f, 1.5f, 12.5f), new Vector3(4f, 3f, 2.6f));

        /// <summary>G3, the donor wall: level and straight on, the wall alone in the frame, the bench in front.</summary>
        public static void G3(DemoRoom r) =>
            r.ExtraCamera("wall", new Vector3(12f, 1.65f, 6.45f), new Vector3(12f, 1.7f, 11.83f), 50f, new Vector3(12f, 1.5f, 10.9f), new Vector3(5f, 3f, 1.6f));

        /// <summary>W9, the nurses' station: from behind the counter, the desk lamp and the phone in front, Marta's corner beyond.</summary>
        public static void W9(DemoRoom r)
        {
            Material shade = DemoRoom.Colour("lamp_shade_red", new Color(0.55f, 0.18f, 0.12f)), black = DemoRoom.Grey(0.08f);
            Thing(r, "Desk_Lamp_Base", new Vector3(12.2f, 1.07f, -5.45f), new Vector3(0.16f, 0.03f, 0.16f), black, false);
            GameObject arm = Thing(r, "Desk_Lamp_Arm", new Vector3(12.12f, 1.3f, -5.5f), new Vector3(0.025f, 0.45f, 0.025f), black, false);
            arm.transform.rotation = Quaternion.Euler(0f, 0f, 18f);
            GameObject head = Thing(r, "Desk_Lamp_Shade", new Vector3(11.98f, 1.5f, -5.55f), new Vector3(0.22f, 0.12f, 0.16f), shade, false);
            head.transform.rotation = Quaternion.Euler(0f, 0f, -30f);
            Thing(r, "Desk_Phone", new Vector3(12.4f, 1.1f, -6.2f), new Vector3(0.22f, 0.08f, 0.18f), black, false);
            r.ExtraCamera("lamp", new Vector3(13.3f, 1.5f, -4.7f), new Vector3(6f, 0.6f, -8.5f), 60f, new Vector3(6.5f, 1.5f, -8.05f), new Vector3(5f, 3f, 3.1f));
        }

        /// <summary>G4, the chapel: from the south-west corner, a tall candle stand in front, the pews and the lectern beyond.</summary>
        public static void G4(DemoRoom r)
        {
            Candelabrum(r, "Chapel_Candles", new Vector3(25.4f, 0f, 7.3f), 1.45f);
            r.ExtraCamera("candles", new Vector3(24.6f, 2.0f, 6.4f), new Vector3(29f, 1.0f, 12.5f), 64f, new Vector3(28.75f, 1.5f, 10.5f), new Vector3(4.5f, 3f, 4f));
        }

        /// <summary>G10, the cafeteria: low by the side counter, Gene's birthday cake on the first table, the banner beyond.</summary>
        public static void G10(DemoRoom r)
        {
            Material cream = DemoRoom.Colour("cake_cream", new Color(0.95f, 0.9f, 0.82f)), pink = DemoRoom.Colour("cake_pink", new Color(0.88f, 0.55f, 0.62f));
            Thing(r, "Cake", new Vector3(31.1f, 0.83f, -2.75f), new Vector3(0.36f, 0.16f, 0.36f), cream, false);
            Thing(r, "Cake_Top", new Vector3(31.1f, 0.92f, -2.75f), new Vector3(0.3f, 0.02f, 0.3f), pink, false);
            for (int i = 0; i < 3; i++)
            {
                Thing(r, $"Cake_Candle_{i}", new Vector3(31.0f + i * 0.1f, 0.98f, -2.75f), new Vector3(0.015f, 0.09f, 0.015f), Wax, false);
                Thing(r, $"Cake_Flame_{i}", new Vector3(31.0f + i * 0.1f, 1.04f, -2.75f), new Vector3(0.012f, 0.03f, 0.012f), Flame, false);
            }
            r.ExtraCamera("cake", new Vector3(29.0f, 1.1f, -3.9f), new Vector3(35f, 1.8f, -0.2f), 62f, new Vector3(34.5f, 1.5f, -2.5f), new Vector3(5f, 3f, 4f));
        }

        /// <summary>G18, the Dining Hall: by the north-west corner, a tall candelabrum in front, the floor between the columns beyond.</summary>
        public static void G18(DemoRoom r)
        {
            Candelabrum(r, "Hall_Candelabrum", new Vector3(36.3f, 0f, 8.6f), 1.7f);
            r.ExtraCamera("candelabrum", new Vector3(35.6f, 2.2f, 9.6f), new Vector3(43f, 0.8f, 3f), 64f, new Vector3(41.75f, 1.5f, 5.2f), new Vector3(2.5f, 3f, 2f));
        }
    }
}
#endif
