using System.Collections;
using UnityEngine;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The clock of a bot. In a normal run it is the real clock. In a record run (-gbRecord 1) each frame takes
    /// longer in real time (the PNG save), and inside a clip the game clock runs at a fixed 30 frames per second,
    /// so a real-time wait or timeout would end too early in the video. There the clock adds one capture step per
    /// frame (1/30 s), so a wait of 0.6 s is 0.6 s of video.
    /// </summary>
    public static class BotClock
    {
        private static float accumulated;
        private static int lastFrame = -1;

        public static float Now
        {
            get
            {
                if (!FrameRecorder.Enabled) return Time.realtimeSinceStartup;
                int frame = Time.frameCount;
                if (lastFrame < 0) lastFrame = frame;
                if (frame != lastFrame)
                {
                    int frames = frame - lastFrame;
                    float step = Time.captureFramerate > 0 ? 1f / Time.captureFramerate : Time.unscaledDeltaTime;
                    accumulated += frames * step;
                    lastFrame = frame;
                }
                return accumulated;
            }
        }

        /// <summary>
        /// Waits while the world stands still (Time.timeScale under 1): the 2.5 s freeze at the start of the short
        /// (Bruno, 2026-10-25). A key press during the freeze is lost, so a bot waits for the world first.
        /// </summary>
        public static IEnumerator WaitWorld(float maxSeconds = 5f)
        {
            float t0 = Now;
            while (Time.timeScale < 0.999f && Now - t0 < maxSeconds) yield return null;
        }

        /// <summary>The bot-clock wait: WaitForSecondsRealtime in a normal run.</summary>
        public static IEnumerator Wait(float seconds)
        {
            if (!FrameRecorder.Enabled)
            {
                yield return new WaitForSecondsRealtime(seconds);
                yield break;
            }
            float t0 = Now;
            while (Now - t0 < seconds) yield return null;
        }
    }
}
