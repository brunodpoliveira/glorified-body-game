using System.Collections;
using UnityEngine;
using GlorifiedBody.Core;

namespace GlorifiedBody.Combat
{
    /// <summary>
    /// TELEKINESIS, first form: the victim rises, hangs, and slams into the floor. The slam is the hit.
    /// An enemy with this component on it does nothing until it ends (see DummyShooter). Props and ragdolls
    /// come later with the physics grab.
    /// </summary>
    public sealed class TelekinesisLift : MonoBehaviour
    {
        public bool IsLifted { get; private set; }

        public static TelekinesisLift Apply(GameObject victim, GameObject attacker, float liftHeight, float liftSeconds, float holdSeconds, float slamDamage)
        {
            if (victim == null) return null;
            TelekinesisLift lift = victim.GetComponent<TelekinesisLift>();
            if (lift != null && lift.IsLifted) return lift;
            if (lift == null) lift = victim.AddComponent<TelekinesisLift>();
            lift.StartCoroutine(lift.Run(attacker, liftHeight, liftSeconds, holdSeconds, slamDamage));
            return lift;
        }

        private IEnumerator Run(GameObject attacker, float liftHeight, float liftSeconds, float holdSeconds, float slamDamage)
        {
            IsLifted = true;
            Vector3 floor = transform.position;
            Vector3 top = floor + Vector3.up * liftHeight;
            float t = 0f;
            while (t < liftSeconds)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(floor, top, Mathf.SmoothStep(0f, 1f, t / liftSeconds));
                yield return null;
            }

            yield return new WaitForSeconds(holdSeconds);

            t = 0f;
            float slamSeconds = 0.15f;
            while (t < slamSeconds)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(top, floor, t / slamSeconds);
                yield return null;
            }
            transform.position = floor;

            Health health = GetComponent<Health>() ?? GetComponentInParent<Health>();
            if (health != null && !health.IsDead)
            {
                health.TakeDamage(slamDamage);
                HitEventBus.Raise(new HitInfo
                {
                    Attacker = attacker,
                    Victim = health.gameObject,
                    HitTransform = health.transform,
                    HitPoint = floor,
                    HitNormal = Vector3.up,
                    DamageAmount = slamDamage,
                    DamageType = DamageType.PsychicTelekinesis,
                    WasFatal = health.IsDead
                });
            }

            IsLifted = false;
            Destroy(this);
        }
    }
}
