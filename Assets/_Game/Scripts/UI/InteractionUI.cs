using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using GlorifiedBody.Interaction;
using GlorifiedBody.Systems;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The screens of the Interact and Scan keys (Bruno, 2026-10-22), on "[RuntimeUI]" of every room:
    /// the hint over the HUD when something is in reach ("E  take the test log", "F  scan"), the question
    /// "Take it?" with its two keys, the log reader, and the scan view (a picture, or a clip played into a
    /// render texture). The key names follow the remap. The look (T46, 2026-11-17) follows the AC3 menus: the game fonts, green
    /// keys, a dark plate under the hint, the compact AC3 dialog for the question and the log, a framed scan view.
    /// </summary>
    public sealed class InteractionUI : MonoBehaviour
    {
        private Canvas canvas;
        private Text hintText;
        private Image hintPlate;
        private string hintPlain = "";
        private GameObject confirmPanel;
        private MenuStyle.MenuFrame confirmFrame;
        private Text confirmText;
        private Text confirmKeys;
        private GameObject readerPanel;
        private Text readerTitle;
        private Text readerBody;
        private Text readerKeys;
        private GameObject scanPanel;
        private RawImage scanImage;
        private Text scanCaption;
        private VideoPlayer video;
        private AudioSource scanAudio;
        private RenderTexture videoTexture;

        private Interactor interactor;
        private PsychicScanner scanner;

        public Canvas Canvas => canvas;
        public string HintShown => hintPlate != null && hintPlate.gameObject.activeSelf ? hintPlain : "";
        public bool ConfirmShown => confirmPanel != null && confirmPanel.activeSelf;
        public string ConfirmTextShown => confirmText != null ? confirmText.text : "";
        public bool ReaderShown => readerPanel != null && readerPanel.activeSelf;
        public bool ScanShown => scanPanel != null && scanPanel.activeSelf;
        public string ReaderTextShown => readerBody != null ? readerBody.text : "";
        public Texture ScanTextureShown => scanImage != null ? scanImage.texture : null;
        /// <summary>The frame index of the clip on screen (-1 when no clip plays).</summary>
        public long VideoFrame => video != null && video.isPlaying ? video.frame : -1;
        public long VideoFramesSeen { get; private set; }

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                enabled = false;
                return;
            }
            interactor = player.GetComponent<Interactor>();
            scanner = player.GetComponent<PsychicScanner>();
            Build();
            if (interactor != null) interactor.OnChanged += Refresh;
            if (scanner != null)
            {
                scanner.OnScanStarted += ShowScan;
                scanner.OnScanEnded += HideScan;
            }
            Refresh();
        }

        private void OnDestroy()
        {
            if (interactor != null) interactor.OnChanged -= Refresh;
            if (scanner != null)
            {
                scanner.OnScanStarted -= ShowScan;
                scanner.OnScanEnded -= HideScan;
            }
            if (videoTexture != null) videoTexture.Release();
        }

        private void Update()
        {
            if (hintText == null) return;
            // The hint: what the two keys would do now. The plain words for the tests, the styled words on screen.
            string hint = "", shown = "";
            bool hintsOn = SettingsManager.Instance == null || SettingsManager.Instance.InteractHintsOn; // Options > Interaction Hints
            if (hintsOn && interactor != null && interactor.Current == Interactor.State.Free && (scanner == null || !scanner.IsViewing))
            {
                // A comment shows no hint (Bruno, 2026-11-08: "no hint"): the key works, the screen stays clean.
                if (interactor.Focus != null && interactor.Focus.ShowsHint)
                {
                    hint = $"{Key(InputManager.GameAction.Interact)}  {interactor.Focus.Hint}";
                    shown = $"{MenuStyle.KeyTag(Key(InputManager.GameAction.Interact))}  {interactor.Focus.Hint.ToUpperInvariant()}";
                }
                if (scanner != null && scanner.Focus != null)
                {
                    hint += (hint.Length > 0 ? "      " : "") + $"{Key(InputManager.GameAction.Scan)}  scan";
                    shown += (shown.Length > 0 ? "        " : "") + $"{MenuStyle.KeyTag(Key(InputManager.GameAction.Scan))}  SCAN";
                }
            }
            if (hint != hintPlain)
            {
                hintPlain = hint;
                hintText.text = shown;
                hintPlate.rectTransform.sizeDelta = new Vector2(hintText.preferredWidth + 64f, 54f);
            }
            hintPlate.gameObject.SetActive(hint.Length > 0);
            if (video != null && video.isPlaying && video.frame > VideoFramesSeen)
            {
                if (VideoFramesSeen <= 0) Debug.Log($"[Scan] the clip plays: frame {video.frame} ({(video.source == VideoSource.Url ? video.url : video.clip?.name)})");
                VideoFramesSeen = video.frame;
            }
        }

        private void Refresh()
        {
            if (canvas == null || interactor == null) return;
            bool asking = interactor.Current == Interactor.State.Confirming;
            bool message = interactor.Current == Interactor.State.Message;
            confirmPanel.SetActive(asking || message);
            if (asking)
            {
                bool item = interactor.Pending != null && interactor.Pending.Gives != null;
                string yes = item ? "TAKE" : "YES";
                string no = item ? "LEAVE IT" : "NO";
                confirmFrame.Title.text = "CONFIRM";
                confirmFrame.Subtitle.text = item ? "ITEM  /  TAKE OR LEAVE" : "YES OR NO";
                confirmText.text = interactor.Text;
                confirmKeys.text = $"{MenuStyle.KeyTag(Key(InputManager.GameAction.Interact))}  {yes}        {MenuStyle.KeyTag(Key(InputManager.GameAction.Back))}  {no}";
                confirmText.rectTransform.offsetMin = new Vector2(10f, 44f);
            }
            else if (message)
            {
                confirmFrame.Title.text = "NOTICE";
                confirmFrame.Subtitle.text = "";
                confirmText.text = interactor.Text;
                confirmKeys.text = "";
                confirmText.rectTransform.offsetMin = new Vector2(10f, 0f);
            }
            readerPanel.SetActive(interactor.Current == Interactor.State.Reading);
            if (interactor.Current == Interactor.State.Reading && interactor.Reading != null)
            {
                readerTitle.text = interactor.Reading.displayName;
                readerBody.text = interactor.Reading.logText;
                readerKeys.text = $"{MenuStyle.KeyTag(Key(InputManager.GameAction.Interact))} / {MenuStyle.KeyTag(Key(InputManager.GameAction.Back))}  CLOSE";
            }
        }

        private void ShowScan(Scannable target)
        {
            scanPanel.SetActive(true);
            scanCaption.text = target.caption;
            VideoFramesSeen = 0;
            if (target.IsVideo)
            {
                if (videoTexture == null)
                {
                    videoTexture = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32) { name = "GB_ScanVideo" };
                    videoTexture.Create();
                }
                scanImage.texture = videoTexture;
                GlorifiedBody.Core.VideoClips.Assign(video, target.clip);
                video.targetTexture = videoTexture;
                video.Play();
            }
            else
            {
                video.Stop();
                scanImage.texture = target.image;
            }
        }

        private void HideScan(Scannable target)
        {
            if (video != null) video.Stop();
            scanPanel.SetActive(false);
        }

        private void OnClipEnded(VideoPlayer player)
        {
            if (scanner != null && scanner.IsViewing) scanner.End("ended");
        }

        private static string Key(InputManager.GameAction action)
        {
            KeyCode key = InputManager.Instance != null ? InputManager.Instance.GetKey(action) : KeyCode.None;
            return key == KeyCode.None ? action.ToString() : InputManager.GetKeyDisplayName(key);
        }

        // ── Build ─────────────────────────────────────────────────────

        private void Build()
        {
            var root = new GameObject("InteractionUI", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // The hint: off-white heads, the keys in green, on a dark rounded plate sized to the words.
            hintPlate = MenuStyle.Plate(root.transform, "Hint", new Color(0.02f, 0.025f, 0.035f, 0.82f), MenuStyle.Rule);
            MenuStyle.Place(hintPlate.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(400f, 54f));
            hintText = MenuStyle.Words(hintPlate.transform, "Text", "", MenuStyle.Head, 30, MenuStyle.OffWhite, TextAnchor.MiddleCenter);
            MenuStyle.Fill(hintText.rectTransform, 0f);
            hintText.supportRichText = true;

            // The question and the short message: the compact AC3 dialog.
            confirmFrame = MenuStyle.Dialog(root.transform, "CONFIRM", "", new Vector2(820f, 330f));
            confirmPanel = confirmFrame.Root.gameObject;
            confirmText = MenuStyle.Words(confirmFrame.Content, "Text", "", MenuStyle.Body, 30, MenuStyle.OffWhite, TextAnchor.MiddleCenter);
            MenuStyle.Fill(confirmText.rectTransform, 10f);
            confirmText.horizontalOverflow = HorizontalWrapMode.Wrap;
            confirmText.verticalOverflow = VerticalWrapMode.Truncate; // best fit needs a fixed box
            confirmText.resizeTextForBestFit = true;
            confirmText.resizeTextMinSize = 20;
            confirmText.resizeTextMaxSize = 30;
            confirmKeys = MenuStyle.Words(confirmFrame.Content, "Keys", "", MenuStyle.Head, 28, MenuStyle.Silver, TextAnchor.MiddleCenter);
            confirmKeys.rectTransform.anchorMin = new Vector2(0f, 0f);
            confirmKeys.rectTransform.anchorMax = new Vector2(1f, 0f);
            confirmKeys.rectTransform.pivot = new Vector2(0.5f, 0f);
            confirmKeys.rectTransform.offsetMin = new Vector2(0f, 4f);
            confirmKeys.rectTransform.offsetMax = new Vector2(0f, 40f);

            // The log reader: a shade over the game, the compact AC3 dialog with the log symbol on its plate.
            var shade = new GameObject("Reader", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(root.transform, false);
            MenuStyle.Fill((RectTransform)shade.transform, 0f);
            shade.GetComponent<Image>().color = MenuStyle.Scrim;
            readerPanel = shade;
            MenuStyle.MenuFrame reader = MenuStyle.Dialog(shade.transform, "", "DATA  /  RECORD", new Vector2(1160f, 800f));
            readerTitle = reader.Title;
            // A fully opaque body: the tutorial line under it must not show through. The project blends in linear space,
            // so even 2 % of a bright word shows as a ghost (measured in the T46 shots).
            reader.Content.parent.GetComponent<Image>().color = new Color(0.03f, 0.035f, 0.05f, 1f);
            Transform plate = reader.Title.transform.parent;
            var iconObj = new GameObject("Icon", typeof(RectTransform));
            iconObj.transform.SetParent(plate, false);
            MenuStyle.Place((RectTransform)iconObj.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(92f, 92f));
            var icon = iconObj.AddComponent<RawImage>();
            icon.texture = Resources.Load<Texture2D>(MainMenuUI.ArtFolder + "UI_menu_log");
            icon.color = new Color(0.03f, 0.03f, 0.04f, 1f);
            icon.raycastTarget = false;
            readerBody = MenuStyle.Words(reader.Content, "Body", "", MenuStyle.Body, 24, MenuStyle.OffWhite, TextAnchor.UpperLeft);
            MenuStyle.Fill(readerBody.rectTransform, 0f);
            readerBody.rectTransform.offsetMin = new Vector2(26f, 56f);
            readerBody.rectTransform.offsetMax = new Vector2(-26f, -14f);
            readerBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            readerBody.lineSpacing = 1.1f;
            var keyRule = new GameObject("KeyRule", typeof(RectTransform), typeof(Image));
            keyRule.transform.SetParent(reader.Content, false);
            var keyRuleRect = (RectTransform)keyRule.transform;
            keyRuleRect.anchorMin = new Vector2(0f, 0f); keyRuleRect.anchorMax = new Vector2(1f, 0f);
            keyRuleRect.offsetMin = new Vector2(26f, 45f); keyRuleRect.offsetMax = new Vector2(-26f, 47f);
            keyRule.GetComponent<Image>().color = MenuStyle.Rule;
            keyRule.GetComponent<Image>().raycastTarget = false;
            readerKeys = MenuStyle.Words(reader.Content, "Keys", "", MenuStyle.Head, 26, MenuStyle.Silver, TextAnchor.MiddleLeft);
            MenuStyle.Place(readerKeys.rectTransform, Vector2.zero, Vector2.zero, new Vector2(26f, 4f), new Vector2(900f, 36f));

            // The scan view: the picture or the clip in an off-white frame, an orange SCAN tag and the stripes over it,
            // the caption under it in the subtitle font.
            scanPanel = MakePanel(root.transform, "Scan", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f), new Color(0f, 0f, 0f, 0.92f));
            var imageObj = new GameObject("Image", typeof(RectTransform));
            imageObj.transform.SetParent(scanPanel.transform, false);
            var imageRect = imageObj.GetComponent<RectTransform>();
            imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.sizeDelta = new Vector2(1440f, 810f);
            imageRect.anchoredPosition = new Vector2(0f, 40f);
            scanImage = imageObj.AddComponent<RawImage>();
            Image scanEdge = MenuStyle.Plate(scanPanel.transform, "Frame", Color.clear, new Color(0.95f, 0.94f, 0.9f, 0.85f));
            MenuStyle.Place(scanEdge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1468f, 838f));
            Image tag = MenuStyle.Plate(scanPanel.transform, "Tag", MenuStyle.Orange, MenuStyle.OrangeDeep);
            MenuStyle.Place(tag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(-734f, 467f), new Vector2(200f, 54f));
            Text tagText = MenuStyle.Words(tag.transform, "Text", "SCAN", MenuStyle.Head, 44, new Color(0.03f, 0.03f, 0.04f, 1f), TextAnchor.MiddleCenter);
            MenuStyle.Fill(tagText.rectTransform, 0f);
            RawImage tagStripes = MenuStyle.StripeStrip(scanPanel.transform, "Stripes", new Vector2(300f, 22f));
            MenuStyle.Place(tagStripes.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(-520f, 483f), new Vector2(300f, 22f));
            scanCaption = MenuStyle.Words(scanPanel.transform, "Caption", "", MenuStyle.Body, 28, MenuStyle.OffWhite, TextAnchor.MiddleCenter);
            MenuStyle.Place(scanCaption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1400f, 40f));
            scanCaption.horizontalOverflow = HorizontalWrapMode.Wrap;

            video = root.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.isLooping = false;
            video.renderMode = VideoRenderMode.RenderTexture;
            // The world stops during a scan (GameState.Reading): the clip runs on real time and its sound passes the listener pause.
            video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            // The clip's own sound on the Voice channel (a memory speaks; muted before 2026-10-26).
            scanAudio = root.AddComponent<AudioSource>();
            GlorifiedBody.Audio.GameAudio.Bind(scanAudio, GlorifiedBody.Audio.SoundChannel.Voice, 1f, spatial: false);
            scanAudio.ignoreListenerPause = true;
            video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            video.SetTargetAudioSource(0, scanAudio);
            video.waitForFirstFrame = true;
            video.skipOnDrop = true;
            video.loopPointReached += OnClipEnded;

            confirmPanel.SetActive(false);
            readerPanel.SetActive(false);
            scanPanel.SetActive(false);
            hintPlate.gameObject.SetActive(false);
        }

        private static GameObject MakePanel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color colour)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            obj.AddComponent<Image>().color = colour;
            return obj;
        }
    }
}
