using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Story
{
    /// <summary>
    /// The police bulletin, the last card of the demo (T11 of the writers' room, 2026-10-31: "A text card after cutscene
    /// 10, before the title. White monospace on black, like a teleprinter, one line at a time. The game picks the wording
    /// from the number of people Lucy killed"). The count is PsychicStats.People (every person she killed, in the saves).
    /// The world stops while it shows (the cutscene state). Interact skips the typing, then closes the card, and the card closes by itself after AutoCloseSeconds. The words
    /// are the writer's draft. The bands are PROPOSED: the map forces about 8 kills.
    /// </summary>
    public sealed class Bulletin : MonoBehaviour
    {
        public const int LowMax = 10, MiddleMax = 20; // PROPOSED: up to 10 low, up to 20 middle, more high
        public const float CharSeconds = 0.012f, LineGap = 0.25f; // round 138: about 1300 characters, about 18 s
        public const float AutoCloseSeconds = 12f; // PROPOSED: the demo ends by itself (Bruno, 2026-11-05: "it needs to exit after the demo is complete")

        /// <summary>
        /// The writer's T11 (game/lines/T11_bulletin.md, CANON round 138), copied to Resources by BulletinSetup.Build: the lines
        /// under "## Low", "## Middle" and "## High". Round 137: no description says "bare-faced".
        /// </summary>
        public const string ResourcePath = "Writer/T11_bulletin";
        private static readonly string[] Fallback = { "BE ON THE LOOKOUT." };
        private static string[] low, middle, high;

        public static string[] Low { get { Load(); return low; } }
        public static string[] Middle { get { Load(); return middle; } }
        public static string[] High { get { Load(); return high; } }

        private static void Load()
        {
            if (low != null) return;
            TextAsset file = Resources.Load<TextAsset>(ResourcePath);
            if (file == null) Debug.LogWarning("[Bulletin] no Resources/" + ResourcePath + ": run BulletinSetup.Build");
            Parse(file != null ? file.text : "", out low, out middle, out high);
        }

        /// <summary>The three bands of the writer's file: the lines under each "## " heading, up to the next one.</summary>
        public static void Parse(string text, out string[] lowLines, out string[] middleLines, out string[] highLines)
        {
            var parts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
            System.Collections.Generic.List<string> current = null;
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                if (raw.StartsWith("## "))
                {
                    string name = raw.Substring(3).Trim().Split(' ')[0];
                    current = new System.Collections.Generic.List<string>();
                    parts[name] = current;
                    continue;
                }
                if (current != null && raw.Trim().Length > 0) current.Add(raw.Trim());
            }
            string[] Band(string name) => parts.TryGetValue(name, out var lines) && lines.Count > 0 ? lines.ToArray() : Fallback;
            lowLines = Band("Low");
            middleLines = Band("Middle");
            highLines = Band("High");
        }

        private static Bulletin instance;
        public static bool IsShowing { get; private set; }
        /// <summary>The text on the card now (tests).</summary>
        public static string Shown => instance != null && instance.text != null ? instance.text.text : "";
        public static string Band { get; private set; } = "";
        /// <summary>The key's hint shows: the card has typed all its lines (tests).</summary>
        public static bool HintShown => instance != null && instance.hint != null && instance.hint.enabled;
        public static Canvas Card => instance != null ? instance.canvas : null;
        /// <summary>The whole text fits the card (round 138: the CLOTHING line is about 150 characters).</summary>
        public static bool Fits => instance != null && instance.text != null && instance.text.preferredHeight <= instance.text.rectTransform.rect.height;

        private Canvas canvas;
        private Text text;
        private Text hint;
        private bool skip;

        /// <summary>The wording for the count: the band's lines, [N] the count.</summary>
        public static string[] WordingFor(int people, out string band)
        {
            string[] lines = people <= LowMax ? Low : people <= MiddleMax ? Middle : High;
            band = people <= LowMax ? "low" : people <= MiddleMax ? "middle" : "high";
            var result = new string[lines.Length];
            for (int i = 0; i < lines.Length; i++) result[i] = lines[i].Replace("[N]", people.ToString());
            return result;
        }

        /// <summary>Shows the card for the count. Calls done when the player closes it. False when one shows.</summary>
        public static bool Show(int people, Action done = null)
        {
            if (IsShowing) return false;
            if (instance == null)
            {
                var obj = new GameObject("[Bulletin]");
                DontDestroyOnLoad(obj);
                instance = obj.AddComponent<Bulletin>();
                instance.Build();
            }
            instance.StartCoroutine(instance.Run(people, done));
            return true;
        }

        private IEnumerator Run(int people, Action done)
        {
            IsShowing = true;
            skip = false;
            string[] lines = WordingFor(people, out string band);
            Band = band;
            bool stopped = GameManager.Instance != null && GameManager.Instance.BeginCutscene();
            canvas.enabled = true;
            text.text = "";
            RunTelemetry.Event("bulletin", $"{band}:{people}");
            yield return null; // the key that started it does not skip it
            // The teleprinter: one character at a time, one line at a time (real time: the world is stopped).
            foreach (string line in lines)
            {
                for (int c = 0; c < line.Length && !skip; c++)
                {
                    text.text += line[c];
                    float until = Time.unscaledTime + CharSeconds;
                    while (Time.unscaledTime < until && !skip) { skip |= Pressed(); yield return null; }
                }
                if (skip) break;
                text.text += "\n";
                float gap = Time.unscaledTime + LineGap;
                while (Time.unscaledTime < gap && !skip) { skip |= Pressed(); yield return null; }
            }
            text.text = string.Join("\n", lines) + "\n";
            hint.text = $"{InputManager.GetKeyDisplayName(InputManager.Instance != null ? InputManager.Instance.GetKey(InputManager.GameAction.Interact) : KeyCode.E)}  continue";
            hint.enabled = true;
            yield return null;
            float closeAt = Time.unscaledTime + AutoCloseSeconds;
            while (!Pressed() && Time.unscaledTime < closeAt) yield return null;
            hint.enabled = false;
            canvas.enabled = false;
            IsShowing = false;
            if (stopped) GameManager.Instance?.EndCutscene();
            done?.Invoke();
        }

        private static bool Pressed() => GameInput.GetActionDown(InputManager.GameAction.Interact, KeyCode.E);

        private void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var back = new GameObject("Black", typeof(RectTransform));
            back.transform.SetParent(transform, false);
            RectTransform backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = Vector2.zero;
            backRect.anchorMax = Vector2.one;
            backRect.offsetMin = backRect.offsetMax = Vector2.zero;
            back.AddComponent<Image>().color = Color.black;
            var textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(transform, false);
            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1720f, 960f);
            text = textObj.AddComponent<Text>();
            Font mono = Font.CreateDynamicFontFromOSFont(new[] { "DejaVu Sans Mono", "Liberation Mono", "Courier New", "Consolas" }, 24);
            text.font = mono != null ? mono : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.lineSpacing = 1.2f;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color(0.93f, 0.93f, 0.93f);
            text.alignment = TextAnchor.MiddleLeft;
            var hintObj = new GameObject("Hint", typeof(RectTransform));
            hintObj.transform.SetParent(transform, false);
            RectTransform hintRect = hintObj.GetComponent<RectTransform>();
            hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0.08f);
            hintRect.sizeDelta = new Vector2(1500f, 50f);
            hint = hintObj.AddComponent<Text>();
            hint.font = text.font;
            hint.fontSize = 24;
            hint.color = new Color(0.6f, 0.6f, 0.6f);
            hint.alignment = TextAnchor.MiddleRight;
            hint.enabled = false;
            canvas.enabled = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            instance = null;
            low = middle = high = null;
            IsShowing = false;
            Band = "";
        }
    }
}
