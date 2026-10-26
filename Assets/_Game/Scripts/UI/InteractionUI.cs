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
    /// render texture). The key names follow the remap. Placeholder look: plain panels, the legacy font.
    /// </summary>
    public sealed class InteractionUI : MonoBehaviour
    {
        private Canvas canvas;
        private Text hintText;
        private GameObject confirmPanel;
        private Text confirmText;
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
        public string HintShown => hintText != null && hintText.gameObject.activeSelf ? hintText.text : "";
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
            // The hint: what the two keys would do now.
            string hint = "";
            if (interactor != null && interactor.Current == Interactor.State.Free && (scanner == null || !scanner.IsViewing))
            {
                if (interactor.Focus != null) hint = $"{Key(InputManager.GameAction.Interact)}  {interactor.Focus.Hint}";
                if (scanner != null && scanner.Focus != null) hint += (hint.Length > 0 ? "      " : "") + $"{Key(InputManager.GameAction.Scan)}  scan";
            }
            hintText.text = hint;
            hintText.gameObject.SetActive(hint.Length > 0);
            if (video != null && video.isPlaying && video.frame > VideoFramesSeen) VideoFramesSeen = video.frame;
        }

        private void Refresh()
        {
            if (canvas == null || interactor == null) return;
            bool asking = interactor.Current == Interactor.State.Confirming;
            bool message = interactor.Current == Interactor.State.Message;
            confirmPanel.SetActive(asking || message);
            if (asking)
            {
                string yes = interactor.Pending is ItemPickup ? "take" : "yes";
                string no = interactor.Pending is ItemPickup ? "leave it" : "no";
                confirmText.text = $"{interactor.Text}\n\n{Key(InputManager.GameAction.Interact)}  {yes}        {Key(InputManager.GameAction.Back)}  {no}";
            }
            else if (message)
            {
                confirmText.text = interactor.Text;
            }
            readerPanel.SetActive(interactor.Current == Interactor.State.Reading);
            if (interactor.Current == Interactor.State.Reading && interactor.Reading != null)
            {
                readerTitle.text = interactor.Reading.displayName;
                readerBody.text = interactor.Reading.logText;
                readerKeys.text = $"{Key(InputManager.GameAction.Interact)} / {Key(InputManager.GameAction.Back)}  close";
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
                video.clip = target.clip;
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

            hintText = MakeText(root.transform, "Hint", 24, FontStyle.Bold, new Color(0.95f, 0.92f, 0.8f), new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(900f, 40f));
            var hintOutline = hintText.gameObject.AddComponent<Outline>();
            hintOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);

            confirmPanel = MakePanel(root.transform, "Confirm", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 200f), new Color(0f, 0f, 0f, 0.82f));
            confirmText = MakeText(confirmPanel.transform, "Text", 28, FontStyle.Bold, new Color(0.96f, 0.93f, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 170f));

            readerPanel = MakePanel(root.transform, "Reader", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 760f), new Color(0.06f, 0.06f, 0.07f, 0.94f));
            readerTitle = MakeText(readerPanel.transform, "Title", 30, FontStyle.Bold, new Color(0.96f, 0.93f, 0.9f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(900f, 50f));
            readerBody = MakeText(readerPanel.transform, "Body", 22, FontStyle.Normal, new Color(0.88f, 0.88f, 0.86f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(880f, 560f));
            readerBody.alignment = TextAnchor.UpperLeft;
            readerKeys = MakeText(readerPanel.transform, "Keys", 18, FontStyle.Normal, new Color(0.7f, 0.75f, 0.85f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 30f));

            scanPanel = MakePanel(root.transform, "Scan", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f), new Color(0f, 0f, 0f, 0.92f));
            var imageObj = new GameObject("Image", typeof(RectTransform));
            imageObj.transform.SetParent(scanPanel.transform, false);
            var imageRect = imageObj.GetComponent<RectTransform>();
            imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.sizeDelta = new Vector2(1440f, 810f);
            imageRect.anchoredPosition = new Vector2(0f, 40f);
            scanImage = imageObj.AddComponent<RawImage>();
            scanCaption = MakeText(scanPanel.transform, "Caption", 24, FontStyle.Italic, new Color(0.8f, 0.88f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1400f, 40f));

            video = root.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.isLooping = false;
            video.renderMode = VideoRenderMode.RenderTexture;
            // The clip's own sound on the Voice channel (a memory speaks; muted before 2026-10-26).
            scanAudio = root.AddComponent<AudioSource>();
            GlorifiedBody.Audio.GameAudio.Bind(scanAudio, GlorifiedBody.Audio.SoundChannel.Voice, 1f, spatial: false);
            video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            video.SetTargetAudioSource(0, scanAudio);
            video.waitForFirstFrame = true;
            video.skipOnDrop = true;
            video.loopPointReached += OnClipEnded;

            confirmPanel.SetActive(false);
            readerPanel.SetActive(false);
            scanPanel.SetActive(false);
            hintText.gameObject.SetActive(false);
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

        private static Text MakeText(Transform parent, string name, int size, FontStyle style, Color colour, Vector2 anchor, Vector2 position, Vector2 box)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
