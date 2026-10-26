#if UNITY_EDITOR
using GlorifiedBody.Editor.Destruction;
using GlorifiedBody.Saving;
using UnityEngine;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The save machine and the checkpoint of a room, for the room builders. The machine is a placeholder: a steel
    /// cabinet 1.1 m tall with a lit screen on its front, the SavePoint on it. The checkpoint is an empty box (its
    /// gizmo shows it in the editor).
    /// </summary>
    public static class SaveAssets
    {
        /// <summary>A save machine at the position, its front (the screen, her spawn side) turned to the yaw.</summary>
        public static GameObject SavePoint(string name, Vector3 position, float yaw)
        {
            GameObject machine = GameObject.Find(name);
            if (machine == null) machine = new GameObject(name);
            machine.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            for (int i = machine.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(machine.transform.GetChild(i).gameObject);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Cabinet";
            body.transform.SetParent(machine.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            body.transform.localScale = new Vector3(0.6f, 1.1f, 0.45f);
            body.GetComponent<MeshRenderer>().sharedMaterial = BreakableAssets.Steel;
            body.isStatic = true;

            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "Screen";
            screen.transform.SetParent(machine.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0.85f, 0.23f);
            screen.transform.localScale = new Vector3(0.42f, 0.28f, 0.02f);
            screen.GetComponent<MeshRenderer>().sharedMaterial = BreakableAssets.LampOn;
            Object.DestroyImmediate(screen.GetComponent<Collider>());

            SavePoint point = machine.GetComponent<SavePoint>() ?? machine.AddComponent<SavePoint>();
            point.range = 1.6f;
            point.point = screen.transform;
            return machine;
        }

        /// <summary>A checkpoint box on the floor at the position; a load starts her at its middle, facing the yaw.</summary>
        public static GameObject Checkpoint(string name, Vector3 position, float yaw, Vector3 size)
        {
            GameObject box = GameObject.Find(name);
            if (box == null) box = new GameObject(name);
            box.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            CheckpointPoint checkpoint = box.GetComponent<CheckpointPoint>() ?? box.AddComponent<CheckpointPoint>();
            checkpoint.size = size;
            return box;
        }
    }
}
#endif
