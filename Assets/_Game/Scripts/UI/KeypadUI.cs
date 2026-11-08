using UnityEngine;
using UnityEngine.UI;
using GlorifiedBody.Interaction;
using GlorifiedBody.Systems;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The keypad of a security terminal (CodeTerminal, DEMO_PLAN §10.2): one digit for each digit of the terminal's code
    /// (Bruno, 2026-11-08: "the correct code to rotate is 8675309", seven). It stops the world as a log does
    /// (GameState.Reading). The forward and back keys change the digit under the cursor, the turn keys move the cursor,
    /// and the number keys type a digit. The Interact key enters the code: the right one enrols the card and the keypad
    /// closes, a wrong one says so and stays open. The Inventory key or Escape closes it. It builds itself at the first use.
    /// </summary>
    public sealed class KeypadUI : MonoBehaviour
    {
        /// <summary>The digits of the open terminal's code.</summary>
        public int Length => digits.Length;
        public const float CloseAfterSeconds = 1.2f;

        public static KeypadUI Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.open;

        public CodeTerminal Terminal { get; private set; }
        public int Cursor { get; private set; }
        public string Message { get; private set; } = "";
        public string Digits => new string(System.Array.ConvertAll(digits, d => (char)('0' + d)));

        private int[] digits = new int[0];
        private bool open;
        private int openFrame;
        private float closeAt = -1f;
        private bool closeRequested;
        private GameObject panel;
        private Text digitsText, messageText, keysText;

        /// <summary>Opens the keypad on the terminal. False when the game is not playing.</summary>
        public static bool Open(CodeTerminal terminal)
        {
            GameManager gm = GameManager.Instance;
            if (terminal == null || gm == null || IsOpen || !gm.BeginReading()) return false;
            if (Instance == null) Instance = new GameObject("[Keypad]").AddComponent<KeypadUI>();
            Instance.Show(terminal);
            return true;
        }

        private void Show(CodeTerminal terminal)
        {
            if (panel == null) Build();
            Terminal = terminal;
            digits = new int[Mathf.Max(1, terminal.code.Length)];
            Cursor = 0;
            Message = "ENTER SITE CODE.";
            closeAt = -1f;
            closeRequested = false;
            open = true;
            openFrame = Time.frameCount;
            panel.SetActive(true);
            GlorifiedBody.Audio.Sfx.Ui(GlorifiedBody.Audio.Sfx.UiOpen);
            Draw();
        }

        /// <summary>Closes the keypad and lets the world run again.</summary>
        public void Close()
        {
            if (!open) return;
            open = false;
            Terminal = null;
            if (panel != null) panel.SetActive(false);
            GameManager.Instance?.EndReading();
        }

        /// <summary>Sets the digit under the cursor and moves the cursor on (a typed number, and the tests).</summary>
        public void Type(int digit)
        {
            digits[Cursor] = ((digit % 10) + 10) % 10;
            Cursor = Mathf.Min(Length - 1, Cursor + 1);
            Draw();
        }

        /// <summary>The Interact key: enters the code on the terminal.</summary>
        public bool Enter()
        {
            if (Terminal == null) return false;
            bool right = Terminal.TryCode(Digits);
            Message = right ? CodeTerminal.EnrolledText : CodeTerminal.WrongText;
            if (right) closeAt = Time.realtimeSinceStartup + CloseAfterSeconds;
            Draw();
            return right;
        }

        private void Update()
        {
            if (!open || Time.frameCount == openFrame) return;
            if (closeAt > 0f)
            {
                if (Time.realtimeSinceStartup >= closeAt) Close();
                return;
            }
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.IsReading) { Close(); return; }

            foreach (char c in Input.inputString)
                if (c >= '0' && c <= '9') Type(c - '0');
            if (GameInput.GetActionDown(InputManager.GameAction.Forward, KeyCode.W)) Step(1);
            if (GameInput.GetActionDown(InputManager.GameAction.Back, KeyCode.S)) Step(-1);
            if (GameInput.GetActionDown(InputManager.GameAction.TurnLeft, KeyCode.A)) Move(-1);
            if (GameInput.GetActionDown(InputManager.GameAction.TurnRight, KeyCode.D)) Move(1);
            if (Input.GetKeyDown(KeyCode.Backspace)) { digits[Cursor] = 0; Move(-1); }
            if (GameInput.GetActionDown(InputManager.GameAction.Interact, KeyCode.E)) Enter();
            else if (GameInput.GetActionDown(InputManager.GameAction.Inventory, KeyCode.I) || Input.GetKeyDown(KeyCode.Escape))
            {
                GlorifiedBody.Audio.Sfx.Ui(GlorifiedBody.Audio.Sfx.UiClose);
                closeRequested = true;
            }
        }

        // The close waits for the end of the frame: the inventory screen and the pause read the same key in this frame,
        // and they must still see the stopped world.
        private void LateUpdate()
        {
            if (!closeRequested) return;
            closeRequested = false;
            Close();
        }

        private void Step(int by)
        {
            digits[Cursor] = (digits[Cursor] + by + 10) % 10;
            GlorifiedBody.Audio.Sfx.Ui(GlorifiedBody.Audio.Sfx.UiMove);
            Draw();
        }

        private void Move(int by)
        {
            Cursor = Mathf.Clamp(Cursor + by, 0, Length - 1);
            Draw();
        }

        private void Draw()
        {
            if (digitsText == null) return;
            var line = new System.Text.StringBuilder();
            for (int i = 0; i < Length; i++)
            {
                if (i > 0) line.Append(' ');
                line.Append(i == Cursor ? $"[{digits[i]}]" : $" {digits[i]} ");
            }
            digitsText.text = line.ToString();
            messageText.text = Message;
            keysText.text = $"{Key(InputManager.GameAction.Forward)} {Key(InputManager.GameAction.Back)}  digit      {Key(InputManager.GameAction.TurnLeft)} {Key(InputManager.GameAction.TurnRight)}  move      0-9  type      {Key(InputManager.GameAction.Interact)}  enter      {Key(InputManager.GameAction.Inventory)}  close";
        }

        private static string Key(InputManager.GameAction action)
        {
            KeyCode key = InputManager.Instance != null ? InputManager.Instance.GetKey(action) : KeyCode.None;
            return key == KeyCode.None ? action.ToString() : InputManager.GetKeyDisplayName(key);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (open) GameManager.Instance?.EndReading();
        }

        // ── Build ─────────────────────────────────────────────────────

        private void Build()
        {
            var root = new GameObject("KeypadCanvas", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1100f, 420f);
            panel.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.04f, 0.95f);

            Text title = MakeText(panel.transform, "Title", 26, new Color(0.55f, 0.9f, 0.6f), new Vector2(0f, 150f), new Vector2(1060f, 40f));
            title.text = "WARD SECURITY. SITE CODE";
            digitsText = MakeText(panel.transform, "Digits", 64, new Color(0.7f, 1f, 0.75f), new Vector2(0f, 40f), new Vector2(1060f, 90f));
            messageText = MakeText(panel.transform, "Message", 26, new Color(0.9f, 0.95f, 0.85f), new Vector2(0f, -60f), new Vector2(1060f, 40f));
            keysText = MakeText(panel.transform, "Keys", 18, new Color(0.6f, 0.7f, 0.65f), new Vector2(0f, -160f), new Vector2(1060f, 30f));
            panel.SetActive(false);
        }

        private static Text MakeText(Transform parent, string name, int size, Color colour, Vector2 position, Vector2 box)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            return text;
        }
    }
}
