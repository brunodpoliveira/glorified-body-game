#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using GlorifiedBody.Destruction;

namespace GlorifiedBody.Editor.Destruction
{
    /// <summary>
    /// The breakable room pieces (Bruno, 2026-10-24: "greebles on tables, the table itself, wall plaster coming
    /// down, and breakable lights"), made by code (rule 6): the materials, the pieces cut in advance (BoxFracture,
    /// one asset per design, the same seed every build), the effect settings, and the builders that put a table, a
    /// barricade, a plaster patch, a lamp and the items in the open scene. Greybox shapes with the look textures;
    /// the real models come later (a closed model goes through OpenFracture or Blender's cell fracture).
    /// Sizes and health PROPOSED.
    /// </summary>
    public static class BreakableAssets
    {
        public const string Folder = "Assets/_Game/Breakables";
        public const string MaterialsFolder = Folder + "/Materials";
        public const string FragmentsFolder = Folder + "/Fragments";
        public const string AudioFolder = Folder + "/Audio";
        public const string TexturesFolder = Folder + "/Textures";
        public const string FxPath = "Assets/_Game/Resources/Destruction/destruction_fx.asset";
        public const string ParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";
        public const string LitShaderName = "Universal Render Pipeline/Lit";
        public const string UnlitShaderName = "Universal Render Pipeline/Unlit";
        public const int DebrisLayerIndex = 19;

        // The designs, in metres.
        public static readonly Vector3 TableTop = new Vector3(1.6f, 0.05f, 0.8f);
        public const float TableHeight = 0.78f;
        public static readonly Vector3 BarricadePanel = new Vector3(1.6f, 1.08f, 0.08f);
        public const float BarricadeHeight = 1.2f; // chest high: her power at 1.2 m stops on it, their muzzle at 1.3 m shoots over
        public static readonly Vector3 LampPanel = new Vector3(0.6f, 0.025f, 0.24f);
        public const float PlasterThickness = 0.035f;

        private const float WoodDensity = 600f, GlassDensity = 2500f, PlasterDensity = 1200f;

        // ── Project set-up ─────────────────────────────────────────────

        /// <summary>The Debris layer (slot 19 is free): the pieces and the items. Breakables sets its collisions at run time.</summary>
        public static void EnsureDebrisLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty slot = layers.GetArrayElementAtIndex(DebrisLayerIndex);
            if (slot.stringValue == Breakables.DebrisLayerName) return;
            if (!string.IsNullOrEmpty(slot.stringValue)) Debug.LogError($"[BreakableAssets] layer {DebrisLayerIndex} is '{slot.stringValue}', not free.");
            slot.stringValue = Breakables.DebrisLayerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("GlorifiedBody/Breakables/Build Assets")]
        public static void EnsureAll()
        {
            EnsureDebrisLayer();
            foreach (string f in new[] { Folder, MaterialsFolder, FragmentsFolder, AudioFolder, TexturesFolder, "Assets/_Game/Resources/Destruction" })
                if (!AssetDatabase.IsValidFolder(f)) Directory.CreateDirectory(f);
            AssetDatabase.Refresh();
            EnsureFx();
            Fragments("table_top", TableTop, 8, 11);
            Fragments("barricade_panel", BarricadePanel, 10, 23);
            Fragments("lamp_panel", LampPanel, 6, 37);
            Fragments("plaster_patch", new Vector3(1.2f, 1f, PlasterThickness), 7, 41);
            AssetDatabase.SaveAssets();
        }

        // ── Materials ──────────────────────────────────────────────────

