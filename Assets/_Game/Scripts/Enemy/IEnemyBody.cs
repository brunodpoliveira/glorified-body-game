using UnityEngine;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The legs and the eyes of an FSM enemy, as SoldierBrain sees them. The Invector AI body implements it in
    /// GlorifiedBody.Integration.Invector (rule 6: only that namespace uses Invector types).
    /// </summary>
    public enum MoveSpeed { Walk, Run, Sprint }

    public interface IEnemyBody
    {
        bool IsDead { get; }
        /// <summary>A target is known (seen now, or seen before and not lost yet).</summary>
        bool HasTarget { get; }
        /// <summary>The target is in the line of sight now.</summary>
        bool CanSeeTarget { get; }
        /// <summary>The last known position of the target, on its feet.</summary>
        Vector3 TargetPosition { get; }
        /// <summary>The body moves (its speed over a small threshold).</summary>
        bool IsMoving { get; }
        bool IsInDestination { get; }
        /// <summary>Walk, run or sprint to the point (the run and sprint animations of the vendor set).</summary>
        void MoveTo(Vector3 point, MoveSpeed speed);
        void Stop();
        /// <summary>Off while a power holds the body (the lift): no navigation, no physics.</summary>
        void Suspend(bool suspended);
        /// <summary>Planted feet while the gun aims and counts down: a walking body cannot push it off its spot.</summary>
        void Brace(bool braced);
        /// <summary>From now on the body keeps her as its target, also out of sight (the alerted soldier).</summary>
        void Track(Transform target);
        /// <summary>The crouch of the vendor set (a cower, the CPR, the drag).</summary>
        void Crouch(bool crouched);
        /// <summary>Walk to the point while it faces the direction (the drag: backward, facing the body).</summary>
        void StrafeTo(Vector3 point, Vector3 facing);
        /// <summary>Turn on the spot to face the direction.</summary>
        void Face(Vector3 direction);
        /// <summary>A hit reaction of the vendor set (the shock at a colleague's death).</summary>
        void Flinch();
    }
}
