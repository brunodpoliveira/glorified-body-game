using UnityEngine;
using GlorifiedBody.Combat;

namespace GlorifiedBody.Destruction
{
    /// <summary>
    /// The leaf of a FireDoor as a breakable (2026-11-03): while it stands jammed in its doorway, the kicks of a soldier or
    /// the power armour (EnemyMelee), the explosions and her powers wear it down (Bruno: "her powers can break a door as
    /// well, although there would be little reason for a player to deliberately break a door; but it should be an
    /// option"). Her powers reach it by their splash: a blast near it, a thrown crate or body. The enemy bullets do not
    /// (PROPOSED). The pieces fall where the leaf stood, and a broken door stays broken (RoomDamageLedger).
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
                case DamageType.EnemyBullet:
                case DamageType.EnemyProjectile:
                    return 0f;
                default:
                    return base.Scale(type); // explosions 2, telekinesis 1.5, fire by the material, the short none
            }
        }

        protected override void OnBreak(Vector3 point) => Broken?.Invoke();

        protected override void OnRestoredBroken() => Broken?.Invoke();
    }
}