        /// <summary>
        /// A greybox surface on URP Lit, the shader of the room's own walls. Simple Lit rendered these pieces about five
        /// times darker than the wall beside them in the cover room, with no keywords at run time (a probe,
        /// 2026-10-24). The look-room surfaces stay on Simple Lit. An emissive one is URP Unlit: it reads as a lamp
        /// and gives the bake nothing.
        /// </summary>
        private static Material Surface(string name, string lookTexture, Color tint, float smoothness, bool glow = false)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Shader shader = Shader.Find(glow ? UnlitShaderName : LitShaderName);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader; // a new shader clears the keywords
            Texture2D texture = lookTexture != null ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{LookTestSetup.TexturesFolder}/{lookTexture}.png") : null;
            m.SetTexture("_BaseMap", texture);
            m.SetTextureScale("_BaseMap", Vector2.one * (glow ? 1f : 0.5f)); // the box UVs are in metres: the picture every 2 m, as the rooms
            m.SetColor("_BaseColor", tint);
            if (!glow)
            {
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", smoothness);
            }
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Laminate => Surface("GB_Break_Laminate", null, new Color(0.72f, 0.71f, 0.67f), 0.35f);
        public static Material Plywood => Surface("GB_Break_Plywood", null, new Color(0.68f, 0.54f, 0.38f), 0.1f);
        public static Material WoodInside => Surface("GB_Break_WoodInside", null, new Color(0.78f, 0.66f, 0.48f), 0.05f);
        public static Material Steel => Surface("GB_Break_Steel", "steel_panel", new Color(0.55f, 0.58f, 0.62f), 0.5f);
        public static Material PlasterSurface => Surface("GB_Break_Plaster", null, new Color(0.86f, 0.84f, 0.8f), 0.1f);
        public static Material PlasterInside => Surface("GB_Break_PlasterInside", null, new Color(0.7f, 0.68f, 0.64f), 0.02f);
        public static Material BareWall => Surface("GB_Break_BareWall", null, new Color(0.5f, 0.49f, 0.47f), 0.05f);
        public static Material Glass => Surface("GB_Break_Glass", "light_panel", new Color(0.75f, 0.85f, 0.9f), 0.9f);
        public static Material LampOn => Surface("GB_Break_LampOn", "light_panel", Color.white, 0f, glow: true);
        public static Material LampOff => Surface("GB_Break_LampOff", "light_panel", new Color(0.18f, 0.18f, 0.2f), 0.6f);
        public static Material Monitor => Surface("GB_Item_Monitor", "crt_face", new Color(0.55f, 0.55f, 0.55f), 0.6f);
        public static Material Plastic => Surface("GB_Item_Plastic", null, new Color(0.2f, 0.2f, 0.22f), 0.3f);
        public static Material BinderRed => Surface("GB_Item_BinderRed", null, new Color(0.55f, 0.1f, 0.1f), 0.2f);
        public static Material BinderBlue => Surface("GB_Item_BinderBlue", null, new Color(0.12f, 0.2f, 0.45f), 0.2f);
        public static Material Cardboard => Surface("GB_Item_Cardboard", null, new Color(0.6f, 0.48f, 0.32f), 0.05f);

        /// <summary>A soft round dot (64 px, white with a falling alpha): the particles are not squares.</summary>
        private static Texture2D SoftDot()
        {
            string path = TexturesFolder + "/soft_dot.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f)) / 32f;
                        float a = Mathf.Clamp01(1f - d);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material Particles(string name, bool additive)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Shader shader = Shader.Find(ParticlesUnlit);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            m.SetTexture("_BaseMap", SoftDot());
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void EnsureFx()
        {
            var fx = AssetDatabase.LoadAssetAtPath<DestructionFxSettings>(FxPath);
            if (fx == null)
            {
                fx = ScriptableObject.CreateInstance<DestructionFxSettings>();
                AssetDatabase.CreateAsset(fx, FxPath);
            }
            AudioClip Clip(string n)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{n}.wav");
                if (clip == null) Debug.LogError($"[BreakableAssets] no {n}.wav: run python3 tools/destruction_sfx.py.");
                return clip;
            }
            fx.woodBreak = Clip("wood_break");
            fx.metalBreak = Clip("metal_break");
            fx.glassBreak = Clip("glass_break");
            fx.plasterBreak = Clip("plaster_break");
            fx.plasticBreak = Clip("plastic_break");
            fx.chip = Clip("chip");
            fx.sparks = Clip("sparks");
            fx.dust = Particles("GB_Fx_Dust", false);
            fx.glow = Particles("GB_Fx_Glow", true);
            EditorUtility.SetDirty(fx);
        }

        // ── The pieces ─────────────────────────────────────────────────

        /// <summary>The cut pieces of a design: one asset with a mesh per piece, made once (the same seed gives the same pieces).</summary>
        public static List<BoxFracture.Piece> Fragments(string design, Vector3 size, int count, int seed)
        {
            string path = $"{FragmentsFolder}/{design}.asset";
            List<BoxFracture.Piece> pieces = BoxFracture.Split(size, count, seed, design);
            // The main asset takes the file's name: the first piece reads "table_top", not "table_top_0".
            string Key(string meshName) => meshName == design ? design + "_0" : meshName;
            Dictionary<string, Mesh> stored = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>()
                .GroupBy(m => Key(m.name)).ToDictionary(g => g.Key, g => g.First());
            if (stored.Count == pieces.Count && pieces.All(p => stored.ContainsKey(p.mesh.name)))
            {
                // Keep the stored meshes (their ids hold the scene references); only the centres come from the cut.
                foreach (BoxFracture.Piece p in pieces)
                {
                    Mesh keep = stored[p.mesh.name];
                    Object.DestroyImmediate(p.mesh);
                    p.mesh = keep;
                }
                return pieces;
            }
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(pieces[0].mesh, path);
            for (int i = 1; i < pieces.Count; i++) AssetDatabase.AddObjectToAsset(pieces[i].mesh, pieces[0].mesh);
            AssetDatabase.SaveAssets();
            return pieces;
        }

