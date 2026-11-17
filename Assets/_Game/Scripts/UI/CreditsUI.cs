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
            overlayImg.color = MenuStyle.Scrim;

            Transform safeAreaRoot = RuntimeUIRootUtility.CreateSafeAreaRoot(canvasObj.transform);

            // The AC3 frame (T46): the header plate holds the title, the framed body holds the names.
            MenuStyle.MenuFrame frame = MenuStyle.Frame(safeAreaRoot, "CREDITS", "GLORIFIED  /  DEMO " + Application.version, "STAFF", "credits", "ESC  BACK");
            rootPanel = CreateUIElement("CreditsRoot", frame.Content);
            var rootRect = rootPanel.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(640, 0);

            // Single vertical layout for all content
            var layout = rootPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.padding = new RectOffset(20, 20, 12, 10);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var p = rootPanel.transform; // shorthand

            // ── Title ──
            CreateSpacer(p, 4);

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
            backBtn = CreateButton(p, "BACK", OnBackClicked);

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
            txt.color = color ?? MenuStyle.OffWhite;
            txt.font = MenuStyle.Body;
            return txt;
        }

        private void CreateCreditSection(Transform parent, string title)
        {
            MenuStyle.HeadText(CreateLabel(parent, title, 18, TextAnchor.MiddleCenter, 24), MenuStyle.Blue);
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
            CreateLabel(parent, name, 19, TextAnchor.MiddleCenter, 25, FontStyle.Normal, MenuStyle.OffWhite);
        }

        private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var obj = CreateUIElement("Btn_" + label, parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = 50;

            obj.AddComponent<Image>();
            var btn = obj.AddComponent<Button>();
            MenuStyle.Style(btn);
            btn.onClick.AddListener(onClick);

            var txtObj = CreateUIElement("Text", obj.transform);
            var txtRect = txtObj.GetComponent<RectTransform>();
            StretchFill(txtRect);

            var txt = txtObj.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = 26;
            txt.alignment = TextAnchor.MiddleCenter;
            MenuStyle.HeadText(txt, MenuStyle.OffWhite);

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
