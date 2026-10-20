using UnityEngine;
using UnityEngine.UI;
using GlorifiedBody.Cameras;
using GlorifiedBody.Player;

namespace GlorifiedBody.DebugTools
{
    /// <summary>
    /// One line of state in the top-left corner of the greybox: the camera, the lock target, the move state.
    /// It makes the bot shots readable. F1 hides and shows it. Gone when the real HUD arrives.
    /// </summary>
    public sealed class DebugStatusLine : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        private Text text;
        private Canvas canvas;
        private TankController tank;
        private LockOnController lockOn;

        private void Start()
        {
            var canvasObj = new GameObject("DebugStatusCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var labelObj = new GameObject("Status", typeof(RectTransform));
            labelObj.transform.SetParent(canvasObj.transform, false);
            var rect = labelObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -12f);
            rect.sizeDelta = new Vector2(1200f, 40f);
            text = labelObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.color = new Color(0.9f, 0.9f, 0.85f);
            text.alignment = TextAnchor.UpperLeft;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey) && canvas != null) canvas.gameObject.SetActive(!canvas.gameObject.activeSelf);
            if (text == null) return;
            if (tank == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                tank = player != null ? player.GetComponent<TankController>() : null;
                lockOn = player != null ? player.GetComponent<LockOnController>() : null;
            }

            string cam = FixedCameraDirector.Instance != null ? FixedCameraDirector.Instance.CurrentName : "-";
            string target = lockOn != null ? lockOn.TargetName : "-";
            string move = tank == null ? "-" : tank.IsDodging ? "DODGE" : tank.MovementLocked ? (lockOn != null && lockOn.IsCharging ? "CHARGE" : "LOCKED") : tank.IsRunning ? "RUN" : tank.MoveInput != 0f || tank.TurnInput != 0f ? "WALK" : "IDLE";
            text.text = $"CAM {cam}   LOCK {target}   {move}";
        }
    }
}
