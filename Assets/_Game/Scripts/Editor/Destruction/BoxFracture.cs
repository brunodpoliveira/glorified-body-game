#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Editor.Destruction
{
    /// <summary>
    /// Cuts a box into convex pieces with flat cuts, in the editor (the study, 2026-10-24: "Prefracture, so the
    /// break costs no CPU at runtime"). Each cut splits the largest piece by a plane through a point near its
    /// middle, turned at random; the cut faces are closed. A seed gives the same pieces every build. Each piece is
    /// a mesh around its own centre with two submeshes: 0 the outside faces, 1 the cut faces (the raw inside).
    /// UVs: a box projection in metres (the texture scale of the room). The pieces are convex, so a convex
    /// MeshCollider fits each one. OpenFracture (MIT) is the tool for closed models; the greybox needs only boxes.
    /// </summary>
    public static class BoxFracture
    {
        public sealed class Piece
        {
            public Mesh mesh;
            public Vector3 centre;  // in the box's space
            public float volume;
        }

        private sealed class Face
        {
            public List<Vector3> points = new List<Vector3>();
            public Vector3 normal;
            public bool cut;
        }

        private sealed class Solid
        {
            public List<Face> faces = new List<Face>();
            public float Volume()
            {
                // The divergence theorem on the triangle fans, around the origin.
                float v = 0f;
                foreach (Face f in faces)
                    for (int i = 1; i + 1 < f.points.Count; i++)
                        v += Vector3.Dot(f.points[0], Vector3.Cross(f.points[i], f.points[i + 1])) / 6f;
                return Mathf.Abs(v);
            }
            public Vector3 Centre()
            {
                Vector3 sum = Vector3.zero;
                int n = 0;
                foreach (Face f in faces) foreach (Vector3 p in f.points) { sum += p; n++; }
                return n > 0 ? sum / n : Vector3.zero;
            }
            public Bounds Bounds()
            {
                var b = new Bounds(faces[0].points[0], Vector3.zero);
                foreach (Face f in faces) foreach (Vector3 p in f.points) b.Encapsulate(p);
                return b;
            }
        }

        /// <summary>Cuts a box of the size (centred on the origin) into the number of pieces. Flat pieces (a panel, a top) are cut across their thin side only.</summary>
        public static List<Piece> Split(Vector3 size, int count, int seed, string name)
        {
            var random = new System.Random(seed);
            var solids = new List<Solid> { Box(size) };
            int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            bool flat = size[thin] < 0.35f * Mathf.Min(size[(thin + 1) % 3], size[(thin + 2) % 3]);
            int guard = 0;
            while (solids.Count < count && guard++ < count * 8)
            {
                solids.Sort((a, b) => b.Volume().CompareTo(a.Volume()));
                Solid big = solids[0];
                Bounds bounds = big.Bounds();
                Vector3 point = big.Centre() + Vector3.Scale(bounds.extents, new Vector3(Rand(random, -0.35f, 0.35f), Rand(random, -0.35f, 0.35f), Rand(random, -0.35f, 0.35f)));
                Vector3 normal = new Vector3(Rand(random, -1f, 1f), Rand(random, -1f, 1f), Rand(random, -1f, 1f));
                if (flat) normal[thin] *= 0.15f; // cut across the panel, not along its face
                // Cut the longest side more often: the pieces stay chunky.
                Vector3 e = bounds.extents;
                normal = Vector3.Scale(normal, new Vector3(e.x, e.y, e.z));
                if (normal.sqrMagnitude < 0.0001f) normal = Vector3.right;
                normal.Normalize();
                if (!Cut(big, point, normal, out Solid a, out Solid b)) continue;
                if (a.Volume() < big.Volume() * 0.08f || b.Volume() < big.Volume() * 0.08f) continue; // no slivers
                solids.RemoveAt(0);
                solids.Add(a);
                solids.Add(b);
            }

            var pieces = new List<Piece>();
            for (int i = 0; i < solids.Count; i++)
            {
                Vector3 centre = solids[i].Centre();
                pieces.Add(new Piece { mesh = ToMesh(solids[i], centre, $"{name}_{i}"), centre = centre, volume = solids[i].Volume() });
            }
            return pieces;
        }

        private static float Rand(System.Random r, float min, float max) => (float)(min + r.NextDouble() * (max - min));

        private static Solid Box(Vector3 size)
        {
            Vector3 h = size * 0.5f;
            var s = new Solid();
            Vector3[] c =
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z),
            };
            AddFace(s, Vector3.back, c[0], c[3], c[2], c[1]);
            AddFace(s, Vector3.forward, c[4], c[5], c[6], c[7]);
            AddFace(s, Vector3.left, c[0], c[4], c[7], c[3]);
            AddFace(s, Vector3.right, c[1], c[2], c[6], c[5]);
            AddFace(s, Vector3.down, c[0], c[1], c[5], c[4]);
            AddFace(s, Vector3.up, c[3], c[7], c[6], c[2]);
            return s;
        }

        private static void AddFace(Solid s, Vector3 normal, params Vector3[] points)
        {
            var f = new Face { normal = normal };
            f.points.AddRange(points);
            s.faces.Add(f);
        }

        /// <summary>Splits a convex solid by a plane. "a" is the side the normal points to.</summary>
        private static bool Cut(Solid solid, Vector3 point, Vector3 normal, out Solid a, out Solid b)
        {
            a = new Solid();
            b = new Solid();
            var section = new List<Vector3>();
            const float eps = 1e-5f;
            foreach (Face face in solid.faces)
            {
                var front = new List<Vector3>();
                var back = new List<Vector3>();
                int n = face.points.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = face.points[i];
                    Vector3 q = face.points[(i + 1) % n];
                    float dp = Vector3.Dot(p - point, normal);
                    float dq = Vector3.Dot(q - point, normal);
                    if (dp >= -eps) front.Add(p);
                    if (dp <= eps) back.Add(p);
                    if ((dp > eps && dq < -eps) || (dp < -eps && dq > eps))
                    {
                        Vector3 x = p + (q - p) * (dp / (dp - dq));
                        front.Add(x);
                        back.Add(x);
                        section.Add(x);
                    }
                    else if (Mathf.Abs(dp) <= eps) section.Add(p);
                }
                if (front.Count >= 3) a.faces.Add(new Face { points = front, normal = face.normal, cut = face.cut });
                if (back.Count >= 3) b.faces.Add(new Face { points = back, normal = face.normal, cut = face.cut });
            }
            if (a.faces.Count < 3 || b.faces.Count < 3) return false;
            List<Vector3> cap = Order(Dedupe(section), normal);
            if (cap.Count < 3) return false;
            var capA = new Face { normal = -normal, cut = true };
            capA.points.AddRange(cap);
            capA.points.Reverse();
            var capB = new Face { normal = normal, cut = true };
            capB.points.AddRange(cap);
            a.faces.Add(capA);
            b.faces.Add(capB);
            return true;
        }

        private static List<Vector3> Dedupe(List<Vector3> points)
        {
            var result = new List<Vector3>();
            foreach (Vector3 p in points)
            {
                bool seen = false;
                foreach (Vector3 q in result) if ((p - q).sqrMagnitude < 1e-8f) { seen = true; break; }
                if (!seen) result.Add(p);
            }
            return result;
        }

        /// <summary>Orders the section points around their centre, counter-clockwise seen along the normal.</summary>
        private static List<Vector3> Order(List<Vector3> points, Vector3 normal)
        {
            if (points.Count < 3) return points;
            Vector3 centre = Vector3.zero;
            foreach (Vector3 p in points) centre += p;
            centre /= points.Count;
            Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(normal, u);
            points.Sort((p, q) =>
            {
                float ap = Mathf.Atan2(Vector3.Dot(p - centre, v), Vector3.Dot(p - centre, u));
                float aq = Mathf.Atan2(Vector3.Dot(q - centre, v), Vector3.Dot(q - centre, u));
                return ap.CompareTo(aq);
            });
            return points;
        }

        private static Mesh ToMesh(Solid solid, Vector3 centre, string name)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var outside = new List<int>();
            var inside = new List<int>();
            foreach (Face f in solid.faces)
            {
                // The winding from the points; the normal decides which way is out.
                Vector3 n = f.normal;
                Vector3 wind = Vector3.Cross(f.points[1] - f.points[0], f.points[2] - f.points[0]);
                bool flip = Vector3.Dot(wind, n) < 0f;
                int start = vertices.Count;
                foreach (Vector3 p in f.points)
                {
                    vertices.Add(p - centre);
                    normals.Add(n);
                    uvs.Add(BoxUv(p, n));
                }
                List<int> target = f.cut ? inside : outside;
                for (int i = 1; i + 1 < f.points.Count; i++)
                {
                    // A front face in Unity: cross(b - a, c - a) points out of the solid.
                    if (!flip) { target.Add(start); target.Add(start + i); target.Add(start + i + 1); }
                    else { target.Add(start); target.Add(start + i + 1); target.Add(start + i); }
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(outside, 0);
            mesh.SetTriangles(inside, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static Vector2 BoxUv(Vector3 p, Vector3 n)
        {
            Vector3 a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            if (a.x >= a.y && a.x >= a.z) return new Vector2(p.z, p.y);
            if (a.y >= a.z) return new Vector2(p.x, p.z);
            return new Vector2(p.x, p.y);
        }
    }
}
#endif
