using UnityEngine;
using UnityEngine.UI;
using GlorifiedBody.Systems;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The game-over screen, Arkham shape: a picture, a voice line, and a hint to beat that enemy
    /// (DeathScreenContent). The killer sets LastKillerId before the lethal hit. Placeholder words until the
    /// writer delivers them.
    /// </summary>
    public sealed class DeathMenuUI : RuntimeOverlayMenu
    {
        public const string ContentResourceName = "DeathScreenContent";

        /// <summary>Set by whoever deals the lethal hit (an enemy type id, a hazard id). Empty = fallback.</summary>
        public static string LastKillerId { get; set; } = "";

        [SerializeField] private DeathScreenContent content;

        private Image picture;
        private Text hint;
        private Sprite placeholderPicture;
        private AudioSource voice;

        protected override string MenuTitle => "YOU DIED";
        protected override string MenuSubtitle => "Retry the room, or return to the menu.";
        protected override UnityEngine.Color AccentColor => new UnityEngine.Color(1f, 0.35f, 0.35f);
        protected override Vector2 PanelSize => new Vector2(640f, 720f);
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
            if (picture != null)
            {
                picture.sprite = entry != null && entry.picture != null ? entry.picture : PlaceholderPicture();
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
