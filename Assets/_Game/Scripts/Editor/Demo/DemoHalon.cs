#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Rendering;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The halon room (DEMO_PLAN §10.4, CANON round 39: the fire, the gas, the pass-out; the rest PROPOSED; Bruno,
    /// 2026-11-10: "create the halon room now"). G17, the server room, and G17b, the facilities room beside it with the
    /// lockout cabinet, off the kitchen G19 (the places PROPOSED: the plan places G17 "when it is built"). The strapped
    /// wall of archive boxes hides the reserve cage (CANON round 41). The run of it is HalonSystem. L15 (G17b), L16 (G12,
    /// at the place marked COLTON), L17 (by the panel), L18 (in the cage).
    /// </summary>
    public static class DemoHalon
    {
        public const string LockoutFlag = "halon_lockout";
        public const string BurnedFlag = "demo_g17_burned";

        public static List<RoomSpec> Rooms() => new List<RoomSpec>
        {
            Spec("G17b", "FACILITIES", 38, -16, 44, -12, 3f, G17b, new Door('N', 43, "G19"), HalonDoor("G17")),
            Spec("G17", "SERVER ROOM", 38, -24, 46, -16, 3f, G17, HalonDoor("G17b")),
        };

        /// <summary>The door of the server room: a steel fire door with no lever (the horn seals it, HalonSystem opens it), and no kick breaks it.</summary>
        public static Door HalonDoor(string to) => new Door(to == "G17" ? 'S' : 'N', 41, to, fire: true, leverRoom: "none", unbreakable: true);

        private static RoomSpec Spec(string id, string title, float x0, float z0, float x1, float z1, float height, System.Action<DemoRoom> dress, params Door[] doors)
        {
            var spec = new RoomSpec { Id = id, Title = title, Floor = DemoItems.Floor1, Music = DemoPublic.Music, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Height = height, Dress = dress };
            spec.Doors.AddRange(doors);
            return spec;
        }

        // ── G17b, facilities ─────────────────────────────────────────

        private static void G17b(DemoRoom r)
        {
            r.Prop("Desk", new Vector3(39.4f, 0f, -13f), new Vector3(1.2f, 0.75f, 0.6f), 0.25f);
            r.Item("L15_Facilities_Memo", L15(), new Vector3(39.4f, 0.76f, -13f), new Vector3(0.22f, 0.02f, 0.3f));
            r.Prop("Filing_Cabinet", new Vector3(38.5f, 0f, -14.9f), new Vector3(0.6f, 1.3f, 0.5f), 0.4f);
            // The lockout cabinet by the door of the server room (§7): turned before the fire, the horn stays quiet.
            r.Prop("Lockout_Cabinet", new Vector3(42.6f, 0f, -15.6f), new Vector3(0.6f, 1.6f, 0.4f), 0.55f);
            r.Sign("Sign_Lockout", "HALON LOCKOUT. SERVICE ONLY. TURN BEFORE ANY HOT WORK IN THIS ROOM.", new Vector3(42.6f, 1.45f, -15.39f), Vector3.forward, new Vector2(0.55f, 0.3f), 14);
            r.Switch("Halon_Lockout", new Vector3(42.6f, 1.05f, -15.36f), LockoutFlag, "Turn the halon lockout?", "turn the halon lockout",
                "The key turns. A lamp on the cabinet goes dark.");
            // The words on the door of the server room (§7).
            r.Sign("Sign_Halon_Danger", "DANGER. HALON 1301 IN THIS ROOM. ON THE HORN THE DOOR SEALS. 30 SECONDS, THEN DISCHARGE. PRESS ABORT ON THE PANEL. THE AGENT CAN MAKE YOU LOSE CONSCIOUSNESS.",
                new Vector3(41f, 2.65f, -15.83f), Vector3.forward, new Vector2(1.9f, 0.5f), 14);
            r.Sign("Sign_Halon_Hand", "Since the partition: do NOT wait to see if it's a false alarm.", new Vector3(39.8f, 1.5f, -15.83f), Vector3.forward, new Vector2(0.6f, 0.3f), 14);
        }

        // ── G17, the server room ─────────────────────────────────────

        private static void G17(DemoRoom r)
        {
            // The kit nobody paid to replace: the racks, the red cylinders of the halon on the west wall.
            for (int i = 0; i < 4; i++)
                r.Prop($"Server_Rack_{(char)('A' + i)}", new Vector3(45.4f, 0f, -17.2f - i * 1.4f), new Vector3(0.8f, 2f, 1.2f), 0.12f);
            Material red = DemoRoom.Colour("halon_red", new Color(0.62f, 0.08f, 0.07f));
            for (int i = 0; i < 4; i++)
            {
                GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cylinder.name = "Halon_Cylinder_" + i;
                cylinder.transform.SetParent(r.Props.transform, false);
                cylinder.transform.position = new Vector3(38.45f, 0.7f, -16.9f - i * 0.5f);
                cylinder.transform.localScale = new Vector3(0.35f, 0.7f, 0.35f);
                cylinder.GetComponent<Renderer>().sharedMaterial = red;
            }

            // The panel inside by the door: ABORT, its display, its words, and the health note (L17).
            GameObject panelBox = r.Box(r.Props, "Halon_Panel", new Vector3(42.5f, 1.35f, -16.2f), new Vector3(0.45f, 0.55f, 0.08f), DemoRoom.Grey(0.75f));
            GameObject display = r.Sign("Halon_Display", "ARMED", new Vector3(42.5f, 1.52f, -16.25f), Vector3.back, new Vector2(0.38f, 0.1f), 22);
            r.Sign("Sign_Halon_Abort", "HALON ABORT. PRESS TO STOP DISCHARGE.", new Vector3(42.5f, 1.9f, -16.18f), Vector3.back, new Vector2(0.7f, 0.2f), 16);
            GameObject button = r.Box(r.Props, "Halon_Abort", new Vector3(42.5f, 1.25f, -16.27f), new Vector3(0.16f, 0.16f, 0.05f), DemoRoom.Colour("abort_red", new Color(0.9f, 0.1f, 0.08f)));
            r.Item("L17_Health_Note", L17(), new Vector3(43.2f, 1.3f, -16.16f), new Vector3(0.21f, 0.28f, 0.02f));

            // The strapped wall of archive boxes (CANON round 41), heavy: no lift. Its fire, its ash.
            Vector3 wallAt = new Vector3(42f, 0f, -22.3f);
            GameObject boxes = MenuLoopSetup.EnsureChild(r.Props, "Archive_Boxes");
            GameObject block = r.Box(boxes, "Box_Wall", wallAt + Vector3.up * 1.1f, new Vector3(3.6f, 2.2f, 0.9f), DemoRoom.Colour("archive_box", new Color(0.55f, 0.43f, 0.3f)));
            foreach (float y in new[] { 0.7f, 1.6f })
            {
                GameObject strap = r.Box(boxes, $"Strap_{y:0.0}", wallAt + new Vector3(0f, y, 0.47f), new Vector3(3.62f, 0.06f, 0.02f), DemoRoom.Grey(0.1f));
                Object.DestroyImmediate(strap.GetComponent<Collider>());
            }
            for (int i = 0; i < 4; i++)
            {
                GameObject seam = r.Box(boxes, "Box_Seam_" + i, wallAt + new Vector3(-1.35f + i * 0.9f, 1.1f, 0.46f), new Vector3(0.015f, 2.2f, 0.01f), DemoRoom.Colour("archive_seam", new Color(0.35f, 0.27f, 0.18f)));
                Object.DestroyImmediate(seam.GetComponent<Collider>());
            }
            block.isStatic = false;
            LockOnTargetable target = MenuLoopSetup.EnsureComponent<LockOnTargetable>(block);
            var so = new SerializedObject(target);
            so.FindProperty("fallbackHeight").floatValue = 0f; // the lock point is the middle of the wall
            so.FindProperty("isProp").boolValue = false;
            so.FindProperty("heavy").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.AI.NavMeshObstacle carve = MenuLoopSetup.EnsureComponent<UnityEngine.AI.NavMeshObstacle>(block);
            carve.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            carve.size = Vector3.one;
            carve.carving = true; // it goes, and the way to the cage opens

            GameObject flames = MenuLoopSetup.EnsureChild(r.Props, "Box_Flames");
            var tongues = new List<Transform>();
            for (int i = 0; i < 7; i++)
            {
                Color c = i % 2 == 0 ? new Color(1f, 0.45f, 0.08f) : new Color(1f, 0.8f, 0.2f);
                GameObject tongue = r.Box(flames, "Flame_" + i, wallAt + new Vector3(-1.5f + i * 0.5f, 1.2f, 0.5f), new Vector3(0.35f, 1.8f, 0.05f), DemoRoom.Colour("flame_" + (i % 2), c));
                Object.DestroyImmediate(tongue.GetComponent<Collider>());
                tongue.isStatic = false;
                tongues.Add(tongue.transform);
            }
            var glowObj = new GameObject("Flame_Light");
            glowObj.transform.SetParent(flames.transform, false);
            glowObj.transform.position = wallAt + new Vector3(0f, 1.4f, 1.4f);
            Light glow = glowObj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 7f;
            glow.color = new Color(1f, 0.55f, 0.2f);
            glow.shadows = LightShadows.None;
            Flicker flicker = MenuLoopSetup.EnsureComponent<Flicker>(flames);
            flicker.tongues = tongues.ToArray();
            flicker.glow = glow;
            EditorUtility.SetDirty(flicker);
            GameObject ash = MenuLoopSetup.EnsureChild(r.Props, "Box_Ash");
            GameObject ashPile = r.Box(ash, "Ash", wallAt + Vector3.up * 0.08f, new Vector3(3.4f, 0.16f, 0.9f), DemoRoom.Grey(0.06f));
            Object.DestroyImmediate(ashPile.GetComponent<Collider>());

            // The reserve cage behind it (CANON round 41): one capsule for each of her three powers and one second wind pill (§10.4;
            // Bruno, 2026-11-10: "one of each").
            GameObject cageFrame = MenuLoopSetup.EnsureChild(r.Props, "Reserve_Cage");
            Vector3 cageAt = new Vector3(42f, 0f, -23.35f);
            foreach (float dx in new[] { -1.5f, 1.5f })
                r.Box(cageFrame, $"Cage_Post_{(dx < 0 ? "W" : "E")}", cageAt + new Vector3(dx, 1f, 0.5f), new Vector3(0.06f, 2f, 0.06f), DemoRoom.Grey(0.45f));
            r.Box(cageFrame, "Cage_Shelf", cageAt + new Vector3(0f, 0.85f, 0f), new Vector3(2.9f, 0.05f, 0.9f), DemoRoom.Grey(0.45f));
            GameObject contents = MenuLoopSetup.EnsureChild(r.Props, "Cage_Contents");
            var stock = new (string name, DrugType type, float x)[]
            {
                ("Cage_Warspite", DrugType.Nalcon, -1.0f),
                ("Cage_Erebus", DrugType.NalconFire, -0.45f),
                ("Cage_Orzabal", DrugType.NalconLift, 0.1f),
                ("Cage_Second_Wind", DrugType.DFelon, 0.65f),
            };
            foreach ((string name, DrugType type, float x) in stock)
                r.Capsule(name, type, cageAt + new Vector3(x, 1.0f, 0f)).transform.SetParent(contents.transform, true);
            // The empty tray with the violet label: the capsule in the glove box, signed out by nobody.
            r.Box(contents, "Tray_Empty_Violet", cageAt + new Vector3(1.25f, 0.9f, 0f), new Vector3(0.3f, 0.04f, 0.2f), DemoRoom.Colour("violet_label", new Color(0.5f, 0.25f, 0.7f)));
            r.Item("L18_Stock_Ledger", L18(), cageAt + new Vector3(0f, 1.5f, 0.45f), new Vector3(0.21f, 0.28f, 0.02f)).transform.SetParent(contents.transform, true);

            // The system: the fire, the horn, the seal, the countdown, the halon.
            GameObject holder = MenuLoopSetup.EnsureChild(r.Story, "Halon_System");
            holder.transform.position = new Vector3(42f, 2.5f, -20f);
            HalonSystem system = MenuLoopSetup.EnsureComponent<HalonSystem>(holder);
            system.roomId = "G17";
            system.wall = target;
            system.boxes = boxes;
            system.flames = flames;
            system.ash = ash;
            system.cage = contents;
            system.panel = display.GetComponentInChildren<UnityEngine.UI.Text>(true);
            system.lockoutFlag = LockoutFlag;
            system.burnedFlag = BurnedFlag;
            foreach (FireDoor door in Object.FindObjectsByType<FireDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (door.name == "FireDoor_G17b_G17" || door.name == "FireDoor_G17_G17b") system.door = door;
            if (system.door == null) Debug.LogError("[DemoHalon] no door of the server room");
            EditorUtility.SetDirty(system);
            HalonAbort abort = MenuLoopSetup.EnsureComponent<HalonAbort>(button);
            abort.system = system;
            abort.range = 1.4f;
            EditorUtility.SetDirty(abort);
        }

        /// <summary>L16 in G12: the unopened folder at the place marked COLTON, by the speakerphone.</summary>
        public static void ColtonFolder(DemoRoom g12) =>
            g12.Item("L16_Counsel_Note", L16(), new Vector3(30.3f, 0.76f, -18.8f), new Vector3(0.22f, 0.03f, 0.3f));

        // ── The logs (§8, L15 to L18) ────────────────────────────────

        public static ItemDefinition L15() => DemoItems.Log("l15_facilities_memo", "A FACILITIES MEMO",
            "FACILITIES. SERVER ROOM, HALON 1301. NEW PRODUCTION ENDED IN 1994 (MONTREAL PROTOCOL). WE ARE NOT REQUIRED TO REMOVE THE SYSTEM. RECYCLED AGENT ONLY. NO TEST DISCHARGE. SWITCHOVER QUOTE: NEW PIPE, NEW NOZZLES, MORE CYLINDERS THAN WE HAVE FLOOR FOR, NINE DAYS WITH BUILDING SYSTEMS DOWN. BUILDING SYSTEMS CANNOT GO DOWN. DEFERRED. FOURTH YEAR.");

        public static ItemDefinition L16() => DemoItems.Log("l16_counsel_note", "A NOTE FROM COUNSEL",
            "OFFICE OF COUNSEL. PRIVILEGED. You asked if the halon exposes us. Having it does not. 1994 stopped the making of it, not the keeping of it, and recycled agent is lawful with the paperwork. What exposes us is a person hurt in a room we knew was wrong, or a release we could have prevented. Please do not use the word \"knew\" in your reply.");

        public static ItemDefinition L17() => DemoItems.Log("l17_health_note", "AN OCCUPATIONAL HEALTH NOTE",
            "OCCUPATIONAL HEALTH, TO FACILITIES. Below 10 percent, halon 1301 feels like being drunk. At 15 to 20 percent, people lose consciousness. In a fire it also breaks down into hydrogen fluoride and hydrogen bromide. This room was designed for about 6 percent. The partition took two thirds of the room, and nobody changed the cylinders. My number is now 18. The door seals on the horn to hold the agent in. ABORT is the only way out. You will not know you are going under.");

        public static ItemDefinition L18() => DemoItems.Log("l18_stock_ledger", "THE STOCK LEDGER",
            "RESERVE CAGE. COUNT.\nWARSPITE 2. EREBUS 2. ORZABAL 2.\nEDGAR 1 (TRAY EMPTY. NO SIGNATURE).\nSECOND WIND 1.\nNOTE: V3 TINCTURE, DOSE CHANGE DAY 0, AUTH. DR. COLTON. RECOUNT DUE.");
    }
}
#endif
