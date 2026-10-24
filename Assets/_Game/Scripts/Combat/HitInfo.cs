using UnityEngine;

namespace GlorifiedBody.Combat
{
    [System.Serializable]
    public class HitInfo
    {
        public GameObject Attacker;
        public GameObject Victim;
        public Collider HitCollider;
        public Transform HitTransform;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public float DamageAmount;
        public DamageType DamageType;
        public bool WasFatal;
        public string HitZone;
        /// <summary>The power's charge, 0..1 (1 = a full circle). 0 for a hit that is not a charged power.</summary>
        public float Charge;

        public HitInfo Clone()
        {
            return (HitInfo)MemberwiseClone();
        }
    }
}
