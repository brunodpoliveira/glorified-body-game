using UnityEngine;
using UnityEngine.AI;

namespace GlorifiedBody.Enemy
{
    /// <summary>NavMesh helpers shared by the brains.</summary>
    public static class EnemyNav
    {
        /// <summary>The NavMesh spot farthest from her, within 14 m of the start (16 directions, three rings).</summary>
        public static Vector3 FarthestFrom(Vector3 start, Vector3 her)
        {
            Vector3 best = start;
            float bestScore = Flat(best, her);
            for (int i = 0; i < 16; i++)
            {
                foreach (float r in new[] { 6f, 10f, 14f })
                {
                    Vector3 candidate = start + Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward * r;
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;
                    float score = Flat(hit.position, her);
                    if (score > bestScore) { bestScore = score; best = hit.position; }
                }
            }
            return best;
        }

        /// <summary>
        /// The farthest spot from her whose NavMesh route never passes within avoidRadius of her (a civilian does not
        /// run past her to get away). False when no such spot gains anything: the civilian is cornered.
        /// </summary>
        public static bool TryEscape(Vector3 start, Vector3 her, float avoidRadius, float minGain, out Vector3 spot)
        {
            spot = start;
            if (NavMesh.SamplePosition(start, out NavMeshHit onMesh, 1f, NavMesh.AllAreas)) start = onMesh.position;
            float startGap = Flat(start, her);
            var candidates = new System.Collections.Generic.List<(float score, Vector3 point)>();
            for (int i = 0; i < 16; i++)
            {
                foreach (float r in new[] { 4f, 7f, 10f, 14f })
                {
                    Vector3 candidate = start + Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward * r;
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;
                    float score = Flat(hit.position, her);
                    if (score - startGap >= minGain) candidates.Add((score, hit.position));
                }
            }
            candidates.Sort((x, y) => y.score.CompareTo(x.score));
            var path = new NavMeshPath();
            int tries = 0;
            foreach ((float score, Vector3 point) in candidates)
            {
                if (++tries > 24) break;
                if (!NavMesh.CalculatePath(start, point, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                if (PassesNear(path.corners, her, avoidRadius)) continue;
                spot = point;
                return true;
            }
            return false;
        }

        /// <summary>
        /// A short step back from her with a clear NavMesh line (the wary civilian walks backward, facing her).
        /// Straight away first, then up to 90 degrees to each side. False when a wall is behind it on every side.
        /// </summary>
        public static bool TryBackStep(Vector3 start, Vector3 her, float distance, out Vector3 step)
        {
            step = start;
            if (NavMesh.SamplePosition(start, out NavMeshHit onMesh, 1f, NavMesh.AllAreas)) start = onMesh.position;
            Vector3 away = start - her;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = Vector3.forward;
            away.Normalize();
            float startGap = Flat(start, her);
            foreach (float angle in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f })
            {
                Vector3 candidate = start + Quaternion.Euler(0f, angle, 0f) * away * distance;
                if (NavMesh.Raycast(start, candidate, out NavMeshHit blocked, NavMesh.AllAreas)) candidate = blocked.position;
                if (Flat(candidate, start) < distance * 0.5f) continue; // a wall right behind on this side
                if (Flat(candidate, her) < startGap + distance * 0.25f) continue; // no real gain
                step = candidate;
                return true;
            }
            return false;
        }

        private static bool PassesNear(Vector3[] corners, Vector3 her, float radius)
        {
            her.y = 0f;
            for (int i = 0; i + 1 < corners.Length; i++)
            {
                Vector3 a = corners[i], b = corners[i + 1];
                a.y = 0f;
                b.y = 0f;
                Vector3 ab = b - a;
                float t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector3.Dot(her - a, ab) / ab.sqrMagnitude);
                if (Vector3.Distance(a + ab * t, her) < radius) return true;
            }
            return false;
        }

        public static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
