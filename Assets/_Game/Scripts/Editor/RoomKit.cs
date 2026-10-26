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
