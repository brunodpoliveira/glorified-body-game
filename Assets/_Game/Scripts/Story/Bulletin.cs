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
    /// The world stops while it shows (the cutscene state). Interact skips the typing, then closes the card. The words
    /// are the writer's draft. The bands are PROPOSED: the map forces about 8 kills.
    /// </summary>
    public sealed class Bulletin : MonoBehaviour
    {
        public const int LowMax = 10, MiddleMax = 20; // PROPOSED: up to 10 low, up to 20 middle, more high
        public const float CharSeconds = 0.03f, LineGap = 0.35f;

        public static readonly string[] Low =
        {
            "BE ON THE LOOKOUT.",
            "FEMALE, APPROXIMATELY 20. BARE-FACED.",
            "WANTED FOR QUESTIONING IN CONNECTION WITH AN INCIDENT AT THE WINWOOD READE MEMORIAL.",
            "LAST SEEN ON FOOT, HEADING TOWARD THE RIVER.",
        };
        public static readonly string[] Middle =
        {
            "BE ON THE LOOKOUT.",
            "FEMALE, APPROXIMATELY 20. BARE-FACED.",
            "ARMED AND DANGEROUS. SOUGHT IN CONNECTION WITH [N] DEATHS AT THE WINWOOD READE MEMORIAL.",
            "LAST SEEN ON FOOT, HEADING TOWARD THE RIVER. DO NOT APPROACH.",
        };
        public static readonly string[] High =
        {
            "BE ON THE LOOKOUT.",
            "FEMALE, APPROXIMATELY 20. BARE-FACED.",
            "SOUGHT IN CONNECTION WITH [N] DEATHS. NO WEAPON RECOVERED.",
            "DO NOT APPROACH. DO NOT MAKE EYE CONTACT.",
            "LAST SEEN ON FOOT, HEADING TOWARD THE CITY.",
        };

        private static Bulletin instance;
        public static bool IsShowing { get; private set; }
        /// <summary>The text on the card now (tests).</summary>
        public static string Shown => instance != null && instance.text != null ? instance.text.text : "";
        public static string Band { get; private set; } = "";

        private Canvas canvas;
        private Text text;
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
            yield return null;
            while (!Pressed()) yield return null;
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
            rect.sizeDelta = new Vector2(1500f, 600f);
            text = textObj.AddComponent<Text>();
            Font mono = Font.CreateDynamicFontFromOSFont(new[] { "DejaVu Sans Mono", "Liberation Mono", "Courier New", "Consolas" }, 30);
            text.font = mono != null ? mono : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.lineSpacing = 1.3f;
            text.color = new Color(0.93f, 0.93f, 0.93f);
            text.alignment = TextAnchor.MiddleLeft;
            canvas.enabled = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            instance = null;
            IsShowing = false;
            Band = "";
        }
    }
}
