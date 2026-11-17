using UnityEngine;
using UnityEngine.UI;
using GlorifiedBody.Systems;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The game-over screen, Arkham shape (Bruno, 2026-11-16): full screen, the killer on his set (DeathStage) says his line
    /// (a subtitle, and the clip when it exists), the hint to beat him shows under him, and at the end the game reloads the
    /// latest checkpoint by itself. The Interact key skips to the reload; Escape opens the menu (reload now, the main menu).
    /// The content comes from DeathScreenContent by the killer id that the killer sets before the lethal hit
    /// ("soldier_rifle"). A hazard has no model: a picture, or the writer's picture note on a card.
    /// </summary>
    public sealed class DeathMenuUI : RuntimeOverlayMenu
    {
        public const string ContentResourceName = "DeathScreenContent";

        /// <summary>Set by whoever deals the lethal hit (an enemy type id, a hazard id). Empty = fallback.</summary>
        public static string LastKillerId { get; set; } = "";
        /// <summary>The personality of the person who dealt the lethal hit (BarkVoice), or "" (a hazard, a gas cloud).</summary>
        public static string LastKillerPersonality { get; set; } = "";

        /// <summary>The killer id and the person who dealt the hit: the death line follows his personality (round 138).</summary>
        public static void SetKiller(string killerId, GameObject who)
        {
            LastKillerId = killerId;
            GlorifiedBody.Enemy.BarkVoice voice = who != null ? GlorifiedBody.Enemy.BarkVoice.Of(who) : null;
            LastKillerPersonality = voice != null ? voice.Personality : "";
        }
        public const string SubtitleKey = "death_screen";

        // A new room starts with no killer: an id from the last run must not name the next death.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookSceneLoads()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            LastKillerId = "";
            LastKillerPersonality = "";
        }

        /// <summary>The entry the screen shows now (for the tests).</summary>
        public DeathScreenContent.Entry Shown { get; private set; }
        public string CaptionShown => caption != null ? caption.text : "";
        public string HintShown => hint != null ? hint.text : "";
        /// <summary>The killer's line on this screen ("SPEAKER: words").</summary>
        public string ShownLine { get; private set; } = "";
        private static string lastLine = "";
        private static string lastHint = "";

        /// <summary>The row's hint or one of its more hints (round 163: "one of the possible soldier game over hints"), at random, not the hint of the last screen.</summary>
        public static string PickHint(DeathScreenContent.Entry entry)
        {
            if (entry == null) return "";
            var pool = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(entry.hint)) pool.Add(entry.hint);
            foreach (string h in entry.moreHints) if (!string.IsNullOrEmpty(h) && !pool.Contains(h)) pool.Add(h);
            if (pool.Count == 0) return "";
            if (pool.Count > 1) pool.Remove(lastHint);
            lastHint = pool[Random.Range(0, pool.Count)];
            return lastHint;
        }

        /// <summary>
        /// The row's line, or one of the writer's death lines at random, not the line of the last screen. With a personality
        /// (round 138: the killer's, LastKillerPersonality), only the lines of that personality when the entry has some.
        /// </summary>
        public static string PickLine(DeathScreenContent.Entry entry) => PickLine(entry, LastKillerPersonality);

        public static string PickLine(DeathScreenContent.Entry entry, string personality)
        {
            if (entry == null) return "";
            var pool = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(personality))
                for (int i = 0; i < entry.moreLines.Count; i++)
                    if (i < entry.linePersonalities.Count && entry.linePersonalities[i] == personality && !string.IsNullOrEmpty(entry.moreLines[i]) && !pool.Contains(entry.moreLines[i]))
                        pool.Add(entry.moreLines[i]);
            if (pool.Count == 0)
            {
                if (!string.IsNullOrEmpty(entry.voiceText)) pool.Add(entry.voiceText);
                foreach (string l in entry.moreLines) if (!string.IsNullOrEmpty(l) && !pool.Contains(l)) pool.Add(l);
            }
            if (pool.Count == 0) return "";
            if (pool.Count > 1) pool.Remove(lastLine);
            lastLine = pool[Random.Range(0, pool.Count)];
            return lastLine;
        }

        public string PictureNoteShown => note != null && note.gameObject.activeSelf ? note.text : card != null && card.gameObject.activeSelf ? card.text : "";

        // ── The scene (Bruno, 2026-11-16) ─────────────────────────────
        // "like the batman arkham games, it is a full screen of the enemy doing the speech, animation and the hint; once completed
        // it automatically reloads to last checkpoint". His pick: the Interact key skips, Escape opens a menu (reload now, or quit).

        public const float MinSeconds = 5f;   // the shortest screen, PROPOSED
        public const float TailSeconds = 2.5f; // after his line, for the hint, PROPOSED
        public const float SkipAfter = 0.8f;   // a key held from the fight does not skip it

        /// <summary>The seconds of this screen: his line, then the tail. Then the reload.</summary>
        public float Duration { get; private set; } = MinSeconds;
        public float Elapsed { get; private set; }
        public bool MenuOpen { get; private set; }
        /// <summary>The set with the killer's model, or null (a hazard: the card).</summary>
        public DeathStage Stage => stage;

        [SerializeField] private DeathScreenContent content;

        private RawImage stageImage;
        private Image picture;
        private Text card;
        private Text note;
        private Text caption;
        private Text hint;
        private Text keys;
        private GameObject panel;
        private DeathStage stage;
        private AudioSource voice;
        private bool reloading;

        protected override string MenuTitle => "YOU DIED";
        protected override string MenuSubtitle => "Restart from the latest checkpoint, or return to the menu.";
        protected override UnityEngine.Color AccentColor => new UnityEngine.Color(1f, 0.35f, 0.35f);
        protected override Vector2 PanelSize => new Vector2(560f, 380f);
        protected override bool ShouldShow => GameManager.Instance != null && GameManager.Instance.IsGameOver;

        protected override void Update()
        {
            base.Update();
            if (!ShouldShow)
            {
                if (stage != null) EndScene();
                return;
            }
            if (reloading) return;
            if (Input.GetKeyDown(KeyCode.Escape)) SetMenu(!MenuOpen);
            if (MenuOpen) return;
            Elapsed += Time.unscaledDeltaTime;
            if (Elapsed >= SkipAfter && (GameInput.GetActionDown(InputManager.GameAction.Interact, KeyCode.E) || Input.GetKeyDown(KeyCode.Return)))
                Reload("skip");
            else if (Elapsed >= Duration)
                Reload("end");
        }

        private void OnDestroy()
        {
            if (stage != null) Destroy(stage.gameObject);
        }

        protected override void BuildBody(Transform parent)
        {
            voice = GetComponent<AudioSource>();
            if (voice == null)
            {
                voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
            }
        }

        protected override void BuildButtons(Transform parent)
        {
            AddButton(parent, "Restart from latest checkpoint", () => Reload("menu"), true); // was "Retry" (Bruno, 2026-11-16)
            AddButton(parent, "Main Menu", () => GameManager.Instance?.LoadMainMenu());
            AddButton(parent, "Back", () => SetMenu(false));
        }

        /// <summary>The full-screen scene under the menu panel: the set's picture, the card, the hint, the keys.</summary>
        private void BuildScene()
        {
            if (stageImage != null || Canvas == null) return;
            foreach (Transform t in Canvas.GetComponentsInChildren<Transform>(true))
                if (t.name == "Panel") panel = t.gameObject;
            if (panel != null) StyleMenuAsLines(panel);
            GameObject scene = CreateElement("Scene", Canvas.transform);
            scene.transform.SetSiblingIndex(1); // over the dim overlay, under the menu panel
            Stretch(scene.GetComponent<RectTransform>());
            scene.AddComponent<Image>().color = Color.black;

            GameObject set = CreateElement("Stage", scene.transform);
            Stretch(set.GetComponent<RectTransform>());
            stageImage = set.AddComponent<RawImage>();

            GameObject pic = CreateElement("Picture", scene.transform);
            Stretch(pic.GetComponent<RectTransform>());
            picture = pic.AddComponent<Image>();
            picture.preserveAspect = true;

            card = AddLabel(scene.transform, "", 30, TextAnchor.MiddleCenter, FontStyle.Normal, MenuStyle.Silver);
            Place(card.rectTransform, new Vector2(0.15f, 0.3f), new Vector2(0.85f, 0.8f));

            note = AddLabel(scene.transform, "", 16, TextAnchor.UpperLeft, FontStyle.Normal, MenuStyle.Dim);
            Place(note.rectTransform, new Vector2(0.02f, 0.86f), new Vector2(0.7f, 0.98f));

            caption = AddLabel(scene.transform, "", 19, TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            caption.gameObject.SetActive(false); // the line shows as a subtitle (Bruno, 2026-10-25)

            hint = AddLabel(scene.transform, "", 30, TextAnchor.MiddleCenter, FontStyle.Italic, MenuStyle.OffWhite);
            Place(hint.rectTransform, new Vector2(0.12f, 0.17f), new Vector2(0.88f, 0.3f));
            hint.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            keys = AddLabel(scene.transform, "", 20, TextAnchor.LowerRight, FontStyle.Normal, MenuStyle.Green);
            Place(keys.rectTransform, new Vector2(0.6f, 0.02f), new Vector2(0.98f, 0.07f));
        }

        /// <summary>
        /// T46 (2026-11-17): the menu keeps the Arkham style (Bruno: "keep the arkham style, it fits better ... change it
        /// to the correct font"). No box: the choices are lines of words at the bottom left, over the stage, under the
        /// hint. The selected line is orange with a blue rule (MenuLine). The title and the subtitle of the old box go.
        /// </summary>
        private static void StyleMenuAsLines(GameObject panel)
        {
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(120f, 24f);
            rect.sizeDelta = new Vector2(820f, 160f);
            Image box = panel.GetComponent<Image>();
            if (box != null) box.color = Color.clear;
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(0, 0, 0, 0);
                layout.spacing = 4;
                layout.childAlignment = TextAnchor.LowerLeft;
            }
            foreach (Transform child in panel.transform)
                if (child.GetComponent<Button>() == null) child.gameObject.SetActive(false);
            foreach (Button b in panel.GetComponentsInChildren<Button>(true))
            {
                MenuLine.Make(b, 40);
                LayoutElement le = b.GetComponent<LayoutElement>();
                if (le != null) le.preferredHeight = 50f;
            }
        }

        private static void Place(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        protected override void OnShown()
        {
            BuildScene();
            if (content == null)
            {
                content = Resources.Load<DeathScreenContent>(ContentResourceName);
            }

            DeathScreenContent.Entry entry = content != null ? content.Find(LastKillerId) : null;
            Shown = entry;
            reloading = false;
            Elapsed = 0f;
            SetMenu(false);

            // The killer's line as a subtitle with its speaker ("COMPUTER: Subject contained."), until the screen goes. With the
            // writer's death lines (DEMO_PLAN §3.6), one at random, never the same twice in a row.
            GlorifiedBody.Subtitles.SubtitleService.Clear();
            ShownLine = PickLine(entry);
            string words = "";
            if (!string.IsNullOrEmpty(ShownLine))
            {
                string speaker;
                (speaker, words) = GlorifiedBody.Subtitles.SubtitleService.Split(ShownLine, GlorifiedBody.Subtitles.SubtitleService.UnknownSpeaker);
                GlorifiedBody.Subtitles.SubtitleService.Show(SubtitleKey, speaker, words);
            }
            caption.text = entry != null ? entry.voiceText : "";

            float speak = GlorifiedBody.Subtitles.SubtitleService.ReadSeconds(words);
            if (voice != null && entry != null && entry.voiceLine != null)
            {
                // The Voice channel. The death screen pauses the world sounds, not its own line.
                GlorifiedBody.Audio.GameAudio.Configure(voice, GlorifiedBody.Audio.SoundChannel.Voice, 1f, false);
                voice.ignoreListenerPause = true;
                voice.PlayOneShot(entry.voiceLine);
                speak = GlorifiedBody.Audio.GameAudio.Length(entry.voiceLine, speak);
            }
            Duration = Mathf.Max(MinSeconds, speak + TailSeconds);

            // The killer on his set; a hazard has a picture or the writer's card.
            if (stage != null) Destroy(stage.gameObject);
            stage = DeathStage.Build(LastKillerId, speak, Duration);
            bool hasPicture = stage == null && entry != null && entry.picture != null;
            stageImage.texture = stage != null ? stage.Texture : null;
            stageImage.enabled = stage != null;
            picture.sprite = hasPicture ? entry.picture : null;
            picture.enabled = hasPicture;
            string pictureNote = entry != null && !string.IsNullOrEmpty(entry.pictureNote) ? entry.pictureNote : "PLACEHOLDER PICTURE";
            string id = string.IsNullOrEmpty(LastKillerId) ? "unknown" : LastKillerId;
            card.text = $"[{pictureNote}]\n\nkiller: {id}";
            card.gameObject.SetActive(stage == null && !hasPicture);
            // In a development build the corner names the killer and the note of the take to come.
            note.text = $"PLACEHOLDER [{pictureNote}]  killer: {id}";
            note.gameObject.SetActive(stage != null && GameBuild.Dev);

            string picked = PickHint(entry);
            hint.text = !string.IsNullOrEmpty(picked) ? picked : "PLACEHOLDER HINT";
            keys.text = $"{KeyName(InputManager.GameAction.Interact, KeyCode.E)}  skip      ESC  menu";
            GlorifiedBody.DebugTools.RunTelemetry.Event("death_screen", $"{id}:{(stage != null ? stage.ClassId : "card")}:{Duration:0.0}s");

            if (OverlayShot.Enabled && Canvas != null)
            {
                StartCoroutine(BotShots());
            }
        }

        /// <summary>Bot runs only: the screen (death_ui.png), then the Escape menu open (death_menu_ui.png), then closed again.</summary>
        private System.Collections.IEnumerator BotShots()
        {
            BotShotsDone = false;
            yield return OverlayShot.Capture(Canvas, "death_ui.png", 0.3f);
            if (IsVisible && !reloading)
            {
                SetMenu(true);
                yield return OverlayShot.Capture(Canvas, "death_menu_ui.png", 0.2f);
                if (IsVisible && !reloading) SetMenu(false);
            }
            BotShotsDone = true;
        }

        /// <summary>The bot's two shots of this screen are saved (the structural bot waits for them before its restart).</summary>
        public static bool BotShotsDone { get; private set; } = true;

        private void SetMenu(bool open)
        {
            MenuOpen = open;
            if (panel != null) panel.SetActive(open);
            if (!open) return;
            UnityEngine.EventSystems.EventSystem events = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            Button first = panel != null ? panel.GetComponentInChildren<Button>() : null;
            if (events != null && first != null) events.SetSelectedGameObject(first.gameObject);
        }

        private void EndScene()
        {
            if (stage != null) Destroy(stage.gameObject);
            stage = null;
            if (stageImage != null) stageImage.texture = null;
            MenuOpen = false;
        }

        /// <summary>The end of the screen: the latest checkpoint (by itself, the skip key, or the menu).</summary>
        private void Reload(string why)
        {
            if (reloading) return;
            reloading = true;
            GlorifiedBody.DebugTools.RunTelemetry.Event("death_screen_end", $"{why}:{Elapsed:0.0}s");
            Restart();
        }

        private static string KeyName(InputManager.GameAction action, KeyCode fallback)
        {
            KeyCode key = InputManager.Instance != null ? InputManager.Instance.GetKey(action) : fallback;
            return InputManager.GetKeyDisplayName(key == KeyCode.None ? fallback : key).ToUpperInvariant();
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
