using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GlorifiedBody.Rendering
{
    /// <summary>
    /// T38 (the Arkham Knight look), switch 4: a planar reflection of the floor at half size. A second camera, mirrored
    /// in the floor plane with an oblique near plane on the floor, renders the room into a half-size target before the
    /// main camera renders (LateUpdate, after the fixed camera moved). The overlay quad (GB/AK/PlanarOverlay) adds it on
    /// the floor. The mirror camera has no shadows, no post-processing, no MSAA and is a Reflection camera, so the light
    /// in the air and the ambient occlusion skip it. A bot that renders the main camera by hand calls
    /// <see cref="RenderFor"/> first, so the frame time counts this pass.
    /// </summary>
    [DefaultExecutionOrder(600)]
    public sealed class AKPlanarReflection : MonoBehaviour
    {
        public static readonly int TexId = Shader.PropertyToID("_AKPlanarTex");
        public static readonly int OnId = Shader.PropertyToID("_AKPlanarOn");

        public float floorY = 0f;
        public float clipOffset = 0.02f;
        [Range(0.25f, 1f)] public float resolution = 0.5f;
        public int rendererIndex = 1;
        public Renderer overlay;
        [Tooltip("Layers the mirror camera does not draw: the overlay quad (Water) and the UI.")]
        public LayerMask exclude = (1 << 4) | (1 << 5);

        private Camera mirror;
        private RenderTexture target;
        private bool on;

        public bool On => on;
        public int Renders { get; private set; }

        public void SetOn(bool value)
        {
            on = value;
            if (overlay != null) overlay.gameObject.SetActive(value);
            Shader.SetGlobalFloat(OnId, value ? 1f : 0f);
            if (!value) Release();
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(OnId, 0f);
            Release();
        }

        private void OnDestroy()
        {
            Release();
            if (mirror != null) Destroy(mirror.gameObject);
        }

        private void LateUpdate()
        {
            if (!on) return;
            // The recorder renders 1920 x 1080 at the end of the frame, whatever the window size.
            bool recording = GlorifiedBody.Automation.FrameRecorder.Instance != null && GlorifiedBody.Automation.FrameRecorder.Instance.IsRecording;
            if (recording) RenderFor(Camera.main, GlorifiedBody.Automation.FrameRecorder.Width, GlorifiedBody.Automation.FrameRecorder.Height);
            else RenderFor(Camera.main);
        }

        /// <summary>Render the mirror image for this camera at its current pose and its target size (or the screen).</summary>
        public void RenderFor(Camera main, int width = 0, int height = 0)
        {
            if (!on || main == null) return;
            if (width <= 0) { width = main.targetTexture != null ? main.targetTexture.width : main.pixelWidth; height = main.targetTexture != null ? main.targetTexture.height : main.pixelHeight; }
            int w = Mathf.Max(64, Mathf.RoundToInt(width * resolution)), h = Mathf.Max(64, Mathf.RoundToInt(height * resolution));
            if (target == null || target.width != w || target.height != h)
            {
                Release();
                target = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "AK_Planar", useMipMap = false, antiAliasing = 1 };
                target.Create();
            }
            EnsureMirror();
            mirror.CopyFrom(main);
            mirror.cameraType = CameraType.Reflection;
            mirror.enabled = false;
            mirror.targetTexture = target;
            mirror.allowMSAA = false;
            mirror.allowHDR = true;
            mirror.cullingMask = main.cullingMask & ~exclude.value;
            mirror.clearFlags = CameraClearFlags.SolidColor;
            mirror.backgroundColor = Color.black;
            var data = mirror.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.SetRenderer(rendererIndex);

            Vector3 normal = Vector3.up;
            float d = -floorY;
            Matrix4x4 reflect = Reflection(new Vector4(normal.x, normal.y, normal.z, d));
            Vector3 pos = main.transform.position;
            mirror.transform.position = reflect.MultiplyPoint(pos);
            Vector3 fwd = Vector3.Reflect(main.transform.forward, normal), up = Vector3.Reflect(main.transform.up, normal);
            mirror.transform.rotation = Quaternion.LookRotation(fwd, up);
            mirror.worldToCameraMatrix = main.worldToCameraMatrix * reflect;
            // The projection for the size of the frame that will sample the mirror (the bot's 1080p target, not the window).
            Vector4 clip = CameraSpacePlane(mirror, new Vector3(0f, floorY, 0f), normal);
            mirror.ResetProjectionMatrix();
            mirror.fieldOfView = main.fieldOfView;
            mirror.aspect = width / (float)height;
            mirror.projectionMatrix = mirror.CalculateObliqueMatrix(clip);

            bool invert = GL.invertCulling;
            GL.invertCulling = !invert;
            mirror.Render();
            GL.invertCulling = invert;
            Shader.SetGlobalTexture(TexId, target);
            Shader.SetGlobalFloat(OnId, 1f);
            Renders++;
        }

        private void EnsureMirror()
        {
            if (mirror != null) return;
            var go = new GameObject("AK_PlanarMirror") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            mirror = go.AddComponent<Camera>();
            mirror.enabled = false;
            go.AddComponent<UniversalAdditionalCameraData>();
        }

        private void Release()
        {
            if (target == null) return;
            if (mirror != null) mirror.targetTexture = null;
            target.Release();
            Destroy(target);
            target = null;
        }

        private Vector4 CameraSpacePlane(Camera cam, Vector3 point, Vector3 normal)
        {
            Matrix4x4 m = cam.worldToCameraMatrix;
            Vector3 cp = m.MultiplyPoint(point + normal * clipOffset);
            Vector3 cn = m.MultiplyVector(normal).normalized;
            return new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn));
        }

        private static Matrix4x4 Reflection(Vector4 p)
        {
            var m = new Matrix4x4();
            m.m00 = 1f - 2f * p.x * p.x; m.m01 = -2f * p.x * p.y; m.m02 = -2f * p.x * p.z; m.m03 = -2f * p.w * p.x;
            m.m10 = -2f * p.y * p.x; m.m11 = 1f - 2f * p.y * p.y; m.m12 = -2f * p.y * p.z; m.m13 = -2f * p.w * p.y;
            m.m20 = -2f * p.z * p.x; m.m21 = -2f * p.z * p.y; m.m22 = 1f - 2f * p.z * p.z; m.m23 = -2f * p.w * p.z;
            m.m30 = 0f; m.m31 = 0f; m.m32 = 0f; m.m33 = 1f;
            return m;
        }
    }
}
