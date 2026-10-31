#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using GlorifiedBody.Audio;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Interaction;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The SCREEN TEST room (2026-10-31, Bruno: "in-world screens (a CRT clip with sound)"; the demo plan: W7, "on
    /// entry, the CRT wall plays the rehearsal tape"). The therapy gym of the look rooms with its doorway shut: its wall
    /// of 24 CRTs is one screen (Screen_CrtWall) that a trigger in the middle of the room starts once (the sequence's
    /// done flag), and a CRT on a cart by the west wall (Screen_Cart) plays on Interact, in a loop. Both play the
    /// placeholder clip (tools/screen_placeholder.sh) with its lines. Not baked: the spots light it. ScreenTests drive it.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.ScreenTestSetup.BuildScreenTest
    /// </summary>
    public static class ScreenTestSetup
    {
        public const string SceneName = "ScreenTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string ClipPath = "Assets/_Game/Screens/screen_placeholder.webm";
        public const string Wall = "Screen_CrtWall", Cart = "Screen_Cart";
        public const string TapeFlag = "screen_test_tape_seen";
        public const string MusicKey = "screen_test";
        public static readonly Vector3 LucyStart = new Vector3(0.3f, 0f, 11f);
        public static readonly Vector3 CartPoint = new Vector3(-3.3f, 0f, 12.8f); // the cart against the west wall, facing east

        /// <summary>The lines of the placeholder clip, at the times of tools/screen_placeholder.sh. PLACEHOLDER words.</summary>
        public static List<TapeLine> Lines() => new List<TapeLine>
        {
            new TapeLine { at = 0.8f, speaker = "SCREEN", text = "This is a placeholder clip on a screen." },
            new TapeLine { at = 5.5f, speaker = "SCREEN", text = "Its sound comes from the screen, in the room." },
            new TapeLine { at = 10.5f, speaker = "SCREEN", text = "The writer gives the words. The real clip comes later." },
            new TapeLine { at = 15.5f, speaker = "SCREEN", text = "A pause stops it with the world." },
        };

        [MenuItem("GlorifiedBody/Build Screen Test Room (a CRT wall, a CRT on a cart)")]
        public static void BuildScreenTest()
        {
            Directory.CreateDirectory(MenuLoopSetup.ScenesFolder);
            AssetDatabase.Refresh();
            LookTestSetup.EnsureLightingSettings();
            Scene scene = LookTestSetup.BeginScene(ScenePath);
            Camera camera = MenuLoopSetup.EnsureCamera(new Vector3(3.3f, 1.7f, 10.7f), new Vector3(-1.6f, 1f, 15.5f), 62f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            LookTestSetup.Palette p = LookTestSetup.EnsurePalette();
            VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(ClipPath);
            if (clip == null) Debug.LogError($"[ScreenTestSetup] no clip at {ClipPath}: run tools/screen_placeholder.sh");

            MenuLoopSetup.RemoveObject("[Room]");
            MenuLoopSetup.RemoveObject("[Lights]");
            MenuLoopSetup.RemoveObject("[Screens]");
            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            GameObject lights = MenuLoopSetup.EnsureRoot("[Lights]");
            LookTestSetup.BuildTherapyGym(room, lights, p, corridorOpening: false);
            LookTestSetup.ProbeGrid(LookTestSetup.GymMin, LookTestSetup.GymMax, 1.5f, 0.6f, 2.4f);
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog();
            MenuLoopSetup.EnsureLucy(catalog, LucyStart, 0f);

            // The wall of CRTs: one screen, 24 faces. Only the trigger starts it (the rehearsal tape of W7).
            GameObject screens = MenuLoopSetup.EnsureRoot("[Screens]");
            GameObject wallObj = MenuLoopSetup.EnsureChild(screens, Wall);
            wallObj.transform.position = new Vector3(LookTestSetup.GymMin.x + 1.75f, 1.2f, LookTestSetup.GymMax.z - 0.5f);
            InWorldScreen wall = MenuLoopSetup.EnsureComponent<InWorldScreen>(wallObj);
            var faces = new List<Renderer>();
            foreach (Transform child in room.transform)
                if (child.name.StartsWith("CrtFace_")) faces.Add(child.GetComponent<Renderer>());
            wall.faces = faces.ToArray();
            wall.clip = clip;
            wall.lines = Lines();
            wall.interactive = false;
            wall.loop = false;
            wall.thing = "the monitors";
            EditorUtility.SetDirty(wall);

            // The CRT on a cart against the west wall, facing east: Interact turns it on and off, it loops.
            GameObject cartObj = MenuLoopSetup.EnsureChild(screens, Cart);
            cartObj.transform.position = Vector3.zero; // the blocks take their places from the room's origin
            LookTestSetup.Block(cartObj, "Cart_Top", PrimitiveType.Cube, CartPoint + new Vector3(0f, 0.8f, 0f), new Vector3(0.55f, 0.04f, 0.7f), p.Steel);
            LookTestSetup.Block(cartObj, "Cart_Shelf", PrimitiveType.Cube, CartPoint + new Vector3(0f, 0.25f, 0f), new Vector3(0.55f, 0.04f, 0.7f), p.Steel);
            foreach (float dz in new[] { -0.32f, 0.32f })
                foreach (float dx in new[] { -0.25f, 0.25f })
                    LookTestSetup.Block(cartObj, $"Cart_Leg_{dx:+0.00;-0.00}_{dz:+0.00;-0.00}", PrimitiveType.Cube, CartPoint + new Vector3(dx, 0.4f, dz), new Vector3(0.03f, 0.8f, 0.03f), p.Steel);
            Vector3 crt = CartPoint + new Vector3(0f, 1.05f, 0f);
            LookTestSetup.Block(cartObj, "Cart_Crt", PrimitiveType.Cube, crt, new Vector3(0.46f, 0.42f, 0.5f), p.DarkSteel);
            LookTestSetup.Face(cartObj, "Cart_CrtFace", crt + new Vector3(0.235f, 0.01f, 0f), Quaternion.Euler(0f, -90f, 0f), 0.4f, 0.32f, p.Crt, 0f, stretch: true);
            InWorldScreen cart = MenuLoopSetup.EnsureComponent<InWorldScreen>(cartObj);
            cart.faces = new[] { cartObj.transform.Find("Cart_CrtFace").GetComponent<Renderer>() };
            cart.clip = clip;
            cart.lines = Lines();
            cart.interactive = true;
            cart.loop = true;
            cart.range = 1.6f;
            cart.thing = "the screen";
            GameObject point = MenuLoopSetup.EnsureChild(cartObj, "Point");
            point.transform.position = crt + new Vector3(0.3f, -0.6f, 0f);
            cart.point = point.transform;
            EditorUtility.SetDirty(cart);

            // The trigger: a step into the room starts the wall once (the done flag in the saves).
            ScriptedSequence tape = RoomKit.Sequence("Seq_RehearsalTape", TapeFlag, ScriptStep.Screen(wallObj));
            RoomKit.Trigger("Trigger_Gym", new Vector3(0f, 1f, 14.4f), new Vector3(7.6f, 2f, 5.4f), tape);

            GameObject music = MenuLoopSetup.EnsureRoot("[Music]");
            MenuLoopSetup.EnsureComponent<RoomMusic>(music).Key = MusicKey;

            // The tutorial's cameras (the same gym, the door shut).
            GameObject camerasRoot = LookTestSetup.ResetCameras();
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera gym = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Gym", new Vector3(3.3f, 1.7f, 10.7f), new Vector3(-1.6f, 1f, 15.5f), 62f);
            FixedCamera north = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Gym_NE", new Vector3(3.6f, 1.7f, 16.9f), new Vector3(-2f, 1f, 12.5f), 62f);
            FixedCamera west = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Gym_W", new Vector3(-3.7f, 1.7f, 13.2f), new Vector3(3f, 1f, 11.5f), 62f);
            director.SetDefaultCamera(gym);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Gym_North", new Vector3(0f, 1.5f, 15.4f), new Vector3(8f, 3f, 3.8f), north, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Gym_DoorStrip", new Vector3(3f, 1.5f, 11.9f), new Vector3(2f, 3f, 3.2f), west, 1);

            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            MenuLoopSetup.EnsureShotPoints(new[] { gym, north, west });
            GameObject probesRoot = MenuLoopSetup.EnsureRoot("[Probes]");
            LightProbeGroup probes = MenuLoopSetup.EnsureComponent<LightProbeGroup>(probesRoot);
            probes.probePositions = LookTestSetup.probePositions.ToArray();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ScreenTestSetup] built {ScenePath}: the CRT wall ({faces.Count} faces) on a trigger, the CRT on a cart on Interact.");
        }
    }
}
#endif
