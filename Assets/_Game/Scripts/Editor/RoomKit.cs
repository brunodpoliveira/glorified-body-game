#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.AI.Navigation;
using GlorifiedBody.Enemy;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The pieces a room builder uses (2026-10-26, the design sweep's rank 5): static boxes, the NavMesh, the enemy and
    /// civilian markers (EnemySpawn: the scene keeps no vendor data, so the room can be in git), exits and entries, a
    /// trigger with its sequence. The game rooms of the demo are built from these.
    /// </summary>
    public static class RoomKit
    {
        /// <summary>A static box of the room (a floor, a wall, a pillar) on the Default layer: it blocks, and the NavMesh reads it.</summary>
        public static GameObject Box(GameObject parent, string name, Vector3 position, Vector3 scale)
        {
            GameObject obj = MenuLoopSetup.EnsureChildPrimitive(parent, name, PrimitiveType.Cube, position, scale);
            obj.isStatic = true;
            obj.layer = LayerMask.NameToLayer("Default");
            return obj;
        }

        /// <summary>
        /// An enemy or a civilian of the kind at the place: a marker the figure replaces at the room's load. A figure of
        /// the same name from an older build (the factory put the whole figure in the scene) is removed.
        /// </summary>
        public static EnemySpawn Spawn(GameObject parent, string name, string kind, Vector3 position, float yaw, int variant = -1)
        {
            Transform old = parent != null ? parent.transform.Find(name) : null;
            GameObject obj = old != null ? old.gameObject : GameObject.Find(name);
            if (obj != null && obj.GetComponent<EnemySpawn>() == null)
            {
                Object.DestroyImmediate(obj);
                obj = null;
            }
            if (obj == null) obj = new GameObject(name);
            if (parent != null) obj.transform.SetParent(parent.transform, true);
            obj.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            EnemySpawn spawn = MenuLoopSetup.EnsureComponent<EnemySpawn>(obj);
            spawn.kind = kind;
            spawn.variant = variant;
            EditorUtility.SetDirty(spawn);
            return spawn;
        }

        /// <summary>The NavMesh of the room's colliders (the figures stay out of it), saved at the path (our data: in git).</summary>
        public static void BakeNavMesh(GameObject room, string assetPath)
        {
            NavMeshSurface surface = MenuLoopSetup.EnsureComponent<NavMeshSurface>(room);
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Default");
            AssetDatabase.DeleteAsset(assetPath);
            NavAgentSettings.Ensure(); // the desk is no step
            surface.BuildNavMesh();
            if (surface.navMeshData != null) AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
            else Debug.LogError("[RoomKit] the NavMesh did not build: " + assetPath);
        }

        /// <summary>A way out: a trigger box past a doorway to the scene's entry (RoomExit). On "Ignore Raycast": no trigger blocks a sight line.</summary>
        public static RoomExit Exit(string name, Vector3 position, Vector3 size, string scene, string entry, string requiredFlag = "")
        {
            GameObject obj = GameObject.Find(name) ?? new GameObject(name);
            obj.transform.position = position;
            BoxCollider box = MenuLoopSetup.EnsureComponent<BoxCollider>(obj);
            box.isTrigger = true;
            box.size = size;
            obj.layer = LayerMask.NameToLayer("Ignore Raycast");
            RoomExit exit = MenuLoopSetup.EnsureComponent<RoomExit>(obj);
            exit.targetScene = scene;
            exit.targetEntry = entry;
            exit.requiredFlag = requiredFlag;
            return exit;
        }

        /// <summary>Where she comes in from another room: inside the doorway, facing into the room.</summary>
        public static RoomEntry Entry(string name, Vector3 position, float yaw)
        {
            GameObject obj = GameObject.Find(name) ?? new GameObject(name);
            obj.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return MenuLoopSetup.EnsureComponent<RoomEntry>(obj);
        }

        /// <summary>
        /// A sign on a wall (2026-10-31; the demo plan §2.3: "in-world text, cheap, and it does a lot"): a thin dark plate
        /// with the words on it, a world-space canvas with a legacy Text (no extra package), so the camera reads it from
        /// its fixed place. The plate stands at the point, its face to the facing (the room side). Size in metres.
        /// </summary>
        public static GameObject Sign(GameObject parent, string name, string words, Vector3 point, Vector3 facing, Vector2 size, int fontSize = 64)
        {
            Transform old = parent != null ? parent.transform.Find(name) : null;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var sign = new GameObject(name);
            if (parent != null) sign.transform.SetParent(parent.transform, false);
            sign.transform.SetPositionAndRotation(point, Quaternion.LookRotation(-facing.normalized, Vector3.up));
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Plate";
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.transform.SetParent(sign.transform, false);
            plate.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            plate.transform.localScale = new Vector3(size.x, size.y, 0.02f);
            plate.GetComponent<Renderer>().sharedMaterial = ItemAssets.UnlitMaterial("sign_plate", new Color(0.06f, 0.06f, 0.07f));
            var canvasObj = new GameObject("Words", typeof(RectTransform));
            canvasObj.transform.SetParent(sign.transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = canvasObj.GetComponent<RectTransform>();
            const float pixelsPerMetre = 400f;
            rect.sizeDelta = size * pixelsPerMetre * 0.9f;
            rect.localScale = Vector3.one / pixelsPerMetre;
            rect.localPosition = new Vector3(0f, 0f, -0.002f);
            rect.localRotation = Quaternion.identity;
            var textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(canvasObj.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.88f, 0.86f, 0.8f);
            text.text = words;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return sign;
        }

        /// <summary>The room's music key on "[Music]". A missing script left there by an older build goes first.</summary>
        public static Audio.RoomMusic Music(string key)
        {
            GameObject music = MenuLoopSetup.EnsureRoot("[Music]");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(music);
            Audio.RoomMusic room = MenuLoopSetup.EnsureComponent<Audio.RoomMusic>(music);
            room.Key = key;
            EditorUtility.SetDirty(room);
            return room;
        }

        /// <summary>A sequence of steps with its done flag (empty: "seq_" + its name).</summary>
        public static ScriptedSequence Sequence(string name, string doneFlag, params ScriptStep[] steps)
        {
            GameObject obj = GameObject.Find(name) ?? new GameObject(name);
            ScriptedSequence sequence = MenuLoopSetup.EnsureComponent<ScriptedSequence>(obj);
            sequence.doneFlag = doneFlag;
            sequence.steps = new List<ScriptStep>(steps);
            EditorUtility.SetDirty(sequence);
            return sequence;
        }

        /// <summary>A box she walks into, which starts the sequence.</summary>
        public static ScriptTrigger Trigger(string name, Vector3 position, Vector3 size, ScriptedSequence sequence)
        {
            GameObject obj = GameObject.Find(name) ?? new GameObject(name);
            obj.transform.position = position;
            BoxCollider box = MenuLoopSetup.EnsureComponent<BoxCollider>(obj);
            box.isTrigger = true;
            box.size = size;
            obj.layer = LayerMask.NameToLayer("Ignore Raycast");
            ScriptTrigger trigger = MenuLoopSetup.EnsureComponent<ScriptTrigger>(obj);
            trigger.sequence = sequence;
            return trigger;
        }
    }
}
#endif
