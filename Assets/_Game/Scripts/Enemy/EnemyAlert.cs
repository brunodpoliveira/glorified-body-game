using System;
using UnityEngine;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The alert between enemies (Bruno, 2026-10-23: "on project warspite, the enemies weren't this dumb"). A
    /// soldier that sees Lucy, takes a hit or fires calls it: every soldier inside the radius hears it and fights
    /// too. PROPOSED: one radius covers a room.
    /// </summary>
    public static class EnemyAlert
    {
        /// <summary>The origin, the radius in metres, and the reason ("sight", "hurt", "gunshot", "power").</summary>
        public static event Action<Vector3, float, string> OnAlert;

        public static void Raise(Vector3 origin, float radius, string reason)
        {
            OnAlert?.Invoke(origin, radius, reason);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            OnAlert = null;
        }
    }
}
