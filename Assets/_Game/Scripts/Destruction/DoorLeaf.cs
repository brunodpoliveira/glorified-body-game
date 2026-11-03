using UnityEngine;
using GlorifiedBody.Combat;

namespace GlorifiedBody.Destruction
{
    /// <summary>
    /// The leaf of a FireDoor as a breakable (2026-11-03): the kicks of a soldier or the power armour (EnemyMelee) and the
    /// explosions wear it down while it stands jammed in its doorway. Her powers and the bullets do not (PROPOSED: Bruno named only the
    /// soldiers). The pieces fall where the leaf stood, and a broken door stays broken (RoomDamageLedger).
    /// </summary>
    [DefaultExecutionOrder(-50)] // before FireDoor: the bounds of the shut leaf, not of the leaf in its pocket
    public sealed class DoorLeaf : Breakable
    {
        /// <summary>The door says when the leaf stands in its doorway (open, it is in the wall).</summary>
        public System.Func<bool> Standing;
        public System.Action Broken;

        protected override float Scale(DamageType type)
        {
            if (Standing != null && !Standing()) return 0f;
            switch (type)
            {
                case DamageType.EnemyMelee:
                    return 1f;
                case DamageType.Explosion:
                case DamageType.Grenade:
                case DamageType.EnemyRocket:
                    return 2f;
                default:
                    return 0f;
            }
        }

        protected override void OnBreak(Vector3 point) => Broken?.Invoke();

        protected override void OnRestoredBroken() => Broken?.Invoke();
    }
}
