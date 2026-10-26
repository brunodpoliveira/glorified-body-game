using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Gore.ThatIndividual;

namespace GlorifiedBody.Gore
{
    /// <summary>
    /// The piece that flies off a limb cut (Bruno, 2026-10-24: "flying piece too"). Before the bone shrinks, the
    /// skinned mesh is baked in its pose of the moment, and the triangles that the cut bone and its children carry
    /// (half the weight or more) become a mesh of their own, with the body's materials, a box collider and a
    /// rigidbody. It flies away from her, the vendor bone ends and meat sit on its open end, and a gush follows it.
    /// The piece stays for the room: when it lies still it freezes (kinematic, no collider). It needs a readable
    /// mesh (CharacterImportSettings). The physics root is on BodyPart (it hits the room and the bodies, not her),
    /// the mesh on the body's layer (the character mark). Numbers PROPOSED.
    /// </summary>
    public sealed class SeveredLimb : MonoBehaviour
    {
        private const float FlySpeed = 4.5f;      // m/s away from her
        private const float LiftSpeed = 2.5f;     // m/s up
        private const float SpinSpeed = 9f;       // rad/s at most
        private const float MassPerMetre = 4f;    // kg: a forearm about 1.5, a whole arm about 3
        private const float SettleSpeed = 0.15f;  // m/s
        private const float SettleSeconds = 0.5f;
        private const float MinFlightSeconds = 0.6f;
        private const float GushSeconds = 3f;
        private const float MinWeight = 0.5f;

        public static int Spawned { get; private set; }
        public static SeveredLimb Last { get; private set; }
        /// <summary>Every piece in the room (the save keeps where each lies).</summary>
        public static IReadOnlyList<SeveredLimb> All { get { all.RemoveAll(l => l == null); return all; } }
        private static readonly List<SeveredLimb> all = new List<SeveredLimb>();

        /// <summary>The body it came from and the cut bone.</summary>
        public GameObject Victim { get; private set; }
        public Transform Bone { get; private set; }
        /// <summary>The bone's turn in the world when the mesh was baked: the piece's vertices lie in that frame.</summary>
        public Quaternion BakedBoneTurn { get; private set; }
        /// <summary>The bone's turn that the piece carries now (a save keeps it, a load turns the new piece to it).</summary>
        public Quaternion BoneTurn => transform.rotation * BakedBoneTurn;

        public bool Settled { get; private set; }
        public int Triangles { get; private set; }
        /// <summary>The centre of the piece in the world at the moment of the cut (the tests compare it with the bone).</summary>
        public Vector3 CutCentre { get; private set; }

        private Rigidbody body;
        private Collider box;
        private float born, still;

        /// <summary>Makes the piece that the cut bone carries, before the bone shrinks. Null when the mesh cannot give one.</summary>
        public static SeveredLimb Cut(GameObject victim, Transform bone, Vector3 away, ThatIndividualGoreProfile profile)
        {
            if (victim == null || bone == null) return null;
            foreach (SkinnedMeshRenderer skin in victim.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                SeveredLimb limb = Cut(skin, bone, away, profile, false);
                if (limb != null)
                {
                    limb.Victim = victim;
                    return limb;
                }
            }
            return null;
        }

        /// <summary>
        /// A piece that a load puts back: cut again from the body (its bone not yet a stump), laid at the saved
        /// place, still, with its meat end and no gush. The body lies in its death pose now, so the piece is turned
        /// to carry its bone as the saved piece did (boneTurn). The bend at the elbow or the wrist can still differ.
        /// </summary>
        public static SeveredLimb Restore(GameObject victim, Transform bone, Vector3 position, Quaternion boneTurn, ThatIndividualGoreProfile profile)
        {
            if (victim == null || bone == null) return null;
            foreach (SkinnedMeshRenderer skin in victim.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                SeveredLimb limb = Cut(skin, bone, Vector3.zero, profile, true);
                if (limb == null) continue;
                limb.Victim = victim;
                limb.transform.SetPositionAndRotation(position, boneTurn * Quaternion.Inverse(limb.BakedBoneTurn));
                return limb;
            }
            return null;
        }