        private static GameObject Child(Transform parent, string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static GameObject Block(Transform parent, string name, Vector3 localCentre, Vector3 size, Material material, int layer)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localCentre;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            obj.layer = layer;
            return obj;
        }

        /// <summary>The cut pieces as rigid bodies under the debris root, placed where they sit in the intact shape.</summary>
        private static void AddPieces(Transform debris, List<BoxFracture.Piece> pieces, Vector3 shapeCentre, Quaternion shapeTurn, Material outside, Material inside, float density)
        {
            int layer = Breakables.DebrisLayer >= 0 ? Breakables.DebrisLayer : DebrisLayerIndex;
            for (int i = 0; i < pieces.Count; i++)
            {
                GameObject obj = Child(debris, $"Piece_{i}");
                obj.layer = layer;
                obj.transform.localPosition = shapeCentre + shapeTurn * pieces[i].centre;
                obj.transform.localRotation = shapeTurn;
                obj.AddComponent<MeshFilter>().sharedMesh = pieces[i].mesh;
                obj.AddComponent<MeshRenderer>().sharedMaterials = new[] { outside, inside };
                var collider = obj.AddComponent<MeshCollider>();
                collider.sharedMesh = pieces[i].mesh;
                collider.convex = true;
                AddBody(obj, Mathf.Max(0.2f, pieces[i].volume * density));
                obj.AddComponent<DebrisPiece>();
            }
        }

        /// <summary>A whole part that falls as one piece (a leg, a foot).</summary>
        private static void AddWholePiece(Transform debris, string name, Vector3 localCentre, Vector3 size, Material material, float density)
        {
            int layer = Breakables.DebrisLayer >= 0 ? Breakables.DebrisLayer : DebrisLayerIndex;
            GameObject obj = Block(debris, name, localCentre, size, material, layer);
            AddBody(obj, Mathf.Max(0.2f, size.x * size.y * size.z * density));
            obj.AddComponent<DebrisPiece>();
        }

