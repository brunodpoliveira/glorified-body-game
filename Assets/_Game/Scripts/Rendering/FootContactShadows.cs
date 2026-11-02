using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GlorifiedBody.Rendering
{
    /// <summary>
    /// T26 switch 3: contact shadows at the feet. URP has no contact shadow, so a soft dark decal sits on the floor
    /// under each foot of the named characters, darkest when the foot is down and gone when it lifts 25 cm, plus a
    /// wide faint one under the hips. The characters' models can change at runtime (the outfit code): the bones are
    /// found again every half second.
    /// </summary>
    public sealed class FootContactShadows : MonoBehaviour
    {
        public string[] roots = { "Lucy", "Figure_Guard", "Figure_LabWorker" };
        public Material decalMaterial;
        public float floorY = 0f;

        private sealed class Blob
        {
            public DecalProjector projector;
            public Transform bone;
            public float size;
            public float strength;
        }

        private readonly List<Blob> blobs = new List<Blob>();
        private float nextFind;

        private void OnDisable()
        {
            foreach (Blob b in blobs) if (b.projector != null) b.projector.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            foreach (Blob b in blobs) if (b.projector != null) b.projector.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextFind)
            {
                nextFind = Time.unscaledTime + 0.5f;
                Find();
            }
            foreach (Blob b in blobs)
            {
                if (b.projector == null) continue;
                if (b.bone == null || !b.bone.gameObject.activeInHierarchy)
                {
                    b.projector.fadeFactor = 0f;
                    continue;
                }
                Vector3 p = b.bone.position;
                float lift = Mathf.Max(0f, p.y - floorY - 0.06f);
                b.projector.transform.position = new Vector3(p.x, floorY + 0.05f, p.z);
                b.projector.fadeFactor = b.strength * Mathf.Clamp01(1f - lift / 0.25f);
            }
        }

        private void Find()
        {
            int used = 0;
            foreach (string name in roots)
            {
                GameObject root = GameObject.Find(name);
                Animator animator = root != null ? root.GetComponentInChildren<Animator>() : null;
                if (animator == null || !animator.isHuman) continue;
                Bind(used++, animator.GetBoneTransform(HumanBodyBones.LeftFoot), 0.32f, 0.9f);
                Bind(used++, animator.GetBoneTransform(HumanBodyBones.RightFoot), 0.32f, 0.9f);
                Bind(used++, animator.GetBoneTransform(HumanBodyBones.Hips), 0.9f, 0.35f);
            }
            for (int i = used; i < blobs.Count; i++) blobs[i].bone = null;
        }

        private void Bind(int i, Transform bone, float size, float strength)
        {
            while (blobs.Count <= i)
            {
                var go = new GameObject($"Contact_{blobs.Count}");
                go.transform.SetParent(transform, false);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var projector = go.AddComponent<DecalProjector>();
                projector.material = decalMaterial;
                projector.pivot = Vector3.zero;
                projector.drawDistance = 40f;
                blobs.Add(new Blob { projector = projector });
            }
            Blob b = blobs[i];
            b.bone = bone;
            b.size = size;
            b.strength = strength;
            b.projector.size = new Vector3(size, size, 0.4f);
        }
    }
}
