using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GlorifiedBody.Systems;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// Runtime-built difficulty panel: the preset rows (EASY, NORMAL, HARD: one active at a time) on top,
    /// the free modifiers under them, each with symbol, name and difficulty %. Opened from the pause menu.
    /// </summary>
    public class DifficultyMenuUI : MonoBehaviour
    {
        private Canvas canvas;
        private GameObject panel;
        private Text totalLabel;
        private Text hintLabel;
        private bool isBuilt = false;
        private System.Action onCloseCallback;

        private readonly List<ModuleRow> rows = new List<ModuleRow>();
        private const string DefaultHintText = "Hover a row to read its effect.";

        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;

        // ── Public API ───────────────────────────────────────────────────

        public void Show(System.Action onClose = null)
        {
            onCloseCallback = onClose;
            if (!isBuilt) BuildUI();
            RefreshAll();
            canvas.gameObject.SetActive(true);
            SelectFirstToggle();
        }

        public void Hide()
        {
            if (canvas != null)
                canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!IsVisible) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void Close()
        {
            Hide();
            onCloseCallback?.Invoke();
            onCloseCallback = null;
        }

        // ── Refresh ──────────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (DifficultyManager.Instance == null) return;

            // Update total
            totalLabel.text = $"DIFFICULTY   {DifficultyManager.Instance.TotalDifficultyPercent}%";

            // Update each row
            foreach (var row in rows)
            {
                bool active = DifficultyManager.Instance.IsModuleActive(row.module);
                row.toggle.SetIsOnWithoutNotify(active);
                row.toggle.interactable = !row.module.isBaseline;
                UpdateRowColors(row, active);
            }
        }

        private void UpdateRowColors(ModuleRow row, bool active)
        {
            // Active: warm amber/orange like SYNTHETIK. Inactive: muted dark.
            if (active)
            {
                row.background.color = new Color(0.85f, 0.55f, 0.1f, 0.9f);
                row.symbolText.color = Color.white;
                row.nameText.color = Color.white;
                row.percentText.color = Color.white;
            }
            else
            {
                row.background.color = new Color(0.15f, 0.15f, 0.19f, 0.94f);
                row.symbolText.color = new Color(0.78f, 0.8f, 0.84f);
                row.nameText.color = new Color(0.88f, 0.9f, 0.94f);
                row.percentText.color = new Color(0.92f, 0.82f, 0.54f);
            }

            // Baseline modules get a subtle blue tint, the active preset a teal one
            if (row.module.isBaseline && active)
            {
                row.background.color = new Color(0.2f, 0.4f, 0.7f, 0.9f);
            }
            else if (row.module.IsPreset && active)
            {
                row.background.color = new Color(0.12f, 0.55f, 0.5f, 0.9f);
            }

            // Negative-difficulty (accessibility) modules get a teal tint when active
            if (row.module.difficultyPercent < 0 && active)
            {
                row.background.color = new Color(0.1f, 0.6f, 0.55f, 0.9f);
                row.percentText.color = new Color(0.6f, 1f, 0.9f);
            }
        }

        // ── Toggle Callback ──────────────────────────────────────────────

        private void OnModuleToggled(DifficultyModuleDefinition mod, bool isOn)
        {
            if (DifficultyManager.Instance == null) return;
            if (mod.isBaseline) return; // Safety check

            if (isOn)
                DifficultyManager.Instance.EnableModule(mod);
            else if (!mod.IsPreset)
                DifficultyManager.Instance.DisableModule(mod);

            RefreshAll();
        }

        // ── UI Construction ──────────────────────────────────────────────

        private void BuildUI()
        {
            // Canvas
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

            // Ensure EventSystem exists
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
            overlayImg.color = new Color(0, 0, 0, 0.7f);

            // Main panel — left-aligned like SYNTHETIK
            Transform safeAreaRoot = RuntimeUIRootUtility.CreateSafeAreaRoot(canvasObj.transform);
            panel = CreateUIElement("DifficultyPanel", safeAreaRoot);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(40f, -30f);
            panelRect.sizeDelta = new Vector2(420f, 0f);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

            // Panel layout
            var panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.spacing = 6;
            panelLayout.padding = new RectOffset(18, 18, 20, 20);
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            var panelFitter = panel.AddComponent<ContentSizeFitter>();
            panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Header
            var headerLabel = CreateLabel(panel.transform, "DIFFICULTY", 26, TextAnchor.MiddleCenter, 42, Color.white);
            headerLabel.fontStyle = FontStyle.Bold;

            // Total difficulty display
            totalLabel = CreateLabel(panel.transform, "DIFFICULTY   100%", 20, TextAnchor.MiddleCenter, 36,
                new Color(1f, 0.8f, 0.2f));
            totalLabel.fontStyle = FontStyle.Bold;

            // Preset rows, then the modifier rows
            if (DifficultyManager.Instance != null)
            {
                var allModules = DifficultyManager.Instance.AllModules;
                CreateSeparator(panel.transform);
                CreateLabel(panel.transform, "PRESET  (one at a time)", 15, TextAnchor.MiddleLeft, 24, new Color(0.7f, 0.9f, 0.86f));
                for (int i = 0; i < allModules.Count; i++)
                {
                    var mod = allModules[i];
                    if (mod == null || mod.hideInUI || !mod.IsPreset) continue;
                    CreateModuleRow(panel.transform, mod);
                }

                CreateSeparator(panel.transform);
                CreateLabel(panel.transform, "MODIFIERS  (free toggles)", 15, TextAnchor.MiddleLeft, 24, new Color(0.92f, 0.82f, 0.54f));
                for (int i = 0; i < allModules.Count; i++)
                {
                    var mod = allModules[i];
                    if (mod == null || mod.hideInUI || mod.IsPreset) continue;
                    CreateModuleRow(panel.transform, mod);
                }
            }

            // Separator before hint
            CreateSeparator(panel.transform);

            // Hover hint label
            hintLabel = CreateLabel(panel.transform, DefaultHintText, 15, TextAnchor.UpperLeft, 56,
                new Color(0.9f, 0.91f, 0.86f));
            hintLabel.fontStyle = FontStyle.Italic;
            hintLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintLabel.verticalOverflow = VerticalWrapMode.Overflow;
            hintLabel.lineSpacing = 1.05f;

            // Spacer
            CreateSpacer(panel.transform, 8);

            // Close hint
            CreateLabel(panel.transform, "Press ESC to close", 15, TextAnchor.MiddleCenter, 24,
                new Color(0.68f, 0.7f, 0.76f));

            isBuilt = true;
        }

        private void CreateModuleRow(Transform parent, DifficultyModuleDefinition mod)
        {
            // Row container
            var rowObj = CreateUIElement("Row_" + mod.moduleId, parent);
            var rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 50;

            var rowImg = rowObj.AddComponent<Image>();
            rowImg.color = new Color(0.15f, 0.15f, 0.19f, 0.94f);

            // Horizontal layout
            var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8;
            rowLayout.padding = new RectOffset(16, 16, 6, 6);
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            // Toggle (checkbox)
            var toggleObj = CreateUIElement("Toggle", rowObj.transform);
            var toggleLE = toggleObj.AddComponent<LayoutElement>();
            toggleLE.minWidth = 12;
            toggleLE.preferredWidth = 12;
            toggleLE.minHeight = 12;
            toggleLE.preferredHeight = 12;

            var toggleBoxObj = CreateUIElement("Box", toggleObj.transform);
            var toggleBoxRect = toggleBoxObj.GetComponent<RectTransform>();
            toggleBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
            toggleBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
            toggleBoxRect.pivot = new Vector2(0.5f, 0.5f);
            toggleBoxRect.sizeDelta = new Vector2(12f, 12f);

            var toggleBg = toggleBoxObj.AddComponent<Image>();
            toggleBg.color = new Color(0.18f, 0.2f, 0.26f, 0.98f);

            var checkObj = CreateUIElement("Checkmark", toggleBoxObj.transform);
            var checkRect = checkObj.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.25f, 0.25f);
            checkRect.anchorMax = new Vector2(0.75f, 0.75f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            var checkImg = checkObj.AddComponent<Image>();
            checkImg.color = Color.white;

            var toggle = toggleObj.AddComponent<Toggle>();
            toggle.targetGraphic = toggleBg;
            toggle.graphic = checkImg;
            toggle.isOn = false;

            // Symbol
            var symbolObj = CreateUIElement("Symbol", rowObj.transform);
            var symbolLE = symbolObj.AddComponent<LayoutElement>();
            symbolLE.preferredWidth = 32;
            var symbolText = symbolObj.AddComponent<Text>();
            symbolText.text = mod.symbol;
            symbolText.fontSize = 24;
            symbolText.alignment = TextAnchor.MiddleCenter;
            symbolText.color = Color.white;
            symbolText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Name
            var nameObj = CreateUIElement("Name", rowObj.transform);
            var nameLE = nameObj.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;
            var nameText = nameObj.AddComponent<Text>();
            nameText.text = mod.displayName;
            nameText.fontSize = 20;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = Color.white;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Percent
            var pctObj = CreateUIElement("Percent", rowObj.transform);
            var pctLE = pctObj.AddComponent<LayoutElement>();
            pctLE.preferredWidth = 72;
            var pctText = pctObj.AddComponent<Text>();
            pctText.text = mod.difficultyPercent >= 0 ? $"+{mod.difficultyPercent}%" : $"{mod.difficultyPercent}%";
            pctText.fontSize = 19;
            pctText.alignment = TextAnchor.MiddleRight;
            pctText.fontStyle = FontStyle.Bold;
            pctText.color = Color.white;
            pctText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Store row reference
            var row = new ModuleRow
            {
                module = mod,
                toggle = toggle,
                background = rowImg,
                symbolText = symbolText,
                nameText = nameText,
                percentText = pctText
            };
            rows.Add(row);

            // Wire toggle listener (capture local reference)
            var capturedMod = mod;
            toggle.onValueChanged.AddListener(isOn => OnModuleToggled(capturedMod, isOn));

            // Wire hover hint via EventTrigger
            var trigger = rowObj.AddComponent<EventTrigger>();

            var pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            pointerEnter.callback.AddListener(_ => { if (hintLabel != null) hintLabel.text = capturedMod.description; });
            trigger.triggers.Add(pointerEnter);

            var pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            pointerExit.callback.AddListener(_ => { if (hintLabel != null) hintLabel.text = DefaultHintText; });
            trigger.triggers.Add(pointerExit);

            var pointerClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            pointerClick.callback.AddListener(_ =>
            {
                if (toggle != null && toggle.interactable)
                {
                    toggle.isOn = !toggle.isOn;
                }
            });
            trigger.triggers.Add(pointerClick);
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

        private void CreateSpacer(Transform parent, float height)
        {
            var obj = CreateUIElement("Spacer", parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }

        private void SelectFirstToggle()
        {
            if (rows.Count == 0) return;
            var es = FindFirstObjectByType<EventSystem>();
            if (es != null)
                es.SetSelectedGameObject(rows[0].toggle.gameObject);
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
    }
}
