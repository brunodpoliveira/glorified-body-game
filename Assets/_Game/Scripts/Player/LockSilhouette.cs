using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GlorifiedBody.Combat;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// The locked target's silhouette (Bruno, 2026-11-16: "if the enemy is not visible by the camera the enemy silhouette must
    /// be highlighted"). Each mesh of the target gets a twin with the GB/Silhouette shader: the same mesh, the same bones, a
    /// flat colour that draws only where something nearer hides the body. The twins show only while the camera does not see
    /// the target (the lines from the camera to its chest and to its head are both blocked): on a body in view, a twin would
    /// also draw where one limb hides another. A twin is a child of its mesh, so it moves with the body, a ragdoll too.
    /// LockOnController keeps it on its target.
    /// </summary>
    public sealed class LockSilhouette
    {
        public static readonly Color Colour = new Color(1f, 0.25f, 0.2f, 1f);

        private static Material material;
        private readonly List<GameObject> twins = new List<GameObject>();

        public LockOnTargetable Target { get; private set; }
        /// <summary>The twins draw now: the camera does not see the target.</summary>
        public bool Showing { get; private set; }

        /// <summary>Puts the silhouette on this target (null: on none). The twins of the last target go.</summary>
        public void Follow(LockOnTargetable target)
        {
            if (target != Target)
            {
                Clear();
                Target = target;
                if (target != null && Material() != null)
                    foreach (Renderer source in target.GetComponentsInChildren<Renderer>())
                    {
                        if (source == null || !source.enabled) continue;
                        GameObject twin = source is SkinnedMeshRenderer skinned ? Twin(skinned) : source is MeshRenderer mesh ? Twin(mesh) : null;
                        if (twin != null) twins.Add(twin);
                    }
            }
            Show(Target != null && Hidden(Target));
        }

        private void Show(bool show)
        {
            if (show == Showing) return;
            Showing = show;
            foreach (GameObject twin in twins)
                if (twin != null) twin.SetActive(show);
        }

        /// <summary>Both lines from the camera, to the chest and to the head, end on a wall or a prop (the Default layer).</summary>
        private static bool Hidden(LockOnTargetable target)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            int solid = LayerMask.GetMask("Default");
            Vector3 eye = cam.transform.position;
            Vector3 chest = target.GetLockPoint();
            Vector3 head = target.transform.position + Vector3.up * Mathf.Max(chest.y - target.transform.position.y + 0.45f, 0.5f);
            return Physics.Linecast(eye, chest, solid, QueryTriggerInteraction.Ignore) && Physics.Linecast(eye, head, solid, QueryTriggerInteraction.Ignore);
        }

        private void Clear()
        {
            foreach (GameObject twin in twins)
                if (twin != null) Object.Destroy(twin);
            twins.Clear();
            Target = null;
            Showing = false;
        }

        private static Material Material()
        {
            if (material != null) return material;
            Shader shader = Resources.Load<Shader>("Shaders/GB_Silhouette");
            if (shader == null) return null;
            material = new Material(shader) { name = "LockSilhouette" };
            material.SetColor("_Color", Colour);
            return material;
        }

        private static GameObject Twin(SkinnedMeshRenderer source)
        {
            if (source.sharedMesh == null) return null;
            var twin = new GameObject(source.name + "_Silhouette");
            twin.layer = source.gameObject.layer;
            twin.transform.SetParent(source.transform, false);
            SkinnedMeshRenderer r = twin.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = source.sharedMesh;
            r.bones = source.bones;
            r.rootBone = source.rootBone;
            r.localBounds = source.localBounds;
            r.updateWhenOffscreen = true;
            Dress(r, source.sharedMesh.subMeshCount);
            twin.SetActive(false);
            return twin;
        }

        private static GameObject Twin(MeshRenderer source)
        {
            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return null;
            var twin = new GameObject(source.name + "_Silhouette");
            twin.layer = source.gameObject.layer;
            twin.transform.SetParent(source.transform, false);
            twin.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            Dress(twin.AddComponent<MeshRenderer>(), filter.sharedMesh.subMeshCount);
            twin.SetActive(false);
            return twin;
        }

        /// <summary>One silhouette material for each submesh, no shadows: the twin is only a colour.</summary>
        private static void Dress(Renderer r, int subMeshes)
        {
            var mats = new Material[Mathf.Max(1, subMeshes)];
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