        private static SeveredLimb Cut(SkinnedMeshRenderer skin, Transform bone, Vector3 away, ThatIndividualGoreProfile profile, bool restored)
        {
            Mesh source = skin.sharedMesh;
            if (source == null || !source.isReadable) return null;
            Transform[] bones = skin.bones;
            var inLimb = new bool[bones.Length];
            bool any = false;
            for (int i = 0; i < bones.Length; i++)
            {
                inLimb[i] = bones[i] != null && (bones[i] == bone || bones[i].IsChildOf(bone));
                any |= inLimb[i];
            }
            if (!any) return null;
            BoneWeight[] weights = source.boneWeights;
            if (weights.Length != source.vertexCount) return null;
            var keep = new bool[weights.Length];
            for (int v = 0; v < weights.Length; v++)
            {
                BoneWeight w = weights[v];
                float sum = (In(inLimb, w.boneIndex0) ? w.weight0 : 0f) + (In(inLimb, w.boneIndex1) ? w.weight1 : 0f)
                          + (In(inLimb, w.boneIndex2) ? w.weight2 : 0f) + (In(inLimb, w.boneIndex3) ? w.weight3 : 0f);
                keep[v] = sum >= MinWeight;
            }

            // The pose of this frame. Baked without the scale, the vertices are in metres around the renderer's
            // position and turn: the rig's scale (the mixamo chain) must not apply a second time.
            var baked = new Mesh();
            skin.BakeMesh(baked, false);
            Vector3[] positions = baked.vertices;
            Vector3[] normals = baked.normals;
            Vector2[] uvs = source.uv;
            Destroy(baked);
            Matrix4x4 toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
            Vector3 pivot = bone.position;

            var map = new Dictionary<int, int>();
            var outPositions = new List<Vector3>();
            var outNormals = new List<Vector3>();
            var outUvs = new List<Vector2>();
            var subMeshes = new List<List<int>>();
            var materials = new List<Material>();
            Material[] skinMaterials = skin.sharedMaterials;
            int triangles = 0;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] tris = source.GetTriangles(s);
                var kept = new List<int>();
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int count = (keep[tris[t]] ? 1 : 0) + (keep[tris[t + 1]] ? 1 : 0) + (keep[tris[t + 2]] ? 1 : 0);
                    if (count < 2) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int index = tris[t + k];
                        if (!map.TryGetValue(index, out int mapped))
                        {
                            mapped = outPositions.Count;
                            map[index] = mapped;
                            outPositions.Add(toWorld.MultiplyPoint3x4(positions[index]) - pivot);
                            outNormals.Add(normals.Length > index ? toWorld.MultiplyVector(normals[index]).normalized : Vector3.up);
                            outUvs.Add(uvs.Length > index ? uvs[index] : Vector2.zero);
                        }
                        kept.Add(mapped);
                    }
                    triangles++;
                }
                if (kept.Count == 0) continue;
                subMeshes.Add(kept);
                materials.Add(skinMaterials.Length > s ? skinMaterials[s] : (skinMaterials.Length > 0 ? skinMaterials[0] : null));
            }
            if (triangles == 0) return null;

            var mesh = new Mesh { name = "SeveredLimb_" + bone.name };
            mesh.SetVertices(outPositions);
            mesh.SetNormals(outNormals);
            mesh.SetUVs(0, outUvs);
            mesh.subMeshCount = subMeshes.Count;
            for (int s = 0; s < subMeshes.Count; s++) mesh.SetTriangles(subMeshes[s], s);
            mesh.RecalculateBounds();

            var root = new GameObject("SeveredLimb");
            int bodyPart = LayerMask.NameToLayer("BodyPart");
            root.layer = bodyPart >= 0 ? bodyPart : 0;
            root.transform.position = pivot;
            var view = new GameObject("Mesh");
            view.layer = skin.gameObject.layer;
            view.transform.SetParent(root.transform, false);
            view.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = view.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials.ToArray();

            var collider = root.AddComponent<BoxCollider>();
            Bounds bounds = mesh.bounds;
            collider.center = bounds.center;
            collider.size = Vector3.Max(bounds.size, Vector3.one * 0.06f);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.5f, bounds.size.magnitude * MassPerMetre);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Vector3 flat = Vector3.ProjectOnPlane(away, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f) flat = Random.insideUnitSphere;
            flat.y = 0f;
            rb.linearVelocity = flat.normalized * FlySpeed + Vector3.up * LiftSpeed;
            rb.angularVelocity = Random.insideUnitSphere * SpinSpeed;

            // The open end: the bone ends and meat face back toward the body, and a gush follows the piece.
            Vector3 along = bone.parent != null ? bone.position - bone.parent.position : Vector3.up;
            Quaternion inward = Quaternion.LookRotation(along.sqrMagnitude > 0.0001f ? -along.normalized : Vector3.down);
            if (profile != null)
            {
                GameObject meat = Pick(profile.limbStumps);
                if (meat != null)
                {
                    GameObject end = Instantiate(meat, pivot, inward, root.transform);
                    end.name = "LimbEnd";
                    if (restored) GruesomeDeathFx.Still(end);
                    foreach (Collider c in end.GetComponentsInChildren<Collider>()) Destroy(c);
                }
                GameObject gush = restored ? null : Pick(profile.limbSprays);
                if (gush != null)
                {
                    GameObject fx = Instantiate(gush, pivot, inward, root.transform);
                    foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Play(true);
                    Destroy(fx, GushSeconds);
                }
            }

            var limb = root.AddComponent<SeveredLimb>();
            limb.body = rb;
            limb.box = collider;
            limb.born = Time.time;
            limb.Triangles = triangles;
            limb.CutCentre = pivot + bounds.center;
            limb.Bone = bone;
            limb.BakedBoneTurn = bone.rotation;
            all.Add(limb);
            if (restored)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                collider.enabled = false;
                limb.Settled = true;
                RunTelemetry.Event("gore_limb_restored", $"{bone.name}:{triangles} tris");
                return limb;
            }
            Spawned++;
            Last = limb;
            RunTelemetry.Event("gore_limb_flies", $"{bone.name}:{triangles} tris");
            return limb;
        }

        private static bool In(bool[] inLimb, int index) => index >= 0 && index < inLimb.Length && inLimb[index];

        private static GameObject Pick(GameObject[] set) => set == null || set.Length == 0 ? null : set[Random.Range(0, set.Length)];

        private void FixedUpdate()
        {
            if (Settled || body == null) return;
            if (Time.time - born < MinFlightSeconds) return;
            still = body.linearVelocity.magnitude < SettleSpeed ? still + Time.fixedDeltaTime : 0f;
            if (still < SettleSeconds) return;
            // It stays where it fell, for the room.
            Settled = true;
            body.isKinematic = true;
            if (box != null) box.enabled = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Spawned = 0;
            Last = null;
            all.Clear();
        }
    }
}
