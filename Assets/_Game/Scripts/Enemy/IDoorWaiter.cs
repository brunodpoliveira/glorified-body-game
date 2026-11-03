using UnityEngine;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// An enemy that a jammed fire door stops on its way to her (FireDoor, 2026-11-03, DEMO_PLAN 13.2). The door decides; the
    /// brain only stands, faces the door and swings. Soldiers and the power armour break a jammed door; guards and the
    /// scientists only bang on it (Bruno: "breachable by the soldier and power armor enemies ... not by scientists and
    /// guards, they can bang on the doors though").
    /// </summary>
    public interface IDoorWaiter
    {
        /// <summary>Alerted and after her (not dead, not held by a script, not in a retreat).</summary>
        bool Hunting { get; }
        /// <summary>Where it thinks she is.</summary>
        Vector3 Quarry { get; }
        /// <summary>A soldier or the power armour: it breaks a jammed door.</summary>
        bool CanBreach { get; }
        /// <summary>The power armour: fewer, harder hits.</summary>
        bool IsHeavy { get; }
        /// <summary>It stands and faces the door for the seconds (the door calls it again while it waits). 0 lets it go.</summary>
        void WaitAtDoor(Vector3 door, float seconds);
        /// <summary>One kick or one bang: the swing of the arms.</summary>
        void SwingAtDoor();
    }
}
