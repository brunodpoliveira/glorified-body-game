#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The items of the game (Bruno, 2026-10-22): the three drugs as grabbables, the test log and the test key as
    /// key items, in Resources/Items/, and the unlit material of each pickup, so it stands out of the room light
    /// (after Resident Evil 4: the things you can take carry no real-time shading). The words are placeholders.
    /// </summary>
    public static class ItemAssets
    {
        public const string ItemsFolder = MenuLoopSetup.ResourcesFolder + "/Items";
        public const string MaterialsFolder = "Assets/_Game/Rendering/Materials";
        public const string UnlitShaderName = "Universal Render Pipeline/Unlit";

        public static readonly Color NalconColour = new Color(0.35f, 0.95f, 0.45f);
        public static readonly Color RedColour = new Color(0.95f, 0.25f, 0.22f);
        public static readonly Color DFelonColour = new Color(0.35f, 0.6f, 1f);
        public static readonly Color LogColour = new Color(0.96f, 0.95f, 0.88f);
        public static readonly Color KeyColour = new Color(1f, 0.82f, 0.2f);
        public static readonly Color PlanColour = new Color(0.6f, 0.82f, 1f);

        public static void CreateAll()
        {
            Directory.CreateDirectory(ItemsFolder);
            Drug(DrugType.Nalcon, "MK CAPSULE", NalconColour); // NALCON before 2026-10-26 (Bruno: no "nalcon" on screen)
            // Each drug does one thing (Bruno, 2026-10-30). PLACEHOLDER names until Bruno picks them (design §10.5).
            Drug(DrugType.Red, "SHORT CAPSULE", RedColour);
            Drug(DrugType.DFelon, "SECOND WIND PILL", DFelonColour);
            TestLog();
            TestKey();
            LabCard();
            FloorPlan(MapAtlasSetup.TestFloor1, "FLOOR PLAN: TEST FLOOR 1");
            AssetDatabase.SaveAssets();
        }

        public static ItemDefinition Drug(DrugType type)
        {
            CreateAll();
            return AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/item_{type.ToString().ToLowerInvariant()}.asset");
        }

        private static ItemDefinition Drug(DrugType type, string name, Color colour)
        {
            string effect = type == DrugType.Nalcon ? "It fills MK."
                : type == DrugType.Red ? "It takes half the SHORT bar away, and it ends a short."
                : "It gives the second wind back: the next lethal hit leaves her at 1 HP.";
            return Item(type.ToString().ToLowerInvariant(), name, ItemKind.Drug, colour, $"A drug. {effect} No drug gives HP. One slot.", "", type);
        }

        public static ItemDefinition TestLog()
        {
            return Item("test_log", "TEST LOG", ItemKind.Log, LogColour, "A placeholder record on a clipboard.",
                "PLACEHOLDER LOG\n\nSession 0. Subject L.\n\nThis page stands in for the logs of the demo: the whiteboard, the orientation program, the tape. "
                + "The writer gives the words later.\n\nIt proves the Interact key: she faces it, the game asks, she takes it, it opens here, and it stays in her inventory.",
                DrugType.Nalcon);
        }

        public static ItemDefinition TestKey()
        {
            return Item("test_key", "TEST KEY", ItemKind.Key, KeyColour, "A placeholder key card. It opens the locked door of the gym.", "", DrugType.Nalcon);
        }

        /// <summary>The key card an armed lab worker carries in the LAB TEST room (ItemCarrier). PLACEHOLDER words.</summary>
        public static ItemDefinition LabCard()
        {
            return Item("lab_card", "LAB KEY CARD", ItemKind.Key, KeyColour, "A placeholder key card from a lab worker's coat. It opens the store room of the lab.", "", DrugType.Nalcon);
        }

        /// <summary>A floor plan: the whole floor shows on the map (Bruno, 2026-10-31). A key item. PLACEHOLDER words.</summary>
        public static ItemDefinition FloorPlan(string floor, string name)
        {
            ItemDefinition item = Item("plan_" + floor, name, ItemKind.Map, PlanColour, "A printed floor plan. The whole floor shows on the map.", "", DrugType.Nalcon);
            item.mapFloor = floor;
            EditorUtility.SetDirty(item);
            return item;
        }

        private static ItemDefinition Item(string id, string name, ItemKind kind, Color colour, string description, string logText, DrugType drug)
        {
            string path = $"{ItemsFolder}/item_{id}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }
            item.itemId = id;
            item.displayName = name;
            item.kind = kind;
            item.colour = colour;
            item.description = description;
            item.logText = logText;
            item.drugType = drug;
            EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>The unlit material of a pickup: one flat colour, no light, no shadow on it.</summary>
        public static Material UnlitMaterial(ItemDefinition item)
        {
            string path = $"{MaterialsFolder}/GB_Pickup_{item.itemId}.mat";
            Shader shader = Shader.Find(UnlitShaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", item.colour);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A pickup in the room: a primitive with a trigger collider (she walks through it, the Interact key takes
        /// it), the unlit material, ItemPickup. A present object keeps its place; a stale script slot (the old
        /// walk-over DrugPickup) is removed.
        /// </summary>
        public static GameObject Pickup(GameObject parent, string name, ItemDefinition item, PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            GameObject obj = parent != null ? MenuLoopSetup.EnsureChildPrimitive(parent, name, type, position, scale) : MenuLoopSetup.EnsurePrimitive(name, type, position, scale);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
            obj.transform.rotation = rotation;
            obj.isStatic = false;
            obj.layer = LayerMask.NameToLayer("Default");
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null) collider.isTrigger = true;
            Renderer renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = UnlitMaterial(item);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ItemPickup pickup = MenuLoopSetup.EnsureComponent<ItemPickup>(obj);
            pickup.item = item;
            pickup.range = 1.6f;
            return obj;
        }
    }
}
#endif
