using System.Collections;
using System.IO;
using UnityEngine;
using GlorifiedBody.Automation;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// Bot runs only: camera shots skip screen-space overlay canvases. This renders one overlay
    /// canvas through a camera once, saves a PNG to runs/&lt;id&gt;/shots, then puts the canvas back.
    /// </summary>
    public static class OverlayShot
    {
        /// <summary>True when a bot run asked for shots (BotArgs.RunDir is set).</summary>
        public static bool Enabled => !string.IsNullOrEmpty(BotArgs.RunDir);

        /// <summary>Waits delaySeconds (unscaled), then saves shots/fileName. Does nothing outside bot runs.</summary>
        public static IEnumerator Capture(Canvas canvas, string fileName, float delaySeconds)
        {
            if (!Enabled)
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(delaySeconds);
            if (canvas == null)
            {
                yield break;
            }

            string path = Path.Combine(BotArgs.RunDir, "shots", fileName);
            Camera cam = Camera.main;
            GameObject tempCam = null;
            if (cam == null)
            {
                // Menu scenes may have no main camera. Use a plain black one.
                tempCam = new GameObject("[OverlayShotCamera]");
                cam = tempCam.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }

            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            float oldPlane = canvas.planeDistance;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.05f;
            Canvas.ForceUpdateCanvases();
            try
            {
                ShotRenderer.Render(cam, path, useTargetTexture: true);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[OverlayShot] " + fileName + " failed: " + ex.Message);
            }
            finally
            {
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera;
                canvas.planeDistance = oldPlane;
                if (tempCam != null)
                {
                    Object.Destroy(tempCam);
                }
            }
        }
    }
}
