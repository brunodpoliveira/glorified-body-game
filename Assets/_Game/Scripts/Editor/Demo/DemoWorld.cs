#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using GlorifiedBody.Automation;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Rooms;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The demo as one world scene (2026-11-02, Bruno: "i want all the map to be built and loaded up in one go, we just
    /// cull rendering"; "it's very annoying to have a loading screen between each room"). Scenes/Demo/Demo.unity holds
    /// every room of both floors under "[World]", the stair room S1 between them (his pick: "a real stair room"), one
    /// Lucy, one camera, one light, one camera director, one NavMesh (his pick: the enemies chase her through the doors).
    /// The ward (floor 2) stands at the plan coordinates; the public floor (floor 1) stands 4 m lower and to the
    /// south-east (Floor1Offset), so the foot of the stair meets G1's north door. RoomTracker finds her room and culls the
    /// drawing of the rooms she cannot see. Batch: tools/unity.sh exec GlorifiedBody.Editor.Demo.DemoWorld.BuildAll,
    /// then MapAtlasSetup.BuildAtlas.
    /// </summary>
    public static class DemoWorld
    {
        public const string SceneName = "Demo";
        public const string ScenePath = DemoRoom.Folder + "/" + SceneName + ".unity";
        public const string NavPath = DemoRoom.Folder + "/" + SceneName + "_NavMesh.asset";
        public const string Stair = "S1";
        /// <summary>Floor 1 from its plan coordinates to the world: G1's north door (plan 2.5, 5) lands at the stair's foot (15, -4, -26).</summary>
        public static readonly Vector3 Floor1Offset = new Vector3(12.5f, -4f, -31f);
        // The stair room S1 in the world (floor 2's frame): 3 m wide, from W12's south door (z -14) down to G1 (z -26).
        public const float StairX0 = 13.5f, StairX1 = 16.5f, StairTop = -14f, StairBottom = -26f, StairRampTop = -16f, StairRampBottom = -24f;

        [MenuItem("GlorifiedBody/Demo/Build the world (both floors)")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(DemoRoom.Folder);
            BarkBookSetup.Build(); // the writer's barks into Resources (the figures read them in play)
            GlorifiedBody.Editor.Destruction.BreakableAssets.EnsureAll();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.32f, 0.35f);
            var sun = new GameObject("Light");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            light.shadows = LightShadows.Hard;
            light.renderingLayerMask = (int)GlorifiedBody.Rendering.Darkness.LitLayer; // the dark room G9 leaves the sun (2026-11-09)
            sun.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
            var world = new GameObject("[World]");
            world.AddComponent<RoomTracker>();
            DemoRoom.Lucy = MenuLoopSetup.EnsureLucy(catalog, Vector3.zero, 0f); // the H1 dressing puts her in the frame (the Hold, 2026-11-13)

            List<RoomSpec> ward = DemoWard.Rooms(), floor1 = DemoPublic.Rooms();
            var ledger = new WallLedger(ward.Concat(floor1).ToList());
            var rooms = new Dictionary<string, DemoRoom>();
            foreach (RoomSpec spec in ward) rooms[spec.Id] = BuildOne(scene, world, spec, catalog, ledger);
            // The rooms of floor 1 build at the plan coordinates (on the ward's ground), then move to their place together.
            var lower = new List<DemoRoom>();
            foreach (RoomSpec spec in floor1) lower.Add(rooms[spec.Id] = BuildOne(scene, world, spec, catalog, ledger));
            foreach (DemoRoom r in lower) r.Root.transform.position += Floor1Offset;
            foreach (DemoRoom r in rooms.Values) r.Root.SetActive(true);
            Physics.SyncTransforms();
            List<FixedCamera> stairCameras = StairRoom(world);
            Neighbours(rooms);
            Reinforcement(rooms);
            DemoBrain.Wire(rooms); // the brain fight of B1 (2026-11-09)
            DemoDark.Wire(rooms);  // the dark room of G9 and G9b (2026-11-09)
            DemoHold.Wire(rooms);  // the Hold (2026-11-13): the guards of H3, the black hall

            GameObject director = MenuLoopSetup.EnsureChild(world, "[CameraDirector]");
            MenuLoopSetup.EnsureComponent<FixedCameraDirector>(director).SetDefaultCamera(rooms["H1"].Cameras[0]);
            Room w1 = world.transform.Find("[H1]").GetComponent<Room>();
            MenuLoopSetup.EnsureCamera(w1.Centre + new Vector3(0f, 3f, -4f), w1.Centre, 60f);
            RoomKit.BakeNavMesh(world, NavPath);
            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            EditorSceneManager.SaveScene(scene, ScenePath);
            // The sight lists from the saved scene, the one the game plays (2026-11-08: a scan of the scene before the save
            // saw W14 and W6 from W1B, and the saved scene does not; the cull test failed after every full build).
            Resight();
            RemoveOldRoomScenes();
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            int cameras = rooms.Values.Sum(r => r.Cameras.Count) + stairCameras.Count;
            Debug.Log($"[DemoWorld] built {ScenePath}: {rooms.Count + 1} rooms, {cameras} cameras");
        }

        /// <summary>The soldiers after the fireteam of G2 (TeamReinforcement): it needs both rooms, so it comes after them.</summary>
        private static void Reinforcement(Dictionary<string, DemoRoom> rooms)
        {
            GameObject watcher = MenuLoopSetup.EnsureChild(rooms["G2"].Story, "Team_Reinforcement");
            TeamReinforcement reinforcement = MenuLoopSetup.EnsureComponent<TeamReinforcement>(watcher);
            reinforcement.afterFlag = DemoPublic.Released;
            reinforcement.reinforcementFlag = DemoPublic.Reinforced;
            reinforcement.teamParent = rooms["G2"].Figures.transform;
            reinforcement.team = new[] { "Soldier_Pair1_A", "Soldier_Pair1_B", "Soldier_Pair2_A", "Soldier_Pair2_B" };
            reinforcement.group = rooms["G2"].Figures.transform.Find(DemoPublic.ReinforcementGroup);
            reinforcement.room = rooms["G2"].Root.GetComponent<Room>();
        }

        /// <summary>
        /// One room under its root, with every other room off: a helper that finds an object by its name (a sequence, a
        /// marker) never takes one of another room, and the camera search sees this room only. New scene roots of the
        /// build go under the room.
        /// </summary>
        internal static DemoRoom BuildOne(Scene scene, GameObject world, RoomSpec spec, CharacterCatalog catalog, WallLedger ledger)
        {
            foreach (Transform child in world.transform)
                if (child.GetComponent<Room>() != null) child.gameObject.SetActive(false);
            var before = new HashSet<GameObject>(scene.GetRootGameObjects());
            var root = new GameObject("[" + spec.Id + "]");
            root.transform.SetParent(world.transform, false);
            DemoRoom room = DemoRoom.Build(spec, root, catalog, ledger);
            // A helper that makes a root object (a pickup, a save machine, a shot point) made it for this room.
            foreach (GameObject obj in scene.GetRootGameObjects())
                if (!before.Contains(obj)) obj.transform.SetParent(root.transform, true);
            return room;
        }

        /// <summary>
        /// The rooms each fixed camera can see (RoomSight): the room cull draws only those, and her room (Bruno, 2026-11-07:
        /// "cull rendering to what the player can see; this is easy due to the fixed camera angles").
        /// </summary>
        public static void Sight(GameObject world)
        {
            Physics.SyncTransforms();
            Room[] all = world.GetComponentsInChildren<Room>(true);
            int total = 0, cameras = 0;
            foreach (FixedCamera cam in world.GetComponentsInChildren<FixedCamera>(true))
            {
                Room own = cam.GetComponentInParent<Room>(true);
                if (own == null) continue;
                RoomSight.Result sight = RoomSight.Scan(cam, own, all);
                cam.sees = sight.Seen.Select(r => r.id).OrderBy(id => id, System.StringComparer.Ordinal).ToArray();
                EditorUtility.SetDirty(cam);
                total += cam.sees.Length;
                cameras++;
            }
            Debug.Log($"[DemoWorld] sight: {cameras} cameras see {(cameras > 0 ? (float)total / cameras : 0f):0.0} of {all.Length} rooms on average");
        }

        /// <summary>The sight lists again, on the saved world (after a change of walls or cameras, with no full build).</summary>
        public static void Resight()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            GameObject world = scene.GetRootGameObjects().First(g => g.GetComponentInChildren<Room>(true) != null);
            Sight(world);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// The stair room S1 (Bruno, 2026-11-02: "a real stair room"): from W12's south door a 2 m landing, an 8 m ramp
        /// down 4 m (26.6 degrees: her controller and the NavMesh take up to 45), a 2 m landing at G1's north door. The
        /// steps are a ramp under the feet; two cameras, one for each half.
        /// </summary>
        private static List<FixedCamera> StairRoom(GameObject world)
        {
            var root = new GameObject("[" + Stair + "]");
            root.transform.SetParent(world.transform, false);
            Room room = root.AddComponent<Room>();
            room.id = Stair;
            room.title = "STAIRS";
            room.floor = DemoItems.Floor2;
            room.rect = new Vector4(StairX0, StairBottom, StairX1, StairTop);
            room.height = 3f;
            room.below = 4.5f;
            GameObject shell = MenuLoopSetup.EnsureChild(root, "[Room]");
            GameObject props = MenuLoopSetup.EnsureChild(root, "[Props]");
            float mid = (StairX0 + StairX1) * 0.5f, width = StairX1 - StairX0, drop = -Floor1Offset.y;
            room.exits = new[]
            {
                new Room.Exit { mouth = new Vector3(mid, 0f, StairTop), outward = Vector3.forward, width = 1.6f, to = "W12" },
                new Room.Exit { mouth = new Vector3(mid, -drop, StairBottom), outward = Vector3.back, width = 1.6f, to = "G1" },
            };
            Material floor = DemoRoom.Grey(0.42f), wall = DemoRoom.Grey(DemoRoom.WallShade);
            Box(shell, "Landing_Top_Floor", new Vector3(mid, -0.05f, (StairTop + StairRampTop) * 0.5f), new Vector3(width, 0.1f, StairTop - StairRampTop), floor);
            Box(shell, "Landing_Bottom_Floor", new Vector3(mid, -drop - 0.05f, (StairRampBottom + StairBottom) * 0.5f), new Vector3(width, 0.1f, StairRampBottom - StairBottom), floor);
            float run = StairRampTop - StairRampBottom, slope = Mathf.Atan2(drop, run) * Mathf.Rad2Deg, length = Mathf.Sqrt(run * run + drop * drop);
            GameObject ramp = Box(shell, "Ramp_Floor", new Vector3(mid, -drop * 0.5f - 0.05f, (StairRampTop + StairRampBottom) * 0.5f), new Vector3(width, 0.1f, length), floor);
            ramp.transform.rotation = Quaternion.Euler(-slope, 0f, 0f); // the north end up
            // The treads: thin stripes on the ramp, a stair to the eye.
            for (int i = 1; i < 16; i++)
            {
                float t = i / 16f;
                Vector3 on = new Vector3(mid, -drop * t + 0.01f, StairRampTop - run * t);
                GameObject tread = Box(props, $"Tread_{i:00}", on, new Vector3(width - 0.1f, 0.02f, 0.04f), DemoRoom.Grey(0.6f));
                tread.transform.rotation = ramp.transform.rotation;
                Object.DestroyImmediate(tread.GetComponent<Collider>());
            }
            float wallH = 3f + drop, wallY = (3f - drop) * 0.5f, len = StairTop - StairBottom;
            Box(shell, "Wall_W", new Vector3(StairX0, wallY, (StairTop + StairBottom) * 0.5f), new Vector3(DemoRoom.Wall, wallH, len), wall);
            Box(shell, "Wall_E", new Vector3(StairX1, wallY, (StairTop + StairBottom) * 0.5f), new Vector3(DemoRoom.Wall, wallH, len), wall);
            // Over G1's north wall (3 m high on floor 1) the shaft goes on up to floor 2's height.
            Box(shell, "Wall_S_Upper", new Vector3(mid, (3f - drop + 3f) * 0.5f, StairBottom), new Vector3(width + DemoRoom.Wall, 3f - (3f - drop), DemoRoom.Wall), wall);
            DemoRoom.Ceiling(shell, new Vector3(mid, room.height, (StairTop + StairBottom) * 0.5f), new Vector2(width + DemoRoom.Wall, len + DemoRoom.Wall));
            Box(props, "Handrail", new Vector3(StairX1 - 0.2f, -drop * 0.5f + 0.9f, (StairRampTop + StairRampBottom) * 0.5f), new Vector3(0.05f, 0.05f, length), DemoRoom.Grey(0.7f)).transform.rotation = ramp.transform.rotation;
            GameObject music = MenuLoopSetup.EnsureChild(root, "[Music]");
            MenuLoopSetup.EnsureComponent<Audio.RoomMusic>(music).Key = DemoWard.Music;
            Physics.SyncTransforms();
            return StairCameras(root, ramp.GetComponent<BoxCollider>(), drop);
        }

        private static GameObject Box(GameObject parent, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject obj = RoomKit.Box(parent, name, position, size);
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        /// <summary>Two cameras for the stair: each half takes the pose (top or foot, three places across, three lenses) that frames the most of its floor points.</summary>
        private static List<FixedCamera> StairCameras(GameObject root, BoxCollider ramp, float drop)
        {
            GameObject camerasRoot = MenuLoopSetup.EnsureChild(root, "[Cameras]");
            int solid = 1 << LayerMask.NameToLayer("Default");
            var cameras = new List<FixedCamera>();
            float half = (StairTop + StairBottom) * 0.5f;
            foreach ((string tag, float z0, float z1) in new[] { ("00", half, StairTop), ("01", StairBottom, half) })
            {
                var points = new List<Vector3>();
                for (float x = StairX0 + 0.45f; x <= StairX1 - 0.45f; x += 0.5f)
                    for (float z = z0 + 0.25f; z <= z1 - 0.25f; z += 0.5f)
                        if (Physics.Raycast(new Vector3(x, 1f, z), Vector3.down, out RaycastHit hit, 10f, solid, QueryTriggerInteraction.Ignore)) points.Add(hit.point);
                Vector3 centre = points.Aggregate(Vector3.zero, (a, p) => a + p) / Mathf.Max(1, points.Count);
                (Vector3 cam, Vector3 look, float fov, int seen) best = (Vector3.zero, Vector3.zero, 62f, -1);
                foreach (float cx in new[] { StairX0 + 0.4f, (StairX0 + StairX1) * 0.5f, StairX1 - 0.4f })
                    foreach (Vector3 cam in new[] { new Vector3(cx, 2.7f, StairTop - 0.4f), new Vector3(cx, -drop + 2.6f, StairBottom + 0.4f) })
                        foreach (float fov in new[] { 62f, 72f, 84f })
                        {
                            Vector3 look = centre + Vector3.up * 0.7f;
                            Quaternion rotation = Quaternion.LookRotation(look - cam);
                            int seen = points.Count(p => CameraCoverage.Sees(cam, rotation, fov, p, CameraCoverage.GameRoomNear, solid));
                            if (seen > best.seen) best = (cam, look, fov, seen);
                        }
                FixedCamera fc = MenuLoopSetup.EnsureFixedCamera(camerasRoot, $"Cam_{Stair}_{tag}", best.cam, best.look, best.fov);
                cameras.Add(fc);
                float zoneY = centre.y + 1.5f;
                MenuLoopSetup.EnsureCameraZone(camerasRoot, $"Zone_{Stair}_{tag}", new Vector3((StairX0 + StairX1) * 0.5f, zoneY, (z0 + z1) * 0.5f), new Vector3(StairX1 - StairX0, 3f + drop * 0.5f, z1 - z0), fc, 1);
                if (best.seen < points.Count) Debug.Log($"[DemoWorld] {fc.cameraName}: {points.Count - best.seen} of {points.Count} points out of frame");
            }
            MenuLoopSetup.EnsureShotPoints(cameras.ToArray());
            foreach (FixedCamera c in cameras)
            {
                GameObject shot = GameObject.Find("ShotPoint_" + c.cameraName);
                if (shot != null) shot.transform.SetParent(root.transform, true);
            }
            return cameras;
        }

        /// <summary>The rooms behind each room's doorways: the doors that name a room, and a fire door's room across (by place).</summary>
        internal static void Neighbours(Dictionary<string, DemoRoom> rooms)
        {
            var all = Object.FindObjectsByType<Room>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (DemoRoom r in rooms.Values)
            {
                var ids = new HashSet<string>();
                foreach (Door d in r.Spec.Doors)
                {
                    if (!string.IsNullOrEmpty(d.To)) { ids.Add(d.To); continue; }
                    Vector3 across = r.Root.transform.position + r.Mouth(d) + DemoRoom.Outward(d.Side) * 0.5f + Vector3.up * 0.2f;
                    Room other = all.FirstOrDefault(o => o.gameObject != r.Root && o.Contains(across));
                    if (other != null) ids.Add(other.id);
                }
                r.Root.GetComponent<Room>().neighbours = ids.ToArray();
            }
            Room stair = all.FirstOrDefault(o => o.id == Stair);
            if (stair != null) stair.neighbours = rooms.Values.Where(r => r.Spec.Doors.Any(d => d.To == Stair)).Select(r => r.Spec.Id).ToArray();
        }

        /// <summary>The scenes of the rooms before 2026-11-02 (Demo_W1 and the rest) and their NavMesh assets.</summary>
        private static void RemoveOldRoomScenes()
        {
            if (!Directory.Exists(DemoRoom.Folder)) return;
            foreach (string path in Directory.GetFiles(DemoRoom.Folder, "Demo_*"))
            {
                if (path.EndsWith(".meta") || path.Replace('\\', '/') == NavPath) continue;
                AssetDatabase.DeleteAsset(path.Replace('\\', '/'));
            }
        }
    }
}
#endif
