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

        /// <summary>A bot can name the overlay canvases of every frame (the tutorial: the prompt, the HUD, the
        /// item questions). Null: the player HUD only.</summary>
        public static Func<List<Canvas>> OverlayProvider;

        /// <summary>A bot can film a clip with its own camera (the AI bot: a close camera that follows the CPR).
        /// Null, or a null return: the main camera. The provider may move its camera on each call.</summary>
        public static Func<Camera> CameraProvider;

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
        /// <summary>A guard: a clip ends by itself after 60 s of video. A bot that waits on the game clock while the
        /// game pauses would otherwise save the same frame forever (2026-10-23: 12,647 frames, 22 GB).</summary>
        public const int MaxFramesPerClip = Fps * 60;

        private void LateUpdate()
        {
            if (clip == null) return;
            if (frame >= MaxFramesPerClip)
            {
                Debug.LogWarning($"[Recorder] {clip} reached {MaxFramesPerClip} frames: the clip ends here");
                End();
                return;
            }
            Camera own = CameraProvider != null ? CameraProvider() : null;
            Camera cam = own != null ? own : Camera.main;
            if (cam == null) return;
            var canvases = new List<Canvas>();
            if (own != null) { } // a bot camera (a close-up): no HUD
            else if (overlay != null) canvases.Add(overlay);
            else if (OverlayProvider != null) canvases.AddRange(OverlayProvider() ?? new List<Canvas>());
            else
            {
                if (hud == null) hud = FindFirstObjectByType<PlayerHud>();
                if (hud != null && hud.Canvas != null && hud.Canvas.gameObject.activeInHierarchy) canvases.Add(hud.Canvas);
            }

            var saved = new List<(Canvas canvas, RenderMode mode, Camera camera, float plane)>();
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                saved.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.05f;
            }
            if (saved.Count > 0) Canvas.ForceUpdateCanvases();
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
                foreach (var s in saved)
                {
                    s.canvas.renderMode = s.mode;
                    s.canvas.worldCamera = s.camera;
                    s.canvas.planeDistance = s.plane;
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
