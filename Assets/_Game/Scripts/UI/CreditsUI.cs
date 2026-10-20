using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// Credits screen overlay. Usable from both MainMenu and PauseMenu.
    /// Creates its own Canvas and UI elements at runtime.
    /// </summary>
    public class CreditsUI : MonoBehaviour
    {
        private Canvas canvas;
        private GameObject rootPanel;
        private bool isBuilt = false;
        private Action onCloseCallback;
        private Button backBtn;

        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;

        void Start()
        {
            BuildUI();
            Hide();
        }

        public void Show(Action onClose = null)
        {
            if (!isBuilt) BuildUI();
            onCloseCallback = onClose;
            canvas.gameObject.SetActive(true);

            // Select back button for keyboard nav
            var es = FindFirstObjectByType<EventSystem>();
            if (es != null && backBtn != null)
                es.SetSelectedGameObject(backBtn.gameObject);
        }

        public void Hide()
        {
            if (canvas != null)
                canvas.gameObject.SetActive(false);
        }

        private void OnBackClicked()
        {
            Hide();
            onCloseCallback?.Invoke();
            onCloseCallback = null;
        }

        void Update()
        {
            if (!IsVisible) return;

            // Allow Escape to close credits
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnBackClicked();
            }
        }

        // ── UI Construction ────────────────────────────────────────────

        private void BuildUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("CreditsCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 115;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Ensure an EventSystem exists
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }

            // Dim overlay
            GameObject overlay = CreateUIElement("Overlay", canvasObj.transform);
            StretchFill(overlay.GetComponent<RectTransform>());
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.8f);

            Transform safeAreaRoot = RuntimeUIRootUtility.CreateSafeAreaRoot(canvasObj.transform);

            // Root panel (centered)
            rootPanel = CreateUIElement("CreditsRoot", safeAreaRoot);
            var rootRect = rootPanel.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(480, 620);

            var panelImg = rootPanel.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.06f, 0.09f, 0.97f);

            // Single vertical layout for all content
            var layout = rootPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.padding = new RectOffset(50, 50, 25, 20);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var p = rootPanel.transform; // shorthand

            // ── Title ──
            CreateLabel(p, "CREDITS", 32, TextAnchor.MiddleCenter, 40, FontStyle.Bold);
            CreateSpacer(p, 4);
            CreateLabel(p, "THE GLORIFIED BODY", 20, TextAnchor.MiddleCenter, 28, FontStyle.Bold, new Color(0.85f, 0.85f, 1f));
            CreateLabel(p, "Vertical Slice Demo", 14, TextAnchor.MiddleCenter, 18, FontStyle.Italic, new Color(0.55f, 0.55f, 0.65f));
            CreateDivider(p, new Color(0.3f, 0.35f, 0.5f, 0.4f), 1);
            CreateSpacer(p, 8);

            // Development
            CreateCreditSection(p, "DEVELOPMENT");
            CreateCreditEntry(p, "Bruno Oliveira");
            CreateCreditEntry(p, "Terry Cradock");
            CreateSpacer(p, 8);

            // Game Design
            CreateCreditSection(p, "GAME DESIGN");
            CreateCreditEntry(p, "Bruno Oliveira");
            CreateSpacer(p, 8);

            // Programming
            CreateCreditSection(p, "PROGRAMMING");
            CreateCreditEntry(p, "Bruno Oliveira");
            CreateSpacer(p, 8);

            // Plugins & Tools
            CreateCreditSection(p, "PLUGINS, ADDONS & TOOLS");
            CreateCreditEntry(p, "Unity 6 (6000.3.10f1)");
            CreateCreditEntry(p, "Invector Third Person Controller");
            CreateSpacer(p, 8);

            // Special Thanks
            CreateCreditSection(p, "SPECIAL THANKS");
            CreateCreditEntry(p, "Simone Collins");
            CreateCreditEntry(p, "Malcolm Collins");
            CreateSpacer(p, 12);

            // ── Back Button ──
            backBtn = CreateButton(p, "Back", OnBackClicked);

            // Wire nav (single button, just needs explicit mode)
            var nav = new Navigation();
            nav.mode = Navigation.Mode.Explicit;
            backBtn.navigation = nav;

            isBuilt = true;
        }

        // ── UI Helpers ─────────────────────────────────────────────────

        private GameObject CreateUIElement(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private void StretchFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private Text CreateLabel(Transform parent, string text, int fontSize, TextAnchor alignment, float height, FontStyle style = FontStyle.Normal, Color? color = null)
        {
            var obj = CreateUIElement("Label_" + text.Replace("\n", ""), parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;

            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.fontStyle = style;
            txt.color = color ?? Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return txt;
        }

        private void CreateCreditSection(Transform parent, string title)
        {
            CreateLabel(parent, title, 13, TextAnchor.MiddleCenter, 18, FontStyle.Bold, new Color(0.55f, 0.65f, 1f));
        }

        private void CreateDivider(Transform parent, Color color, float height)
        {
            var obj = CreateUIElement("Divider", parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            var img = obj.AddComponent<Image>();
            img.color = color;
        }

        private void CreateCreditEntry(Transform parent, string name)
        {
            CreateLabel(parent, name, 16, TextAnchor.MiddleCenter, 22, FontStyle.Normal, new Color(0.9f, 0.9f, 0.9f));
        }

        private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var obj = CreateUIElement("Btn_" + label, parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = 50;

            var img = obj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            var btn = obj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.2f, 0.2f, 0.25f, 1f);
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.4f, 1f);
            colors.selectedColor = new Color(0.35f, 0.35f, 0.4f, 1f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.2f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var txtObj = CreateUIElement("Text", obj.transform);
            var txtRect = txtObj.GetComponent<RectTransform>();
            StretchFill(txtRect);

            var txt = txtObj.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = 22;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return btn;
        }

        private void CreateSpacer(Transform parent, float height)
        {
            var obj = CreateUIElement("Spacer", parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }
    }
}