        private static void AddBody(GameObject obj, float mass)
        {
            var body = obj.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private static GameObject Root(GameObject parent, string name, Vector3 position, float yaw)
        {
            Transform old = parent.transform.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            GameObject root = Child(parent.transform, name);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return root;
        }

        private static void Obstacle(GameObject root, Vector3 centre, Vector3 size)
        {
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = centre;
            obstacle.size = size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
        }

        /// <summary>The scene keeps the pieces and the broken look hidden (Breakable shows them at the break).</summary>
        private static void HideUntilBroken(GameObject debris, GameObject broken)
        {
            if (debris != null) debris.SetActive(false);
            if (broken != null) broken.SetActive(false);
        }

        // ── The builders ───────────────────────────────────────────────

        /// <summary>A lab table (wood laminate on four legs, 0.78 m high) with items on it. Not cover: too low.</summary>
        public static Breakable Table(GameObject parent, string id, Vector3 floor, float yaw, bool items = true)
        {
            GameObject root = Root(parent, id, floor, yaw);
            GameObject intact = Child(root.transform, "Intact");
            GameObject debris = Child(root.transform, "Debris");
            int room = LayerMask.NameToLayer("Default");
            Vector3 top = new Vector3(0f, TableHeight - TableTop.y * 0.5f, 0f);
            Block(intact.transform, "Top", top, TableTop, Laminate, room);
            float legH = TableHeight - TableTop.y;
            Vector3 legSize = new Vector3(0.05f, legH, 0.05f);
            var legs = new List<Vector3>();
            foreach (float x in new[] { -0.74f, 0.74f })
                foreach (float z in new[] { -0.34f, 0.34f })
                    legs.Add(new Vector3(x, legH * 0.5f, z));
            for (int i = 0; i < legs.Count; i++) Block(intact.transform, $"Leg_{i}", legs[i], legSize, Steel, room);

            AddPieces(debris.transform, Fragments("table_top", TableTop, 8, 11), top, Quaternion.identity, Laminate, WoodInside, WoodDensity);
            for (int i = 0; i < legs.Count; i++) AddWholePiece(debris.transform, $"Leg_{i}", legs[i], legSize, Steel, 7800f * 0.15f);

            Greeble[] onTop = items ? TableItems(parent, id, root.transform, TableHeight) : new Greeble[0];
            Obstacle(root, new Vector3(0f, TableHeight * 0.5f, 0f), new Vector3(TableTop.x, TableHeight, TableTop.z));
            HideUntilBroken(debris, null);
            var b = root.AddComponent<Breakable>();
            b.Bind(id, Breakable.Kind.Wood, 30f, false, intact, debris, null, onTop, 3.5f); // a full blast with the MK boost beside it breaks it
            return b;
        }

        /// <summary>A chest-high mobile barricade (a plywood panel on two steel feet): cover for the enemies.</summary>
        public static Breakable Barricade(GameObject parent, string id, Vector3 floor, float yaw)
        {
            GameObject root = Root(parent, id, floor, yaw);
            GameObject intact = Child(root.transform, "Intact");
            GameObject debris = Child(root.transform, "Debris");
            int room = LayerMask.NameToLayer("Default");
            Vector3 panel = new Vector3(0f, BarricadeHeight - BarricadePanel.y * 0.5f, 0f);
            Block(intact.transform, "Panel", panel, BarricadePanel, Plywood, room);
            Vector3 footSize = new Vector3(0.08f, 0.1f, 0.55f);
            Vector3[] feet = { new Vector3(-0.7f, 0.05f, 0f), new Vector3(0.7f, 0.05f, 0f) };
            for (int i = 0; i < feet.Length; i++) Block(intact.transform, $"Foot_{i}", feet[i], footSize, Steel, room);

            AddPieces(debris.transform, Fragments("barricade_panel", BarricadePanel, 10, 23), panel, Quaternion.identity, Plywood, WoodInside, WoodDensity);
            for (int i = 0; i < feet.Length; i++) AddWholePiece(debris.transform, $"Foot_{i}", feet[i], footSize, Steel, 7800f * 0.2f);

            Obstacle(root, new Vector3(0f, BarricadeHeight * 0.5f, 0f), new Vector3(BarricadePanel.x, BarricadeHeight, 0.5f));
            HideUntilBroken(debris, null);
            var b = root.AddComponent<Breakable>();
            b.Bind(id, Breakable.Kind.Wood, 90f, true, intact, debris, null, new Greeble[0], 4f);
            return b;
        }

        /// <summary>A plaster patch on a wall face: it comes down in chunks and leaves the bare concrete.</summary>
        public static Breakable Plaster(GameObject parent, string id, Vector3 wallPoint, Vector3 outward, Vector2 size)
        {
            outward.y = 0f;
            outward.Normalize();
            GameObject root = Root(parent, id, wallPoint, Quaternion.LookRotation(outward).eulerAngles.y);
            GameObject intact = Child(root.transform, "Intact");
            GameObject debris = Child(root.transform, "Debris");
            GameObject broken = Child(root.transform, "Bare");
            int room = LayerMask.NameToLayer("Default");
            Vector3 slab = new Vector3(size.x, size.y, PlasterThickness);
            Vector3 centre = new Vector3(0f, 0f, PlasterThickness * 0.5f);
            Block(intact.transform, "Plaster", centre, slab, PlasterSurface, room);
            GameObject bare = Block(broken.transform, "Bare", new Vector3(0f, 0f, 0.004f), new Vector3(size.x * 0.96f, size.y * 0.96f, 0.006f), BareWall, room);
            Object.DestroyImmediate(bare.GetComponent<Collider>());
            AddPieces(debris.transform, Fragments("plaster_patch", slab, 7, 41), centre, Quaternion.identity, PlasterSurface, PlasterInside, PlasterDensity);
            HideUntilBroken(debris, broken);
            var b = root.AddComponent<Breakable>();
            b.Bind(id, Breakable.Kind.Plaster, 25f, false, intact, debris, broken, new Greeble[0], 2f);
            return b;
        }

        /// <summary>
        /// A ceiling lamp: a steel housing (it stays), a glowing panel (it breaks into glass), a Realtime spot with
        /// hard shadows, and its fake bounce (a weak point light near the floor, no shadows). Realtime: the bake
        /// leaves it out, so a broken lamp leaves no glow on the walls.
        /// </summary>
        public static BreakableLight Lamp(GameObject parent, string id, Vector3 ceiling, Color colour, float intensity, float angle, float range = 9f)
        {
            GameObject root = Root(parent, id, ceiling, 0f);
            GameObject intact = Child(root.transform, "Intact");
            GameObject debris = Child(root.transform, "Debris");
            GameObject broken = Child(root.transform, "Dark");
            int room = LayerMask.NameToLayer("Default");
            Block(root.transform, "Housing", new Vector3(0f, -0.04f, 0f), new Vector3(0.7f, 0.08f, 0.3f), Steel, room);
            Vector3 panel = new Vector3(0f, -0.08f - LampPanel.y * 0.5f, 0f);
            GameObject glow = Block(intact.transform, "Panel", panel, LampPanel, LampOn, room);
            glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            GameObject off = Block(broken.transform, "Frame", panel + Vector3.up * 0.008f, new Vector3(LampPanel.x, 0.01f, LampPanel.z), LampOff, room);
            Object.DestroyImmediate(off.GetComponent<Collider>());
            AddPieces(debris.transform, Fragments("lamp_panel", LampPanel, 6, 37), panel, Quaternion.identity, Glass, Glass, GlassDensity);

            GameObject spotObj = Child(root.transform, "Light");
            spotObj.transform.localPosition = panel + Vector3.down * 0.03f;
            spotObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Light spot = spotObj.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = colour;
            spot.intensity = intensity;
            spot.range = range;
            spot.spotAngle = angle;
            spot.innerSpotAngle = angle * 0.55f;
            spot.shadows = LightShadows.Hard;
            spot.shadowStrength = 1f;
            spot.shadowBias = 0.02f;
            spot.shadowNormalBias = 0.3f;
            spot.lightmapBakeType = LightmapBakeType.Realtime; // Bruno: "realtime with a fake bounce"

            Physics.SyncTransforms();
            // The bounce: where the cone meets the floor, a little above it, about a tenth of the lamp.
            float floorY = Physics.Raycast(ceiling + Vector3.down * 0.2f, Vector3.down, out RaycastHit hit, 10f, 1, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
            GameObject bounceObj = Child(root.transform, "Bounce");
            bounceObj.transform.position = new Vector3(ceiling.x, floorY + 0.7f, ceiling.z);
            Light bounce = bounceObj.AddComponent<Light>();
            bounce.type = LightType.Point;
            bounce.color = Color.Lerp(colour, new Color(0.8f, 0.75f, 0.7f), 0.4f);
            bounce.intensity = intensity * 0.08f;
            bounce.range = Mathf.Min(6f, range * 0.6f);
            bounce.shadows = LightShadows.None;
            bounce.lightmapBakeType = LightmapBakeType.Realtime;

            HideUntilBroken(debris, broken);
            var b = root.AddComponent<BreakableLight>();
            b.Bind(id, Breakable.Kind.Glass, 8f, false, intact, debris, broken, new Greeble[0], 1.5f);
            b.BindLight(spot, bounce, null, null);
            return b;
        }

        // ── The items ──────────────────────────────────────────────────

        private static Greeble[] TableItems(GameObject parent, string id, Transform table, float height)
        {
            Transform old = parent.transform.Find(id + "_Items");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            GameObject items = Child(parent.transform, id + "_Items");
            items.transform.SetPositionAndRotation(table.position, table.rotation);
            var list = new List<Greeble>
            {
                Item(items.transform, id + "_Monitor", new Vector3(-0.35f, height, 0.1f), new Vector3(0.5f, 0.36f, 0.08f), Monitor, 8f, 0f),
                Item(items.transform, id + "_Binder1", new Vector3(0.35f, height, -0.15f), new Vector3(0.08f, 0.3f, 0.26f), BinderRed, 1.2f, 4f),
                Item(items.transform, id + "_Binder2", new Vector3(0.45f, height, -0.15f), new Vector3(0.08f, 0.3f, 0.26f), BinderBlue, 1.2f, -3f),
                Item(items.transform, id + "_Box", new Vector3(0.45f, height, 0.22f), new Vector3(0.32f, 0.2f, 0.24f), Cardboard, 2f, 12f),
            };
            return list.ToArray();
        }

        /// <summary>One item standing on the surface at the local point: a box with a body that sleeps until a hit.</summary>
        public static Greeble Item(Transform parent, string id, Vector3 localFoot, Vector3 size, Material material, float mass, float yaw)
        {
            int layer = Breakables.DebrisLayer >= 0 ? Breakables.DebrisLayer : DebrisLayerIndex;
            GameObject obj = Block(parent, id, localFoot + Vector3.up * size.y * 0.5f, size, material, layer);
            obj.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            AddBody(obj, mass);
            var g = obj.AddComponent<Greeble>();
            g.Bind(id);
            return g;
        }
    }
}
#endif
