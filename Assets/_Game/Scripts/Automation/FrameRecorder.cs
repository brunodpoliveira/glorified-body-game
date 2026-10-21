using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using GlorifiedBody.UI;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// Video frames for the devlog. Active only in a bot run started with -gbRecord 1 (tools/unity.sh record).
    /// Between Begin(clip) and End() the game clock runs at a fixed 30 frames per second (Time.captureFramerate),
    /// and every frame is saved as runs/&lt;id&gt;/frames/&lt;clip&gt;/00001.png at 1920x1080 through the main camera,
    /// with the player HUD drawn over it. unity.sh turns each folder into one MP4 with ffmpeg.
    /// Outside a clip the clock is free, so the bot behaves as in a normal play run.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class FrameRecorder : MonoBehaviour
    {
        public const int Fps = 30;
        public const int Width = 1920;
        public const int Height = 1080;

        public static FrameRecorder Instance { get; private set; }
        public static bool Enabled => BotArgs.Record && !string.IsNullOrEmpty(BotArgs.RunDir);

        private readonly List<string> manifest = new List<string>();
        private string clip;
        private string dir;
        private int frame;
        private Canvas overlay;
        private PlayerHud hud;

        public bool IsRecording => clip != null;

        public static FrameRecorder Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[FrameRecorder]");
            DontDestroyOnLoad(go);
            return go.AddComponent<FrameRecorder>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            End();
            if (Instance == this)
            {
                Instance = null;
                Time.captureFramerate = 0;
            }
        }

        /// <summary>Starts a clip. overlayCanvas: a screen-space canvas to draw over the frame (default: the player HUD).</summary>
        public void Begin(string name, Canvas overlayCanvas = null)
        {
            End();
            clip = SafeName(name);
            dir = Path.Combine(BotArgs.RunDir, "frames", clip);
            Directory.CreateDirectory(dir);
            frame = 0;
            overlay = overlayCanvas;
            Time.captureFramerate = Fps;
            Debug.Log("[Recorder] begin " + clip);
        }

        public void End()
        {
            if (clip == null) return;
            manifest.Add("{\"clip\":\"" + clip + "\",\"frames\":" + frame + ",\"fps\":" + Fps + "}");
            File.WriteAllText(Path.Combine(BotArgs.RunDir, "frames", "clips.json"), "[" + string.Join(",", manifest) + "]");
            Debug.Log($"[Recorder] end {clip}: {frame} frames");
            clip = null;
            overlay = null;
            Time.captureFramerate = 0;
        }

        // LateUpdate, not WaitForEndOfFrame: the end-of-frame callback is not reliable in batch mode. A manual
        // camera render here shows the state of this frame, after every Update.
        private void LateUpdate()
        {
            if (clip == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Canvas canvas = overlay;
            if (canvas == null)
            {
                if (hud == null) hud = FindFirstObjectByType<PlayerHud>();
                canvas = hud != null ? hud.Canvas : null;
            }

            RenderMode oldMode = RenderMode.ScreenSpaceOverlay;
            Camera oldCamera = null;
            float oldPlane = 0f;
            if (canvas != null)
            {
                oldMode = canvas.renderMode;
                oldCamera = canvas.worldCamera;
                oldPlane = canvas.planeDistance;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.05f;
                Canvas.ForceUpdateCanvases();
            }
            try
            {
                frame++;
                ShotRenderer.Render(cam, Path.Combine(dir, frame.ToString("00000") + ".png"), Width, Height, useTargetTexture: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Recorder] frame failed: " + ex.Message);
            }
            finally
            {
                if (canvas != null)
                {
                    canvas.renderMode = oldMode;
                    canvas.worldCamera = oldCamera;
                    canvas.planeDistance = oldPlane;
                }
            }
        }

        private static string SafeName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }
    }
}
