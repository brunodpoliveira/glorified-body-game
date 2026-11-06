using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Destruction;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// One place to take cover. Low: he crouches at Position behind a chest-high thing and stands to fire over it. Tall: he
    /// hides at Position behind a corner and steps to Step to fire. Facing points at the thing that covers him.
    /// </summary>
    public sealed class CoverSpot
    {
        public Vector3 Position;
        public Vector3 Step;
        public Vector3 Facing;
        public bool Low;
        public Breakable Piece; // null: a thing that does not break
        public SoldierBrain Owner;

        public Vector3 FirePoint => Low ? Position : Step;
        /// <summary>A breakable spot ends when its piece breaks.</summary>
        public bool Usable => !hasPiece || (Piece != null && Piece.IsCover);
        internal bool hasPiece;
    }

    /// <summary>
    /// The cover of a scene, found by itself (Bruno, 2026-11-06: "can those be added automatically/programatically? adding them
    /// manuially one by one is a non-starter"). At the first need it walks the live NavMesh on a 0.5 m grid and casts rays at
    /// each point: a hit at hip height with a clear line over it is low cover, a hit up to the head with a clear side is a
    /// corner. So every room, prop and wall counts with no marks and no build step. Numbers PROPOSED.
    /// </summary>
    public static class CoverMap
    {
        private const float Grid = 0.5f;
        private const float HipRay = 0.7f;      // m: a thing lower than this covers no one (a bench, a bed)
        private const float OverRay = 1.35f;    // m: clear here, and the muzzle at 1.3 m fires over it
        private const float Reach = 1.2f;       // m from the point to the thing (the NavMesh ends 0.5 m from it, the grid adds up to 0.5 m)
        private const float BodyHalf = 0.3f;    // m each side: the thing must cover his width
        private const float StepOut = 0.9f;     // m to the side, to fire past a corner
        public const float ClaimRadius = 1.4f;  // m: no two soldiers closer than this
        public const float SmallPiece = 2.5f;   // m: a breakable narrower than this takes one soldier

        private static readonly List<CoverSpot> spots = new List<CoverSpot>();
        private static readonly List<CoverSpot> claimed = new List<CoverSpot>();
        private static bool built;

        public static float BuildMilliseconds { get; private set; }

        /// <summary>The spots of the scene (they build at the first call).</summary>
        public static IReadOnlyList<CoverSpot> Spots
        {
            get
            {
                if (!built) Build();
                return spots;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Clear();

        private static void Clear()
        {
            spots.Clear();
            claimed.Clear();
            built = false;
        }

        /// <summary>A soldier may take it: nobody near it, and a small piece takes one soldier.</summary>
        public static bool Free(CoverSpot spot, SoldierBrain who)
        {
            foreach (CoverSpot other in claimed)
            {
                if (other.Owner == null || other.Owner == who) continue;
                if ((other.FirePoint - spot.FirePoint).sqrMagnitude < ClaimRadius * ClaimRadius) return false;
                if (spot.hasPiece && other.Piece == spot.Piece && Mathf.Max(spot.Piece.Bounds.size.x, spot.Piece.Bounds.size.z) < SmallPiece) return false;
            }
            return true;
        }

        public static void Claim(CoverSpot spot, SoldierBrain who)
        {
            spot.Owner = who;
            if (!claimed.Contains(spot)) claimed.Add(spot);
        }

        public static void Release(CoverSpot spot)
        {
            spot.Owner = null;
            claimed.Remove(spot);
        }

        private static void Build()
        {
            built = true;
            var clock = Stopwatch.StartNew();
            NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
            int solid = LayerMask.GetMask("Default");
            var seen = new HashSet<(int, int, int)>();
            for (int t = 0; t + 2 < mesh.indices.Length; t += 3)
            {
                Vector3 a = mesh.vertices[mesh.indices[t]], b = mesh.vertices[mesh.indices[t + 1]], c = mesh.vertices[mesh.indices[t + 2]];
                int x0 = Mathf.CeilToInt(Mathf.Min(a.x, b.x, c.x) / Grid), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x, c.x) / Grid);
                int z0 = Mathf.CeilToInt(Mathf.Min(a.z, b.z, c.z) / Grid), z1 = Mathf.FloorToInt(Mathf.Max(a.z, b.z, c.z) / Grid);
                for (int ix = x0; ix <= x1; ix++)
                    for (int iz = z0; iz <= z1; iz++)
                    {
                        var flat = new Vector2(ix * Grid, iz * Grid);
                        if (!OnTriangle(flat, a, b, c, out float y)) continue;
                        if (seen.Add((ix, iz, Mathf.RoundToInt(y)))) Scan(new Vector3(flat.x, y, flat.y), solid);
                    }
            }
            clock.Stop();
            BuildMilliseconds = (float)clock.Elapsed.TotalMilliseconds;
            RunTelemetry.Event("cover_map", $"{spots.Count} spots, {seen.Count} points, {BuildMilliseconds:0} ms");
        }

        private static bool OnTriangle(Vector2 p, Vector3 a, Vector3 b, Vector3 c, out float y)
        {
            y = 0f;
            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(d) < 0.0001f) return false;
            float u = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.y - c.z)) / d;
            float v = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.y - c.z)) / d;
            float w = 1f - u - v;
            if (u < 0f || v < 0f || w < 0f) return false;
            y = u * a.y + v * b.y + w * c.y;
            return true;
        }

        private static readonly Vector3[] Directions =
        {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            new Vector3(0.7071f, 0f, 0.7071f), new Vector3(-0.7071f, 0f, 0.7071f), new Vector3(0.7071f, 0f, -0.7071f), new Vector3(-0.7071f, 0f, -0.7071f),
        };

        private static void Scan(Vector3 p, int solid)
        {
            foreach (Vector3 d in Directions)
            {
                if (!Ray(p + Vector3.up * HipRay, d, solid, out RaycastHit hit)) continue;
                if (hit.collider.GetComponentInParent<Health>() != null) continue; // a crate to throw is no cover
                Breakable piece = hit.collider.GetComponentInParent<Breakable>();
                if (piece != null && !piece.IsCover) continue; // a door leaf, a table, her own screen
                Vector3 side = Vector3.Cross(Vector3.up, d);
                bool over = !Ray(p + Vector3.up * OverRay, d, solid, out _);
                if (over) AddLow(p, d, side, piece, solid);
                else if (piece == null) AddCorner(p, d, side, solid);
            }
        }

        private static void AddLow(Vector3 p, Vector3 d, Vector3 side, Breakable piece, int solid)
        {
            Vector3 hip = p + Vector3.up * HipRay;
            if (!Ray(hip + side * BodyHalf, d, solid, out _) || !Ray(hip - side * BodyHalf, d, solid, out _)) return;
            spots.Add(new CoverSpot { Position = p, Step = p, Facing = d, Low = true, Piece = piece, hasPiece = piece != null });
        }

        private static void AddCorner(Vector3 p, Vector3 d, Vector3 side, int solid)
        {
            foreach (float s in new[] { 1f, -1f })
            {
                Vector3 toCorner = side * s;
                Vector3 chest = p + Vector3.up * 1.2f;
                if (!Ray(chest - toCorner * BodyHalf, d, solid, out _) || !Ray(chest + toCorner * BodyHalf, d, solid, out _)) continue; // he hides whole
                Vector3 step = p + toCorner * StepOut;
                if (Ray(step + Vector3.up * 1.2f, d, solid, out _)) continue; // no corner here: the thing goes on
                if (!NavMesh.SamplePosition(step, out NavMeshHit on, 0.3f, NavMesh.AllAreas)) continue;
                if (NavMesh.Raycast(p, on.position, out _, NavMesh.AllAreas)) continue;
                spots.Add(new CoverSpot { Position = p, Step = on.position, Facing = d });
                return;
            }
        }

        private static bool Ray(Vector3 from, Vector3 direction, int solid, out RaycastHit hit) =>
            Physics.Raycast(from, direction, out hit, Reach, solid, QueryTriggerInteraction.Ignore);
    }
}
