using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GlorifiedBody.Rendering;
using GlorifiedBody.Systems;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// Options menu with tabs: Audio, Display/Graphics, Controls (mouse + key remapping).
    /// Usable from both MainMenu and PauseMenu.
    /// Creates its own Canvas and UI elements at runtime.
    /// </summary>
    public class OptionsMenuUI : MonoBehaviour
    {
        private Canvas canvas;
        private GameObject rootPanel;
        private bool isBuilt = false;

        // Tab panels
        private GameObject audioPanel;
        private GameObject displayPanel;
        private GameObject controlsPanel;

        // Tab buttons for highlight tracking
        private Button audioTabBtn;
        private Button displayTabBtn;
        private Button controlsTabBtn;

        // Audio sliders
        private Slider masterSlider;
        private Slider sfxSlider;
        private Slider musicSlider;

        // Display controls
        private Dropdown resolutionDropdown;
        private Toggle fullscreenToggle;
        private Dropdown vsyncDropdown;
        private Toggle rainToggle;
        private Button lookAuthenticButton;
        private Dropdown goreDropdown;
        private Toggle subtitlesToggle;
        private Dropdown subtitleSizeDropdown;
        private Button lookClearButton;

        // Mouse
        private Slider sensitivitySlider;
        private Text sensitivityValueLabel;
        private Button wallrunToggleButton;
        private Button wallrunHoldButton;
        private Button slideToggleButton;
        private Button slideHoldButton;
        private Button catchThrowHoldButton;
        private Button catchThrowToggleButton;

        // Key remapping
        private Transform keybindContainer;
        private List<KeybindRow> keybindRows = new List<KeybindRow>();
        private Text rebindPromptText;

        // Callback for when options menu is closed
        private Action onCloseCallback;

        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;

        private struct KeybindRow
        {
            public InputManager.GameAction action;
            public Text keyLabel;
            public Button rebindButton;
        }

        void Start()
        {
            BuildUI();
            Hide();
        }

        void Update()
        {
            if (!IsVisible) return;

            // Update rebind prompt
            if (InputManager.Instance != null && InputManager.Instance.IsRebinding)
            {
                if (rebindPromptText != null)
                    rebindPromptText.gameObject.SetActive(true);
            }
            else
            {
                if (rebindPromptText != null)
                    rebindPromptText.gameObject.SetActive(false);

                // Allow Escape to close options (only when not rebinding)
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    OnBackClicked();
                }
            }
        }

        public void Show(Action onClose = null)
        {
            if (!isBuilt) BuildUI();
            onCloseCallback = onClose;
            canvas.gameObject.SetActive(true);
            RefreshAllControls();
            ShowTab(audioPanel, audioTabBtn);
        }

        public void Hide()
        {
            if (canvas != null)
                canvas.gameObject.SetActive(false);
        }

        // ── Tab Switching ──────────────────────────────────────────────

        private void ShowTab(GameObject panel, Button activeBtn)
        {
            audioPanel.SetActive(panel == audioPanel);
            displayPanel.SetActive(panel == displayPanel);
            controlsPanel.SetActive(panel == controlsPanel);

            SetTabHighlight(audioTabBtn, panel == audioPanel);
            SetTabHighlight(displayTabBtn, panel == displayPanel);
            SetTabHighlight(controlsTabBtn, panel == controlsPanel);

            ScrollPanelToTop(panel);
        }

        private void SetTabHighlight(Button btn, bool active)
        {
            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = active ? new Color(0.3f, 0.3f, 0.4f, 1f) : new Color(0.18f, 0.18f, 0.22f, 1f);
        }

        private void ScrollPanelToTop(GameObject panel)
        {
            if (panel == null) return;

            var scrollRect = panel.GetComponent<ScrollRect>();
            if (scrollRect == null) return;

            Canvas.ForceUpdateCanvases();
            if (scrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel.GetComponent<RectTransform>());
            Canvas.ForceUpdateCanvases();
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        // ── Refresh ────────────────────────────────────────────────────

        private void RefreshAllControls()
        {
            if (SettingsManager.Instance != null)
            {
                masterSlider.SetValueWithoutNotify(SettingsManager.Instance.MasterVolume);
                sfxSlider.SetValueWithoutNotify(SettingsManager.Instance.SFXVolume);
                musicSlider.SetValueWithoutNotify(SettingsManager.Instance.MusicVolume);
                sensitivitySlider.SetValueWithoutNotify(SettingsManager.Instance.MouseSensitivity);
                UpdateSensitivityLabel(SettingsManager.Instance.MouseSensitivity);
                RefreshModeButtons();
                fullscreenToggle.SetIsOnWithoutNotify(SettingsManager.Instance.Fullscreen);
                vsyncDropdown.SetValueWithoutNotify(SettingsManager.Instance.VSyncCount);
                if (rainToggle != null)
                    rainToggle.SetIsOnWithoutNotify(SettingsManager.Instance.RainEnabled);
                if (goreDropdown != null)
                    goreDropdown.SetValueWithoutNotify(SettingsManager.Instance.GoreLevel);
                if (subtitlesToggle != null)
                    subtitlesToggle.SetIsOnWithoutNotify(SettingsManager.Instance.SubtitlesOn);
                if (subtitleSizeDropdown != null)
                    subtitleSizeDropdown.SetValueWithoutNotify(SettingsManager.Instance.SubtitleSize);
            }

            RefreshResolutionDropdown();
            RefreshKeybindLabels();
        }

        private void RefreshResolutionDropdown()
        {
            resolutionDropdown.ClearOptions();
            Resolution[] resolutions = Screen.resolutions;
            List<string> options = new List<string>();
            int current = 0;

            for (int i = 0; i < resolutions.Length; i++)
            {
                options.Add($"{resolutions[i].width} x {resolutions[i].height}");
                if (SettingsManager.Instance != null && i == SettingsManager.Instance.ResolutionIndex)
                    current = i;
            }

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.SetValueWithoutNotify(current);
        }

        private void RefreshKeybindLabels()
        {
            if (InputManager.Instance == null) return;

            foreach (var row in keybindRows)
            {
                KeyCode key = InputManager.Instance.GetKey(row.action);
                row.keyLabel.text = InputManager.GetKeyDisplayName(key);
            }
        }

        private void UpdateSensitivityLabel(float value)
        {
            if (sensitivityValueLabel != null)
                sensitivityValueLabel.text = value.ToString("F1");
        }

        // ── Callbacks ──────────────────────────────────────────────────

        private void RefreshModeButtons()
        {
            if (SettingsManager.Instance == null)
            {
                return;
            }

            SetModeButtonHighlight(wallrunToggleButton, SettingsManager.Instance.WallRunMode == SettingsManager.WallRunInputMode.Toggle);
            SetModeButtonHighlight(wallrunHoldButton, SettingsManager.Instance.WallRunMode == SettingsManager.WallRunInputMode.Hold);
            SetModeButtonHighlight(slideToggleButton, SettingsManager.Instance.SlideMode == SettingsManager.SlideInputMode.Toggle);
            SetModeButtonHighlight(slideHoldButton, SettingsManager.Instance.SlideMode == SettingsManager.SlideInputMode.Hold);
            SetModeButtonHighlight(catchThrowHoldButton, SettingsManager.Instance.CatchThrowMode == SettingsManager.CatchThrowInputMode.Hold);
            SetModeButtonHighlight(catchThrowToggleButton, SettingsManager.Instance.CatchThrowMode == SettingsManager.CatchThrowInputMode.Toggle);
            SetModeButtonHighlight(lookAuthenticButton, SettingsManager.Instance.LookQuality == LookQuality.Authentic);
            SetModeButtonHighlight(lookClearButton, SettingsManager.Instance.LookQuality == LookQuality.Clear);
        }

        private void SetModeButtonHighlight(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = active ? new Color(0.36f, 0.36f, 0.48f, 1f) : new Color(0.2f, 0.2f, 0.25f, 1f);
            }
        }

        private void OnMasterVolumeChanged(float value)
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.MasterVolume = value;
        }

        private void OnSFXVolumeChanged(float value)
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.SFXVolume = value;
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.MusicVolume = value;
        }

        private void OnSensitivityChanged(float value)
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.MouseSensitivity = value;
            UpdateSensitivityLabel(value);
        }

        private void OnWallRunModeChanged(int index)
        {
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.WallRunMode = (SettingsManager.WallRunInputMode)Mathf.Clamp(index, 0, 1);
            RefreshModeButtons();
        }

        private void OnSlideModeChanged(int index)
        {
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.SlideMode = (SettingsManager.SlideInputMode)Mathf.Clamp(index, 0, 1);
            RefreshModeButtons();
        }

        private void OnCatchThrowModeChanged(int index)
        {
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.CatchThrowMode = (SettingsManager.CatchThrowInputMode)Mathf.Clamp(index, 0, 1);
            RefreshModeButtons();
        }

        private void OnLookQualityChanged(int index)
        {
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.LookQuality = (LookQuality)Mathf.Clamp(index, 0, 1);
            RefreshModeButtons();
        }

        private void OnFullscreenChanged(bool value)
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.Fullscreen = value;
        }

        private void OnResolutionChanged(int index)
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.ResolutionIndex = index;
        }

        private void OnVSyncChanged(int index)
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.VSyncCount = index;
        }

        private void OnGoreChanged(int index)
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.GoreLevel = index;
        }

        private void OnRainChanged(bool value)
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.RainEnabled = value;
        }

        private void OnRebindClicked(InputManager.GameAction action)
        {
            if (InputManager.Instance == null || InputManager.Instance.IsRebinding) return;

            // Find the row and update label to show prompt
            foreach (var row in keybindRows)
            {
                if (row.action == action)
                {
                    row.keyLabel.text = "...";
                    break;
                }
            }

            InputManager.Instance.StartRebind(action, (success) =>
            {
                RefreshKeybindLabels();
            });
        }

        private void OnResetBindingsClicked()
        {
            InputManager.Instance?.ResetToDefaults();
            RefreshKeybindLabels();
        }

        private void OnResetSettingsClicked()
        {
            SettingsManager.Instance?.ResetToDefaults();
            RefreshAllControls();
        }

        private void OnBackClicked()
        {
            Hide();
            onCloseCallback?.Invoke();
            onCloseCallback = null;
        }

        // ── UI Construction ────────────────────────────────────────────

        private void BuildUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("OptionsMenuCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;

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

            // Dim overlay
            GameObject overlay = CreateUIElement("Overlay", canvasObj.transform);
            StretchFill(overlay.GetComponent<RectTransform>());
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.7f);

            Transform safeAreaRoot = RuntimeUIRootUtility.CreateSafeAreaRoot(canvasObj.transform);

            // Root panel
            rootPanel = CreateUIElement("OptionsRoot", safeAreaRoot);
            var rootRect = rootPanel.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(860, 680);
            var rootImg = rootPanel.AddComponent<Image>();
            rootImg.color = new Color(0.1f, 0.1f, 0.12f, 0.97f);

            var rootLayout = rootPanel.AddComponent<VerticalLayoutGroup>();
            rootLayout.spacing = 0;
            rootLayout.padding = new RectOffset(0, 0, 0, 0);
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // Title bar
            var titleBar = CreateUIElement("TitleBar", rootPanel.transform);
            var titleLE = titleBar.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 50;
            var titleImg = titleBar.AddComponent<Image>();
            titleImg.color = new Color(0.08f, 0.08f, 0.1f, 1f);
            CreateLabel(titleBar.transform, "OPTIONS", 28, TextAnchor.MiddleCenter, 50);

            // Tab bar
            var tabBar = CreateUIElement("TabBar", rootPanel.transform);
            var tabLE = tabBar.AddComponent<LayoutElement>();
            tabLE.preferredHeight = 40;
            var tabLayout = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 2;
            tabLayout.childControlWidth = true;
            tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;

            audioTabBtn = CreateTabButton(tabBar.transform, "Audio", () => ShowTab(audioPanel, audioTabBtn));
            displayTabBtn = CreateTabButton(tabBar.transform, "Display/Graphics", () => ShowTab(displayPanel, displayTabBtn));
            controlsTabBtn = CreateTabButton(tabBar.transform, "Controls", () => ShowTab(controlsPanel, controlsTabBtn));

            // Content area
            var contentArea = CreateUIElement("ContentArea", rootPanel.transform);
            var contentLE = contentArea.AddComponent<LayoutElement>();
            contentLE.minHeight = 420;
            contentLE.preferredHeight = 520;
            contentLE.flexibleHeight = 1;

            // Build tab panels inside content area
            audioPanel = BuildAudioPanel(contentArea.transform);
            displayPanel = BuildDisplayPanel(contentArea.transform);
            controlsPanel = BuildControlsPanel(contentArea.transform);

            // Bottom bar with Back button
            var bottomBar = CreateUIElement("BottomBar", rootPanel.transform);
            var bottomLE = bottomBar.AddComponent<LayoutElement>();
            bottomLE.preferredHeight = 58;
            var bottomLayout = bottomBar.AddComponent<HorizontalLayoutGroup>();
            bottomLayout.spacing = 10;
            bottomLayout.padding = new RectOffset(24, 24, 10, 10);
            bottomLayout.childControlWidth = false;
            bottomLayout.childControlHeight = true;
            bottomLayout.childForceExpandWidth = false;
            bottomLayout.childForceExpandHeight = true;
            bottomLayout.childAlignment = TextAnchor.MiddleRight;

            CreateFixedButton(bottomBar.transform, "Reset Settings", 150, OnResetSettingsClicked);
            CreateFixedButton(bottomBar.transform, "Back", 100, OnBackClicked);

            // Rebind prompt overlay (hidden by default)
            rebindPromptText = CreateLabel(rootPanel.transform, "Press any key to bind...\n(Escape to cancel)", 24, TextAnchor.MiddleCenter, 50);
            rebindPromptText.gameObject.SetActive(false);

            isBuilt = true;
        }

        private GameObject BuildAudioPanel(Transform parent)
        {
            var panel = CreateScrollablePanel("AudioPanel", parent, 15f, new RectOffset(40, 40, 30, 20), out Transform content);

            masterSlider = CreateSliderRow(content, "Master Volume", 0f, 1f, OnMasterVolumeChanged);
            sfxSlider = CreateSliderRow(content, "SFX Volume", 0f, 1f, OnSFXVolumeChanged);
            musicSlider = CreateSliderRow(content, "Music Volume", 0f, 1f, OnMusicVolumeChanged);

            return panel;
        }

        private GameObject BuildDisplayPanel(Transform parent)
        {
            var panel = CreateScrollablePanel("DisplayPanel", parent, 15f, new RectOffset(40, 40, 30, 20), out Transform content);

            // Resolution
            var resRow = CreateUIElement("ResolutionRow", content);
            var resRowLE = resRow.AddComponent<LayoutElement>();
            resRowLE.preferredHeight = 40;
            var resRowLayout = resRow.AddComponent<HorizontalLayoutGroup>();
            resRowLayout.spacing = 10;
            resRowLayout.childControlWidth = true;
            resRowLayout.childControlHeight = true;
            resRowLayout.childForceExpandWidth = false;
            resRowLayout.childForceExpandHeight = true;

            var resLabel = CreateLabel(resRow.transform, "Resolution", 18, TextAnchor.MiddleLeft, 40);
            var resLabelLE = resLabel.gameObject.AddComponent<LayoutElement>();
            resLabelLE.preferredWidth = 200;

            var dropdownObj = CreateUIElement("ResDropdown", resRow.transform);
            var dropdownLE = dropdownObj.AddComponent<LayoutElement>();
            dropdownLE.flexibleWidth = 1;
            resolutionDropdown = CreateDropdown(dropdownObj);
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

            // Fullscreen toggle
            fullscreenToggle = CreateToggleRow(content, "Fullscreen", OnFullscreenChanged);

            // VSync
            var vsyncRow = CreateUIElement("VSyncRow", content);
            var vsyncRowLE = vsyncRow.AddComponent<LayoutElement>();
            vsyncRowLE.preferredHeight = 40;
            var vsyncRowLayout = vsyncRow.AddComponent<HorizontalLayoutGroup>();
            vsyncRowLayout.spacing = 10;
            vsyncRowLayout.childControlWidth = true;
            vsyncRowLayout.childControlHeight = true;
            vsyncRowLayout.childForceExpandWidth = false;
            vsyncRowLayout.childForceExpandHeight = true;

            var vsyncLabel = CreateLabel(vsyncRow.transform, "VSync", 18, TextAnchor.MiddleLeft, 40);
            var vsyncLabelLE = vsyncLabel.gameObject.AddComponent<LayoutElement>();
            vsyncLabelLE.preferredWidth = 200;

            var vsyncDropObj = CreateUIElement("VSyncDropdown", vsyncRow.transform);
            var vsyncDropLE = vsyncDropObj.AddComponent<LayoutElement>();
            vsyncDropLE.flexibleWidth = 1;
            vsyncDropdown = CreateDropdown(vsyncDropObj);
            vsyncDropdown.ClearOptions();
            vsyncDropdown.AddOptions(new List<string> { "Off", "Every V-Blank", "Every 2nd V-Blank" });
            vsyncDropdown.onValueChanged.AddListener(OnVSyncChanged);

            rainToggle = CreateToggleRow(content, "Rain Effects", OnRainChanged);

            // The LOOK (Bruno, 2026-10-22): the 1999 frame with the dither, or the same picture clear with anti-aliasing.
            CreateModeRow(
                content,
                "Look",
                OnLookQualityChanged,
                out lookAuthenticButton,
                out lookClearButton,
                firstOptionLabel: "Authentic (1999)",
                secondOptionLabel: "Clear (anti-aliasing)");

            // The gore (Bruno, 2026-10-24): Off, Low (less blood, no head bursts), Full.
            goreDropdown = CreateDropdownRow(content, "Gore", new List<string> { "Off", "Low", "Full" }, OnGoreChanged);

            // The subtitles (Bruno, 2026-10-25): on by default, at the bottom, yellow, the speaker named.
            subtitlesToggle = CreateToggleRow(content, "Subtitles", value => { if (SettingsManager.Instance != null) SettingsManager.Instance.SubtitlesOn = value; });
            subtitleSizeDropdown = CreateDropdownRow(content, "Subtitle Size", new List<string> { "Small", "Medium", "Large" },
                index => { if (SettingsManager.Instance != null) SettingsManager.Instance.SubtitleSize = index; });

            return panel;
        }

        private GameObject BuildControlsPanel(Transform parent)
        {
            var panel = CreateScrollablePanel("ControlsPanel", parent, 8f, new RectOffset(40, 40, 20, 20), out Transform content);

            // Mouse sensitivity
            float minSens = 0.5f;
            float maxSens = 10f;
            if (SettingsManager.Instance != null)
            {
                minSens = SettingsManager.Instance.MinMouseSensitivity;
                maxSens = SettingsManager.Instance.MaxMouseSensitivity;
            }
            sensitivitySlider = CreateSliderRow(content, "Mouse Sensitivity", minSens, maxSens, OnSensitivityChanged, out sensitivityValueLabel);
            CreateModeRow(
                content,
                "Wallrun Mode",
                OnWallRunModeChanged,
                out wallrunToggleButton,
                out wallrunHoldButton);
            CreateModeRow(
                content,
                "Slide Mode",
                OnSlideModeChanged,
                out slideToggleButton,
                out slideHoldButton);
            CreateModeRow(
                content,
                "Catch/Throw Mode",
                OnCatchThrowModeChanged,
                out catchThrowHoldButton,
                out catchThrowToggleButton,
                firstOptionLabel: "Hold",
                secondOptionLabel: "Toggle");

            CreateSpacer(content, 10);

            // Section header
            CreateLabel(content, "KEY BINDINGS", 16, TextAnchor.MiddleLeft, 25);

            keybindContainer = content;

            // Build keybind rows
            if (InputManager.Instance != null)
            {
                var allBindings = InputManager.Instance.GetAllBindings();
                foreach (var binding in allBindings)
                {
                    CreateKeybindRow(keybindContainer, binding);
                }
            }

            CreateSpacer(content, 5);

            // Reset bindings button
            CreateFixedButton(content, "Reset Bindings to Default", 250, OnResetBindingsClicked);

            return panel;
        }

        private GameObject CreateScrollablePanel(string name, Transform parent, float spacing, RectOffset padding, out Transform content)
        {
            var panel = CreateUIElement(name, parent);
            StretchFill(panel.GetComponent<RectTransform>());
            panel.AddComponent<Image>().color = Color.clear;

            var scrollRect = panel.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            var viewport = CreateUIElement("Viewport", panel.transform);
            var viewportRect = viewport.GetComponent<RectTransform>();
            StretchFill(viewportRect);
            viewportRect.offsetMax = new Vector2(-18f, 0f);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRect;

            var scrollbarObj = CreateUIElement("Scrollbar", panel.transform);
            var scrollbarRect = scrollbarObj.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 1f);
            scrollbarRect.offsetMin = new Vector2(-14f, 8f);
            scrollbarRect.offsetMax = new Vector2(-4f, -8f);
            scrollbarObj.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

            var slidingArea = CreateUIElement("SlidingArea", scrollbarObj.transform);
            var slidingAreaRect = slidingArea.GetComponent<RectTransform>();
            StretchFill(slidingAreaRect);
            slidingAreaRect.offsetMin = new Vector2(2f, 2f);
            slidingAreaRect.offsetMax = new Vector2(-2f, -2f);

            var handleObj = CreateUIElement("Handle", slidingArea.transform);
            var handleRect = handleObj.GetComponent<RectTransform>();
            StretchFill(handleRect);
            var handleImage = handleObj.AddComponent<Image>();
            handleImage.color = new Color(0.55f, 0.55f, 0.65f, 0.95f);

            var scrollbar = scrollbarObj.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            var contentObj = CreateUIElement("Content", viewport.transform);
            var contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = contentObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;
            content = contentObj.transform;
            return panel;
        }

        private void CreateKeybindRow(Transform parent, InputManager.KeyBinding binding)
        {
            var row = CreateUIElement("Keybind_" + binding.action, parent);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 32;
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            // Action name
            var nameLabel = CreateLabel(row.transform, binding.DisplayName, 16, TextAnchor.MiddleLeft, 32);
            var nameLabelLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLabelLE.preferredWidth = 250;

            // Current key label + rebind button
            var btnObj = CreateUIElement("RebindBtn", row.transform);
            var btnLE = btnObj.AddComponent<LayoutElement>();
            btnLE.flexibleWidth = 1;
            btnLE.preferredHeight = 32;
            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.22f, 0.22f, 0.28f, 1f);

            var btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.4f, 1f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.2f, 1f);
            btn.colors = colors;

            var keyLabel = CreateLabel(btnObj.transform, InputManager.GetKeyDisplayName(binding.currentKey), 16, TextAnchor.MiddleCenter, 32);

            var action = binding.action; // Capture for closure
            btn.onClick.AddListener(() => OnRebindClicked(action));

            keybindRows.Add(new KeybindRow
            {
                action = binding.action,
                keyLabel = keyLabel,
                rebindButton = btn
            });
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

        private Text CreateLabel(Transform parent, string text, int fontSize, TextAnchor alignment, float height)
        {
            var obj = CreateUIElement("Label_" + text, parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;

            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return txt;
        }

        private Slider CreateSliderRow(Transform parent, string label, float min, float max, UnityEngine.Events.UnityAction<float> onChange, out Text valueLabel)
        {
            var row = CreateUIElement("SliderRow_" + label, parent);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 40;
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            var nameLabel = CreateLabel(row.transform, label, 18, TextAnchor.MiddleLeft, 40);
            var nameLabelLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLabelLE.preferredWidth = 200;

            // Slider
            var sliderObj = CreateUIElement("Slider", row.transform);
            var sliderLE = sliderObj.AddComponent<LayoutElement>();
            sliderLE.flexibleWidth = 1;

            // Background
            var bgObj = CreateUIElement("Background", sliderObj.transform);
            var bgRect = bgObj.GetComponent<RectTransform>();
            StretchFill(bgRect);
            bgObj.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 1f);

            // Fill area
            var fillArea = CreateUIElement("FillArea", sliderObj.transform);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-5, 0);

            var fill = CreateUIElement("Fill", fillArea.transform);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.AddComponent<Image>().color = new Color(0.4f, 0.6f, 0.8f, 1f);

            // Handle area
            var handleArea = CreateUIElement("HandleSlideArea", sliderObj.transform);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            var handle = CreateUIElement("Handle", handleArea.transform);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 0);
            handle.AddComponent<Image>().color = Color.white;

            var slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(onChange);

            // Value label
            valueLabel = CreateLabel(row.transform, "1.0", 16, TextAnchor.MiddleRight, 40);
            var valLE = valueLabel.gameObject.AddComponent<LayoutElement>();
            valLE.preferredWidth = 50;

            return slider;
        }

        private Slider CreateSliderRow(Transform parent, string label, float min, float max, UnityEngine.Events.UnityAction<float> onChange)
        {
            return CreateSliderRow(parent, label, min, max, onChange, out _);
        }

        private Toggle CreateToggleRow(Transform parent, string label, UnityEngine.Events.UnityAction<bool> onChange)
        {
            var row = CreateUIElement("ToggleRow_" + label, parent);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 40;
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            var nameLabel = CreateLabel(row.transform, label, 18, TextAnchor.MiddleLeft, 40);
            var nameLabelLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLabelLE.preferredWidth = 200;

            var toggleObj = CreateUIElement("Toggle", row.transform);
            var toggleLE = toggleObj.AddComponent<LayoutElement>();
            toggleLE.preferredWidth = 30;
            toggleLE.preferredHeight = 30;

            // Background
            var toggleBg = CreateUIElement("Background", toggleObj.transform);
            var bgRect = toggleBg.GetComponent<RectTransform>();
            StretchFill(bgRect);
            toggleBg.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 1f);

            // Checkmark
            var checkmark = CreateUIElement("Checkmark", toggleBg.transform);
            var checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.15f, 0.15f);
            checkRect.anchorMax = new Vector2(0.85f, 0.85f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            checkmark.AddComponent<Image>().color = new Color(0.4f, 0.8f, 0.4f, 1f);

            var toggle = toggleObj.AddComponent<Toggle>();
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.targetGraphic = toggleBg.GetComponent<Image>();
            toggle.onValueChanged.AddListener(onChange);

            return toggle;
        }

        private void CreateModeRow(
            Transform parent,
            string label,
            UnityEngine.Events.UnityAction<int> onChange,
            out Button toggleButton,
            out Button holdButton,
            string firstOptionLabel = "Toggle",
            string secondOptionLabel = "Hold")
        {
            var row = CreateUIElement("ModeRow_" + label, parent);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 40;
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            var nameLabel = CreateLabel(row.transform, label, 18, TextAnchor.MiddleLeft, 40);
            var nameLabelLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLabelLE.preferredWidth = 200;

            var segmentGroup = CreateUIElement("Segments_" + label, row.transform);
            var segmentLE = segmentGroup.AddComponent<LayoutElement>();
            segmentLE.flexibleWidth = 1;
            var segmentLayout = segmentGroup.AddComponent<HorizontalLayoutGroup>();
            segmentLayout.spacing = 4;
            segmentLayout.childControlWidth = true;
            segmentLayout.childControlHeight = true;
            segmentLayout.childForceExpandWidth = true;
            segmentLayout.childForceExpandHeight = true;

            toggleButton = CreateModeButton(segmentGroup.transform, firstOptionLabel, () => onChange?.Invoke(0));
            holdButton = CreateModeButton(segmentGroup.transform, secondOptionLabel, () => onChange?.Invoke(1));
        }

        private Button CreateModeButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var obj = CreateUIElement("ModeBtn_" + label, parent);
            var le = obj.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.preferredHeight = 34;

            var image = obj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            var button = obj.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.42f, 0.42f, 0.5f, 1f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.2f, 1f);
            button.colors = colors;
            button.onClick.AddListener(onClick);

            CreateLabel(obj.transform, label, 16, TextAnchor.MiddleCenter, 34);
            return button;
        }

        private Dropdown CreateDropdownRow(Transform parent, string label, List<string> options, UnityEngine.Events.UnityAction<int> onChange)
        {
            var row = CreateUIElement("DropdownRow_" + label, parent);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 40;
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            var nameLabel = CreateLabel(row.transform, label, 18, TextAnchor.MiddleLeft, 40);
            var nameLabelLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLabelLE.preferredWidth = 200;

            var dropdownObj = CreateUIElement("Dropdown_" + label, row.transform);
            var dropdownLE = dropdownObj.AddComponent<LayoutElement>();
            dropdownLE.flexibleWidth = 1;

            var dropdown = CreateDropdown(dropdownObj, Mathf.Clamp(options.Count, 1, 5));
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.onValueChanged.AddListener(onChange);
            return dropdown;
        }

        private Dropdown CreateDropdown(GameObject parent, int visibleItemCount = 5)
        {
            const float itemHeight = 30f;
            var bgImg = parent.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            // Caption
            var captionObj = CreateUIElement("Label", parent.transform);
            var captionRect = captionObj.GetComponent<RectTransform>();
            captionRect.anchorMin = new Vector2(0, 0);
            captionRect.anchorMax = new Vector2(1, 1);
            captionRect.offsetMin = new Vector2(10, 0);
            captionRect.offsetMax = new Vector2(-25, 0);
            var captionText = captionObj.AddComponent<Text>();
            captionText.fontSize = 16;
            captionText.alignment = TextAnchor.MiddleLeft;
            captionText.color = Color.white;
            captionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Arrow
            var arrowObj = CreateUIElement("Arrow", parent.transform);
            var arrowRect = arrowObj.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0);
            arrowRect.anchorMax = new Vector2(1, 1);
            arrowRect.offsetMin = new Vector2(-25, 5);
            arrowRect.offsetMax = new Vector2(-5, -5);
            arrowObj.AddComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f, 1f);

            // Template
            var templateObj = CreateUIElement("Template", parent.transform);
            var templateRect = templateObj.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.sizeDelta = new Vector2(0, Mathf.Max(1, visibleItemCount) * itemHeight + 4f);
            templateObj.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 1f);

            var scrollRect = templateObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            // Viewport
            var vpObj = CreateUIElement("Viewport", templateObj.transform);
            var vpRect = vpObj.GetComponent<RectTransform>();
            StretchFill(vpRect);
            var mask = vpObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            vpObj.AddComponent<Image>().color = Color.clear;
            scrollRect.viewport = vpRect;

            // Content
            var contentObj = CreateUIElement("Content", vpObj.transform);
            var contentObjRect = contentObj.GetComponent<RectTransform>();
            contentObjRect.anchorMin = new Vector2(0, 1);
            contentObjRect.anchorMax = new Vector2(1, 1);
            contentObjRect.pivot = new Vector2(0.5f, 1f);
            contentObjRect.sizeDelta = new Vector2(0, Mathf.Max(1, visibleItemCount) * itemHeight);
            var contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.spacing = 0f;
            contentLayout.padding = new RectOffset(0, 0, 0, 0);
            var contentFitter = contentObj.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentObjRect;

            // Item
            var itemObj = CreateUIElement("Item", contentObj.transform);
            var itemRect = itemObj.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 1);
            itemRect.anchorMax = new Vector2(1, 1);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.sizeDelta = new Vector2(0, itemHeight);
            var itemLE = itemObj.AddComponent<LayoutElement>();
            itemLE.preferredHeight = itemHeight;
            var itemToggle = itemObj.AddComponent<Toggle>();

            // Item background
            var itemBg = CreateUIElement("Item Background", itemObj.transform);
            var itemBgRect = itemBg.GetComponent<RectTransform>();
            StretchFill(itemBgRect);
            var itemBgImage = itemBg.AddComponent<Image>();
            itemBgImage.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            itemToggle.targetGraphic = itemBgImage;

            // Item checkmark
            var itemCheck = CreateUIElement("Item Checkmark", itemObj.transform);
            var itemCheckRect = itemCheck.GetComponent<RectTransform>();
            itemCheckRect.anchorMin = new Vector2(0, 0);
            itemCheckRect.anchorMax = new Vector2(0, 1);
            itemCheckRect.offsetMin = new Vector2(2, 2);
            itemCheckRect.offsetMax = new Vector2(22, -2);
            itemCheck.AddComponent<Image>().color = new Color(0.4f, 0.8f, 0.4f, 1f);
            itemToggle.graphic = itemCheck.GetComponent<Image>();

            // Item label
            var itemLabelObj = CreateUIElement("Item Label", itemObj.transform);
            var itemLabelRect = itemLabelObj.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(25, 0);
            itemLabelRect.offsetMax = new Vector2(-5, 0);
            var itemLabel = itemLabelObj.AddComponent<Text>();
            itemLabel.fontSize = 14;
            itemLabel.alignment = TextAnchor.MiddleLeft;
            itemLabel.color = Color.white;
            itemLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemLabel.raycastTarget = false;
            itemLabel.text = "Option";

            templateObj.SetActive(false);

            var dropdown = parent.AddComponent<Dropdown>();
            dropdown.targetGraphic = bgImg;
            dropdown.template = templateRect;
            dropdown.captionText = captionText;
            dropdown.itemText = itemLabel;

            return dropdown;
        }

        private Button CreateTabButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var obj = CreateUIElement("Tab_" + label, parent);
            var img = obj.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.22f, 1f);

            var btn = obj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.28f, 0.28f, 0.35f, 1f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.2f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var txt = CreateLabel(obj.transform, label, 18, TextAnchor.MiddleCenter, 40);

            return btn;
        }

        private Button CreateFixedButton(Transform parent, string label, float width, UnityEngine.Events.UnityAction onClick)
        {
            var obj = CreateUIElement("Btn_" + label, parent);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = 34;

            var img = obj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            var btn = obj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.4f, 1f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.2f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var txt = CreateLabel(obj.transform, label, 16, TextAnchor.MiddleCenter, 34);

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
