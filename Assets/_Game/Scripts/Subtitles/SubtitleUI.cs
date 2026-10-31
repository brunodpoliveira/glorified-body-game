using System.Collections.Generic;
using GlorifiedBody.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace GlorifiedBody.Subtitles
{
    /// <summary>
    /// The subtitles at the bottom of the screen (Bruno, 2026-10-25): yellow words with a black outline, the speaker's
    /// name first on every line. On by default; Options > Display turns them off and sets the size. One overlay for
    /// the whole game (it lives across scenes), over the HUD, the death screen and the freeze flash. The sizes are
    /// for a 1080p frame and PROPOSED.
    /// </summary>
    public sealed class SubtitleUI : MonoBehaviour
    {
        public static readonly Color Yellow = new Color(1f, 0.9f, 0.1f);
        public static readonly int[] Sizes = { 28, 34, 42 }; // Small, Medium, Large
        private const int SortingOrder = 112;
        private const float BottomGap = 56f;
        private const float Width = 1500f;

        private static SubtitleUI instance;
        private Canvas canvas;
        private readonly List<Text> texts = new List<Text>();
        private int drawnVersion = -1;
        private int drawnSize = -1;

        public static SubtitleUI Instance => instance;
        /// <summary>The lines the screen shows now, as drawn (tests).</summary>
        public IEnumerable<string> Shown
        {
            get
            {
                if (!canvas.enabled) yield break;
                foreach (Text t in texts) if (t.gameObject.activeSelf) yield return t.text;
            }
        }
        public bool Visible => canvas != null && canvas.enabled;
        public int FontSize => texts.Count > 0 ? texts[0].fontSize : 0;
        public IReadOnlyList<Text> Texts => texts;

        /// <summary>Makes the overlay once (the first line said makes it).</summary>
        public static void Ensure()
        {
            if (instance != null) return;
            var root = new GameObject("[Subtitles]");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<SubtitleUI>();
        }

        private void Awake()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var box = new GameObject("Lines", typeof(RectTransform));
            box.transform.SetParent(transform, false);
            var rect = (RectTransform)box.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, BottomGap);
            rect.sizeDelta = new Vector2(Width, 0f);
            var layout = box.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            box.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < SubtitleService.MaxLines; i++)
            {
                var obj = new GameObject("Line" + i, typeof(RectTransform));
                obj.transform.SetParent(box.transform, false);
                var text = obj.AddComponent<Text>();
                text.font = font;
                text.color = Yellow;
                text.alignment = TextAnchor.LowerCenter;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                var outline = obj.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(2f, -2f);
                obj.SetActive(false);
                texts.Add(text);
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            SubtitleService.Tick();
            SettingsManager settings = SettingsManager.Instance;
            // Hidden under the pause menu and the inventory (2026-10-26: a line drew over the pause menu).
            GameManager gm = GameManager.Instance;
            bool stopped = gm != null && (gm.IsPaused || gm.IsInventory || gm.IsMap);
            canvas.enabled = (settings == null || settings.SubtitlesOn) && !stopped;
            int size = Sizes[Mathf.Clamp(settings != null ? settings.SubtitleSize : 1, 0, Sizes.Length - 1)];
            if (SubtitleService.Version == drawnVersion && size == drawnSize) return;
            drawnVersion = SubtitleService.Version;
            drawnSize = size;
            IReadOnlyList<SubtitleService.Line> lines = SubtitleService.Lines;
            for (int i = 0; i < texts.Count; i++)
            {
                bool used = i < lines.Count;
                texts[i].gameObject.SetActive(used);
                texts[i].fontSize = size;
                if (used) texts[i].text = SubtitleService.Format(lines[i]);
            }
        }
    }
}
