using UnityEngine;
using UnityEngine.UI;
using GlorifiedBody.Boss;
using GlorifiedBody.Core;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// What the screen shows in the brain fight (DEMO_SCRIPT, cutscene 09 note 3: "The brain fight has no HUD bars in the
    /// Crown style. The game agent decides what the player sees instead"). PROPOSED, 2026-11-08: the brain's own view. A lock
    /// frame on her that closes with its ring and turns red, and her vitals as it reads them (SUBJECT, a percentage). The
    /// Crown HUD is gone (PlayerHudFlags.CrownGone). BrainFight adds it.
    /// </summary>
    public sealed class BrainHud : MonoBehaviour
    {
        private RectTransform frame;
        private Image[] sides;
        private Text vitals;
        private Canvas canvas;

        /// <summary>The words of the vitals line now (the tests).</summary>
        public string VitalsShown => vitals != null && vitals.gameObject.activeInHierarchy ? vitals.text : "";
        public bool FrameShown => frame != null && frame.gameObject.activeSelf;

        private void Start() => Build();

        private void LateUpdate()
        {
            BrainFight fight = BrainFight.Instance;
            bool on = fight != null && fight.Current == BrainFight.Phase.Fighting;
            canvas.enabled = on;
            if (!on) return;
            GameObject lucy = GameObject.FindWithTag("Player");
            Camera cam = Camera.main;
            if (lucy == null || cam == null) return;
            Health h = lucy.GetComponentInChildren<Health>();
            vitals.text = h != null ? $"SUBJECT  {Mathf.CeilToInt(h.HealthPercent * 100f)}%" : "SUBJECT";
            // Viewport units: right for any size of the screen or of a shot's camera.
            Vector3 view = cam.WorldToViewportPoint(lucy.transform.position + Vector3.up * 1.0f);
            frame.gameObject.SetActive(view.z > 0f);
            frame.anchorMin = frame.anchorMax = new Vector2(view.x, view.y);
            frame.anchoredPosition = Vector2.zero;
            float progress = fight.RingProgress;
            float size = progress >= 0f ? Mathf.Lerp(260f, 70f, progress) : 150f;
            frame.sizeDelta = new Vector2(size, size * 1.3f);
            Color c = progress >= 0f ? new Color(1f, 0.15f, 0.12f, 0.95f) : new Color(0.85f, 0.85f, 0.85f, 0.35f);
            foreach (Image s in sides) s.color = c;
        }

        private void Build()
        {
            var root = new GameObject("BrainHudCanvas", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 55;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var frameObj = new GameObject("LockFrame", typeof(RectTransform));
            frameObj.transform.SetParent(root.transform, false);
            frame = frameObj.GetComponent<RectTransform>();
            frame.anchorMin = frame.anchorMax = Vector2.zero;
            frame.pivot = new Vector2(0.5f, 0.5f);
            sides = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var side = new GameObject("Side_" + i, typeof(RectTransform));
                side.transform.SetParent(frameObj.transform, false);
                var r = side.GetComponent<RectTransform>();
                bool vertical = i >= 2;
                r.anchorMin = vertical ? new Vector2(i == 2 ? 0f : 1f, 0f) : new Vector2(0f, i == 0 ? 0f : 1f);
                r.anchorMax = vertical ? new Vector2(i == 2 ? 0f : 1f, 1f) : new Vector2(1f, i == 0 ? 0f : 1f);
                r.sizeDelta = vertical ? new Vector2(4f, 0f) : new Vector2(0f, 4f);
                sides[i] = side.AddComponent<Image>();
            }

            var textObj = new GameObject("Vitals", typeof(RectTransform));
            textObj.transform.SetParent(root.transform, false);
            var tr = textObj.GetComponent<RectTransform>();
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f);
            tr.pivot = new Vector2(0f, 1f);
            tr.anchoredPosition = new Vector2(40f, -70f); // under the dev line of a dev build
            tr.sizeDelta = new Vector2(600f, 50f);
            vitals = textObj.AddComponent<Text>();
            vitals.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            vitals.fontSize = 30;
            vitals.fontStyle = FontStyle.Bold;
            vitals.color = new Color(1f, 0.3f, 0.25f);
            vitals.alignment = TextAnchor.UpperLeft;
            canvas.enabled = false;
        }
    }
}
