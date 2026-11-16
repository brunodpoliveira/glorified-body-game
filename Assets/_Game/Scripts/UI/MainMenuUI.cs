using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GlorifiedBody.Systems;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The main menu (T46, round 173, after Ace Combat 3 Electrosphere): the Memorial background, the red-striped logo,
    /// one row of minimal symbols. START (CONTINUE when a save exists), LOAD, ADVANCED, EXIT. ADVANCED is a second row:
    /// NEW GAME (with a save only), DIFFICULTY, OPTIONS, TUTORIAL, HOW TO PLAY, CREDITS, BACK. The art loads from
    /// Resources/UI/Menu and the fonts from Resources/UI/Fonts (production/ASSET_NAMES.md §6b).
    /// START goes to the demo (Bruno, 2026-10-31). The test rooms (dev only) keep their own column on the right.
    /// Builds its Canvas at run time and tells GameManager that the main menu is ready.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private OptionsMenuUI optionsMenu;
        [SerializeField] private CreditsUI creditsUI;
        [SerializeField] private HowToPlayUI howToPlayUI;
        [SerializeField] private DifficultyMenuUI difficultyMenu;

        [SerializeField] private string tutorialScene = "Tutorial"; // the therapy gym demonstration (Bruno, 2026-10-22)
        [SerializeField] private string aiTestScene = "AiTest";     // the FSM soldiers' hall (Bruno, 2026-10-23)
        [SerializeField] private string kitTestScene = "KitTest";       // every system in one room (2026-10-26)
        [SerializeField] private string physicsTestScene = "PhysicsTest"; // the carry and the boxes (2026-11-06)
        public const string BrainTestScene = "BrainTest"; // the brain fight (2026-11-08)
        private const float TestButtonHeight = 32f;
        /// <summary>START: the demo, one world scene with every room (2026-11-02; a scene per room since 2026-10-31).</summary>
        public const string DemoScene = "Demo";

        // The art (T46).
        public const string ArtFolder = "UI/Menu/";
        public const string FontFolder = "UI/Fonts/";
        private const float HeadSqueeze = 0.64f;    // Roboto Condensed 700 at 64 % (round 129)
        private const float ProfileSqueeze = 0.77f; // Arimo 600 at 77 % (round 129)
        private const float BackgroundBrightness = 0.85f;

        private static Font headFont, profileFont;
        public static Font HeadFont => headFont != null ? headFont : headFont = LoadFont("RobotoCondensed-wght700");
        public static Font ProfileFont => profileFont != null ? profileFont : profileFont = LoadFont("Arimo-wght600");

        private static Font LoadFont(string name)
        {
            Font f = Resources.Load<Font>(FontFolder + name);
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private Canvas canvas;
        private GameObject menuPanel;
        private GameObject advancedPanel;
        private GameObject testColumn;
        private Text stripLeft;
        private bool advancedOpen;
        private bool hasSave;
        private string saveSpot = "";
        private bool isBuilt = false;

        private Button defaultBtn;
        private Button startBtn;
        private Button advancedBtn;
        private Button advancedDefaultBtn;
        private Button modifiersBtn;
        private Button optionsBtn, howToBtn, creditsBtn;
        private Button firstTestBtn;
        private EventSystem eventSystem;

        private Text modifiersLabel;

        // The new-game intro (round 174).
        private RawImage backgroundPicture;
        private RectTransform logoRect;
        private RectTransform stripesRect;
        private RawImage stripesImage;
        private CanvasGroup stripGroup;
        private Image curtain;
        private bool introRunning;
        private float introTime;
        public const float IntroSeconds = 2.6f;

        /// <summary>The new-game intro plays (tests).</summary>
        public bool IntroRunning => introRunning;

        /// <summary>The ADVANCED row is open (tests).</summary>
        public bool AdvancedOpen => advancedOpen;

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

            // The "intro" bot (tools/unity.sh play Main_Menu Main_Menu intro 120): START after the menu shot, the intro,
            // its checks (RunIntroBot); no level loads.
            if (IntroBot) StartCoroutine(RunIntroBot());
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
            if (menuPanel == null || introRunning) return;

            bool subMenu = (optionsMenu != null && optionsMenu.IsVisible)
                        || (creditsUI != null && creditsUI.IsVisible)
                        || (howToPlayUI != null && howToPlayUI.IsVisible)
                        || (difficultyMenu != null && difficultyMenu.IsVisible);
            bool prompt = tutorialPrompt != null && tutorialPrompt.activeSelf;
            bool showMain = !subMenu && !prompt && !advancedOpen;
            bool showAdvanced = !subMenu && !prompt && advancedOpen;
            if (menuPanel.activeSelf != showMain || advancedPanel.activeSelf != showAdvanced)
            {
                menuPanel.SetActive(showMain);
                advancedPanel.SetActive(showAdvanced);
                if (showMain || showAdvanced)
                {
                    RefreshDifficultyPanel();
                    SelectDefaultButton();
                }
            }
            stripLeft.text = advancedOpen ? "ADVANCED" : (hasSave ? $"SAVE DATA  {saveSpot}".TrimEnd() : "NO SAVE DATA");

            // Escape on the ADVANCED row goes back to the main row.
            if (showAdvanced && Input.GetKeyDown(KeyCode.Escape)) OnBackClicked();

            // A click on empty space drops the selection: put it back, so the keys keep working.
            if ((showMain || showAdvanced) && eventSystem != null && eventSystem.currentSelectedGameObject == null)
                SelectDefaultButton();
        }

        private void SelectDefaultButton()
        {
            if (eventSystem == null)
                eventSystem = FindFirstObjectByType<EventSystem>();
            Button target = advancedOpen ? advancedDefaultBtn : defaultBtn;
            if (eventSystem != null && target != null)
                eventSystem.SetSelectedGameObject(target.gameObject);
        }

        private void Select(Button b)
        {
            if (eventSystem == null) eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem != null && b != null) eventSystem.SetSelectedGameObject(b.gameObject);
        }

        private void OnAdvancedClicked()
        {
            advancedOpen = true;
            menuPanel.SetActive(false);
            advancedPanel.SetActive(true);
            RefreshDifficultyPanel();
            Select(advancedDefaultBtn);
        }

        private void OnBackClicked()
        {
            advancedOpen = false;
            advancedPanel.SetActive(false);
            menuPanel.SetActive(true);
            Select(advancedBtn);
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
            TutorialRecord.StartGameAfter = false; // from the menu: the end screen of the tutorial, as before
            GameManager.Instance?.LoadLevel(tutorialScene);
        }

        private void OnAiTestClicked()
        {
            GameManager.Instance?.LoadLevel(aiTestScene);
        }

        private void OnKitTestClicked() => GameManager.Instance?.LoadLevel(kitTestScene);
        private void OnPhysicsTestClicked() => GameManager.Instance?.LoadLevel(physicsTestScene);
        private void OnBrainTestClicked() => GameManager.Instance?.LoadLevel(BrainTestScene);
        private void OnTrainingClicked() => GameManager.Instance?.LoadGameplay();
        // Inside the glass doors from G2, the hall in front of her (the point of DiningHallTests).
        private void OnDiningHallClicked() => GlorifiedBody.DebugTools.DevRoomStart.Begin("G18", new Vector3(36.3f, 0f, 3f), 90f);

        /// <summary>
        /// START (Bruno, 2026-11-16: "add a prompt when starting the game: play tutorial first (Recommended) yes/no"): until the
        /// player finished the tutorial once (TutorialRecord), START asks first. YES plays the tutorial, and the game starts at
        /// its end. NO starts the game, and the next START asks again. Batch runs do not ask (the START test).
        /// </summary>
        private void OnStartClicked()
        {
            if (!TutorialRecord.Done && !Application.isBatchMode)
            {
                ShowTutorialPrompt();
                return;
            }
            PlayNewGameIntro(DemoScene);
        }

        private GameObject tutorialPrompt;

        private void ShowTutorialPrompt()
        {
            if (tutorialPrompt == null) BuildTutorialPrompt();
            HideRows();
            tutorialPrompt.SetActive(true);
            Button yes = tutorialPrompt.GetComponentInChildren<Button>();
            if (eventSystem != null && yes != null) eventSystem.SetSelectedGameObject(yes.gameObject);
        }

        private void BuildTutorialPrompt()
        {
            tutorialPrompt = CreateUIElement("TutorialPrompt", menuPanel.transform.parent);
            var rect = tutorialPrompt.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.55f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(760f, 0f);
            var layout = tutorialPrompt.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            tutorialPrompt.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Text ask = CreateLabel(tutorialPrompt.transform, "Play the tutorial first?", 40, TextAnchor.MiddleCenter, 60, FontStyle.Bold);
            ask.rectTransform.sizeDelta = new Vector2(760f, ask.rectTransform.sizeDelta.y);
            Text why = CreateLabel(tutorialPrompt.transform, "It teaches the powers, the lock and the dodge in a safe room. The game starts after it.", 22, TextAnchor.MiddleCenter, 34, FontStyle.Normal, new Color(0.7f, 0.7f, 0.78f));
            why.rectTransform.sizeDelta = new Vector2(760f, why.rectTransform.sizeDelta.y);
            Button yes = CreateButton(tutorialPrompt.transform, "YES (RECOMMENDED)", 520, OnTutorialFirst, 64, 28,
                new Color(0.6f, 0.3f, 0.05f, 0.95f), MenuSymbol.Orange, MenuSymbol.OffWhite);
            Button no = CreateButton(tutorialPrompt.transform, "NO, START THE GAME", 420, OnSkipTutorial, 48);
            Button back = CreateButton(tutorialPrompt.transform, "BACK", 420, OnTutorialPromptBack, 48);
            SetVerticalNav(yes, back, no);
            SetVerticalNav(no, yes, back);
            SetVerticalNav(back, no, yes);
            tutorialPrompt.SetActive(false);
        }

        private void OnTutorialFirst()
        {
            TutorialRecord.StartGameAfter = true;
            PlayNewGameIntro(tutorialScene); // a new game that starts with the tutorial
        }

        private void OnSkipTutorial() => PlayNewGameIntro(DemoScene);

        private void OnTutorialPromptBack()
        {
            tutorialPrompt.SetActive(false);
            if (advancedOpen) advancedPanel.SetActive(true); else menuPanel.SetActive(true);
            Select(startBtn);
        }

        private void HideRows()
        {
            menuPanel.SetActive(false);
            advancedPanel.SetActive(false);
        }

        /// <summary>A sub-menu closed: show the row that opened it, with the focus on its button.</summary>
        private void ShowRow(Button focus)
        {
            menuPanel.SetActive(!advancedOpen);
            advancedPanel.SetActive(advancedOpen);
            RefreshDifficultyPanel();
            Select(focus);
        }

        private void OnModifiersClicked()
        {
            if (difficultyMenu == null) return;
            HideRows();
            difficultyMenu.Show(() => ShowRow(modifiersBtn));
        }

        private void OnOptionsClicked()
        {
            if (optionsMenu == null) return;
            HideRows();
            optionsMenu.Show(() => ShowRow(optionsBtn));
        }

        private void OnHowToPlayClicked()
        {
            if (howToPlayUI == null) return;
            HideRows();
            howToPlayUI.Show(() => ShowRow(howToBtn));
        }

        private void OnCreditsClicked()
        {
            if (creditsUI == null) return;
            HideRows();
            creditsUI.Show(() => ShowRow(creditsBtn));
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

            int newest = GlorifiedBody.Saving.SaveSystem.Newest();
            hasSave = newest >= 0;
            if (hasSave)
            {
                var data = GlorifiedBody.Saving.SaveSystem.Read(newest);
                // The date and the play time, as the load screen shows them (the spot is an internal id).
                if (data != null)
                {
                    int t = Mathf.FloorToInt(data.playSeconds);
                    saveSpot = $"{data.savedAt}   {t / 3600}:{t / 60 % 60:00}:{t % 60:00}";
                }
            }

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

            // The background: T46 A_memorial_2, a stand-in until the render of the final hospital model (round 173), a little darker, so the symbols read. It covers the screen
            // at any aspect (the picture is 16:9; a wider screen crops the top and the bottom).
            GameObject bg = CreateUIElement("Background", canvasObj.transform);
            StretchFill(bg.GetComponent<RectTransform>());
            var bgFill = bg.AddComponent<Image>();
            bgFill.color = Color.black;
            Texture2D bgTex = Resources.Load<Texture2D>(ArtFolder + "UI_menu_background");
            if (bgTex != null)
            {
                GameObject pic = CreateUIElement("Picture", bg.transform);
                StretchFill(pic.GetComponent<RectTransform>());
                var raw = pic.AddComponent<RawImage>();
                backgroundPicture = raw;
                raw.texture = bgTex;
                raw.raycastTarget = false;
                raw.color = new Color(BackgroundBrightness, BackgroundBrightness, BackgroundBrightness, 1f);
                var fit = pic.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = (float)bgTex.width / bgTex.height;
            }

            // The stripe field of the new-game intro (round 174): hidden until START, behind the logo, on the canvas root
            // (the whole screen; the safe area root can be smaller).
            Texture2D stripesTex = Resources.Load<Texture2D>(ArtFolder + "UI_menu_stripes");
            GameObject stripes = CreateUIElement("Stripes", canvasObj.transform);
            stripesRect = stripes.GetComponent<RectTransform>();
            stripesRect.anchorMin = stripesRect.anchorMax = new Vector2(0.5f, 0.5f);
            stripesRect.pivot = new Vector2(0.5f, 0.5f);
            stripesImage = stripes.AddComponent<RawImage>();
            stripesImage.texture = stripesTex;
            stripesImage.raycastTarget = false;
            stripesImage.enabled = false;

            Transform safeAreaRoot = RuntimeUIRootUtility.CreateSafeAreaRoot(canvasObj.transform);

            // The logo: the round 16 menu logo as is, black letters on the red stripes (round 174). It stays on both rows.
            Texture2D logoTex = Resources.Load<Texture2D>(ArtFolder + "UI_menu_logo");
            GameObject logo = CreateUIElement("Logo", safeAreaRoot);
            logoRect = logo.GetComponent<RectTransform>();
            logoRect.anchorMin = logoRect.anchorMax = new Vector2(0.5f, 1f);
            logoRect.pivot = new Vector2(0.5f, 0.5f);
            logoRect.anchoredPosition = new Vector2(0f, -260f);
            if (logoTex != null)
            {
                var raw = logo.AddComponent<RawImage>();
                raw.texture = logoTex;
                raw.raycastTarget = false;
                logoRect.sizeDelta = new Vector2(980f, 980f * logoTex.height / logoTex.width);
            }
            else
            {
                logoRect.sizeDelta = new Vector2(980f, 150f);
                Text t = CreateLabel(logo.transform, "GLORIFIED", 120, TextAnchor.MiddleCenter, 150, FontStyle.Bold);
                StretchFill(t.rectTransform);
            }

            // The bottom strip, after the reference: one thin rule, the save state in green, the name in silver.
            GameObject strip = CreateUIElement("Strip", safeAreaRoot);
            StretchFill(strip.GetComponent<RectTransform>());
            stripGroup = strip.AddComponent<CanvasGroup>();
            stripGroup.blocksRaycasts = false;
            GameObject rule = CreateUIElement("StripRule", strip.transform);
            var ruleRect = rule.GetComponent<RectTransform>();
            ruleRect.anchorMin = new Vector2(0f, 0f);
            ruleRect.anchorMax = new Vector2(1f, 0f);
            ruleRect.offsetMin = new Vector2(120f, 79f);
            ruleRect.offsetMax = new Vector2(-120f, 81f);
            var ruleImg = rule.AddComponent<Image>();
            Color ruleColor = MenuSymbol.Silver; ruleColor.a = 0.47f;
            ruleImg.color = ruleColor;
            ruleImg.raycastTarget = false;
            stripLeft = CreateSmallPrint(strip.transform, "StripLeft", "", MenuSymbol.Green, 0f);
            CreateSmallPrint(strip.transform, "StripRight", "GLORIFIED  DEMO " + Application.version, MenuSymbol.Silver, 1f);

            // ── The main row ──
            menuPanel = CreateUIElement("MenuPanel", safeAreaRoot);
            StretchFill(menuPanel.GetComponent<RectTransform>());
            var main = new List<Button>();
            // START, or CONTINUE when a save exists (round 173): CONTINUE takes the newest file.
            startBtn = hasSave
                ? CreateSymbol(menuPanel.transform, "CONTINUE", "continue", OnContinueClicked)
                : CreateSymbol(menuPanel.transform, "START", "start", OnStartClicked);
            main.Add(startBtn);
            main.Add(CreateSymbol(menuPanel.transform, "LOAD", "load", OnLoadClicked));
            advancedBtn = CreateSymbol(menuPanel.transform, "ADVANCED", "advanced", OnAdvancedClicked);
            main.Add(advancedBtn);
            Button exitBtn = CreateSymbol(menuPanel.transform, "EXIT", "exit", OnQuitClicked);
            main.Add(exitBtn);
            // The web build: a page cannot quit (Application.Quit does nothing in a browser).
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                exitBtn.gameObject.SetActive(false);
                main.Remove(exitBtn);
            }
            LayRow(main, 230f);

            // ── The ADVANCED row ──
            advancedPanel = CreateUIElement("AdvancedPanel", safeAreaRoot);
            StretchFill(advancedPanel.GetComponent<RectTransform>());
            var adv = new List<Button>();
            // With a save, CONTINUE has the place of START, so a new game starts here.
            if (hasSave) adv.Add(CreateSymbol(advancedPanel.transform, "NEW GAME", "start", OnStartClicked));
            modifiersBtn = CreateSymbol(advancedPanel.transform, "DIFFICULTY", "difficulty", OnModifiersClicked);
            modifiersLabel = modifiersBtn.GetComponentInChildren<Text>();
            adv.Add(modifiersBtn);
            optionsBtn = CreateSymbol(advancedPanel.transform, "OPTIONS", "options", OnOptionsClicked);
            adv.Add(optionsBtn);
            adv.Add(CreateSymbol(advancedPanel.transform, "TUTORIAL", "tutorial", OnTutorialClicked));
            howToBtn = CreateSymbol(advancedPanel.transform, "HOW TO PLAY", "howtoplay", OnHowToPlayClicked);
            adv.Add(howToBtn);
            creditsBtn = CreateSymbol(advancedPanel.transform, "CREDITS", "credits", OnCreditsClicked);
            adv.Add(creditsBtn);
            adv.Add(CreateSymbol(advancedPanel.transform, "BACK", "back", OnBackClicked));
            LayRow(adv, hasSave ? 200f : 220f);
            advancedDefaultBtn = modifiersBtn;
            advancedPanel.SetActive(false);

            // ── The test rooms (right, dev only, rank 16) ──
            testColumn = CreateUIElement("TestColumn", menuPanel.transform);
            testColumn.SetActive(GameBuild.Dev);
            var testsRect = testColumn.GetComponent<RectTransform>();
            testsRect.anchorMin = testsRect.anchorMax = new Vector2(0.9f, 0.71f);
            testsRect.pivot = new Vector2(0.5f, 1f);
            testsRect.sizeDelta = new Vector2(300f, 0f);
            var testsLayout = testColumn.AddComponent<VerticalLayoutGroup>();
            testsLayout.spacing = 5;
            testsLayout.childAlignment = TextAnchor.UpperCenter;
            testsLayout.childControlWidth = false;
            testsLayout.childControlHeight = true;
            testsLayout.childForceExpandWidth = false;
            testsLayout.childForceExpandHeight = false;
            testColumn.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Text testsHeader = CreateLabel(testColumn.transform, "TEST ROOMS", 22, TextAnchor.MiddleCenter, 36, FontStyle.Normal, MenuSymbol.Silver);
            testsHeader.rectTransform.sizeDelta = new Vector2(280f, testsHeader.rectTransform.sizeDelta.y);
            var tests = new List<Button>();
            // The training room is dev only (Bruno, 2026-11-02: "remove the training room, or set it to dev only").
            tests.Add(CreateButton(testColumn.transform, "TRAINING ROOM", 280, OnTrainingClicked, TestButtonHeight, 20));
            // The demo from the Dining Hall (Bruno, 2026-11-15: "get the player dragged to the dining hall").
            tests.Add(CreateButton(testColumn.transform, "DEMO: DINING HALL", 280, OnDiningHallClicked, TestButtonHeight, 20));
            // Bruno, 2026-11-16: "remove all redundant debug rooms: keep only the current useful ones for us".
            tests.Add(CreateButton(testColumn.transform, "AI TEST", 280, OnAiTestClicked, TestButtonHeight, 20));
            tests.Add(CreateButton(testColumn.transform, "KIT TEST", 280, OnKitTestClicked, TestButtonHeight, 20));
            tests.Add(CreateButton(testColumn.transform, "BRAIN TEST", 280, OnBrainTestClicked, TestButtonHeight, 20));
            tests.Add(CreateButton(testColumn.transform, "PHYSICS TEST", 280, OnPhysicsTestClicked, TestButtonHeight, 20));

            // ── The keys: left and right along a row (it wraps), down from the main row into the test rooms ──
            firstTestBtn = GameBuild.Dev ? tests[0] : null;
            WireRow(main, firstTestBtn);
            WireRow(adv, null);
            if (GameBuild.Dev)
                for (int i = 0; i < tests.Count; i++)
                    SetVerticalNav(tests[i], i > 0 ? tests[i - 1] : startBtn, i < tests.Count - 1 ? tests[i + 1] : null);

            defaultBtn = startBtn;
            SelectDefaultButton();
            RefreshDifficultyPanel();

            // The last black of the intro: over everything, clear until the end of the intro.
            GameObject cur = CreateUIElement("Curtain", canvasObj.transform);
            StretchFill(cur.GetComponent<RectTransform>());
            curtain = cur.AddComponent<Image>();
            curtain.color = new Color(0f, 0f, 0f, 0f);
            curtain.raycastTarget = false;

            isBuilt = true;
        }

        /// <summary>
        /// The new-game intro (Bruno, 2026-11-16, round 174: "as the game starts (only on a new game, not in continue nor
        /// load) the stripes grow and occupy the screen as the bg fades to black"), after the opening titles of the
        /// reference. The symbols and the strip fade out, the background fades to black, the stripe field grows from the
        /// logo box to the whole screen behind the letters; then a short black and the scene loads.
        /// </summary>
        private static bool IntroBot => OverlayShot.Enabled && string.Equals(GlorifiedBody.Automation.BotArgs.Bot, "intro", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The "intro" bot: START, the intro to its end, then three checks into playthrough.json: the stripes cover the
        /// screen, the background is black, no level loaded. No shots: a camera-mode render of the growing stripes
        /// crashes the batch editor (exit 139, five runs on 2026-11-16, T46); the game draws the menu in overlay mode.
        /// </summary>
        private System.Collections.IEnumerator RunIntroBot()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            PlayNewGameIntro(DemoScene);
            yield return null;
            while (introRunning) yield return null;
            var root = (RectTransform)stripesRect.parent;
            bool covers = stripesRect.sizeDelta.x >= root.rect.width && stripesRect.sizeDelta.y >= root.rect.height;
            bool black = backgroundPicture == null || backgroundPicture.color.r < 0.01f;
            bool stayed = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Main_Menu";
            string Step(string name, bool ok, string detail) =>
                $"{{\"name\": \"{name}\", \"ok\": {(ok ? "true" : "false")}, \"t\": {introTime.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}, \"detail\": \"{detail}\"}}";
            string json = "{\n  \"bot\": \"intro\",\n  \"pass\": " + (covers && black && stayed ? "true" : "false") + ",\n  \"steps\": [\n    "
                + Step("stripes_cover_screen", covers, $"stripes {stripesRect.sizeDelta} screen {root.rect.size}") + ",\n    "
                + Step("background_black", black, $"brightness {(backgroundPicture != null ? backgroundPicture.color.r : 0f):0.000}") + ",\n    "
                + Step("no_level_in_bot", stayed, "") + "\n  ]\n}\n";
            System.IO.File.WriteAllText(System.IO.Path.Combine(GlorifiedBody.Automation.BotArgs.RunDir, "playthrough.json"), json);
            Debug.Log("[IntroBot] finished");
        }

        private void PlayNewGameIntro(string scene)
        {
            if (introRunning) return;
            StartCoroutine(NewGameIntro(scene));
        }

        private System.Collections.IEnumerator NewGameIntro(string scene)
        {
            introRunning = true;
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);

            var rows = new List<CanvasGroup>();
            foreach (GameObject g in new[] { menuPanel, advancedPanel, tutorialPrompt })
            {
                if (g == null || !g.activeSelf) continue;
                CanvasGroup cg = g.GetComponent<CanvasGroup>();
                if (cg == null) cg = g.AddComponent<CanvasGroup>();
                cg.interactable = false;
                rows.Add(cg);
            }

            // From the logo box (measured in the root's space) to the whole screen.
            var root = (RectTransform)stripesRect.parent;
            var corners = new Vector3[4];
            logoRect.GetWorldCorners(corners);
            Vector2 lo = root.InverseTransformPoint(corners[0]), hi = root.InverseTransformPoint(corners[2]);
            Vector2 fromSize = hi - lo, fromPos = (lo + hi) / 2f;
            Vector2 toSize = root.rect.size * 1.06f, toPos = Vector2.zero;
            Debug.Log($"[MainMenu] new-game intro: stripes from {fromSize} at {fromPos} to {toSize}");
            stripesRect.sizeDelta = fromSize;
            stripesRect.anchoredPosition = fromPos;
            stripesImage.color = new Color(1f, 1f, 1f, 0f);
            stripesImage.enabled = true;

            float t = 0f;
            while (t < IntroSeconds)
            {
                t += Time.unscaledDeltaTime;
                introTime = t;
                float chrome = 1f - Mathf.Clamp01(t / 0.4f);
                foreach (CanvasGroup cg in rows) cg.alpha = chrome;
                stripGroup.alpha = chrome;

                float bg = BackgroundBrightness * (1f - Mathf.Clamp01(t / 1.6f));
                if (backgroundPicture != null) backgroundPicture.color = new Color(bg, bg, bg, 1f);

                float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.15f) / 1.85f));
                stripesRect.sizeDelta = Vector2.Lerp(fromSize, toSize, grow);
                stripesRect.anchoredPosition = Vector2.Lerp(fromPos, toPos, grow);
                stripesImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01((t - 0.15f) / 0.35f));

                curtain.color = new Color(0f, 0f, 0f, Mathf.Clamp01((t - 2.2f) / 0.4f));
                yield return null;
            }
            curtain.color = Color.black;
            if (IntroBot)
            {
                introRunning = false; // the bot stops here: no level loads
                yield break;
            }
            GameManager.Instance?.LoadLevel(scene);
        }

        /// <summary>One symbol: a clear hit box, the tinted icon, the label and the blue rule under it (MenuSymbol).</summary>
        private Button CreateSymbol(Transform parent, string label, string icon, UnityEngine.Events.UnityAction onClick)
        {
            var obj = CreateUIElement("Btn_" + label.Replace(" ", "_"), parent);
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180f, 150f);
            var hit = obj.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            var btn = obj.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            btn.onClick.AddListener(onClick);

            var sym = obj.AddComponent<MenuSymbol>();

            var iconObj = CreateUIElement("Icon", obj.transform);
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
            sym.Icon = iconObj.AddComponent<RawImage>();
            sym.Icon.texture = Resources.Load<Texture2D>(ArtFolder + "UI_menu_" + icon);
            sym.Icon.raycastTarget = false;

            // The label: Roboto Condensed 700 squeezed to 64 % (a narrow scale on a wide rect).
            var labelObj = CreateUIElement("Label", obj.transform);
            var labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -84f);
            labelRect.sizeDelta = new Vector2(900f, 76f);
            labelRect.localScale = new Vector3(HeadSqueeze, 1f, 1f);
            sym.Label = labelObj.AddComponent<Text>();
            sym.Label.text = label;
            sym.Label.font = HeadFont;
            sym.Label.fontSize = 64;
            sym.Label.alignment = TextAnchor.MiddleCenter;
            sym.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
            sym.Label.raycastTarget = false;

            var ruleObj = CreateUIElement("Rule", obj.transform);
            var ruleRect = ruleObj.GetComponent<RectTransform>();
            ruleRect.anchorMin = ruleRect.anchorMax = new Vector2(0.5f, 0.5f);
            ruleRect.pivot = new Vector2(0.5f, 1f);
            ruleRect.anchoredPosition = new Vector2(0f, -164f);
            ruleRect.sizeDelta = new Vector2(140f, 3f);
            sym.Rule = ruleObj.AddComponent<Image>();
            sym.Rule.color = MenuSymbol.Blue;
            sym.Rule.raycastTarget = false;
            return btn;
        }

        /// <summary>Centres the symbols on one row, 360 px over the bottom of the 1080 frame (the mockup).</summary>
        private static void LayRow(List<Button> row, float gap)
        {
            float x0 = -(row.Count - 1) * gap / 2f;
            for (int i = 0; i < row.Count; i++)
            {
                var r = (RectTransform)row[i].transform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(x0 + i * gap, 360f);
            }
        }

        private static void WireRow(List<Button> row, Button down)
        {
            for (int i = 0; i < row.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnLeft = row[(i + row.Count - 1) % row.Count];
                nav.selectOnRight = row[(i + 1) % row.Count];
                nav.selectOnDown = down;
                row[i].navigation = nav;
            }
        }

        /// <summary>Arimo 600 squeezed to 77 %, 26 px, on the bottom strip (left or right end).</summary>
        private Text CreateSmallPrint(Transform parent, string name, string text, Color color, float side)
        {
            var obj = CreateUIElement(name, parent);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(side, 0f);
            rect.pivot = new Vector2(side, 1f);
            rect.anchoredPosition = new Vector2(side < 0.5f ? 120f : -120f, 72f);
            rect.sizeDelta = new Vector2(1000f, 36f);
            rect.localScale = new Vector3(ProfileSqueeze, 1f, 1f);
            var t = obj.AddComponent<Text>();
            t.text = text;
            t.font = ProfileFont;
            t.fontSize = 26;
            t.color = color;
            t.alignment = side < 0.5f ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
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
            txt.font = HeadFont;
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
            txt.font = HeadFont;

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
