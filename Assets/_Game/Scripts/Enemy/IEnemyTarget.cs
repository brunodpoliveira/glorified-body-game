using UnityEngine;
using GlorifiedBody.Core;

namespace GlorifiedBody.Enemy
{
    public interface IEnemyTarget
    {
        bool IsAlive { get; }
        bool IsStaggered { get; }
        bool CanBeRagdolled { get; }
        Health Health { get; }
        Transform Root { get; }
    }
}
