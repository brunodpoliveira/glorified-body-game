using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// Renders a camera to a PNG file. Works in edit mode, Play mode and batch mode with graphics.
    /// useTargetTexture: render through cam.targetTexture, so the camera pixel rect equals the file size.
    /// Screen-space-camera canvases size themselves from that rect. The render request path keeps the
    /// batch-mode screen size (4:3) and leaves black strips on a 16:9 file.
    /// </summary>
    public static class ShotRenderer
    {
        public static bool Render(Camera cam, string path, int width = 1280, int height = 720, bool useTargetTexture = false)
        {
            if (cam == null) return false;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture prevTarget = cam.targetTexture;
            try
            {
                var request = new RenderPipeline.StandardRequest();
                if (!useTargetTexture && RenderPipeline.SupportsRenderRequest(cam, request))
                {
                    request.destination = rt;
                    RenderPipeline.SubmitRenderRequest(cam, request);
                }
                else
                {
                    cam.targetTexture = rt;
                    cam.Render();
                }

                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return true;
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
