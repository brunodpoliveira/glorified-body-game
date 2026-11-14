#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The stills of the odd cameras for Bruno's pick (rounds 147 and 148, DEMO_PLAN §10.5b item 4): W14 low and looking up,
    /// G3 with the donor wall framed alone, and the foyer with Modesty seen through the columns. The demo scene opens, a
    /// camera renders at each pose, nothing is saved. The foyer is not built: a greybox of it stands in an empty scene
    /// (the circular wall, eight chrome columns, the skylight with its cross grid, Modesty larger than life on a plinth).
    /// Run: tools/unity.sh exec GlorifiedBody.Editor.Demo.CameraStills.Run
    /// The stills: ~/MEGA/glorified/renders/game/round148/.
    /// </summary>
    public static class CameraStills
    {
        public const int Width = 1920, Height = 1080;
        public static string Folder => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "MEGA/glorified/renders/game/round148");

        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Demo/Demo.unity", OpenSceneMode.Single);
            // W14, the children's day room: from the floor by the west wall, looking up at the room and the TV (low, a child's eye).
            Shot("cam_w14_low_looking_up.png", new Vector3(-4.6f, 0.3f, 9.4f), new Vector3(-2.0f, 2.4f, 13.9f), 64f);
            // The same room as the corner camera sees it today, for the comparison.
            Shot("cam_w14_today_corner.png", new Vector3(-9.6f, 2.7f, 9.3f), new Vector3(-3f, 0.6f, 12.5f), 60f);
            // G3, the donor wall: straight on and level, the wall alone in the frame (the cherry tree of hands when it is drawn).
            Shot("cam_g3_donor_wall_alone.png", new Vector3(24.5f, -2.35f, -24.55f), new Vector3(24.5f, -2.3f, -19.17f), 50f);
            // The built cameras of 2026-11-14: the foyer's entry view, the main entrance down its axis, the conference table (Network).
            foreach (string name in new[] { "Cam_G5_low_north", "Cam_G5_low_south", "Cam_G5_lift", "Cam_G13b_axis", "Cam_G12_table", "Cam_G12_side", "Cam_W14_low", "Cam_G3_wall", "Cam_W9_lamp", "Cam_G4_candles", "Cam_G10_cake", "Cam_G18_candelabrum" })
            {
                GameObject built = GameObject.Find(name);
                if (built == null) { Debug.LogWarning("[CameraStills] no camera " + name + ": run DemoWorld.BuildAll"); continue; }
                var fixedCam = built.GetComponent<GlorifiedBody.Cameras.FixedCamera>();
                float fov = fixedCam != null ? fixedCam.fieldOfView : 60f;
                Shot("built_" + name.ToLowerInvariant() + ".png", built.transform.position, built.transform.position + built.transform.forward * 10f, fov);
            }
            // The foyer, a greybox in an empty scene.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Foyer();
            // Modesty through the columns: low, by the wall, a column in the foreground, the statue under the skylight.
            Vector3 low = Quaternion.Euler(0f, 300f, 0f) * Vector3.forward * 6.9f + Vector3.up * 1.0f;
            Shot("cam_foyer_through_the_columns.png", low, new Vector3(0f, 3.0f, 0f), 48f);
            // From the spoke of G2 (north), the whole round room.
            Shot("cam_foyer_from_the_concourse.png", new Vector3(0f, 2.2f, 7.9f), new Vector3(0f, 1.8f, 0f), 75f);
            Debug.Log("[CameraStills] the stills in " + Folder);
        }

        private static void Shot(string file, Vector3 at, Vector3 lookAt, float fov)
        {
            var go = new GameObject("[StillCamera]");
            Camera cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(at, Quaternion.LookRotation(lookAt - at, Vector3.up));
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.025f);
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Folder, file), tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            Debug.Log($"[CameraStills] {file}: at {at}, to {lookAt}, fov {fov}");
        }

        private static Material Lit(Color c, float smooth, float metal = 0f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            return m;
        }

        private static GameObject Part(PrimitiveType type, string name, Vector3 at, Vector3 scale, Material m, Transform parent, Quaternion? turn = null)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = at;
            g.transform.localRotation = turn ?? Quaternion.identity;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        /// <summary>
        /// The foyer greybox (round 148: "a circular lift lobby", the picture lobby_round_skylight_chrome_lift.jpg; Bruno,
        /// 2026-11-14: "in the middle of the map ... the other rooms connected to it like spokes on a wheel"): the place of
        /// G5, 15 m across, 6 m high, curved frosted panels, the nine spoke doorways of G5's rooms, the lift on the north-west
        /// arc, eight chrome columns lit from below, a lit skylight with a cross grid, a dark round floor, and Modesty on a
        /// plinth in the centre, 2.6 m tall (larger than life).
        /// </summary>
        public static void Foyer()
        {
            Transform root = new GameObject("Foyer").transform;
            Material floor = Lit(new Color(0.06f, 0.06f, 0.07f), 0.85f);
            Material panel = Lit(new Color(0.78f, 0.8f, 0.82f), 0.3f);
            Material chrome = Lit(new Color(0.85f, 0.86f, 0.88f), 0.95f, 1f);
            Material marble = Lit(new Color(0.93f, 0.91f, 0.87f), 0.55f);
            Material dark = Lit(new Color(0.12f, 0.12f, 0.13f), 0.4f);
            const float r = 7.5f, h = 6f;
            float[] spokes = { 0f, 60.6f, 82.9f, 116.6f, 138.8f, 180f, 221.2f, 243.4f, 290.6f }; // G2, G10, G6, G9, G12, U1, G8, G7, G13
            Part(PrimitiveType.Cylinder, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(2f * r, 0.05f, 2f * r), floor, root);
            Part(PrimitiveType.Cylinder, "Ceiling", new Vector3(0f, h + 0.05f, 0f), new Vector3(2f * r, 0.05f, 2f * r), dark, root);
            for (int i = 0; i < 40; i++)
            {
                float deg = i * 9f;
                float a = deg * Mathf.Deg2Rad;
                Vector3 at = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                bool door = System.Array.Exists(spokes, sp => Mathf.Abs(Mathf.DeltaAngle(sp, deg)) < 6f);
                bool lift = Mathf.Abs(Mathf.DeltaAngle(315f, deg)) < 6f; // the lift on the north-west arc
                Quaternion face = Quaternion.LookRotation(-at.normalized, Vector3.up);
                if (door)
                {
                    Part(PrimitiveType.Cube, "Lintel", at + Vector3.up * (h - 0.9f), new Vector3(1.25f, 1.8f, 0.12f), panel, root, face);
                    // The spoke's corridor: dark, 3 m deep (the room behind is not part of the greybox).
                    Vector3 away = at.normalized;
                    Part(PrimitiveType.Cube, "Spoke_Floor", at + away * 1.5f + Vector3.down * 0.04f, new Vector3(1.3f, 0.05f, 3f), floor, root, face);
                    Part(PrimitiveType.Cube, "Spoke_End", at + away * 3f + Vector3.up * 2.1f, new Vector3(1.4f, 4.2f, 0.1f), dark, root, face);
                    Part(PrimitiveType.Cube, "Spoke_Top", at + away * 1.5f + Vector3.up * 4.2f, new Vector3(1.4f, 0.1f, 3f), dark, root, face);
                    foreach (float side in new[] { -0.68f, 0.68f })
                        Part(PrimitiveType.Cube, "Spoke_Side", at + away * 1.5f + face * Vector3.right * side + Vector3.up * 2.1f, new Vector3(0.06f, 4.2f, 3f), dark, root, face);
                    continue;
                }
                Part(PrimitiveType.Cube, lift ? "Lift_Door" : "Panel", at + Vector3.up * h * 0.5f, new Vector3(1.25f, h, 0.12f), lift ? chrome : panel, root, face);
            }
            for (int k = 0; k < 8; k++)
            {
                float a = (k + 0.5f) * Mathf.PI / 4f;
                Vector3 at = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 5.8f;
                Part(PrimitiveType.Cylinder, "Column", at + Vector3.up * h * 0.5f, new Vector3(0.36f, h * 0.5f, 0.36f), chrome, root);
                var up = new GameObject("Uplight").AddComponent<Light>();
                up.transform.SetParent(root, false);
                up.transform.localPosition = at + Vector3.up * 0.15f + (-at.normalized * 0.35f);
                up.transform.localRotation = Quaternion.LookRotation(Vector3.up);
                up.type = LightType.Spot;
                up.spotAngle = 40f;
                up.range = 6f;
                up.intensity = 6f;
                up.color = new Color(1f, 0.95f, 0.88f);
            }
            // The skylight: a lit disc with a cross grid.
            Material sky = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            sky.SetColor("_BaseColor", new Color(1f, 0.98f, 0.92f));
            Part(PrimitiveType.Cylinder, "Skylight", new Vector3(0f, h, 0f), new Vector3(5f, 0.02f, 5f), sky, root);
            for (int g = -2; g <= 2; g++)
            {
                Part(PrimitiveType.Cube, "Grid_X", new Vector3(g * 0.95f, h - 0.03f, 0f), new Vector3(0.06f, 0.05f, 5f), dark, root);
                Part(PrimitiveType.Cube, "Grid_Z", new Vector3(0f, h - 0.03f, g * 0.95f), new Vector3(5f, 0.05f, 0.06f), dark, root);
            }
            var key = new GameObject("Skylight_Light").AddComponent<Light>();
            key.transform.SetParent(root, false);
            key.transform.localPosition = new Vector3(0f, h - 0.1f, 0f);
            key.transform.localRotation = Quaternion.LookRotation(Vector3.down);
            key.type = LightType.Spot;
            key.spotAngle = 70f;
            key.range = 14f;
            key.intensity = 45f;
            key.shadows = LightShadows.Hard;
            RenderSettings.ambientLight = new Color(0.12f, 0.12f, 0.14f);
            // Modesty, a placeholder: the plinth, the body under the veil, the head, the veil's fall. 2.6 m tall on the plinth.
            Part(PrimitiveType.Cylinder, "Plinth", new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 0.5f, 1.4f), marble, root);
            Part(PrimitiveType.Capsule, "Modesty_Body", new Vector3(0f, 2.05f, 0f), new Vector3(0.75f, 1.05f, 0.6f), marble, root);
            Part(PrimitiveType.Sphere, "Modesty_Head", new Vector3(0f, 3.35f, 0.04f), new Vector3(0.4f, 0.48f, 0.42f), marble, root);
            Part(PrimitiveType.Cylinder, "Modesty_Veil", new Vector3(0f, 2.9f, -0.05f), new Vector3(0.62f, 0.55f, 0.5f), marble, root);
        }
    }
}
#endif
