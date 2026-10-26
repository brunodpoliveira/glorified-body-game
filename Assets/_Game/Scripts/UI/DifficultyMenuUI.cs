using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GlorifiedBody.Systems;
using Section = GlorifiedBody.Systems.DifficultyModuleDefinition.Section;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The difficulty screen (Bruno, 2026-10-26), from the main menu and the pause menu: the total on top, the three
    /// presets (100 %, 150 %, 200 %) under it, then the modules in two columns, HARDER and EASIER. A change of a module
    /// makes the set CUSTOM unless it matches a preset. The row under the pointer or the keyboard selection shows its
    /// effect at the bottom. Built at runtime.
    /// </summary>
    public class DifficultyMenuUI : MonoBehaviour
    {
        private Canvas canvas;
        private Text totalLabel;
        private Text hintLabel;
        private bool isBuilt = false;
        private System.Action onCloseCallback;

        private readonly List<ModuleRow> rows = new List<ModuleRow>();
        private readonly List<PresetButton> presetButtons = new List<PresetButton>();
        private const string DefaultHintText = "Pick a preset, or turn modules on and off for a custom difficulty.";

        private static readonly Color HarderColour = new Color(0.85f, 0.55f, 0.1f, 0.92f);
        private static readonly Color EasierColour = new Color(0.1f, 0.55f, 0.5f, 0.92f);
        private static readonly Color OffColour = new Color(0.15f, 0.15f, 0.19f, 0.94f);

        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;

        /// <summary>The rows of a section on screen (tests and the bot).</summary>
        public int RowCount(Section section) => rows.FindAll(r => r.module.section == section).Count;

        /// <summary>The words of the total line (tests and the bot).</summary>
        public string TotalText => totalLabel != null ? totalLabel.text : "";

        // ── Public API ───────────────────────────────────────────────────

        public void Show(System.Action onClose = null)
        {
            onCloseCallback = onClose;
            if (!isBuilt) BuildUI();
            RefreshAll();
            canvas.gameObject.SetActive(true);
            SelectFirst();
        }

        public void Hide()
        {
            if (canvas != null)
                canvas.gameObject.SetActive(false);
        }

        /// <summary>The click on a preset button (the UI, tests and the bot).</summary>
        public void PressPreset(int percent)
        {
            DifficultyManager.Instance?.SetPreset(percent);
            RefreshAll();
        }

        /// <summary>The click on a module row: it goes on or off (the UI, tests and the bot).</summary>
        public void PressModule(string moduleId)
        {
            ModuleRow row = rows.Find(r => r.module.moduleId == moduleId);
            if (row == null || DifficultyManager.Instance == null) return;
            DifficultyManager.Instance.ToggleModule(row.module);
            RefreshAll();
        }

        private void Update()
        {
            if (!IsVisible) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        /// <summary>Hides the screen and calls back the menu that opened it (ESC, tests and the bot).</summary>
        public void Close()
        {
            Hide();
            onCloseCallback?.Invoke();
            onCloseCallback = null;
        }

        // ── Refresh ──────────────────────────────────────────────────────

        private void RefreshAll()
        {
            DifficultyManager manager = DifficultyManager.Instance;
            if (manager == null || totalLabel == null) return;

            int preset = manager.ActivePresetPercent;
            totalLabel.text = preset > 0 ? $"{manager.TotalDifficultyPercent}%   PRESET" : $"{manager.TotalDifficultyPercent}%   CUSTOM";

            foreach (var button in presetButtons)
            {
                bool on = button.percent == preset;
                button.image.color = on ? new Color(0.62f, 0.1f, 0.12f, 0.95f) : OffColour;
                button.label.color = on ? Color.white : new Color(0.86f, 0.88f, 0.92f);
            }

            foreach (var row in rows)
            {
                bool active = manager.IsModuleActive(row.module);
                row.toggle.SetIsOnWithoutNotify(active);
                row.background.color = active ? (row.module.section == Section.Easier ? EasierColour : HarderColour) : OffColour;
                row.nameText.color = active ? Color.white : new Color(0.88f, 0.9f, 0.94f);
                row.symbolText.color = active ? Color.white : new Color(0.78f, 0.8f, 0.84f);
                row.percentText.color = active ? Color.white : (row.module.section == Section.Easier ? new Color(0.6f, 1f, 0.9f) : new Color(0.92f, 0.82f, 0.54f));
            }
        }

        // ── UI Construction ──────────────────────────────────────────────

        private void BuildUI()
        {
            GameObject canvasObj = new GameObject("DifficultyMenuCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;

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
            GameObject panel = CreateUIElement("DifficultyPanel", safeAreaRoot);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1180f, 0f);
            panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.97f);
            var panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.spacing = 8;
            panelLayout.padding = new RectOffset(28, 28, 20, 20);
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateLabel(panel.transform, "DIFFICULTY", 30, TextAnchor.MiddleCenter, 44, Color.white).fontStyle = FontStyle.Bold;
            totalLabel = CreateLabel(panel.transform, "150%", 24, TextAnchor.MiddleCenter, 36, new Color(1f, 0.8f, 0.2f));
            totalLabel.fontStyle = FontStyle.Bold;

            // The presets in one row.
            GameObject presetRow = CreateUIElement("Presets", panel.transform);
            presetRow.AddComponent<LayoutElement>().preferredHeight = 56;
            var presetLayout = presetRow.AddComponent<HorizontalLayoutGroup>();
            presetLayout.spacing = 16;
            presetLayout.childAlignment = TextAnchor.MiddleCenter;
            presetLayout.childControlWidth = true;
            presetLayout.childControlHeight = true;
            presetLayout.childForceExpandWidth = false;
            presetLayout.childForceExpandHeight = true;
            foreach (int percent in DifficultyManager.PresetPercents) CreatePresetButton(presetRow.transform, percent);

            CreateSeparator(panel.transform);

            // The two sections side by side.
            GameObject columns = CreateUIElement("Sections", panel.transform);
            var columnsLayout = columns.AddComponent<HorizontalLayoutGroup>();
            columnsLayout.spacing = 24;
            columnsLayout.childAlignment = TextAnchor.UpperCenter;
            columnsLayout.childControlWidth = true;
            columnsLayout.childControlHeight = true;
            columnsLayout.childForceExpandWidth = true;
            columnsLayout.childForceExpandHeight = false;
            Transform harder = CreateColumn(columns.transform, "HARDER", new Color(0.95f, 0.75f, 0.4f));
            Transform easier = CreateColumn(columns.transform, "EASIER", new Color(0.6f, 0.95f, 0.88f));
            if (DifficultyManager.Instance != null)
            {
                foreach (var mod in DifficultyManager.Instance.AllModules)
                {
                    if (mod == null || mod.hideInUI || mod.isBaseline) continue;
                    CreateModuleRow(mod.section == Section.Easier ? easier : harder, mod);
                }
            }

            CreateSeparator(panel.transform);
            hintLabel = CreateLabel(panel.transform, DefaultHintText, 19, TextAnchor.MiddleCenter, 40, new Color(0.9f, 0.91f, 0.86f));
            hintLabel.fontStyle = FontStyle.Italic;
            hintLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            CreateLabel(panel.transform, "Press ESC to close", 16, TextAnchor.MiddleCenter, 26, new Color(0.68f, 0.7f, 0.76f));

            isBuilt = true;
        }

        private Transform CreateColumn(Transform parent, string title, Color titleColour)
        {
            GameObject column = CreateUIElement("Column_" + title, parent);
            var layout = column.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            CreateLabel(column.transform, title, 20, TextAnchor.MiddleLeft, 32, titleColour).fontStyle = FontStyle.Bold;
            return column.transform;
        }

        private void CreatePresetButton(Transform parent, int percent)
        {
            GameObject obj = CreateUIElement("Preset_" + percent, parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredWidth = 220;
            var image = obj.AddComponent<Image>();
            image.color = OffColour;
            var button = obj.AddComponent<Button>();
            var colours = button.colors;
            colours.highlightedColor = new Color(1.25f, 1.25f, 1.35f, 1f);
            colours.selectedColor = new Color(1.25f, 1.25f, 1.35f, 1f);
            button.colors = colours;
            Text label = CreateLabel(obj.transform, percent + "%", 26, TextAnchor.MiddleCenter, 56, Color.white);
            label.fontStyle = FontStyle.Bold;
            StretchFill(label.rectTransform);
            int captured = percent;
            button.onClick.AddListener(() => PressPreset(captured));
            AddHint(obj, $"The {percent}% preset: " + PresetText(percent));
            presetButtons.Add(new PresetButton { percent = percent, button = button, image = image, label = label });
        }

        /// <summary>The names of the modules a preset turns on.</summary>
        private static string PresetText(int percent)
        {
            if (DifficultyManager.Instance == null) return "";
            var names = new List<string>();
            foreach (var mod in DifficultyManager.Instance.AllModules)
            {
                if (mod != null && !mod.isBaseline && mod.InPreset(percent)) names.Add(mod.displayName);
            }
            return names.Count == 0 ? "the lab as designed, no module." : string.Join(", ", names) + ".";
        }

        private void CreateModuleRow(Transform parent, DifficultyModuleDefinition mod)
        {
            var rowObj = CreateUIElement("Row_" + mod.moduleId, parent);
            rowObj.AddComponent<LayoutElement>().preferredHeight = 44;
            var rowImg = rowObj.AddComponent<Image>();
            rowImg.color = OffColour;

            var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8;
            rowLayout.padding = new RectOffset(14, 14, 4, 4);
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            // The toggle is the row: it takes the clicks and the keyboard selection.
            var toggle = rowObj.AddComponent<Toggle>();
            toggle.targetGraphic = rowImg;
            toggle.transition = Selectable.Transition.None;
            var outline = rowObj.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0f);
            outline.effectDistance = new Vector2(2f, -2f);

            var symbolText = AddRowText(rowObj.transform, "Symbol", mod.symbol, 22, TextAnchor.MiddleCenter, 40, 0);
            var nameText = AddRowText(rowObj.transform, "Name", mod.displayName, 20, TextAnchor.MiddleLeft, 0, 1);
            nameText.fontStyle = FontStyle.Bold;
            string pct = mod.difficultyPercent >= 0 ? $"+{mod.difficultyPercent}%" : $"{mod.difficultyPercent}%";
            var percentText = AddRowText(rowObj.transform, "Percent", pct, 19, TextAnchor.MiddleRight, 72, 0);
            percentText.fontStyle = FontStyle.Bold;

            var row = new ModuleRow { module = mod, toggle = toggle, background = rowImg, symbolText = symbolText, nameText = nameText, percentText = percentText };
            rows.Add(row);

            string id = mod.moduleId;
            toggle.onValueChanged.AddListener(_ => PressModule(id));
            AddHint(rowObj, mod.description + (string.IsNullOrEmpty(mod.exclusiveGroup) ? "" : "  (One of a kind: it turns off the others of its kind.)"),
                () => outline.effectColor = new Color(1f, 1f, 1f, 0.8f), () => outline.effectColor = new Color(1f, 1f, 1f, 0f));
        }

        private Text AddRowText(Transform parent, string name, string text, int size, TextAnchor anchor, float width, float flexible)
        {
            var obj = CreateUIElement(name, parent);
            var le = obj.AddComponent<LayoutElement>();
            if (width > 0f) le.preferredWidth = width;
            le.flexibleWidth = flexible;
            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = size;
            txt.alignment = anchor;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.raycastTarget = false;
            return txt;
        }

        /// <summary>The pointer over it or the keyboard selection on it shows the words in the hint line.</summary>
        private void AddHint(GameObject obj, string words, System.Action on = null, System.Action off = null)
        {
            var trigger = obj.AddComponent<EventTrigger>();
            void Add(EventTriggerType type, System.Action action)
            {
                var entry = new EventTrigger.Entry { eventID = type };
                entry.callback.AddListener(_ => action());
                trigger.triggers.Add(entry);
            }
            System.Action show = () => { if (hintLabel != null) hintLabel.text = words; on?.Invoke(); };
            System.Action hide = () => { if (hintLabel != null) hintLabel.text = DefaultHintText; off?.Invoke(); };
            Add(EventTriggerType.PointerEnter, show);
            Add(EventTriggerType.Select, show);
            Add(EventTriggerType.PointerExit, hide);
            Add(EventTriggerType.Deselect, hide);
        }

        // ── UI Helpers ───────────────────────────────────────────────────

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

        private Text CreateLabel(Transform parent, string text, int fontSize, TextAnchor alignment, float height, Color color)
        {
            var obj = CreateUIElement("Label_" + text.Replace(" ", ""), parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;

            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = color;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.raycastTarget = false;
            return txt;
        }

        private void CreateSeparator(Transform parent)
        {
            var obj = CreateUIElement("Separator", parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = 2;
            var img = obj.AddComponent<Image>();
            img.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        }

        private void SelectFirst()
        {
            var es = FindFirstObjectByType<EventSystem>();
            if (es == null || presetButtons.Count == 0) return;
            int preset = DifficultyManager.Instance != null ? DifficultyManager.Instance.ActivePresetPercent : 0;
            PresetButton pick = presetButtons.Find(b => b.percent == preset) ?? presetButtons[0];
            es.SetSelectedGameObject(pick.button.gameObject);
        }

        // ── Data ─────────────────────────────────────────────────────────

        private class ModuleRow
        {
            public DifficultyModuleDefinition module;
            public Toggle toggle;
            public Image background;
            public Text symbolText;
            public Text nameText;
            public Text percentText;
        }

        private class PresetButton
        {
            public int percent;
            public Button button;
            public Image image;
            public Text label;
        }
    }
}
