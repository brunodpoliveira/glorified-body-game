using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The inventory overlay (Bruno, 2026-10-22: "the inventory should be an overlay over gameplay; it should
    /// pause the game"). The Inventory key (I) opens it over the frozen room, laid out after the Galerians item
    /// screen: a row of cells at the bottom (the key items first, then the carried objects, then the drugs with
    /// their counts), the selected item's name and words to the right, and the slots used. A and D move the
    /// selection. Interact reads a log, uses a drug, or says where a key goes. The Inventory key or Back closes
    /// it (Back closes an open page first). Placeholder look: plain panels, coloured cells, the legacy font.
    /// </summary>
    public sealed class InventoryUI : MonoBehaviour
    {
        public static event Action OnOpened;
        public static event Action OnClosed;
        /// <summary>A log read on the inventory screen (the tutorial's log step counts it).</summary>
        public static event Action<ItemDefinition> OnLogRead;

        public sealed class Entry
        {
            public string Label;
            public string Name;
            public string Words;
            public Color Colour;
            public ItemDefinition Item;
            public DrugType? Drug;
            public int Count;
        }

        private Canvas canvas;
        private GameObject root;
        private Text titleText;
        private Text wordsText;
        private Text keysText;
        private Text slotsText;
        private RectTransform cellRow;
        private GameObject readerPanel;
        private Text readerTitle;
        private Text readerBody;
        private readonly List<Image> cells = new List<Image>();

        private ItemInventory inventory;
        private DrugInventory drugs;
        private PsychicPowers powers;
        private Interactor interactor;
        private PsychicScanner scanner;
        private readonly List<Entry> entries = new List<Entry>();
        private int openFrame = -1;
        private bool drugMode; // opened by the drug key: a taken drug closes it
        private int pendingDrug = -1; // the drug the question "Use the ...?" is about (Bruno, 2026-10-22: ask first)

        public bool AskingDrug => pendingDrug >= 0;

        public Canvas Canvas => canvas;
        public bool IsOpen => root != null && root.activeSelf;
        public bool ReaderOpen => readerPanel != null && readerPanel.activeSelf;
        public IReadOnlyList<Entry> Entries => entries;
        public int Selected { get; private set; }
        public string SlotsShown => slotsText != null ? slotsText.text : "";
        public string WordsShown => wordsText != null ? wordsText.text : "";

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                enabled = false;
                return;
            }
            inventory = player.GetComponent<ItemInventory>();
            drugs = player.GetComponent<DrugInventory>();
            powers = player.GetComponent<PsychicPowers>();
            interactor = player.GetComponent<Interactor>();
            scanner = player.GetComponent<PsychicScanner>();
            Build();
            PsychicPowers.OpenDrugMenu = OpenDrugs;
        }

        private void OnDestroy()
        {
            if (PsychicPowers.OpenDrugMenu == (System.Func<bool>)OpenDrugs) PsychicPowers.OpenDrugMenu = null;
        }

        /// <summary>
        /// The drug key opens the overlay on the drugs (Bruno, 2026-10-24: "Q can't use the pills straight away ...
        /// a submenu so the player can choose which to take (or take none)"). The interact key asks and takes, the
        /// drug key or the inventory key closes it with none taken. A taken drug closes it.
        /// </summary>
        public bool OpenDrugs()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying || IsOpen || inventory == null) return false;
            if ((interactor != null && interactor.Current != Interactor.State.Free) || (scanner != null && scanner.IsViewing)) return false;
            Open();
            if (!IsOpen) return false;
            drugMode = true;
            // The choice opens on the drug she needs most (the SHORT capsule in a short), else on the first drug.
            DrugType? need = powers != null ? powers.MostNeededDrug() : null;
            int first = -1, wanted = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].Drug.HasValue) continue;
                if (first < 0) first = i;
                if (need.HasValue && entries[i].Drug.Value == need.Value) { wanted = i; break; }
            }
            if (wanted >= 0 || first >= 0) Select(wanted >= 0 ? wanted : first);
            return true;
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || inventory == null) return;
            if (!IsOpen)
            {
                bool busy = (interactor != null && interactor.Current != Interactor.State.Free) || (scanner != null && scanner.IsViewing);
                if (gm.IsPlaying && !busy && Time.frameCount != openFrame && GameInput.GetActionDown(InputManager.GameAction.Inventory, KeyCode.I)) Open();
                return;
            }
            if (!gm.IsInventory)
            {
                // Escape closed it through the GameManager.
                Hide();
                return;
            }
            if (Time.frameCount == openFrame) return;
            bool toggle = GameInput.GetActionDown(InputManager.GameAction.Inventory, KeyCode.I)
                || GameInput.GetActionDown(InputManager.GameAction.UseDrug, KeyCode.Q);
            bool back = GameInput.GetActionDown(InputManager.GameAction.Back, KeyCode.S);
            bool use = GameInput.GetActionDown(InputManager.GameAction.Interact, KeyCode.E);
            if (ReaderOpen)
            {
                if (back || use || toggle) readerPanel.SetActive(false);
                return;
            }
            if (AskingDrug)
            {
                if (use && Act().StartsWith("used:") && drugMode) Close();
                else if (back || toggle) { pendingDrug = -1; Select(Selected); }
                return;
            }
            if (toggle || back)
            {
                Close();
                return;
            }
            if (GameInput.GetActionDown(InputManager.GameAction.TurnLeft, KeyCode.A)) Select(Selected - 1);
            if (GameInput.GetActionDown(InputManager.GameAction.TurnRight, KeyCode.D)) Select(Selected + 1);
            if (use) Act();
        }

        public void Open()
        {
            GameManager.Instance?.OpenInventory();
            if (GameManager.Instance != null && !GameManager.Instance.IsInventory) return;
            openFrame = Time.frameCount;
            root.SetActive(true);
            readerPanel.SetActive(false);
            Rebuild();
            Select(0);
            OnOpened?.Invoke();
        }

        public void Close()
        {
            GameManager.Instance?.CloseInventory();
            Hide();
        }

        private void Hide()
        {
            if (!IsOpen) return;
            openFrame = Time.frameCount;
            drugMode = false;
            root.SetActive(false);
            OnClosed?.Invoke();
        }

        /// <summary>The entries: the key items, the carried objects, then one entry per drug type held.</summary>
        private void Rebuild()
        {
            entries.Clear();
            foreach (ItemDefinition item in inventory.Critical) entries.Add(FromItem(item, 1));
            foreach (ItemDefinition item in inventory.Carried) entries.Add(FromItem(item, 1));
            if (drugs != null)
            {
                foreach (DrugType type in (DrugType[])Enum.GetValues(typeof(DrugType)))
                {
                    int count = drugs.Count(type);
                    if (count <= 0) continue;
                    ItemDefinition def = Resources.Load<ItemDefinition>("Items/item_" + type.ToString().ToLowerInvariant());
                    Entry e = FromItem(def, count);
                    e.Drug = type;
                    if (def == null)
                    {
                        e.Name = type.ToString().ToUpperInvariant();
                        e.Words = "A drug.";
                        e.Colour = new Color(0.4f, 0.8f, 0.4f);
                    }
                    e.Label = $"{e.Name}\nx{count}";
                    entries.Add(e);
                }
            }
            for (int i = 0; i < cells.Count; i++)
            {
                bool used = i < entries.Count;
                cells[i].gameObject.SetActive(used);
                if (!used) continue;
                cells[i].color = entries[i].Colour * 0.8f + new Color(0f, 0f, 0f, 0.2f);
                cells[i].GetComponentInChildren<Text>().text = entries[i].Label;
            }
            slotsText.text = $"SLOTS {inventory.UsedSlots} / {inventory.Capacity}      KEY ITEMS {inventory.Critical.Count} (no slot)";
        }

        private static Entry FromItem(ItemDefinition item, int count)
        {
            if (item == null) return new Entry { Label = "?", Name = "?", Words = "", Colour = Color.grey, Count = count };
            string tag = item.IsCritical ? "KEY ITEM" : "CARRIED";
            return new Entry
            {
                Label = item.displayName,
                Name = item.displayName,
                Words = $"{tag}. {item.description}",
                Colour = item.colour,
                Item = item,
                Count = count
            };
        }

        public void SelectIndex(int index) => Select(index);

        private void Select(int index)
        {
            if (entries.Count == 0)
            {
                Selected = 0;
                titleText.text = "Item...  (nothing)";
                wordsText.text = "She carries nothing yet.";
                keysText.text = $"{Key(InputManager.GameAction.Inventory)} / {Key(InputManager.GameAction.Back)}  close";
                return;
            }
            Selected = (index % entries.Count + entries.Count) % entries.Count;
            pendingDrug = -1;
            for (int i = 0; i < entries.Count && i < cells.Count; i++)
            {
                Outline outline = cells[i].GetComponent<Outline>();
                outline.effectColor = i == Selected ? new Color(1f, 0.95f, 0.6f, 1f) : new Color(0f, 0f, 0f, 0.6f);
                outline.effectDistance = i == Selected ? new Vector2(4f, 4f) : new Vector2(1f, 1f);
            }
            Entry e = entries[Selected];
            titleText.text = "Item...  " + e.Name;
            wordsText.text = e.Words;
            string action = e.Drug.HasValue ? "use" : e.Item != null && e.Item.kind == ItemKind.Log ? "read" : "look";
            keysText.text = $"{Key(InputManager.GameAction.TurnLeft)} {Key(InputManager.GameAction.TurnRight)}  select      {Key(InputManager.GameAction.Interact)}  {action}      {Key(InputManager.GameAction.Inventory)} / {Key(InputManager.GameAction.Back)}  close";
        }

        /// <summary>Interact on the selected entry: read a log, use a drug, or say where a key goes.</summary>
        public string Act()
        {
            if (entries.Count == 0) return "none";
            Entry e = entries[Selected];
            if (e.Drug.HasValue)
            {
                if (pendingDrug != (int)e.Drug.Value)
                {
                    // The first press asks (Bruno, 2026-10-22): "Use the RED?".
                    pendingDrug = (int)e.Drug.Value;
                    wordsText.text = $"Use the {ItemDefinition.Spoken(e.Name)}?";
                    keysText.text = $"{Key(InputManager.GameAction.Interact)}  yes      {Key(InputManager.GameAction.Back)}  no";
                    return "ask:" + e.Drug.Value;
                }
                pendingDrug = -1;
                bool used = powers != null && powers.UseDrug(e.Drug.Value);
                Rebuild();
                Select(Selected);
                if (used || powers == null) return used ? "used:" + e.Drug.Value : "none";
                // A drug with nothing to do stays in the inventory: say why (Bruno, 2026-10-30: each drug does one thing).
                string why = powers.LastResult;
                wordsText.text = why == "mk_full" ? "MK is full." : why == "short_empty" ? "The SHORT bar is empty."
                    : why == "second_wind_ready" ? "The second wind is ready." : "It does nothing now.";
                return "refused:" + why;
            }
            if (e.Item != null && e.Item.kind == ItemKind.Log)
            {
                readerTitle.text = e.Item.displayName;
                readerBody.text = e.Item.logText;
                readerPanel.SetActive(true);
                OnLogRead?.Invoke(e.Item);
                return "read:" + e.Item.itemId;
            }
            wordsText.text = e.Item != null && e.Item.kind == ItemKind.Key ? "Use it at the door it opens: face the door and press " + Key(InputManager.GameAction.Interact) + "." : e.Words;
            return "look";
        }

        private static string Key(InputManager.GameAction action)
        {
            KeyCode key = InputManager.Instance != null ? InputManager.Instance.GetKey(action) : KeyCode.None;
            return key == KeyCode.None ? action.ToString() : InputManager.GetKeyDisplayName(key);
        }

        // ── Build ─────────────────────────────────────────────────────

        private void Build()
        {
            var canvasObj = new GameObject("InventoryUI", typeof(RectTransform));
            canvasObj.transform.SetParent(transform, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            root = Panel(canvasObj.transform, "Root", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f), new Color(0f, 0f, 0f, 0.35f));

            // Left: the item row under its title (Galerians: "Ability... Nalcon" over the icons).
            GameObject titleBox = Panel(root.transform, "TitleBox", new Vector2(0.5f, 0f), new Vector2(-330f, 330f), new Vector2(1000f, 70f), new Color(0.08f, 0.1f, 0.12f, 0.92f));
            titleBox.AddComponent<Outline>().effectColor = new Color(0.85f, 0.88f, 0.9f, 0.9f);
            titleText = Label(titleBox.transform, "Title", 32, FontStyle.Bold, new Color(0.7f, 0.95f, 0.95f), TextAnchor.MiddleLeft, new Vector2(960f, 60f));
            GameObject rowBox = Panel(root.transform, "RowBox", new Vector2(0.5f, 0f), new Vector2(-330f, 185f), new Vector2(1000f, 200f), new Color(0.08f, 0.1f, 0.12f, 0.92f));
            rowBox.AddComponent<Outline>().effectColor = new Color(0.85f, 0.88f, 0.9f, 0.9f);
            var rowObj = new GameObject("Cells", typeof(RectTransform));
            rowObj.transform.SetParent(rowBox.transform, false);
            cellRow = rowObj.GetComponent<RectTransform>();
            cellRow.anchorMin = cellRow.anchorMax = new Vector2(0.5f, 0.5f);
            cellRow.sizeDelta = new Vector2(960f, 150f);
            var layout = rowObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false; // the cells sit side by side from the left, no stretched gaps
            layout.childForceExpandHeight = false;
            for (int i = 0; i < 8; i++)
            {
                GameObject cell = new GameObject("Cell_" + i, typeof(RectTransform));
                cell.transform.SetParent(rowObj.transform, false);
                cell.GetComponent<RectTransform>().sizeDelta = new Vector2(104f, 140f);
                Image image = cell.AddComponent<Image>();
                cell.AddComponent<Outline>();
                Text label = Label(cell.transform, "Label", 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, new Vector2(100f, 136f));
                label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                cells.Add(image);
            }

            // Right: the words and the keys (Galerians: "FACULTY  Select Shock Wave?  X SELECT  Δ CANCEL").
            GameObject wordsBox = Panel(root.transform, "WordsBox", new Vector2(0.5f, 0f), new Vector2(520f, 220f), new Vector2(620f, 270f), new Color(0.08f, 0.1f, 0.12f, 0.92f));
            wordsBox.AddComponent<Outline>().effectColor = new Color(0.85f, 0.88f, 0.9f, 0.9f);
            Text header = Label(wordsBox.transform, "Header", 26, FontStyle.Bold, new Color(0.9f, 0.9f, 0.9f), TextAnchor.UpperRight, new Vector2(580f, 230f));
            header.text = "INVENTORY";
            wordsText = Label(wordsBox.transform, "Words", 22, FontStyle.Normal, new Color(0.75f, 0.95f, 0.95f), TextAnchor.MiddleLeft, new Vector2(580f, 120f));
            keysText = Label(wordsBox.transform, "Keys", 18, FontStyle.Bold, new Color(0.85f, 0.85f, 0.85f), TextAnchor.LowerCenter, new Vector2(580f, 240f));
            slotsText = Label(root.transform, "Slots", 22, FontStyle.Bold, new Color(0.95f, 0.92f, 0.8f), TextAnchor.MiddleCenter, new Vector2(1400f, 40f));
            slotsText.rectTransform.anchorMin = slotsText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            slotsText.rectTransform.anchoredPosition = new Vector2(-330f, 395f);

            readerPanel = Panel(root.transform, "Reader", new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1000f, 640f), new Color(0.06f, 0.06f, 0.07f, 0.96f));
            readerTitle = Label(readerPanel.transform, "Title", 30, FontStyle.Bold, new Color(0.96f, 0.93f, 0.9f), TextAnchor.UpperCenter, new Vector2(900f, 600f));
            readerBody = Label(readerPanel.transform, "Body", 22, FontStyle.Normal, new Color(0.88f, 0.88f, 0.86f), TextAnchor.MiddleLeft, new Vector2(880f, 460f));
            readerPanel.SetActive(false);
            root.SetActive(false);
        }

        private static GameObject Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color colour)
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

        private static Text Label(Transform parent, string name, int size, FontStyle style, Color colour, TextAnchor anchor, Vector2 box)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = box;
            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = colour;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
