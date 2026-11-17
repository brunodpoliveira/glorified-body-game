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
    /// On "[RuntimeUI]" of the tutorial room. Hidden when there is no director. The look (T46, 2026-11-17) follows the AC3
    /// menus: a dark rounded plate, the step count on an orange tag with the stripes, the line in Arimo, the keys in green.
    /// </summary>
    public sealed class TutorialPromptUI : MonoBehaviour
    {
        private static readonly Regex Token = new Regex(@"\{(\w+)\}");

        private Canvas canvas;
        private Text stepText;
        private Text lineText;
        private Text hintText;
        private string hintPlain = "";
        private TutorialDirector director;

        public Canvas Canvas => canvas;
        public string LineShown => lineText != null ? lineText.text : "";
        public string HintShown => hintText != null ? hintPlain : "";

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

            Image panel = MenuStyle.Plate(root.transform, "Panel", new Color(0.02f, 0.025f, 0.035f, 0.84f), new Color(0.95f, 0.94f, 0.9f, 0.55f));
            MenuStyle.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1240f, 130f));

            // The step count on an orange tag over the top edge, the stripes after it.
            Image tag = MenuStyle.Plate(panel.transform, "Tag", MenuStyle.Orange, MenuStyle.OrangeDeep);
            MenuStyle.Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(400f, 38f));
            stepText = MenuStyle.Words(tag.transform, "Step", "", MenuStyle.Head, 24, new Color(0.03f, 0.03f, 0.04f, 1f), TextAnchor.MiddleLeft);
            MenuStyle.Fill(stepText.rectTransform, 0f);
            stepText.rectTransform.offsetMin = new Vector2(18f, 0f);
            RawImage stripes = MenuStyle.StripeStrip(panel.transform, "Stripes", new Vector2(180f, 16f));
            MenuStyle.Place(stripes.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(444f, 0f), new Vector2(180f, 16f));

            lineText = MakeText(panel.transform, "Line", MenuStyle.Body, 30, MenuStyle.OffWhite, new Vector2(0f, 10f), 40f);
            hintText = MakeText(panel.transform, "Hint", MenuStyle.Head, 24, MenuStyle.Silver, new Vector2(0f, -36f), 32f);
        }

        private static Text MakeText(Transform parent, string name, Font font, int size, Color colour, Vector2 offset, float height)
        {
            Text text = MenuStyle.Words(parent, name, "", font, size, colour, TextAnchor.MiddleCenter);
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(-48f, height);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            // One line in its box: a long hint gets smaller (some run to 125 characters), down to 70 % of its size.
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = size;
            text.resizeTextMinSize = Mathf.RoundToInt(size * 0.7f);
            return text;
        }

        private void Show(TutorialStep step)
        {
            if (canvas == null) return;
            canvas.gameObject.SetActive(true);
            stepText.text = $"THE DEMONSTRATION   {director.StepIndex + 1} / {director.Steps.Count}";
            lineText.text = step.Line;
            hintPlain = Resolve(step.Hint);
            hintText.text = Resolve(step.Hint, true);
        }

        private void OnCompleted()
        {
            if (stepText != null) stepText.text = "THE DEMONSTRATION   COMPLETE";
            hintPlain = "";
            if (hintText != null) hintText.text = "";
        }

        /// <summary>The {Action} tokens of a hint become the bound key names ("Space", "Mouse0", "W").</summary>
        /// <param name="green">Wrap each key name in a green colour tag (the words on screen).</param>
        public static string Resolve(string hint, bool green = false)
        {
            if (string.IsNullOrEmpty(hint)) return "";
            return Token.Replace(hint, m =>
            {
                if (!System.Enum.TryParse(m.Groups[1].Value, out InputManager.GameAction action)) return m.Value;
                KeyCode key = InputManager.Instance != null ? InputManager.Instance.GetKey(action) : KeyCode.None;
                string name = key == KeyCode.None ? m.Groups[1].Value : InputManager.GetKeyDisplayName(key);
                return green ? MenuStyle.KeyTag(name) : name;
            });
        }
    }
}
