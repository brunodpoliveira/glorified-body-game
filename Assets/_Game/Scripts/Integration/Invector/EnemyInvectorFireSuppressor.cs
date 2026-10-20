using Invector.vCharacterController.AI;
using UnityEngine;

namespace GlorifiedBody.Integration.Invector
{
    /// <summary>
    /// Warspite owns all enemy firing through <c>GlorifiedBody.Enemy.EnemyLogic</c> + EnemyConfig.
    /// Invector's <see cref="vControlAIShooter"/> ships its own independent shot loop
    /// (CheckCanShot / Attack / Shot) which the FSM_ShooterCover graph drives via
    /// vAIShooterAttack, firing from the AI weapon's own clipSize/dispersion/automaticWeapon
    /// values. Left alone it lands extra shots on top of EnemyLogic's bursts.
    ///
    /// The component itself must stay enabled: vControlAIShooter IS the vControlAI, and
    /// EnemyLogic drives locomotion, aim and IK through it. So we neutralise only the
    /// shooting half.
    ///
    /// Primary gate is <c>maxAngleToShot</c>. ValidateShotAngle computes
    /// <c>IsInShotAngle = angle &lt;= maxAngleToShot</c> from a Vector3.Angle, which is never
    /// negative - so a negative threshold makes IsInShotAngle unconditionally false, and
    /// Shot() requires it. This defeats the forceCanShot path that the "Normal Attack 0"
    /// FSM action uses (forceCanAttack: 1), which a zeroed shooting window alone does not.
    ///
    /// Secondary gates: a zero shooting window keeps _canShot false so Attack() never
    /// increments 'shots' on the non-forced path, and doReloadWhileWaiting off stops Shot()
    /// from kicking off Invector's reload coroutine while it idles.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(vControlAIShooter))]
    [DefaultExecutionOrder(-100)]
    public sealed class EnemyInvectorFireSuppressor : MonoBehaviour
    {
        /// <summary>Any negative value works; -1 reads clearly in the inspector.</summary>
        public const float SuppressedMaxAngleToShot = -1f;

        [SerializeField] private vControlAIShooter shooter;

        public bool IsSuppressed =>
            shooter != null
            && shooter.maxAngleToShot < 0f
            && shooter.minTimeShooting <= 0f
            && shooter.maxTimeShooting <= 0f
            && !shooter.doReloadWhileWaiting;

        private void Awake()
        {
            Resolve();
            Apply();
        }

        private void OnEnable()
        {
            Resolve();
            Apply();
        }

        private void OnValidate()
        {
            Resolve();
            Apply();
        }

        // Invector's Start() -> InitShooter() runs after our Awake, and Phase4Setup can
        // re-instantiate an enemy from the Invector source prefab. Holding the clamp for the
        // enemy's lifetime makes the suppression immune to both. Four field writes, no
        // allocation, no reflection.
        private void LateUpdate()
        {
            Apply();
        }

        private void Resolve()
        {
            if (shooter == null)
            {
                shooter = GetComponent<vControlAIShooter>();
            }
        }

        private void Apply()
        {
            if (shooter == null)
            {
                return;
            }

            shooter.maxAngleToShot = SuppressedMaxAngleToShot;
            shooter.minTimeShooting = 0f;
            shooter.maxTimeShooting = 0f;
            shooter.doReloadWhileWaiting = false;
        }
    }
}
