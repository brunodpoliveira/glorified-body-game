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

            // The HUD after the post-processing, as the game draws it (a Screen Space Overlay canvas): the scene through the
            // main camera, then the canvases alone through a HUD camera with no post, once on black and once on white, and
            // out = hud_on_black + scene * (hud_on_white - hud_on_black), on the GPU (GB_HudComposite.shader). Before T38
            // the canvases went through the main camera and took its grade, bloom and chromatic aberration (fault 4 of
            // T38 run 1). During the capture the canvases sit on HudLayer, and the HUD camera draws that layer only.
            canvases.RemoveAll(c => c == null || c.renderMode != RenderMode.ScreenSpaceOverlay || !c.gameObject.activeInHierarchy);
            try
            {
                frame++;
                string file = Path.Combine(dir, frame.ToString("00000") + ".png");
                if (canvases.Count == 0) ShotRenderer.Render(cam, file, Width, Height, useTargetTexture: true);
                else SaveWithHud(cam, canvases, file);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Recorder] frame failed: " + ex.Message);
            }
        }

        private Camera hudCamera;

        private Camera HudCamera()
        {
            if (hudCamera != null) return hudCamera;
            var go = new GameObject("[RecorderHudCamera]") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            hudCamera = go.AddComponent<Camera>();
            hudCamera.enabled = false;
            hudCamera.clearFlags = CameraClearFlags.SolidColor;
            hudCamera.allowHDR = false;
            hudCamera.allowMSAA = false;
            hudCamera.nearClipPlane = 0.1f;
            hudCamera.farClipPlane = 10f;
            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            data.SetRenderer(0);
            return hudCamera;
        }

        /// <summary>The layer the HUD canvases sit on during a capture. Layer 30 has no name and no script uses it. The
        /// game makes its canvases in code on the Default layer, so a mask built from their own layers also drew every
        /// Default object within the HUD camera's 10 m (the tutorial check of 2026-11-12: a white frame, a line and a
        /// red ellipse over the room).</summary>
        public const int HudLayer = 30;

        private static Material compositeMaterial;
        private static bool compositeMissingLogged;

        private static Material CompositeMaterial()
        {
            if (compositeMaterial != null) return compositeMaterial;
            Shader shader = Shader.Find("Hidden/GB/HudComposite");
            if (shader == null || !shader.isSupported)
            {
                if (!compositeMissingLogged) Debug.LogWarning("[Recorder] Hidden/GB/HudComposite is missing: the HUD blend runs on the CPU (slow)");
                compositeMissingLogged = true;
                return null;
            }
            compositeMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            return compositeMaterial;
        }

        private static void RenderInto(Camera c, RenderTexture rt)
        {
            RenderTexture prevTarget = c.targetTexture;
            try
            {
                c.targetTexture = rt;
                c.Render();
            }
            finally
            {
                c.targetTexture = prevTarget;
            }
        }

        private static Color32[] Read(RenderTexture rt)
        {
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            Color32[] px = tex.GetPixels32();
            DestroyImmediate(tex);
            return px;
        }

        private void SaveWithHud(Camera cam, List<Canvas> canvases, string file)
        {
            var urp = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset;
            int samples = cam.allowMSAA && urp != null ? Mathf.Max(1, urp.msaaSampleCount) : 1;
            RenderTexture scene = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, samples);
            RenderTexture onBlack = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
            RenderTexture onWhite = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
            RenderTexture output = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
            RenderTexture prevActive = RenderTexture.active;
            try
            {
                RenderInto(cam, scene);

                Camera hudCam = HudCamera();
                hudCam.cullingMask = 1 << HudLayer;
                var savedCanvases = new List<(Canvas canvas, Camera camera, float plane)>();
                var savedLayers = new List<(GameObject go, int layer)>();
                try
                {
                    foreach (Canvas canvas in canvases)
                    {
                        savedCanvases.Add((canvas, canvas.worldCamera, canvas.planeDistance));
                        foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
                        {
                            savedLayers.Add((t.gameObject, t.gameObject.layer));
                            t.gameObject.layer = HudLayer;
                        }
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = hudCam;
                        canvas.planeDistance = 1f;
                    }
                    Canvas.ForceUpdateCanvases();
                    hudCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                    RenderInto(hudCam, onBlack);
                    hudCam.backgroundColor = new Color(1f, 1f, 1f, 0f);
                    RenderInto(hudCam, onWhite);
                }
                finally
                {
                    foreach (var s in savedCanvases)
                    {
                        s.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        s.canvas.worldCamera = s.camera;
                        s.canvas.planeDistance = s.plane;
                    }
                    foreach (var s in savedLayers)
                        if (s.go != null) s.go.layer = s.layer;
                }

                var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                Material composite = CompositeMaterial();
                if (composite != null)
                {
                    composite.SetTexture("_HudBlack", onBlack);
                    composite.SetTexture("_HudWhite", onWhite);
                    Graphics.Blit(scene, output, composite);
                    RenderTexture.active = output;
                    tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                }
                else
                {
                    // The CPU fallback (about twice the frame time of a clip without HUD, 2026-11-12).
                    Color32[] s = Read(scene), b = Read(onBlack), w = Read(onWhite);
                    var outPx = new Color32[s.Length];
                    for (int i = 0; i < s.Length; i++)
                    {
                        outPx[i] = new Color32(
                            (byte)Mathf.Min(255, b[i].r + s[i].r * Mathf.Max(0, w[i].r - b[i].r) / 255),
                            (byte)Mathf.Min(255, b[i].g + s[i].g * Mathf.Max(0, w[i].g - b[i].g) / 255),
                            (byte)Mathf.Min(255, b[i].b + s[i].b * Mathf.Max(0, w[i].b - b[i].b) / 255), 255);
                    }
                    tex.SetPixels32(outPx);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(file, tex.EncodeToPNG());
                DestroyImmediate(tex);
            }
            finally
            {
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(scene);
                RenderTexture.ReleaseTemporary(onBlack);
                RenderTexture.ReleaseTemporary(onWhite);
                RenderTexture.ReleaseTemporary(output);
            }
        }

        private static string SafeName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }
    }
}
