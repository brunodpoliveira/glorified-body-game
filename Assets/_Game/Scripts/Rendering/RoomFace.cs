using UnityEngine;

namespace GlorifiedBody.Rendering
{
    /// <summary>
    /// One flat face of a room (a wall, the floor, the ceiling, a panel): a quad built at Awake with its UVs
    /// in metres over tileMetres, so a 2 m texture repeats as 2 m on a 16 m wall and on a 4 m wall alike. A
    /// stretched face maps the whole picture once (an emblem, a grille, a fern card, a screen). The second UV
    /// set covers 0..1, so the lightmapper packs the face without overlaps. The mesh lives in memory only: the
    /// scene carries the numbers, never a mesh asset. The box collider is the thickness behind the face, so
    /// the tank controller stops at the wall.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RoomFace : MonoBehaviour
    {
        [Min(0.01f)] public float width = 1f;
        [Min(0.01f)] public float height = 1f;
        [Min(0.01f)] public float tileMetres = 2f;
        [Min(0f)] public float colliderDepth = 0.3f;
        [Tooltip("The whole picture once over the face, whatever its size (an emblem, a grille, a fern card).")]
        public bool stretchUv;

        private void Awake()
        {
            Build();
        }

        public void Build()
        {
            var mesh = new Mesh { name = "RoomFace " + name };
            float hw = width * 0.5f, hh = height * 0.5f;
            float u = stretchUv ? 1f : width / tileMetres, v = stretchUv ? 1f : height / tileMetres;
            mesh.vertices = new[]
            {
                new Vector3(-hw, -hh, 0f), new Vector3(hw, -hh, 0f), new Vector3(-hw, hh, 0f), new Vector3(hw, hh, 0f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(u, 0f), new Vector2(0f, v), new Vector2(u, v) };
            mesh.uv2 = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.tangents = new[] { new Vector4(1f, 0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;

            if (colliderDepth > 0f)
            {
                BoxCollider box = GetComponent<BoxCollider>();
                if (box == null) box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(width, height, colliderDepth);
                box.center = new Vector3(0f, 0f, colliderDepth * 0.5f);
            }
        }
    }
}
