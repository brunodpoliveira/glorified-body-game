using System;
using UnityEngine;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The psychic scan (design §2.9, round 8 and 9; Bruno 2026-10-22: its own key, separate from Interact).
    /// The Scan key reads the nearest Scannable in range inside the cone of her facing: a picture or a short
    /// clip fills the screen (InteractionUI). A picture closes with Scan, Interact or Back; a clip closes at
    /// its end or with one of those keys. The world stops while she sees it (GameState.Reading, Bruno, 2026-10-26;
    /// on 2026-10-22 the room went on). The reach is close, 1.5 m (Scannable.DefaultRange).
    /// </summary>
    public sealed class PsychicScanner : MonoBehaviour
    {
        public const float FacingCone = 50f; // degrees each side of her facing, PROPOSED
        public const float MinViewSeconds = 0.3f;

        public bool IsViewing => Viewing != null;
        public Scannable Viewing { get; private set; }
        public Scannable Focus { get; private set; }
        public float ViewStartTime { get; private set; }
        public int Scans { get; private set; }
        public string LastResult { get; private set; } = "";
        /// <summary>The scan id of the last view that ended, and whether it was a clip.</summary>
        public string LastClosedId { get; private set; } = "";
        public bool LastClosedWasVideo { get; private set; }
        /// <summary>The frame a view ended: the key that closed it must not also act in the interactor.</summary>
        public int EndFrame { get; private set; } = -1;

        public event Action<Scannable> OnScanStarted;
        public event Action<Scannable> OnScanEnded;

        private TankController tank;
        private Interactor interactor;
        private bool locked;
        private int startFrame;

        private void Awake()
        {
            tank = GetComponent<TankController>();
            interactor = GetComponent<Interactor>();
        }

        private void OnDisable()
        {
            SetLock(false);
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying && !(gm.IsReading && Viewing != null)) return;
            bool scan = GameInput.GetActionDown(InputManager.GameAction.Scan, KeyCode.F);
            if (Viewing == null)
            {
                Focus = FindFocus();
                if (scan && (interactor == null || interactor.Current == Interactor.State.Free))
                {
                    if (Focus != null) Begin(Focus);
                    else Result("nothing");
                }
                return;
            }
            if (Time.frameCount == startFrame || Time.unscaledTime - ViewStartTime < MinViewSeconds) return;
            bool close = scan
                || GameInput.GetActionDown(InputManager.GameAction.Interact, KeyCode.E)
                || GameInput.GetActionDown(InputManager.GameAction.Back, KeyCode.S);
            if (close) End("closed");
        }

        /// <summary>The nearest scannable in its range and inside the cone of her facing. Null when none.</summary>
        public Scannable FindFocus()
        {
            Scannable best = null;
            float bestDistance = float.MaxValue;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            foreach (Scannable target in Scannable.All)
            {
                if (target == null || (target.image == null && target.clip == null)) continue;
                Vector3 to = Vector3.ProjectOnPlane(target.FocusPoint - transform.position, Vector3.up);
                float distance = to.magnitude;
                if (distance > target.range || distance >= bestDistance) continue;
                if (Vector3.Angle(forward, to) > FacingCone) continue;
                best = target;
                bestDistance = distance;
            }
            return best;
        }

        private void Begin(Scannable target)
        {
            Viewing = target;
            ViewStartTime = Time.unscaledTime;
            startFrame = Time.frameCount;
            Scans++;
            SetLock(true);
            GameManager.Instance?.BeginReading();
            Result((target.IsVideo ? "video:" : "image:") + target.scanId);
            OnScanStarted?.Invoke(target);
        }

        /// <summary>Ends the view (a key, or the clip reached its end: InteractionUI calls it).</summary>
        public void End(string why)
        {
            if (Viewing == null) return;
            Scannable ended = Viewing;
            LastClosedId = ended.scanId;
            LastClosedWasVideo = ended.IsVideo;
            Viewing = null;
            EndFrame = Time.frameCount;
            SetLock(false);
            GameManager.Instance?.EndReading();
            Result($"{why}:{ended.scanId}:{Time.unscaledTime - ViewStartTime:0.0}s");
            OnScanEnded?.Invoke(ended);
        }

        private void SetLock(bool value)
        {
            if (locked == value || tank == null) return;
            locked = value;
            tank.LockMovement(value);
        }

        private void Result(string what)
        {
            LastResult = what;
            RunTelemetry.Event("scan", what);
            GlorifiedBody.Audio.InteractionSounds.OnResult(what, transform.position);
        }
    }
}
