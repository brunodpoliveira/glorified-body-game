using UnityEngine;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// Pure steering math for the input bot: world direction to camera-relative stick axes,
    /// camera look deltas, path waypoint advance, and simple combat choices. No scene access.
    /// </summary>
    public static class BotSteering
    {
        /// <summary>
        /// Stick axes (x = right, y = forward) that make MomentumLocomotion move along worldDir,
        /// given the camera forward. Length = planar length of worldDir, clamped to 1.
        /// </summary>
        public static Vector2 MoveAxesFor(Vector3 worldDir, Vector3 cameraForward)
        {
            Vector3 dir = new Vector3(worldDir.x, 0f, worldDir.z);
            float magnitude = Mathf.Min(1f, dir.magnitude);
            if (magnitude < 0.0001f) return Vector2.zero;

            Vector3 forward = new Vector3(cameraForward.x, 0f, cameraForward.z);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x); // Cross(up, forward)

            dir /= dir.magnitude;
            Vector2 axes = new Vector2(Vector3.Dot(dir, right), Vector3.Dot(dir, forward));
            return axes.normalized * magnitude;
        }

        /// <summary>Yaw in degrees (0 = +Z, 90 = +X) of the planar direction from a to b.</summary>
        public static float YawTo(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Mouse-axis value for one frame that turns the camera from currentDeg toward desiredDeg.
        /// Proportional turn (gain per second), capped at maxDegPerSec, divided by the camera's
        /// mouse sensitivity (the camera multiplies the axis by it).
        /// </summary>
        public static float LookDelta(float currentDeg, float desiredDeg, float deltaTime, float gain, float maxDegPerSec, float sensitivity)
        {
            float error = Mathf.DeltaAngle(currentDeg, desiredDeg);
            float maxStep = maxDegPerSec * deltaTime;
            float step = Mathf.Clamp(error * Mathf.Clamp01(gain * deltaTime), -maxStep, maxStep);
            return step / Mathf.Max(0.01f, sensitivity);
        }

        /// <summary>
        /// Skips path corners the agent already reached (planar distance under reachRadius).
        /// Never skips the last corner.
        /// </summary>
        public static int AdvanceWaypoint(Vector3[] corners, int count, int index, Vector3 position, float reachRadius)
        {
            if (corners == null || count <= 0) return 0;
            index = Mathf.Clamp(index, 0, count - 1);
            float r2 = reachRadius * reachRadius;
            while (index < count - 1)
            {
                Vector3 d = corners[index] - position;
                d.y = 0f;
                if (d.sqrMagnitude > r2) break;
                index++;
            }
            return index;
        }

        public static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Planar direction perpendicular to toTarget, to the given side (+1 right, -1 left).</summary>
        public static Vector3 Strafe(Vector3 toTarget, int side)
        {
            Vector3 d = new Vector3(toTarget.x, 0f, toTarget.z);
            if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
            d.Normalize();
            return new Vector3(d.z, 0f, -d.x) * (side >= 0 ? 1f : -1f);
        }

        /// <summary>
        /// Time dilation choice: +1 = press SlowTime, -1 = press SpeedUpTime, 0 = no change.
        /// Slow down when a crowd is near and the resource allows; speed up when the crowd is gone
        /// or the resource runs low.
        /// </summary>
        public static int TimeDilationStep(int enemiesNear, int currentLevel, float resourcePercent, int crowdSize, int maxLevel)
        {
            return TimeDilationStep(enemiesNear, currentLevel, resourcePercent, crowdSize, maxLevel, false);
        }

        /// <summary>
        /// Same as above. projectileIncoming = a catchable projectile will pass close soon. Catching
        /// needs the deepest slow, so the bot steps down to it and stays there while the threat lasts.
        /// </summary>
        public static int TimeDilationStep(int enemiesNear, int currentLevel, float resourcePercent, int crowdSize, int maxLevel, bool projectileIncoming)
        {
            if (projectileIncoming && resourcePercent >= 0.15f)
            {
                if (currentLevel < maxLevel && resourcePercent > 0.2f) return 1;
                return 0;
            }
            if (currentLevel > 0 && (enemiesNear <= 1 || resourcePercent < 0.15f)) return -1;
            if (enemiesNear >= crowdSize && currentLevel < maxLevel && resourcePercent > 0.35f) return 1;
            return 0;
        }

        /// <summary>
        /// Closest approach of a point moving with relVelocity, starting at relPosition (both relative
        /// to the observer). Returns the time of closest approach clamped to [0, horizon] and the
        /// distance at that time. Used to see a projectile coming before it is inside the catch radius.
        /// </summary>
        public static float ClosestApproach(Vector3 relPosition, Vector3 relVelocity, float horizon, out float distance)
        {
            float v2 = relVelocity.sqrMagnitude;
            float t = v2 < 0.0001f ? 0f : Mathf.Clamp(-Vector3.Dot(relPosition, relVelocity) / v2, 0f, Mathf.Max(0f, horizon));
            distance = (relPosition + relVelocity * t).magnitude;
            return t;
        }

        /// <summary>
        /// Tracks progress while the bot wants to move. Reports "stuck" when the planar position
        /// changed less than minDistance during window seconds.
        /// </summary>
        public sealed class StuckDetector
        {
            private readonly float window;
            private readonly float minDistance;
            private Vector3 anchor;
            private float anchorTime = float.NegativeInfinity;

            public StuckDetector(float window, float minDistance)
            {
                this.window = window;
                this.minDistance = minDistance;
            }

            public void Reset(Vector3 position, float time)
            {
                anchor = position;
                anchorTime = time;
            }

            /// <summary>Returns true once per stuck window. Resets the anchor after each window.</summary>
            public bool Sample(Vector3 position, float time)
            {
                if (float.IsNegativeInfinity(anchorTime))
                {
                    Reset(position, time);
                    return false;
                }

                if (time - anchorTime < window) return false;
                bool stuck = PlanarDistance(position, anchor) < minDistance;
                Reset(position, time);
                return stuck;
            }
        }
    }
}
