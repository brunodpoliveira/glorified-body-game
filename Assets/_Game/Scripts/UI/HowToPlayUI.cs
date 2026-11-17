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
            overlay.AddComponent<Image>().color = MenuStyle.Scrim;

            Transform safeAreaRoot = RuntimeUIRootUtility.CreateSafeAreaRoot(canvasObj.transform);

            // The AC3 frame (T46). Three columns: the controls in two, the system keys in the third. One column of
            // 1050 px overflowed with 39 rows, and the layout then shrank the premise into the controls (2026-11-17).
            MenuStyle.MenuFrame frame = MenuStyle.Frame(safeAreaRoot, "HOW TO PLAY", "THE PREMISE  /  THE KEYS  /  OPTIONS > CONTROLS REMAPS THEM", "MANUAL", "howtoplay", "ESC  BACK");
            rootPanel = CreateUIElement("HowToPlayRoot", frame.Content);
            var rootRect = rootPanel.GetComponent<RectTransform>();
            StretchFill(rootRect);

            var layout = rootPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 3;
            layout.padding = new RectOffset(24, 24, 10, 8);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var p = rootPanel.transform;

            // ── Premise ──
            CreateSection(p, "PREMISE");
            CreateBody(p,
                "You are Lucy. You wake in the lab with no memory and three psychic powers: mk_warspite (blast), " +
                "mk_erebus (fire), mk_orzabal (telekinesis). Every attack is a circle charge: hold to charge, release to strike. You stand " +
                "still while you charge. Your bars: HP, one MK bar for each power, and SHORT. MK does not come back by itself: only the capsule " +
                "of that power fills its bar. Every power you use, every kill and every hit you take fills SHORT. A short takes about half your HP, and below that it kills you. PLACEHOLDER TEXT: the writer supplies the final words.");
            CreateSpacer(p, 10);

            var columns = CreateUIElement("Columns", p);
            columns.AddComponent<LayoutElement>().flexibleHeight = 1;
            var cols = columns.AddComponent<HorizontalLayoutGroup>();
            cols.spacing = 40;
            cols.childControlWidth = true;
            cols.childControlHeight = true;
            cols.childForceExpandWidth = true;
            cols.childForceExpandHeight = true;
            Transform left = CreateColumn(columns.transform);
            Transform middle = CreateColumn(columns.transform);
            Transform right = CreateColumn(columns.transform);

            // ── Controls: the live bindings, so a remap shows here too. Half in each of the first two columns. ──
            var controls = new System.Collections.Generic.List<(string key, string desc)>();
            if (InputManager.Instance != null)
            {
                foreach (var binding in InputManager.Instance.GetAllBindings())
                    controls.Add((InputManager.GetKeyDisplayName(InputManager.Instance.GetKey(binding.action)), binding.DisplayName));
                controls.Add(("Mouse left / right", "Turn left / right (OPTIONS > CONTROLS: Mouse Turn)"));
            }
            else
            {
                controls.Add(("W / S", "Walk forward / back"));
                controls.Add(("A / D", "Turn left / right"));
                controls.Add(("LMB (hold)", "Charge a power, release to strike"));
            }
            int half = (controls.Count + 1) / 2;
            CreateSection(left, "CONTROLS");
            CreateSection(middle, " ");
            for (int i = 0; i < controls.Count; i++)
                CreateControl(i < half ? left : middle, controls[i].key, controls[i].desc);
            p = right;
            CreateSection(p, "SYSTEM");
            CreateControl(p, "Esc", "Pause / menus");
            CreateControl(p, "Space + W/A/S/D", "Dodge that way (no direction key: a step back)");
            CreateControl(p, "W/A/S/D twice", "A double tap: the same dodge, no Space");
            CreateControl(p, "Mouse wheel", "Switch the target. With a thing in the air (TK): raise it");
            CreateControl(p, "LMB (TK)", "Lift the locked target at once");
            CreateControl(p, "LMB hold (TK), a thing in the air", "Turn to aim first. A longer hold throws it harder, for more MK. A full circle slams it");
            CreateControl(p, "RMB (TK), bullet near", "Defend: catch the bullet, send it back at the shooter");
            CreateControl(p, "RMB (mk_edgar), a mind taken", "Send it to a switch in reach. The switch glows violet");
            CreateControl(p, "RMB (TK), a box in the air", "Let it down gently. It costs nothing, and it does not break");
            // The debug keys only where they work (GameBuild.Dev): a release does not list them (2026-10-31).
            if (GlorifiedBody.Systems.GameBuild.Dev)
            {
                CreateControl(p, "F4", "Debug: kill Lucy (shows the death screen)");
                CreateControl(p, "F5 / F6 / F7", "Debug: fill SHORT / start the short / refill (debug only) + drugs");
                CreateControl(p, "F8 / F9", "Debug: selected MK -25 / +25");
                CreateControl(p, "F12", "Debug: the soldiers' care counts her as unseen"); // F11 left with the look test rooms (2026-11-16)
            }
            p = rootPanel.transform;
            CreateSpacer(p, 6);

            backBtn = CreateButton(p, "BACK", OnBackClicked);
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

        private Transform CreateColumn(Transform parent)
        {
            var col = CreateUIElement("Column", parent);
            var v = col.AddComponent<VerticalLayoutGroup>();
            v.spacing = 3;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return col.transform;
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
            txt.color = color ?? MenuStyle.OffWhite;
            txt.font = MenuStyle.Body;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private void CreateSection(Transform parent, string title)
        {
            MenuStyle.HeadText(CreateLabel(parent, title, 20, TextAnchor.MiddleLeft, 28), MenuStyle.Blue);
        }

        private void CreateBody(Transform parent, string text)
        {
            var label = CreateLabel(parent, text, 16, TextAnchor.UpperLeft, 0, FontStyle.Normal, MenuStyle.Silver);
            // The vertical layout controls the height. A LayoutElement with -1 is "not set", so the Text supplies its
            // own preferred height from the wrapped lines. With 0 the row collapsed and the text overlapped the
            // controls (seen in how_to_play_ui.png, 2026-10-20). A ContentSizeFitter fights the layout group.
            label.GetComponent<LayoutElement>().preferredHeight = -1f;
        }

        private void CreateControl(Transform parent, string key, string desc)
        {
            var row = CreateUIElement("Control", parent);
            // One line is 24 px. A long description takes a second line. No flexible height: with it, the rows
            // spread out to fill a column that has room (the HorizontalLayoutGroup reports a flexible height of 1).
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 24;
            rowLe.flexibleHeight = 0;
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.childForceExpandHeight = false;
            h.spacing = 12;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;

            var keyObj = CreateUIElement("Key", row.transform);
            var keyLe = keyObj.AddComponent<LayoutElement>();
            keyLe.preferredWidth = 190;
            keyLe.minWidth = 190;
            var keyTxt = keyObj.AddComponent<Text>();
            keyTxt.text = key;
            keyTxt.fontSize = 18;
            keyTxt.alignment = TextAnchor.MiddleLeft;
            keyTxt.color = MenuStyle.Orange;
            keyTxt.font = MenuStyle.Head;
            keyTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            keyTxt.verticalOverflow = VerticalWrapMode.Overflow;
            keyTxt.alignment = TextAnchor.UpperLeft;

            var descObj = CreateUIElement("Desc", row.transform);
            descObj.AddComponent<LayoutElement>().flexibleWidth = 1;
            var descTxt = descObj.AddComponent<Text>();
            descTxt.text = desc;
            descTxt.fontSize = 16;
            descTxt.alignment = TextAnchor.UpperLeft;
            descTxt.color = MenuStyle.OffWhite;
            descTxt.font = MenuStyle.Body;
            descTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            descTxt.verticalOverflow = VerticalWrapMode.Overflow;
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
            obj.AddComponent<LayoutElement>().preferredHeight = 44;

            obj.AddComponent<Image>();
            var btn = obj.AddComponent<Button>();
            MenuStyle.Style(btn);
            btn.onClick.AddListener(onClick);

            var txtObj = CreateUIElement("Text", obj.transform);
            StretchFill(txtObj.GetComponent<RectTransform>());
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
            obj.AddComponent<LayoutElement>().preferredHeight = height;
        }
    }
}
