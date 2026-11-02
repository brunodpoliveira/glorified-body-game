using UnityEngine;

namespace GlorifiedBody.Rendering
{
    /// <summary>
    /// T26, profile B: a second body for one figure (the full-mesh 3D Studio build, its own Humanoid avatar) that follows
    /// the game body. The game body keeps its Animator, its colliders and its code; while B shows, its renderers are off
    /// and the B body plays the same state of the same controller at the same time every frame (the Humanoid retarget
    /// moves the other skeleton). Placed by RemakeLookSetup; RemakeDirector calls Use.
    /// </summary>
    [DefaultExecutionOrder(600)]
    public sealed class RemakeBodySwap : MonoBehaviour
    {
        public string rootName = "Figure_Guard";
        public GameObject bodyB;

        private GameObject root, instance;
        private Animator animA, animB;
        private Renderer[] renderersA = new Renderer[0];
        private bool showB;

        public bool ShowingB => showB && instance != null;
        public GameObject Instance => instance;

        public void Use(bool b)
        {
            showB = b;
            Ensure();
            if (root == null) return;
            foreach (Renderer r in renderersA) if (r != null) r.enabled = !ShowingB;
            if (instance != null) instance.SetActive(ShowingB);
        }

        private void Ensure()
        {
            if (root != null && (instance != null || bodyB == null)) return;
            root = GameObject.Find(rootName);
            if (root == null) return;
            animA = root.GetComponentInChildren<Animator>();
            renderersA = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (bodyB == null || instance != null) return;
            instance = Instantiate(bodyB, root.transform);
            instance.name = "BodyB";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            // the same layer as the game body (the stencil mark, the masks of the contrast check)
            int layer = renderersA.Length > 0 ? renderersA[0].gameObject.layer : root.layer;
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            animB = instance.GetComponentInChildren<Animator>();
        }

        private void LateUpdate()
        {
            if (!ShowingB || animA == null || animB == null || !animA.isActiveAndEnabled) return;
            animB.speed = animA.speed;
            int layers = Mathf.Min(animA.layerCount, animB.layerCount);
            for (int i = 0; i < layers; i++)
            {
                AnimatorStateInfo state = animA.GetCurrentAnimatorStateInfo(i);
                animB.Play(state.fullPathHash, i, state.normalizedTime);
                if (i > 0) animB.SetLayerWeight(i, animA.GetLayerWeight(i));
            }
        }
    }
}
