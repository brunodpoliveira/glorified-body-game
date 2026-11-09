#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Boss;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Rendering;
using GlorifiedBody.Rooms;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The BRAIN TEST room (2026-11-08): the brain fight of DEMO_PLAN §14.1 in isolation, before B1 enters the demo. Three
    /// rooms built like the demo:
    ///   B0, the unfinished wing, where she starts;
    ///   B1, the brain sector, 18 x 12 m and 6 m high, pink-grey (the greybox stand-in of §12): the brain itself, one mass at its eye on the south wall
    ///   (Bruno, 2026-11-08: "it also needs hp for the boss", "it should by default lock on onto the brain"),
    ///   six work lights, six grabber mouths, and the brain's eye, a camera high on the south wall that follows her;
    ///   B2, the builders' door, behind a leaf that goes when the brain dies.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.Demo.BrainTestSetup.BuildBrainTest
    /// </summary>
    public static class BrainTestSetup
    {
        public const string SceneName = "BrainTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string NavPath = MenuLoopSetup.ScenesFolder + "/" + SceneName + "_NavMesh.asset";
        public const string Floor = "brain_test";
        public const string Wing = "B0", Sector = "B1", Exit = "B2";
        public const string DeadFlag = "brain_test_dead";
        public const float BrainHealth = 600f; // PROPOSED
        public static readonly Vector3 LucyStart = new Vector3(9f, 0f, 1.5f);

        public static List<RoomSpec> Rooms() => new List<RoomSpec>
        {
            Spec(Wing, "THE UNFINISHED WING", 6, 0, 12, 6, 3.2f, DressWing, new Door('N', 9, Sector)),
            Spec(Sector, "THE BRAIN SECTOR", 0, 6, 18, 18, 6f, r => DressSector(r, Exit), new Door('S', 9, Wing), new Door('N', 9, Exit)),
            Spec(Exit, "THE BUILDERS' DOOR", 7, 18, 11, 21, 3.2f, null, new Door('S', 9, Sector)),
        };

        private static RoomSpec Spec(string id, string title, float x0, float z0, float x1, float z1, float height, System.Action<DemoRoom> dress, params Door[] doors)
        {
            var spec = new RoomSpec { Id = id, Title = title, Floor = Floor, Music = "brain_test", X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Height = height, Dress = dress };
            spec.Doors.AddRange(doors);
            return spec;
        }

        [MenuItem("GlorifiedBody/Build Brain Test Room (the brain fight)")]
        public static void BuildBrainTest()
        {
            Directory.CreateDirectory(MenuLoopSetup.ScenesFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.3f, 0.32f);
            var sun = new GameObject("Light");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.8f;
            light.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(60f, 20f, 0f);
            var world = new GameObject("[World]");
            world.AddComponent<RoomTracker>();
            DemoRoom.Lucy = MenuLoopSetup.EnsureLucy(catalog, LucyStart, 0f);

            List<RoomSpec> specs = Rooms();
            var ledger = new WallLedger(specs);
            var rooms = new Dictionary<string, DemoRoom>();
            foreach (RoomSpec spec in specs) rooms[spec.Id] = DemoWorld.BuildOne(scene, world, spec, catalog, ledger);
            foreach (DemoRoom r in rooms.Values) r.Root.SetActive(true);
            Physics.SyncTransforms();
            DemoWorld.Neighbours(rooms);
            Wire(rooms[Sector], DeadFlag, GlorifiedBody.UI.PlayerHudFlags.CrownGone); // the grabber took the Crown in U1

            GameObject director = MenuLoopSetup.EnsureChild(world, "[CameraDirector]");
            MenuLoopSetup.EnsureComponent<FixedCameraDirector>(director).SetDefaultCamera(rooms[Wing].Cameras[0]);
            MenuLoopSetup.EnsureCamera(new Vector3(9f, 3f, -1f), new Vector3(9f, 1f, 3f), 60f);
            RoomKit.BakeNavMesh(world, NavPath);
            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            DemoWorld.Sight(world);
            EditorSceneManager.SaveScene(scene, ScenePath);
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[BrainTestSetup] built {ScenePath}: {rooms.Count} rooms, {rooms.Values.Sum(r => r.Cameras.Count)} cameras");
        }

        /// <summary>The fight on a sector that DressSector built (BRAIN TEST, and the demo's B1).</summary>
        public static void Wire(DemoRoom sector, string deadFlag, string beginFlag)
        {
            // The brain's eye follows her: the camera of the whole sector.
            FixedCamera eye = sector.Root.GetComponentsInChildren<FixedCamera>(true).First(c => c.name == "Cam_B1_eye");
            eye.followPlayerYaw = true;
            eye.followMaxYaw = 100f; // on the south wall: it turns to look along the walls into the corners (the coverage rule)
            eye.followSpeed = 90f;
            EditorUtility.SetDirty(eye);
            // The walls and the floor are brain (the greybox stand-in: pink-grey).
            foreach (Renderer r in sector.Room.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "Ceiling") continue;
                r.sharedMaterial = r.name == "Floor" ? DemoRoom.Colour("brain_floor", new Color(0.34f, 0.24f, 0.26f)) : DemoRoom.Colour("brain_fold", new Color(0.46f, 0.33f, 0.35f));
            }
            GameObject holder = MenuLoopSetup.EnsureChild(sector.Story, "Brain");
            BrainFight fight = MenuLoopSetup.EnsureComponent<BrainFight>(holder);
            fight.room = sector.Root.GetComponent<Room>();
            fight.eye = eye;
            fight.nodes = sector.Props.GetComponentsInChildren<BrainNode>(true);
            fight.workLights = Enumerable.Range(0, 6).Select(i => sector.Props.transform.Find("Work_Light_" + i).GetComponent<Renderer>()).ToArray();
            fight.workGlow = fight.workLights.Select(r => r.GetComponentInChildren<Light>(true)).ToArray();
            fight.grabberMouths = Enumerable.Range(0, 6).Select(i => sector.Props.transform.Find("Grabber_Mouth_" + i)).ToArray();
            fight.exitLeaf = sector.Props.transform.Find("Exit_Leaf").gameObject;
            fight.deadFlag = deadFlag;
            fight.beginFlag = beginFlag;
            EditorUtility.SetDirty(fight);
        }

        // ── The rooms ─────────────────────────────────────────────────

        private static void DressWing(DemoRoom r)
        {
            r.PlaceLucy(LucyStart, 0f);
            r.Sign("Sign_Rules", "KEEP MOVING. BLAST A GRABBER. LIFT ONE AND THROW IT", new Vector3(9f, 2.55f, 0.16f), Vector3.forward, new Vector2(3.6f, 0.35f), 40);
        }

        /// <summary>
        /// The brain sector, 18 x 12 m and 6 m high (BRAIN TEST, and the demo's B1): every place from the room's south-west
        /// corner, so the same sector stands anywhere. exitId: the room behind the exit leaf of the north doorway.
        /// </summary>
        public static void DressSector(DemoRoom r, string exitId)
        {
            Vector3 corner = new Vector3(r.Spec.X0, 0f, r.Spec.Z0);
            Vector3 P(float x, float y, float z) => corner + new Vector3(x, y, z - 6f); // the test room's numbers: its sector starts at z 6
            r.ExtraCamera("eye", P(9f, 5.4f, 6.8f), P(9f, 0.5f, 13f), 70f, P(9f, 1.5f, 12f), new Vector3(17.6f, 3f, 11.6f));
            // The brain: one mass at its eye, high on the south wall, just behind the camera (Bruno, 2026-11-08: "the target
            // should be where the camera is at; the idea is that we're seeing it from the brain's pov"). She turns to the
            // screen to hurt it. All its life is here, and the auto-lock picks it first (LockOnTargetable.IsPreferred).
            {
                GameObject node = r.Box(r.Props, "The_Brain", P(9f, 5.2f, 6.35f), new Vector3(2f, 1.4f, 0.6f), DemoRoom.Colour("brain_node", Color.white));
                node.isStatic = false;
                node.tag = "Enemy";
                node.layer = LayerMask.NameToLayer("Enemy");
                Health health = MenuLoopSetup.EnsureComponent<Health>(node);
                var so = new SerializedObject(health);
                so.FindProperty("maxHealth").floatValue = BrainHealth;
                so.FindProperty("destroyOnDeath").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                LockOnTargetable target = MenuLoopSetup.EnsureComponent<LockOnTargetable>(node);
                var tso = new SerializedObject(target);
                tso.FindProperty("fallbackHeight").floatValue = 0f;
                tso.ApplyModifiedPropertiesWithoutUndo();
                target.SetHeavy(true);       // the brain is not lifted
                target.SetShockImmune(true); // a boss: no short, no mk_edgar
                target.SetPreferred(true);   // she aims at it unless she switches
                MenuLoopSetup.EnsureComponent<BrainNode>(node);
                EditorUtility.SetDirty(target);
            }
            // The work lights, high on the long walls; they go out as the brain dies.
            for (int i = 0; i < 6; i++)
            {
                bool west = i < 3;
                Vector3 at = P(west ? 0.2f : 17.8f, 4.6f, 8f + (i % 3) * 4f);
                GameObject lamp = r.Box(r.Props, "Work_Light_" + i, at, new Vector3(0.12f, 0.25f, 0.6f), DemoRoom.Colour("work_light", Color.white));
                lamp.isStatic = false;
                lamp.AddComponent<Emissive>();
                var glow = new GameObject("Glow");
                glow.transform.SetParent(lamp.transform, false);
                glow.transform.position = at + (west ? Vector3.right : Vector3.left) * 0.6f;
                Light l = glow.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 7f;
                l.intensity = 1.2f;
                l.color = new Color(1f, 0.9f, 0.7f);
                l.shadows = LightShadows.None;
            }
            // The grabber mouths: dark openings low on the long walls.
            for (int i = 0; i < 6; i++)
            {
                bool west = i < 3;
                Vector3 at = P(west ? 0.35f : 17.65f, 1.3f, 8f + (i % 3) * 4f);
                r.Box(r.Props, "Grabber_Mouth_" + i, at, new Vector3(0.1f, 0.45f, 0.45f), DemoRoom.Colour("brain_mouth", new Color(0.12f, 0.06f, 0.07f)));
            }
            // The exit leaf in the north doorway: it goes when the brain dies.
            Door exit = r.DoorTo(exitId);
            GameObject leaf = r.Box(r.Props, "Exit_Leaf", r.Mouth(exit) + Vector3.up * 1.15f, new Vector3(exit.Width, 2.3f, 0.2f), DemoRoom.Colour("brain_floor", new Color(0.34f, 0.24f, 0.26f)));
            leaf.isStatic = false;
            DemoRoom.Carve(leaf);
            r.Sign("Label_L6", "AZOTH. ROUTE COMPUTE SERIES. ACCELERATED GROWTH, UNIT ONE. CONTAINED. IN SERVICE.", P(13f, 1.2f, 17.82f), Vector3.back, new Vector2(3.2f, 0.4f), 26);
        }
    }
}
#endif
