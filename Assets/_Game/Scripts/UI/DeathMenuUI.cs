using UnityEngine;
using UnityEngine.UI;
using GlorifiedBody.Systems;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The game-over screen, Arkham shape: a picture, a voice line, and a hint to beat that enemy
    /// (DeathScreenContent). The killer sets LastKillerId before the lethal hit ("soldier_rifle"). Until the picture
    /// and the voice clip exist, a grey card shows the writer's picture note and the killer id, and the voice line
    /// shows as a caption (Bruno, 2026-10-25: test the feature in code before the assets).
    /// </summary>
    public sealed class DeathMenuUI : RuntimeOverlayMenu
    {
        public const string ContentResourceName = "DeathScreenContent";

        /// <summary>Set by whoever deals the lethal hit (an enemy type id, a hazard id). Empty = fallback.</summary>
        public static string LastKillerId { get; set; } = "";
        public const string SubtitleKey = "death_screen";

        // A new room starts with no killer: an id from the last run must not name the next death.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookSceneLoads()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => LastKillerId = "";

        /// <summary>The entry the screen shows now (for the tests).</summary>
        public DeathScreenContent.Entry Shown { get; private set; }
        public string CaptionShown => caption != null ? caption.text : "";
        public string HintShown => hint != null ? hint.text : "";
        public string PictureNoteShown => pictureNote != null && pictureNote.gameObject.activeSelf ? pictureNote.text : "";

        [SerializeField] private DeathScreenContent content;

        private Image picture;
        private Text pictureNote;
        private Text caption;
        private Text hint;
        private Sprite placeholderPicture;
        private AudioSource voice;

        protected override string MenuTitle => "YOU DIED";
        protected override string MenuSubtitle => "Retry the room, or return to the menu.";
        protected override UnityEngine.Color AccentColor => new UnityEngine.Color(1f, 0.35f, 0.35f);
        protected override Vector2 PanelSize => new Vector2(640f, 780f);
        protected override bool ShouldShow => GameManager.Instance != null && GameManager.Instance.IsGameOver;

        protected override void Update()
        {
            base.Update();
            if (!ShouldShow)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.R))
            {
                Restart();
            }
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M))
            {
                GameManager.Instance?.LoadMainMenu();
            }
        }

        protected override void BuildBody(Transform parent)
        {
            var pictureObj = CreateElement("Picture", parent);
            pictureObj.AddComponent<LayoutElement>().preferredHeight = 270f;
            picture = pictureObj.AddComponent<Image>();
            picture.preserveAspect = true;
            picture.color = Color.white;
            pictureNote = AddLabel(pictureObj.transform, "", 17, TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.82f, 0.82f, 0.86f));
            RectTransform noteRect = pictureNote.rectTransform;
            noteRect.anchorMin = Vector2.zero;
            noteRect.anchorMax = Vector2.one;
            noteRect.offsetMin = new Vector2(70f, 16f);
            noteRect.offsetMax = new Vector2(-70f, -16f);

            caption = AddLabel(parent, "", 19, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.95f, 0.95f, 0.95f));
            caption.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

            hint = AddLabel(parent, "", 20, TextAnchor.MiddleCenter, FontStyle.Italic, new Color(0.9f, 0.88f, 0.8f));
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;

            voice = GetComponent<AudioSource>();
            if (voice == null)
            {
                voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
            }
        }

        protected override void BuildButtons(Transform parent)
        {
            AddButton(parent, "Retry", Restart, true);
            AddButton(parent, "Main Menu", () => GameManager.Instance?.LoadMainMenu());
        }

        protected override void OnShown()
        {
            if (content == null)
            {
                content = Resources.Load<DeathScreenContent>(ContentResourceName);
            }

            DeathScreenContent.Entry entry = content != null ? content.Find(LastKillerId) : null;
            Shown = entry;
            bool hasPicture = entry != null && entry.picture != null;
            if (picture != null)
            {
                picture.sprite = hasPicture ? entry.picture : PlaceholderPicture();
            }

            if (pictureNote != null)
            {
                // The card until the picture exists: what it will show, and who killed her.
                pictureNote.gameObject.SetActive(!hasPicture);
                string note = entry != null && !string.IsNullOrEmpty(entry.pictureNote) ? entry.pictureNote : "PLACEHOLDER PICTURE";
                string id = string.IsNullOrEmpty(LastKillerId) ? "unknown" : LastKillerId;
                pictureNote.text = $"[{note}]\n\nkiller: {id}";
            }

            if (caption != null)
            {
                caption.text = entry != null ? entry.voiceText : "";
                caption.gameObject.SetActive(false); // the line shows as a subtitle (Bruno, 2026-10-25)
            }

            // The voice line as a subtitle with its speaker ("COMPUTER: Subject contained."), until the screen goes.
            GlorifiedBody.Subtitles.SubtitleService.Clear();
            if (entry != null && !string.IsNullOrEmpty(entry.voiceText))
            {
                (string speaker, string words) = GlorifiedBody.Subtitles.SubtitleService.Split(entry.voiceText, GlorifiedBody.Subtitles.SubtitleService.UnknownSpeaker);
                GlorifiedBody.Subtitles.SubtitleService.Show(SubtitleKey, speaker, words);
            }

            if (hint != null)
            {
                hint.text = entry != null && !string.IsNullOrEmpty(entry.hint) ? entry.hint : "PLACEHOLDER HINT";
            }

            if (voice != null && entry != null && entry.voiceLine != null)
            {
                voice.PlayOneShot(entry.voiceLine);
            }

            if (OverlayShot.Enabled && Canvas != null)
            {
                StartCoroutine(OverlayShot.Capture(Canvas, "death_ui.png", 0.3f));
            }
        }

        private Sprite PlaceholderPicture()
        {
            if (placeholderPicture != null) return placeholderPicture;
            var texture = new Texture2D(16, 9, TextureFormat.RGBA32, false);
            var pixels = new Color32[16 * 9];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(70, 70, 76, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            placeholderPicture = Sprite.Create(texture, new Rect(0f, 0f, 16f, 9f), new Vector2(0.5f, 0.5f), 1f);
            return placeholderPicture;
        }

        private static void Restart()
        {
            if (GameManager.Instance != null && !GameManager.Instance.RestartFromLatestCheckpoint())
            {
                GameManager.Instance.RestartLevel();
            }
        }
    }
}
