using UnityEngine;
using UnityEngine.UI;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// The knock-out of the soldiers' gas (DEMO_PLAN, the death screens: soldier_gas, "She is knocked out"; the writer's line
    /// "Knock-out's working. She's out."). Each second in a gas cloud fills it (GasCloud.Breathe), and out of the clouds it
    /// drains at half the speed. The screen darkens as it fills, and over half she walks slowly. Full: she is knocked out,
    /// a game over with the cloud's killer id (Health.Kill: the last-hit rule and the second wind do not wake her; while
    /// she is invulnerable it waits). The
    /// poison of the cloud stays (Bruno, 2026-10-24: "the gas is poison"). Numbers PROPOSED.
    /// </summary>
    public sealed class Drowsiness : MonoBehaviour
    {
        public const float KnockoutSeconds = 4f;
        public const float DrainShare = 0.5f;
        public const float SlowFrom = 0.5f;

        /// <summary>0 awake, 1 knocked out.</summary>
        public float Level { get; private set; }
        public bool KnockedOut { get; private set; }
        public float VeilShown => veil != null && veil.enabled ? veil.color.a : 0f;

        private int breathedFrame = -1;
        private string killerId = "";
        private TankController tank;
        private Health health;
        private bool slowSet;
        private Image veil;

        /// <summary>She breathes the gas this frame (a cloud calls it).</summary>
        public void Breathe(float dt, string killer)
        {
            if (KnockedOut || breathedFrame == Time.frameCount) return;
            breathedFrame = Time.frameCount;
            killerId = killer;
            Level = Mathf.Min(1f, Level + dt / KnockoutSeconds);
            // Invulnerable (a dodge, the test rooms' god mode): the knock-out waits, full, for the next breath.
            if (Level >= 1f && (health == null || !health.IsInvulnerable)) KnockOut();
        }

        private void Awake()
        {
            tank = GetComponent<TankController>();
            health = GetComponentInChildren<Health>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (!KnockedOut && breathedFrame != Time.frameCount && Breathless) Level = Mathf.Max(0f, Level - dt * DrainShare / KnockoutSeconds);
            bool slow = !KnockedOut && Level >= SlowFrom;
            if (tank != null && slow != slowSet)
            {
                if (slow && !tank.SlowWalk) { tank.SetSlowWalk(true); slowSet = true; }
                else if (!slow && slowSet) { tank.SetSlowWalk(false); slowSet = false; }
            }
            Veil(Level * 0.85f);
        }

        // Not this frame: a cloud that runs after this component calls Breathe in the same frame, so the drain waits a frame.
        private bool Breathless => Time.frameCount - breathedFrame > 1;

        private void KnockOut()
        {
            KnockedOut = true;
            RunTelemetry.Event("knocked_out", killerId);
            if (slowSet && tank != null) { tank.SetSlowWalk(false); slowSet = false; }
            if (health == null || health.IsDead) return;
            GlorifiedBody.UI.DeathMenuUI.LastKillerId = killerId;
            health.Kill();
        }

        private void Veil(float alpha)
        {
            if (alpha <= 0.001f && veil == null) return;
            if (veil == null)
            {
                var root = new GameObject("[GasVeil]", typeof(RectTransform));
                root.transform.SetParent(transform, false);
                Canvas canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5; // over the world, under the HUD and the menus
                var img = new GameObject("Veil", typeof(RectTransform));
                img.transform.SetParent(root.transform, false);
                var rect = img.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                veil = img.AddComponent<Image>();
                veil.raycastTarget = false;
            }
            veil.enabled = alpha > 0.001f;
            veil.color = new Color(0.02f, 0.02f, 0.03f, alpha);
        }

        private void OnDestroy()
        {
            if (slowSet && tank != null) tank.SetSlowWalk(false);
        }
    }
}
