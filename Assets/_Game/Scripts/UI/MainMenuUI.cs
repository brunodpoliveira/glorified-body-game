using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GlorifiedBody.Systems;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// Main menu screen. Shown when the MainMenu scene loads.
    /// Creates its own Canvas and UI elements at runtime.
    /// The first, default-selected button starts the game (GameManager.LoadGameplay, the training room in
    /// build order step 1). Difficulty presets and modifiers sit in a left-side panel that the DIFFICULTY
    /// button shows and hides. Notifies GameManager that the main menu is ready.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private OptionsMenuUI optionsMenu;
        [SerializeField] private CreditsUI creditsUI;
        [SerializeField] private HowToPlayUI howToPlayUI;

        [Header("Labels")]
        [SerializeField] private string startLabel = "START";
        [SerializeField] private string tutorialScene = "Tutorial"; // the therapy gym demonstration (Bruno, 2026-10-22)
        [SerializeField] private string subtitleLabel = "WORK IN PROGRESS";

        private Canvas canvas;
        private GameObject menuPanel;
        private bool isBuilt = false;

        // Button references for keyboard navigation wiring
        private Button defaultBtn;
        private Button startBtn;
        private Button modifiersBtn;
        private Button tutorialBtn;
        private Button howToBtn;
        private Button optionsBtn;
        private Button creditsBtn;
        private Button quitBtn;
        private EventSystem eventSystem;

        // Difficulty panel state
        private GameObject difficultyPanel;
        private Text modifiersLabel;
        private Text totalDifficultyLabel;
        private Text hintLabel;
        private readonly List<ModuleRow> moduleRows = new List<ModuleRow>();
        private const string DefaultDifficultyHint = "Hover a modifier for details.";

        void Start()
        {
            EnsureDifficultyManagerExists();
            EnsureSubMenusExist();
            BuildUI();

            // Notify GameManager
            if (GameManager.Instance != null)
                GameManager.Instance.OnMainMenuReady();

            // Bot runs only: save one shot of the menu (overlay canvases miss camera shots).
            if (OverlayShot.Enabled)
                StartCoroutine(OverlayShot.Capture(canvas, "main_menu_ui.png", 0.4f));
        }

        private void EnsureDifficultyManagerExists()
        {
            if (DifficultyManager.Instance == null)
            {
                // DifficultyManager may live on the [GameManagers] object or needs its own
                var managers = FindFirstObjectByType<GameManager>();
                if (managers != null)
                {
                    managers.gameObject.AddComponent<DifficultyManager>();
                }
                else
                {
                    var obj = new GameObject("[DifficultyManager]");
                    obj.AddComponent<DifficultyManager>();
                }
            }
        }

        private void EnsureSubMenusExist()
        {
            // Runtime-assembled main menu objects have no serialized refs; resolve or create siblings.
            if (optionsMenu == null)
            {
                optionsMenu = GetComponent<OptionsMenuUI>();
                if (optionsMenu == null) optionsMenu = gameObject.AddComponent<OptionsMenuUI>();
            }

            if (creditsUI == null)
            {
                creditsUI = GetComponent<CreditsUI>();
                if (creditsUI == null) creditsUI = gameObject.AddComponent<CreditsUI>();
            }

            if (howToPlayUI == null)
            {
                howToPlayUI = GetComponent<HowToPlayUI>();
                if (howToPlayUI == null) howToPlayUI = gameObject.AddComponent<HowToPlayUI>();
            }
        }

        void Update()
        {
            if (menuPanel == null) return;

            if (difficultyPanel != null && difficultyPanel.activeSelf && menuPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                SetModifiersOpen(false);

            // Show menu panel only when sub-menus are not visible
            bool shouldShow = (optionsMenu == null || !optionsMenu.IsVisible)
                           && (creditsUI == null || !creditsUI.IsVisible)
                           && (howToPlayUI == null || !howToPlayUI.IsVisible);
            if (menuPanel.activeSelf != shouldShow)
            {
                menuPanel.SetActive(shouldShow);
                if (shouldShow)
                {
                    RefreshDifficultyPanel();
                    SelectDefaultButton();
                }
            }
        }

        private void SelectDefaultButton()
        {
            if (eventSystem == null)
                eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem != null && defaultBtn != null)
                eventSystem.SetSelectedGameObject(defaultBtn.gameObject);
        }

        // ── Button Callbacks ─────────────────────────────────────────────

        private void OnTutorialClicked()
        {
            GameManager.Instance?.LoadLevel(tutorialScene);
        }

        private void OnStartClicked()
        {
            GameManager.Instance?.LoadGameplay();
        }

        private void OnModifiersClicked()
        {
            SetModifiersOpen(difficultyPanel == null || !difficultyPanel.activeSelf);
        }

        private void SetModifiersOpen(bool open)
        {
            if (difficultyPanel == null) return;
            difficultyPanel.SetActive(open);
            WireModifierNav(open);
            RefreshDifficultyPanel();
        }

        /// <summary>
        /// Keyboard/gamepad: while the panel is open, Left from MODIFIERS goes to the first row.
        /// The rows chain up/down, and Right goes back to MODIFIERS. Opening selects the first row;
        /// closing selects MODIFIERS again.
        /// </summary>
        private void WireModifierNav(bool open)
        {
            if (modifiersBtn == null) return;
            Navigation btnNav = modifiersBtn.navigation;
            Toggle first = open && moduleRows.Count > 0 ? moduleRows[0].toggle : null;
            btnNav.selectOnLeft = first;
            modifiersBtn.navigation = btnNav;

            for (int i = 0; i < moduleRows.Count; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? moduleRows[i - 1].toggle : null,
                    selectOnDown = i < moduleRows.Count - 1 ? moduleRows[i + 1].toggle : null,
                    selectOnRight = modifiersBtn
                };
                moduleRows[i].toggle.navigation = nav;
            }

            if (eventSystem == null)
                eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null) return;
            GameObject target = open ? (first != null ? first.gameObject : null) : modifiersBtn.gameObject;
            if (target != null)
                eventSystem.SetSelectedGameObject(target);
        }


        private void OnOptionsClicked()
        {
            if (optionsMenu != null)
            {
                menuPanel.SetActive(false);
                optionsMenu.Show(OnOptionsClosed);
            }
        }

        private void OnOptionsClosed()
        {
            menuPanel.SetActive(true);
        }

        private void OnHowToPlayClicked()
        {
            if (howToPlayUI != null)
            {
                menuPanel.SetActive(false);
                howToPlayUI.Show(OnHowToPlayClosed);
            }
        }

        private void OnHowToPlayClosed()
        {
            menuPanel.SetActive(true);
        }

        private void OnCreditsClicked()
        {
            if (creditsUI != null)
            {
                menuPanel.SetActive(false);
                creditsUI.Show(OnCreditsClosed);
            }
        }

        private void OnCreditsClosed()
        {
            menuPanel.SetActive(true);
        }

        private void OnQuitClicked()
        {
            GameManager.Instance?.QuitGame();
        }

        // ── Difficulty Module Callbacks ───────────────────────────────

        private void OnModuleToggled(DifficultyModuleDefinition mod, bool isOn)
        {
            if (DifficultyManager.Instance == null) return;
            if (mod.isBaseline) return;

            if (isOn)
                DifficultyManager.Instance.EnableModule(mod);
            else if (!mod.IsPreset)
                DifficultyManager.Instance.DisableModule(mod);

            RefreshDifficultyPanel();
        }

        private void RefreshDifficultyPanel()
        {
            if (DifficultyManager.Instance == null) return;

            totalDifficultyLabel.text = $"DIFFICULTY   {DifficultyManager.Instance.TotalDifficultyPercent}%";
            if (modifiersLabel != null)
            {
                bool open = difficultyPanel != null && difficultyPanel.activeSelf;
                string preset = DifficultyManager.Instance.ActivePreset != null ? DifficultyManager.Instance.ActivePreset.displayName : "-";
                modifiersLabel.text = $"DIFFICULTY  {preset}{(open ? "  [HIDE]" : "")}";
            }

            foreach (var row in moduleRows)
            {
                bool active = DifficultyManager.Instance.IsModuleActive(row.module);
                row.toggle.SetIsOnWithoutNotify(active);
                row.toggle.interactable = !row.module.isBaseline;

                if (active)
                {
                    row.background.color = row.module.isBaseline
                        ? new Color(0.2f, 0.4f, 0.7f, 0.9f)
                        : row.module.IsPreset
                            ? new Color(0.12f, 0.55f, 0.5f, 0.9f)
                            : new Color(0.85f, 0.55f, 0.1f, 0.9f);
                    row.symbolText.color = Color.white;
                    row.nameText.color = Color.white;
                    row.percentText.color = Color.white;
                }
                else
                {
                    row.background.color = new Color(0.13f, 0.13f, 0.17f, 0.94f);
                    row.symbolText.color = new Color(0.78f, 0.8f, 0.84f);
                    row.nameText.color = new Color(0.86f, 0.88f, 0.92f);
                    row.percentText.color = new Color(0.92f, 0.82f, 0.54f);
                }
            }
        }

        // ── UI Construction ────────────────────────────────────────────

        private void BuildUI()
        {
            if (isBuilt) return;

            // Canvas
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Ensure an EventSystem exists for UI interaction
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }

            // Background
            GameObject bg = CreateUIElement("Background", canvasObj.transform);
            var bgRect = bg.GetComponent<RectTransform>();
            StretchFill(bgRect);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.08f, 1f);

            Transform safeAreaRoot = RuntimeUIRootUtility.CreateSafeAreaRoot(canvasObj.transform);

            // Full-screen menu panel
            menuPanel = CreateUIElement("MenuPanel", safeAreaRoot);
            var panelRect = menuPanel.GetComponent<RectTransform>();
            StretchFill(panelRect);

            // ── Title area (upper centre) ──
            var titleArea = CreateUIElement("TitleArea", menuPanel.transform);
            var titleRect = titleArea.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.8f);
            titleRect.anchorMax = new Vector2(0.5f, 0.8f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(900, 150);

            var titleLayout = titleArea.AddComponent<VerticalLayoutGroup>();
            titleLayout.spacing = 8;
            titleLayout.childAlignment = TextAnchor.MiddleCenter;
            titleLayout.childControlWidth = true;
            titleLayout.childControlHeight = false;
            titleLayout.childForceExpandWidth = true;
            titleLayout.childForceExpandHeight = false;

            CreateLabel(titleArea.transform, "THE GLORIFIED BODY", 72, TextAnchor.MiddleCenter, 86, FontStyle.Bold);
            CreateLabel(titleArea.transform, subtitleLabel, 20, TextAnchor.MiddleCenter, 30, FontStyle.Normal, new Color(0.6f, 0.6f, 0.7f));

            // ── Left-side difficulty panel, hidden until DIFFICULTY ──
            BuildDifficultyPanel(menuPanel.transform);
            difficultyPanel.SetActive(false);

            // ── Main column (centre): start, then the rest ──
            var column = CreateUIElement("MainColumn", menuPanel.transform);
            var colRect = column.GetComponent<RectTransform>();
            colRect.anchorMin = new Vector2(0.5f, 0.62f);
            colRect.anchorMax = new Vector2(0.5f, 0.62f);
            colRect.pivot = new Vector2(0.5f, 1f);
            colRect.sizeDelta = new Vector2(760f, 0f);

            var colLayout = column.AddComponent<VerticalLayoutGroup>();
            colLayout.spacing = 10;
            colLayout.childAlignment = TextAnchor.UpperCenter;
            colLayout.childControlWidth = false;
            colLayout.childControlHeight = true;
            colLayout.childForceExpandWidth = false;
            colLayout.childForceExpandHeight = false;
            var colFitter = column.AddComponent<ContentSizeFitter>();
            colFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var nav = new List<Button>();
            startBtn = CreateButton(column.transform, startLabel, 760, OnStartClicked, 84, 34,
                new Color(0.62f, 0.1f, 0.12f, 0.95f), new Color(0.85f, 0.2f, 0.22f, 1f), new Color(0.96f, 0.93f, 0.9f));
            nav.Add(startBtn);
            CreateSpacer(column.transform, 10f);
            tutorialBtn = CreateButton(column.transform, "TUTORIAL", 420, OnTutorialClicked, 52);
            nav.Add(tutorialBtn);
            howToBtn = CreateButton(column.transform, "HOW TO PLAY", 420, OnHowToPlayClicked, 52);
            modifiersBtn = CreateButton(column.transform, "DIFFICULTY", 420, OnModifiersClicked, 52);
            modifiersLabel = modifiersBtn.GetComponentInChildren<Text>();
            optionsBtn = CreateButton(column.transform, "OPTIONS", 420, OnOptionsClicked, 52);
            creditsBtn = CreateButton(column.transform, "CREDITS", 420, OnCreditsClicked, 52);
            quitBtn = CreateButton(column.transform, "QUIT", 420, OnQuitClicked, 52);
            nav.Add(howToBtn);
            nav.Add(modifiersBtn);
            nav.Add(optionsBtn);
            nav.Add(creditsBtn);
            nav.Add(quitBtn);

            // ── Wire keyboard navigation (up/down) ──
            for (int i = 0; i < nav.Count; i++)
            {
                SetVerticalNav(nav[i], i > 0 ? nav[i - 1] : null, i < nav.Count - 1 ? nav[i + 1] : null);
            }

            defaultBtn = nav[0];

            // Select first button
            SelectDefaultButton();

            // Initial refresh
            RefreshDifficultyPanel();

            isBuilt = true;
        }

        // ── Difficulty Panel Construction ──────────────────────────────

        private void BuildDifficultyPanel(Transform parent)
        {
            // Container anchored to the left edge with a fixed on-screen margin.
            var container = CreateUIElement("DifficultyPanel", parent);
            difficultyPanel = container;
            var cRect = container.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0f, 1f);
            cRect.anchorMax = new Vector2(0f, 1f);
            cRect.pivot = new Vector2(0f, 1f);
            cRect.anchoredPosition = new Vector2(24f, -8f);
            cRect.sizeDelta = new Vector2(340f, 0f);

            // Panel background
            var panelBg = container.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.06f, 0.08f, 0.9f);

            // Vertical layout
            var layout = container.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 1;
            layout.padding = new RectOffset(10, 10, 6, 6);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = container.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Header
            CreateLabel(container.transform, "DIFFICULTY", 14, TextAnchor.MiddleCenter, 18, FontStyle.Bold,
                new Color(0.96f, 0.96f, 1f));

            // Total difficulty
            totalDifficultyLabel = CreateLabel(container.transform, "DIFFICULTY   100%", 13,
                TextAnchor.MiddleCenter, 16, FontStyle.Bold, new Color(1f, 0.8f, 0.2f));

            // Separator
            CreateSeparator(container.transform);

            // Module rows
            if (DifficultyManager.Instance != null)
            {
                var allModules = DifficultyManager.Instance.AllModules;
                for (int i = 0; i < allModules.Count; i++)
                {
                    var mod = allModules[i];
                    if (mod == null || mod.hideInUI) continue;
                    BuildModuleRow(container.transform, mod);
                }
            }

            // Separator before hint
            CreateSeparator(container.transform);

            // Hover hint label at bottom of panel
            hintLabel = CreateLabel(container.transform, DefaultDifficultyHint, 18, TextAnchor.UpperLeft, 32,
                FontStyle.Italic, new Color(0.9f, 0.91f, 0.86f));
            hintLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintLabel.verticalOverflow = VerticalWrapMode.Overflow;
            hintLabel.lineSpacing = 1f;
        }

        private void BuildModuleRow(Transform parent, DifficultyModuleDefinition mod)
        {
            // Outer row container
            var rowObj = CreateUIElement("Row_" + mod.moduleId, parent);
            var rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 28;

            var rowImg = rowObj.AddComponent<Image>();
            rowImg.color = new Color(0.13f, 0.13f, 0.17f, 0.94f);

            // Use a vertical layout inside: top line (toggle+symbol+name+%) and bottom line (description)
            var rowLayout = rowObj.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 0;
            rowLayout.padding = new RectOffset(10, 10, 2, 2);
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            // ── Top line: toggle + symbol + name + percent ──
            var topLine = CreateUIElement("TopLine", rowObj.transform);
            var topLE = topLine.AddComponent<LayoutElement>();
            topLE.preferredHeight = 18;

            var topLayout = topLine.AddComponent<HorizontalLayoutGroup>();
            topLayout.spacing = 4;
            topLayout.padding = new RectOffset(0, 0, 0, 0);
            topLayout.childAlignment = TextAnchor.MiddleLeft;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;
            topLayout.childForceExpandHeight = false;

            // Toggle checkbox
            var toggleObj = CreateUIElement("Toggle", topLine.transform);
            var toggleLE = toggleObj.AddComponent<LayoutElement>();
            toggleLE.minWidth = 10;
            toggleLE.preferredWidth = 10;
            toggleLE.minHeight = 10;
            toggleLE.preferredHeight = 10;

            var toggleBoxObj = CreateUIElement("Box", toggleObj.transform);
            var toggleBoxRect = toggleBoxObj.GetComponent<RectTransform>();
            toggleBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
            toggleBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
            toggleBoxRect.pivot = new Vector2(0.5f, 0.5f);
            toggleBoxRect.sizeDelta = new Vector2(10f, 10f);

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
            var symbolObj = CreateUIElement("Symbol", topLine.transform);
            var symbolLE = symbolObj.AddComponent<LayoutElement>();
            symbolLE.preferredWidth = 14;
            var symbolText = symbolObj.AddComponent<Text>();
            symbolText.text = mod.symbol;
            symbolText.fontSize = 12;
            symbolText.alignment = TextAnchor.MiddleCenter;
            symbolText.color = Color.white;
            symbolText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Name
            var nameObj = CreateUIElement("Name", topLine.transform);
            var nameLE = nameObj.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;
            var nameText = nameObj.AddComponent<Text>();
            nameText.text = mod.displayName;
            nameText.fontSize = 13;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = Color.white;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Percent
            var pctObj = CreateUIElement("Percent", topLine.transform);
            var pctLE = pctObj.AddComponent<LayoutElement>();
            pctLE.preferredWidth = 46;
            var pctText = pctObj.AddComponent<Text>();
            pctText.text = $"+{mod.difficultyPercent}%";
            pctText.fontSize = 13;
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
            moduleRows.Add(row);

            // Wire toggle
            var capturedMod = mod;
            toggle.onValueChanged.AddListener(isOn => OnModuleToggled(capturedMod, isOn));

            // Wire hover hint via EventTrigger
            var trigger = rowObj.AddComponent<EventTrigger>();

            var pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            pointerEnter.callback.AddListener(_ => { if (hintLabel != null) hintLabel.text = capturedMod.description; });
            trigger.triggers.Add(pointerEnter);

            var pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            pointerExit.callback.AddListener(_ => { if (hintLabel != null) hintLabel.text = DefaultDifficultyHint; });
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

            // Keyboard/gamepad selection shows the same description as the mouse hover.
            var selectTrigger = toggleObj.AddComponent<EventTrigger>();
            var select = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            select.callback.AddListener(_ => { if (hintLabel != null) hintLabel.text = capturedMod.description; });
            selectTrigger.triggers.Add(select);
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

        private Button CreateButton(Transform parent, string label, float width, UnityEngine.Events.UnityAction onClick,
            float height = 55, int fontSize = 24, Color? normal = null, Color? highlight = null, Color? textColor = null)
        {
            var obj = CreateUIElement("Btn_" + label, parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            ((RectTransform)obj.transform).sizeDelta = new Vector2(width, height);

            // The Image stays white; the Button tint gives the colour, so the selected state shows.
            var img = obj.AddComponent<Image>();
            img.color = Color.white;

            var btn = obj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = normal ?? new Color(0.12f, 0.12f, 0.15f, 0.9f);
            colors.highlightedColor = highlight ?? new Color(0.3f, 0.3f, 0.4f, 1f);
            colors.selectedColor = highlight ?? new Color(0.3f, 0.3f, 0.4f, 1f);
            colors.pressedColor = new Color(0.1f, 0.1f, 0.13f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            // Button label
            var txtObj = CreateUIElement("Text", obj.transform);
            var txtRect = txtObj.GetComponent<RectTransform>();
            StretchFill(txtRect);

            var txt = txtObj.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = fontSize;
            txt.fontStyle = fontSize >= 30 ? FontStyle.Bold : FontStyle.Normal;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = textColor ?? Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return btn;
        }

        /// <summary>
        /// Wire explicit up/down navigation between buttons.
        /// </summary>
        private void SetVerticalNav(Button btn, Button up, Button down)
        {
            var nav = new Navigation();
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = up;
            nav.selectOnDown = down;
            btn.navigation = nav;
        }

        private void CreateSeparator(Transform parent)
        {
            var obj = CreateUIElement("Separator", parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = 1;
            var img = obj.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        }

        private void CreateSpacer(Transform parent, float height)
        {
            var obj = CreateUIElement("Spacer", parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }

        // ── Data ─────────────────────────────────────────────────────

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
