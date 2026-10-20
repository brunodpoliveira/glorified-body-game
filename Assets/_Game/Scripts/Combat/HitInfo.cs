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

        public HitInfo Clone()
        {
            return (HitInfo)MemberwiseClone();
        }
    }
}
