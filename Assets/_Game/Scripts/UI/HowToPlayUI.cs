using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GlorifiedBody.Systems;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// "How To Play" overlay: game premise plus the control scheme.
    /// Usable from the main menu (and reusable from pause). Runtime-built, mirrors CreditsUI.
    /// </summary>
    public class HowToPlayUI : MonoBehaviour
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
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnBackClicked();
            }
        }

        // ── UI Construction ────────────────────────────────────────────

        private void BuildUI()
        {
            GameObject canvasObj = new GameObject("HowToPlayCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 116;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }

            GameObject overlay = CreateUIElement("Overlay", canvasObj.transform);
            StretchFill(overlay.GetComponent<RectTransform>());
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            Transform safeAreaRoot = RuntimeUIRootUtility.CreateSafeAreaRoot(canvasObj.transform);

            rootPanel = CreateUIElement("HowToPlayRoot", safeAreaRoot);
            var rootRect = rootPanel.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            // 17 control rows at 24 px, the premise at its own height, the button: 760 overflowed after the dodge
            // and debug rows (2026-10-20). 990 holds them all, with the two telekinesis rows and the Defend binding,
            // on the 1080 reference height.
            rootRect.sizeDelta = new Vector2(720, 990);

            rootPanel.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.09f, 0.97f);

            var layout = rootPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 3;
            layout.padding = new RectOffset(48, 48, 24, 20);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var p = rootPanel.transform;

            // ── Title + premise ──
            CreateLabel(p, "HOW TO PLAY", 32, TextAnchor.MiddleCenter, 40, FontStyle.Bold);
            CreateDivider(p, new Color(0.3f, 0.35f, 0.5f, 0.4f), 1);
            CreateSpacer(p, 6);

            CreateSection(p, "PREMISE");
            CreateBody(p,
                "You are Lucy. You wake in the lab with no memory and three psychic powers: BLAST, FIRE, " +
                "TELEKINESIS. Every attack is a circle charge: hold to charge, release to strike. You stand " +
                "still while you charge. Three bars: HP, NALCON (psychic energy) and SHORT (it fills with " +
                "every kill and every hit you take). PLACEHOLDER TEXT: the writer supplies the final words.");
            CreateSpacer(p, 10);

            // ── Controls: the live bindings, so a remap shows here too ──
            CreateSection(p, "CONTROLS  (remap them in OPTIONS > CONTROLS)");
            if (InputManager.Instance != null)
            {
                foreach (var binding in InputManager.Instance.GetAllBindings())
                {
                    string key = InputManager.GetKeyDisplayName(InputManager.Instance.GetKey(binding.action));
                    CreateControl(p, key, binding.DisplayName);
                }
            }
            else
            {
                CreateControl(p, "W / S", "Walk forward / back");
                CreateControl(p, "A / D", "Turn left / right");
                CreateControl(p, "LMB (hold)", "Charge a power, release to strike");
            }
            CreateSpacer(p, 8);

            CreateSection(p, "SYSTEM");
            CreateControl(p, "Esc", "Pause / menus");
            CreateControl(p, "Space + W/A/S/D", "Dodge that way (no direction key: a step back)");
            CreateControl(p, "W/A/S/D twice", "A double tap: the same dodge, no Space");
            CreateControl(p, "LMB again (TK)", "Enemy in the air: turn to aim, throw it into another one");
            CreateControl(p, "RMB (TK), bullet near", "Defend: catch the bullet, send it back at the shooter");
            CreateControl(p, "F4", "Debug: kill Lucy (shows the death screen)");
            CreateControl(p, "F5 / F6 / F7", "Debug: fill SHORT / start the short / refill + drugs");
            CreateSpacer(p, 14);

            backBtn = CreateButton(p, "Back", OnBackClicked);
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
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
            var obj = CreateUIElement("Label", parent);
            obj.AddComponent<LayoutElement>().preferredHeight = height;

            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.fontStyle = style;
            txt.color = color ?? Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private void CreateSection(Transform parent, string title)
        {
            CreateLabel(parent, title, 15, TextAnchor.MiddleLeft, 22, FontStyle.Bold, new Color(0.55f, 0.65f, 1f));
        }

        private void CreateBody(Transform parent, string text)
        {
            var label = CreateLabel(parent, text, 14, TextAnchor.UpperLeft, 0, FontStyle.Italic, new Color(0.78f, 0.8f, 0.86f));
            // The vertical layout controls the height. A LayoutElement with -1 is "not set", so the Text supplies its
            // own preferred height from the wrapped lines. With 0 the row collapsed and the text overlapped the
            // controls (seen in how_to_play_ui.png, 2026-10-20). A ContentSizeFitter fights the layout group.
            label.GetComponent<LayoutElement>().preferredHeight = -1f;
        }

        private void CreateControl(Transform parent, string key, string desc)
        {
            var row = CreateUIElement("Control", parent);
            row.AddComponent<LayoutElement>().preferredHeight = 24;
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;

            var keyObj = CreateUIElement("Key", row.transform);
            var keyLe = keyObj.AddComponent<LayoutElement>();
            keyLe.preferredWidth = 220;
            keyLe.minWidth = 220;
            var keyTxt = keyObj.AddComponent<Text>();
            keyTxt.text = key;
            keyTxt.fontSize = 16;
            keyTxt.alignment = TextAnchor.MiddleLeft;
            keyTxt.fontStyle = FontStyle.Bold;
            keyTxt.color = new Color(1f, 0.85f, 0.4f);
            keyTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var descObj = CreateUIElement("Desc", row.transform);
            descObj.AddComponent<LayoutElement>().flexibleWidth = 1;
            var descTxt = descObj.AddComponent<Text>();
            descTxt.text = desc;
            descTxt.fontSize = 16;
            descTxt.alignment = TextAnchor.MiddleLeft;
            descTxt.color = new Color(0.9f, 0.9f, 0.92f);
            descTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            descTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private void CreateDivider(Transform parent, Color color, float height)
        {
            var obj = CreateUIElement("Divider", parent);
            obj.AddComponent<LayoutElement>().preferredHeight = height;
            obj.AddComponent<Image>().color = color;
        }

        private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var obj = CreateUIElement("Btn_" + label, parent);
            obj.AddComponent<LayoutElement>().preferredHeight = 50;

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
            StretchFill(txtObj.GetComponent<RectTransform>());
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
            obj.AddComponent<LayoutElement>().preferredHeight = height;
        }
    }
}
