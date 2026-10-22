using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using GlorifiedBody.Systems;
using GlorifiedBody.Tutorial;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The prompt of the tutorial at the bottom of the screen: the step count, the presenter's line and the
    /// control hint with the bound keys in place of the {Action} tokens (the remap changes the words).
    /// On "[RuntimeUI]" of the tutorial room. Hidden when there is no director.
    /// </summary>
    public sealed class TutorialPromptUI : MonoBehaviour
    {
        private static readonly Regex Token = new Regex(@"\{(\w+)\}");

        private Canvas canvas;
        private Text stepText;
        private Text lineText;
        private Text hintText;
        private TutorialDirector director;

        public Canvas Canvas => canvas;
        public string LineShown => lineText != null ? lineText.text : "";
        public string HintShown => hintText != null ? hintText.text : "";

        private void Start()
        {
            director = TutorialDirector.Instance;
            if (director == null)
            {
                enabled = false;
                return;
            }
            Build();
            director.OnStepChanged += Show;
            director.OnCompleted += OnCompleted;
            if (director.Current != null) Show(director.Current);
        }

        private void OnDestroy()
        {
            if (director == null) return;
            director.OnStepChanged -= Show;
            director.OnCompleted -= OnCompleted;
        }

        private void Build()
        {
            var root = new GameObject("TutorialPrompt", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 150f);
            panelRect.sizeDelta = new Vector2(1240f, 130f);
            var image = panel.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.72f);

            stepText = MakeText(panel.transform, "Step", 18, FontStyle.Normal, new Color(0.7f, 0.7f, 0.75f), new Vector2(0f, 48f), 24f);
            lineText = MakeText(panel.transform, "Line", 26, FontStyle.Bold, new Color(0.96f, 0.93f, 0.9f), new Vector2(0f, 10f), 38f);
            hintText = MakeText(panel.transform, "Hint", 20, FontStyle.Normal, new Color(0.85f, 0.9f, 1f), new Vector2(0f, -30f), 32f);
        }

        private static Text MakeText(Transform parent, string name, int size, FontStyle style, Color colour, Vector2 offset, float height)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(-40f, height);
            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void Show(TutorialStep step)
        {
            if (canvas == null) return;
            canvas.gameObject.SetActive(true);
            stepText.text = $"THE DEMONSTRATION   {director.StepIndex + 1} / {director.Steps.Count}";
            lineText.text = step.Line;
            hintText.text = Resolve(step.Hint);
        }

        private void OnCompleted()
        {
            if (stepText != null) stepText.text = "THE DEMONSTRATION   complete";
            if (hintText != null) hintText.text = "";
        }

        /// <summary>The {Action} tokens of a hint become the bound key names ("Space", "Mouse0", "W").</summary>
        public static string Resolve(string hint)
        {
            if (string.IsNullOrEmpty(hint)) return "";
            return Token.Replace(hint, m =>
            {
                if (!System.Enum.TryParse(m.Groups[1].Value, out InputManager.GameAction action)) return m.Value;
                KeyCode key = InputManager.Instance != null ? InputManager.Instance.GetKey(action) : KeyCode.None;
                return key == KeyCode.None ? m.Groups[1].Value : InputManager.GetKeyDisplayName(key);
            });
        }
    }
}
