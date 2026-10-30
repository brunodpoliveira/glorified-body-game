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
    /// build order step 1). The DIFFICULTY button opens the difficulty screen (DifficultyMenuUI, the same as in
    /// the pause menu; Bruno, 2026-10-26). Notifies GameManager that the main menu is ready.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private OptionsMenuUI optionsMenu;
        [SerializeField] private CreditsUI creditsUI;
        [SerializeField] private HowToPlayUI howToPlayUI;
        [SerializeField] private DifficultyMenuUI difficultyMenu;

        [Header("Labels")]
        [SerializeField] private string startLabel = "START";
        [SerializeField] private string tutorialScene = "Tutorial"; // the therapy gym demonstration (Bruno, 2026-10-22)
        [SerializeField] private string aiTestScene = "AiTest";     // the FSM soldiers' hall (Bruno, 2026-10-23)
        [SerializeField] private string coverTestScene = "CoverTest"; // the breakable cover (Bruno, 2026-10-24)
        [SerializeField] private string civilianTestScene = "CivilianTest"; // the unarmed civilians (Bruno, 2026-10-25)
        [SerializeField] private string kitTestScene = "KitTest";       // every system in one room (2026-10-26)
        [SerializeField] private string scriptTestScene = "ScriptTest"; // the ultimatum, a gate, a cutscene (2026-10-26)
        [SerializeField] private string travelTestScene = "TravelTest_A"; // two rooms and a door (2026-10-26)
        [SerializeField] private string labTestScene = "LabTest";       // the armed lab workers, a carried key (2026-10-27)
        [SerializeField] private string wardTestScene = "WardTest";     // a patient, a scan on a person, feeding (2026-10-30)
        [SerializeField] private string subtitleLabel = "WORK IN PROGRESS";

        private Canvas canvas;
        private GameObject menuPanel;
        private bool isBuilt = false;

        // Button references for keyboard navigation wiring
        private Button defaultBtn;
        private Button startBtn;
        private Button modifiersBtn;
        private Button tutorialBtn;
        private Button continueBtn;
        private Button loadBtn;
        private Button aiTestBtn;
        private Button coverTestBtn;
        private Button civilianTestBtn;
        private Button howToBtn;
        private Button optionsBtn;
        private Button creditsBtn;
        private Button quitBtn;
        private EventSystem eventSystem;

        private Text modifiersLabel;

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

            if (difficultyMenu == null)
            {
                difficultyMenu = GetComponent<DifficultyMenuUI>();
                if (difficultyMenu == null) difficultyMenu = gameObject.AddComponent<DifficultyMenuUI>();
            }
        }

        void Update()
        {
            if (menuPanel == null) return;

            // Show menu panel only when sub-menus are not visible
            bool shouldShow = (optionsMenu == null || !optionsMenu.IsVisible)
                           && (creditsUI == null || !creditsUI.IsVisible)
                           && (howToPlayUI == null || !howToPlayUI.IsVisible)
                           && (difficultyMenu == null || !difficultyMenu.IsVisible);
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

        private void OnContinueClicked()
        {
            int slot = GlorifiedBody.Saving.SaveSystem.Newest();
            if (slot >= 0) GlorifiedBody.Saving.SaveSystem.Load(slot);
        }

        private void OnLoadClicked() => SaveMenuUI.OpenLoad();

        private void OnTutorialClicked()
        {
            GameManager.Instance?.LoadLevel(tutorialScene);
        }

        private void OnAiTestClicked()
        {
            GameManager.Instance?.LoadLevel(aiTestScene);
        }

        private void OnCoverTestClicked()
        {
            GameManager.Instance?.LoadLevel(coverTestScene);
        }

        private void OnCivilianTestClicked()
        {
            GameManager.Instance?.LoadLevel(civilianTestScene);
        }

        private void OnKitTestClicked() => GameManager.Instance?.LoadLevel(kitTestScene);
        private void OnScriptTestClicked() => GameManager.Instance?.LoadLevel(scriptTestScene);
        private void OnTravelTestClicked() => GameManager.Instance?.LoadLevel(travelTestScene);
        private void OnLabTestClicked() => GameManager.Instance?.LoadLevel(labTestScene);
        private void OnWardTestClicked() => GameManager.Instance?.LoadLevel(wardTestScene);

        private void OnStartClicked()
        {
            GameManager.Instance?.LoadGameplay();
        }

        private void OnModifiersClicked()
        {
            if (difficultyMenu == null) return;
            menuPanel.SetActive(false);
            difficultyMenu.Show(OnDifficultyClosed);
        }

        private void OnDifficultyClosed()
        {
            menuPanel.SetActive(true);
            RefreshDifficultyPanel();
            if (eventSystem == null) eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem != null && modifiersBtn != null) eventSystem.SetSelectedGameObject(modifiersBtn.gameObject);
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

        /// <summary>The DIFFICULTY button names the active preset, or CUSTOM and its total.</summary>
        private void RefreshDifficultyPanel()
        {
            if (DifficultyManager.Instance == null || modifiersLabel == null) return;
            modifiersLabel.text = $"DIFFICULTY  {DifficultyManager.Instance.Label}";
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
            // The saves (Bruno, 2026-10-25): CONTINUE takes the newest file, LOAD GAME lists the slots.
            continueBtn = CreateButton(column.transform, "CONTINUE", 420, OnContinueClicked, 48);
            continueBtn.interactable = GlorifiedBody.Saving.SaveSystem.Newest() >= 0;
            nav.Add(continueBtn);
            loadBtn = CreateButton(column.transform, "LOAD GAME", 420, OnLoadClicked, 48);
            nav.Add(loadBtn);
            CreateSpacer(column.transform, 10f);
            tutorialBtn = CreateButton(column.transform, "TUTORIAL", 420, OnTutorialClicked, 48);
            nav.Add(tutorialBtn);
            howToBtn = CreateButton(column.transform, "HOW TO PLAY", 420, OnHowToPlayClicked, 48);
            modifiersBtn = CreateButton(column.transform, "DIFFICULTY", 420, OnModifiersClicked, 48);
            modifiersLabel = modifiersBtn.GetComponentInChildren<Text>();
            optionsBtn = CreateButton(column.transform, "OPTIONS", 420, OnOptionsClicked, 48);
            creditsBtn = CreateButton(column.transform, "CREDITS", 420, OnCreditsClicked, 48);
            quitBtn = CreateButton(column.transform, "QUIT", 420, OnQuitClicked, 48);
            nav.Add(howToBtn);
            nav.Add(modifiersBtn);
            nav.Add(optionsBtn);
            nav.Add(creditsBtn);
            nav.Add(quitBtn);

            // ── The test rooms (right): one column of their own, so the main column fits the screen (2026-10-26:
            // with the three new rooms the column ran past the bottom) ──
            var tests = CreateUIElement("TestColumn", menuPanel.transform);
            var testsRect = tests.GetComponent<RectTransform>();
            testsRect.anchorMin = new Vector2(0.84f, 0.62f);
            testsRect.anchorMax = new Vector2(0.84f, 0.62f);
            testsRect.pivot = new Vector2(0.5f, 1f);
            testsRect.sizeDelta = new Vector2(320f, 0f);
            var testsLayout = tests.AddComponent<VerticalLayoutGroup>();
            testsLayout.spacing = 10;
            testsLayout.childAlignment = TextAnchor.UpperCenter;
            testsLayout.childControlWidth = false;
            testsLayout.childControlHeight = true;
            testsLayout.childForceExpandWidth = false;
            testsLayout.childForceExpandHeight = false;
            tests.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Text testsHeader = CreateLabel(tests.transform, "TEST ROOMS", 22, TextAnchor.MiddleCenter, 36, FontStyle.Bold, new Color(0.6f, 0.6f, 0.7f));
            testsHeader.rectTransform.sizeDelta = new Vector2(300f, testsHeader.rectTransform.sizeDelta.y); // the column does not size its children
            aiTestBtn = CreateButton(tests.transform, "AI TEST", 300, OnAiTestClicked, 44);
            coverTestBtn = CreateButton(tests.transform, "COVER TEST", 300, OnCoverTestClicked, 44);
            civilianTestBtn = CreateButton(tests.transform, "CIVILIAN TEST", 300, OnCivilianTestClicked, 44);
            nav.Add(aiTestBtn);
            nav.Add(coverTestBtn);
            nav.Add(civilianTestBtn);
            nav.Add(CreateButton(tests.transform, "KIT TEST", 300, OnKitTestClicked, 44));
            nav.Add(CreateButton(tests.transform, "SCRIPT TEST", 300, OnScriptTestClicked, 44));
            nav.Add(CreateButton(tests.transform, "TRAVEL TEST", 300, OnTravelTestClicked, 44));
            nav.Add(CreateButton(tests.transform, "LAB TEST", 300, OnLabTestClicked, 44));
            nav.Add(CreateButton(tests.transform, "WARD TEST", 300, OnWardTestClicked, 44));

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

        private void CreateSpacer(Transform parent, float height)
        {
            var obj = CreateUIElement("Spacer", parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }
    }
}
