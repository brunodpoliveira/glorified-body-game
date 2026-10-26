using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// A black screen over everything for a room crossing (and later a cutscene cut): Out darkens it and then runs
    /// the action, In clears it. Unscaled time, so a frozen world still fades. One object for the whole game.
    /// </summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        private static ScreenFade instance;
        private Image image;
        private Coroutine running;

        public static float Alpha => instance != null && instance.image != null ? instance.image.color.a : 0f;

        public static void Out(float seconds, Action then) => Ensure().Run(1f, seconds, then);
        public static void In(float seconds) => Ensure().Run(0f, seconds, null);

        private static ScreenFade Ensure()
        {
            if (instance != null) return instance;
            var obj = new GameObject("[ScreenFade]");
            DontDestroyOnLoad(obj);
            var canvas = obj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var imageObj = new GameObject("Black", typeof(RectTransform));
            imageObj.transform.SetParent(obj.transform, false);
            var rect = imageObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            instance = obj.AddComponent<ScreenFade>();
            instance.image = imageObj.AddComponent<Image>();
            instance.image.color = new Color(0f, 0f, 0f, 0f);
            instance.image.raycastTarget = false;
            return instance;
        }

        private void Run(float target, float seconds, Action then)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Fade(target, seconds, then));
        }

        private IEnumerator Fade(float target, float seconds, Action then)
        {
            float from = image.color.a;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                image.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, target, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            image.color = new Color(0f, 0f, 0f, target);
            running = null;
            then?.Invoke();
        }
    }
}
