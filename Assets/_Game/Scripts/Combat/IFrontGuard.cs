using UnityEngine;

namespace GlorifiedBody.Combat
{
    /// <summary>
    /// Something that blocks attacks from its front (the shield soldier, Bruno 2026-10-24: mk_warspite, mk_erebus
    /// and returned bullets stop on the shield; the side and the back are open; mk_orzabal still lifts him).
    /// </summary>
    public interface IFrontGuard
    {
        /// <summary>True when an attack that comes from this point stops on the guard.</summary>
        bool Blocks(Vector3 from);

        /// <summary>The attack stopped: the spark, the sound, the alert.</summary>
        void Blocked(Vector3 point, string what);
    }
}
