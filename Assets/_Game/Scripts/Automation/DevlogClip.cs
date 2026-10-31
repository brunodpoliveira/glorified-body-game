using System;
using System.Collections;
using UnityEngine;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// Clip markers for the devlog videos (tools/unity.sh record). A bot calls Room or Close at the start of a moment
    /// and End after it. Nothing happens in a normal run: every call returns at once when FrameRecorder is off.
    /// Room films through the room's fixed cameras with the HUD. Close films through a camera that follows a target
    /// each frame, with no HUD (a close-up of a person: a plea, a cower, the CPR).
    /// </summary>
    public static class DevlogClip
    {
        private static Camera close;

        public static void Room(string name)
        {
            if (!FrameRecorder.Enabled) return;
            FrameRecorder.CameraProvider = null;
            FrameRecorder.Ensure().Begin(name);
        }

        /// <param name="facing">The side the camera stands on, from the target (flattened).</param>
        public static void Close(string name, Transform target, Func<Transform, Vector3> facing, float distance = 2.8f, float height = 0.9f)
        {
            if (!FrameRecorder.Enabled || target == null) return;
            if (close == null)
            {
                var go = new GameObject("[DevlogCloseCamera]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                close = go.AddComponent<Camera>();
                close.fieldOfView = 40f;
                close.nearClipPlane = 0.05f;
                close.enabled = false; // only the recorder renders it
            }
            Camera main = Camera.main;
            if (main != null)
            {
                close.clearFlags = main.clearFlags;
                close.backgroundColor = main.backgroundColor;
            }
            FrameRecorder.CameraProvider = () =>
            {
                if (target == null) return null;
                Vector3 point = target.position + Vector3.up * height;
                Vector3 side = facing(target);
                side.y = 0f;
                if (side.sqrMagnitude < 0.001f) side = Vector3.back;
                close.transform.position = point + side.normalized * distance + Vector3.up * 0.3f;
                close.transform.LookAt(point);
                return close;
            };
            FrameRecorder.Ensure().Begin(name);
        }

        /// <summary>Holds the clip for a moment on the bot clock (video only), then ends it.</summary>
        public static IEnumerator End(float holdSeconds = 0f)
        {
            if (FrameRecorder.Instance == null || !FrameRecorder.Instance.IsRecording) yield break;
            if (holdSeconds > 0f) yield return BotClock.Wait(holdSeconds);
            FrameRecorder.Instance.End();
            FrameRecorder.CameraProvider = null;
        }
    }
}
