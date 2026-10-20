using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GlorifiedBody.UI
{
    public abstract class RuntimeOverlayMenu : MonoBehaviour
    {
        [SerializeField] private string title = "MENU";
        [SerializeField] private string subtitle = "";
        [SerializeField] private Color panelColor = new Color(0.1f, 0.1f, 0.12f, 0.96f);
        [SerializeField] private Color accentColor = new Color(0.85f, 0.9f, 1f);
        [SerializeField] private int sortingOrder = 100;

        private Canvas canvas;
        private Button defaultButton;

        protected bool IsVisible => canvas != null && canvas.gameObject.activeSelf;
        protected Canvas Canvas => canvas;
        protected virtual string MenuTitle => title;
        protected virtual string MenuSubtitle => subtitle;
        protected virtual Color AccentColor => accentColor;
        protected virtual Vector2 PanelSize => new Vector2(500f, 390f);

        protected virtual void Start()
        {
            BuildUI();
            Hide();
        }

        protected abstract bool ShouldShow { get; }
        protected abstract void BuildButtons(Transform parent);

        /// <summary>Content between the subtitle and the buttons. The base menu adds nothing.</summary>
        protected virtual void BuildBody(Transform parent) { }

        /// <summary>Called each time the menu becomes visible.</summary>
        protected virtual void OnShown() { }

        protected virtual void Update()
        {
            bool shouldShow = ShouldShow;
            if (shouldShow && !IsVisible)
            {
                Show();
            }
            else if (!shouldShow && IsVisible)
            {
                Hide();
            }
        }

        protected void Show()
        {
            if (canvas == null)
            {
                BuildUI();
            }

            canvas.gameObject.SetActive(true);
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem != null && defaultButton != null)
            {
                eventSystem.SetSelectedGameObject(defaultButton.gameObject);
            }

            OnShown();
        }

        protected void Hide()
        {
            if (canvas != null)
            {
                canvas.gameObject.SetActive(false);
            }
        }

        protected Button AddButton(Transform parent, string label, UnityAction onClick, bool makeDefault = false)
        {
            var obj = CreateElement("Btn_" + label.Replace(" ", "_"), parent);
            var layoutElement = obj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 56f;

            var image = obj.AddComponent<Image>();
            image.color = new Color(0.17f, 0.18f, 0.2f, 1f);

            var button = obj.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.28f, 0.3f, 0.34f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.1f, 0.11f, 0.13f, 1f);
            button.colors = colors;
            button.onClick.AddListener(onClick);

            Text text = AddLabel(obj.transform, label, 22, TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            Stretch(text.rectTransform);

            if (defaultButton == null || makeDefault)
            {
                defaultButton = button;
            }

            return button;
        }

        protected Text AddLabel(Transform parent, string text, int fontSize, TextAnchor alignment, FontStyle fontStyle, Color color)
        {
            var obj = CreateElement("Label", parent);
            var label = obj.AddComponent<Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.fontStyle = fontStyle;
            label.color = color;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return label;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        protected static GameObject CreateElement(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private void BuildUI()
        {
            if (canvas != null)
            {
                return;
            }

            var canvasObj = new GameObject(GetType().Name + "Canvas");
            canvasObj.transform.SetParent(transform, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            var overlay = CreateElement("Overlay", canvasObj.transform);
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);

            Transform safeRoot = RuntimeUIRootUtility.CreateSafeAreaRoot(canvasObj.transform);
            var panel = CreateElement("Panel", safeRoot);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = PanelSize;
            panel.AddComponent<Image>().color = panelColor;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.padding = new RectOffset(34, 34, 34, 34);
            layout.childAlignment = TextAnchor.UpperCenter;
            // Control the height, so each LayoutElement.preferredHeight applies (buttons 56, title 52, picture 270).
            // With childControlHeight off every element keeps the default 100 units and the panel overflows.
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text titleText = AddLabel(panel.transform, MenuTitle, 34, TextAnchor.MiddleCenter, FontStyle.Bold, AccentColor);
            titleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;

            if (!string.IsNullOrWhiteSpace(MenuSubtitle))
            {
                Text subtitleText = AddLabel(panel.transform, MenuSubtitle, 18, TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.85f, 0.86f, 0.9f));
                subtitleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            }

            BuildBody(panel.transform);
            BuildButtons(panel.transform);
        }
    }
}
