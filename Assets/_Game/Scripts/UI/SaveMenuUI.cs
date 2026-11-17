using System.Collections.Generic;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The slot menu (Bruno, 2026-10-25: "3 manual save slots, one for the auto checkpoints"). At a save point it
    /// shows the three slots over the stopped game (the inventory's stop: only the menu keys pass); W and S move,
    /// Interact saves (a used slot asks first), Back or Escape closes. On the main menu (LOAD GAME) it shows the
    /// three slots and the checkpoint, and Interact loads. It also shows the short notes "SAVED IN SLOT 2" and
    /// "CHECKPOINT". Placeholder look: plain panels and the legacy font.
    /// </summary>
    public sealed class SaveMenuUI : MonoBehaviour
    {
        public enum Mode { Closed, Save, Load }

        private const float ToastSeconds = 2.2f;

        private static SaveMenuUI instance;
        private Canvas canvas;
        private GameObject root;
        private Text title;
        private Text caption; // the name under the badge of the AC3 frame (T46)
        private Text keys;
        private Text toast;
        private readonly List<Text> rows = new List<Text>();
        private readonly List<int> slots = new List<int>();
        private int openFrame = -1;
        private float toastUntil;
        private int asking = -1; // the used slot the question "Overwrite?" is about

        public static SaveMenuUI Instance => instance;
        public Mode Current { get; private set; } = Mode.Closed;
        public bool IsOpen => Current != Mode.Closed;
        public int Selected { get; private set; }
        public bool AskingOverwrite => asking >= 0;
        public IReadOnlyList<int> Slots => slots;
        public string RowText(int index) => index >= 0 && index < rows.Count ? rows[index].text : "";
        public string ToastShown => toast != null && toast.gameObject.activeSelf ? toast.text : "";
        public string Spot { get; private set; } = "";

        public static SaveMenuUI Ensure()
        {
            if (instance != null) return instance;
            var obj = new GameObject("[SaveMenu]");
            DontDestroyOnLoad(obj);
            instance = obj.AddComponent<SaveMenuUI>();
            return instance;
        }

        /// <summary>Made at the start, so the notes of a checkpoint show before any menu opens.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() => Ensure();

        /// <summary>A save point's yes: the slots over the stopped game.</summary>
        public static void OpenSave(SavePoint point)
        {
            SaveMenuUI menu = Ensure();
            GameManager.Instance?.OpenInventory();
            if (GameManager.Instance != null && !GameManager.Instance.IsInventory) return;
            menu.Spot = point != null ? point.SpotId : "";
            menu.Show(Mode.Save);
        }

        /// <summary>The main menu's LOAD GAME.</summary>
        public static void OpenLoad() => Ensure().Show(Mode.Load);

        private void Awake()
        {
            Build();
            SaveSystem.OnToast += ShowToast;
        }

        private void OnDestroy()
        {
            SaveSystem.OnToast -= ShowToast;
            if (instance == this) instance = null;
        }

        private void Show(Mode mode)
        {
            Current = mode;
            openFrame = Time.frameCount;
            asking = -1;
            slots.Clear();
            for (int slot = 1; slot <= SaveSystem.ManualSlots; slot++) slots.Add(slot);
            if (mode == Mode.Load) slots.Add(SaveSystem.CheckpointSlot);
            title.text = mode == Mode.Save ? "SAVE" : "LOAD GAME";
            if (caption != null) caption.text = mode == Mode.Save ? "SAVE" : "LOAD";
            root.SetActive(true);
            Select(mode == Mode.Load ? Mathf.Max(0, slots.IndexOf(SaveSystem.Newest())) : 0);
        }

        public void Close()
        {
            if (Current == Mode.Save) GameManager.Instance?.CloseInventory();
            Current = Mode.Closed;
            asking = -1;
            root.SetActive(false);
        }

        private void Update()
        {
            if (toast.gameObject.activeSelf && Time.unscaledTime >= toastUntil) toast.gameObject.SetActive(false);
            if (!IsOpen) return;
            if (Current == Mode.Save && GameManager.Instance != null && !GameManager.Instance.IsInventory)
            {
                // Escape closed it through the GameManager.
                Current = Mode.Closed;
                root.SetActive(false);
                return;
            }
            if (Time.frameCount == openFrame) return; // the Interact press that opened it
            bool up = GameInput.GetActionDown(InputManager.GameAction.Forward, KeyCode.W);
            bool down = GameInput.GetActionDown(InputManager.GameAction.Back, KeyCode.S);
            bool use = GameInput.GetActionDown(InputManager.GameAction.Interact, KeyCode.E);
            bool cancel = Input.GetKeyDown(KeyCode.Escape) || GameInput.GetActionDown(InputManager.GameAction.Inventory, KeyCode.I);
            if (AskingOverwrite)
            {
                if (use) Write(asking);
                else if (down || cancel) { asking = -1; Select(Selected); }
                return;
            }
            if (cancel) { Close(); return; }
            if (up) Select(Selected - 1);
            if (down) Select(Selected + 1);
            if (use) Choose(Selected);
        }

        /// <summary>The row's action: save into it (a used one asks first) or load it. Tests and bots call it too.</summary>
        public void Choose(int index)
        {
            if (index < 0 || index >= slots.Count) return;
            int slot = slots[index];
            if (Current == Mode.Save)
            {
                if (SaveSystem.Read(slot) != null && asking != slot)
                {
                    asking = slot;
                    keys.text = $"Overwrite slot {slot}?   {KeyName(InputManager.GameAction.Interact)} YES   {KeyName(InputManager.GameAction.Back)} NO";
                    return;
                }
                Write(slot);
            }
            else if (Current == Mode.Load && SaveSystem.Read(slot) != null)
            {
                Close();
                SaveSystem.Load(slot);
            }
        }

        private void Write(int slot)
        {
            asking = -1;
            bool saved = SaveSystem.SaveManual(slot, Spot);
            Close();
            if (!saved) ShowToast("THE SAVE FAILED");
        }

        private void Select(int index)
        {
            if (slots.Count == 0) return;
            Selected = (index % slots.Count + slots.Count) % slots.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < slots.Count;
                rows[i].transform.parent.gameObject.SetActive(used);
                if (!used) continue;
                rows[i].text = Describe(slots[i]);
                bool on = i == Selected;
                rows[i].color = on ? MenuStyle.OffWhite : MenuStyle.Silver;
                rows[i].transform.parent.GetComponent<Image>().color = on ? MenuStyle.WellOn : MenuStyle.Well;
            }
            string action = Current == Mode.Save ? "SAVE" : "LOAD";
            keys.text = $"{KeyName(InputManager.GameAction.Forward)}/{KeyName(InputManager.GameAction.Back)} MOVE   {KeyName(InputManager.GameAction.Interact)} {action}   ESC CANCEL";
        }

        /// <summary>"SLOT 2   TrainingRoom   2026-10-25 21:10   0:12:34", or EMPTY.</summary>
        public static string Describe(int slot)
        {
            string name = slot == SaveSystem.CheckpointSlot ? "CHECKPOINT" : $"SLOT {slot}";
            SaveData data = SaveSystem.Read(slot);
            if (data == null) return SaveSystem.Exists(slot) ? $"{name}   (cannot read this file)" : $"{name}   - EMPTY -";
            int s = Mathf.FloorToInt(data.playSeconds);
            return $"{name}   {data.scene}   {data.savedAt}   {s / 3600}:{s / 60 % 60:00}:{s % 60:00}";
        }

        private void ShowToast(string words)
        {
            toast.text = words;
            toast.gameObject.SetActive(true);
            toastUntil = Time.unscaledTime + ToastSeconds;
        }

        private static string KeyName(InputManager.GameAction action)
        {
            KeyCode key = InputManager.Instance != null ? InputManager.Instance.GetKey(action) : KeyCode.None;
            return key == KeyCode.None ? action.ToString().ToUpperInvariant() : InputManager.GetKeyDisplayName(key).ToUpperInvariant();
        }

        // ── Build ─────────────────────────────────────────────────────

        private void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 85;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            gameObject.AddComponent<GraphicRaycaster>();

            // T46: the menu look. A dark see-through panel, a thin silver edge, the selected slot in orange.
            root = Box(transform, "Root", Vector2.zero, new Vector2(1920f, 1080f), MenuStyle.Scrim);
            MenuStyle.Fill((RectTransform)root.transform, 0f); // the whole screen at any shape (the frame stretches over it)
            MenuStyle.MenuFrame frame = MenuStyle.Frame(root.transform, "LOAD GAME", "THE SLOTS  /  THE CHECKPOINT", "MEMORY", "load", "ESC  CANCEL");
            title = frame.Title;
            caption = frame.Caption;
            GameObject panel = Box(frame.Content, "Panel", new Vector2(0f, 40f), new Vector2(1100f, 520f), Color.clear);
            for (int i = 0; i <= SaveSystem.ManualSlots; i++)
            {
                GameObject row = Box(panel.transform, "Row" + i, new Vector2(0f, 110f - i * 88f), new Vector2(1000f, 74f), MenuStyle.Well);
                int index = i;
                row.AddComponent<Button>().onClick.AddListener(() => { Select(index); Choose(index); });
                Text rowText = Label(row.transform, "Text", 28, FontStyle.Normal, MenuStyle.OffWhite, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(960f, 70f));
                rowText.font = MenuStyle.Head;
                rows.Add(rowText);
            }
            keys = Label(panel.transform, "Keys", 20, FontStyle.Normal, MenuStyle.Dim, TextAnchor.LowerCenter, new Vector2(0f, -225f), new Vector2(1000f, 40f));
            root.SetActive(false);

            toast = Label(transform, "Toast", 32, FontStyle.Normal, MenuStyle.Green, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(900f, 50f));
            toast.font = MenuStyle.Head;
            toast.rectTransform.anchorMin = toast.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            toast.rectTransform.anchoredPosition = new Vector2(0f, -90f);
            toast.gameObject.AddComponent<Outline>().effectColor = Color.black;
            toast.gameObject.SetActive(false);
        }

        private static GameObject Box(Transform parent, string name, Vector2 position, Vector2 size, Color colour)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            obj.AddComponent<Image>().color = colour;
            return obj;
        }

        private static Text Label(Transform parent, string name, int size, FontStyle style, Color colour, TextAnchor anchor, Vector2 position, Vector2 box)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            var text = obj.AddComponent<Text>();
            text.font = MenuStyle.Body;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = colour;
            text.alignment = anchor;
            text.raycastTarget = false;
            return text;
        }
    }
}
