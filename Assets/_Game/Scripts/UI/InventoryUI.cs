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
            public int Category;
        }

        // Bruno, 2026-11-06: "three categories: consumables, keys, logs, in that order; can be scrolled each cat by going up or down".
        public const int Consumables = 0, Keys = 1, Logs = 2;
        public static readonly string[] CategoryNames = { "CONSUMABLES", "KEYS", "LOGS" };
        public const int VisibleRows = 6;
        private static readonly DrugType[] DrugOrder = { DrugType.Nalcon, DrugType.NalconFire, DrugType.NalconLift, DrugType.NalconMind, DrugType.Red, DrugType.DFelon };

        private Canvas canvas;
        private GameObject root;
        private Text titleText;
        private Text wordsText;
        private Text keysText;
        private Text slotsText;
        private GameObject readerPanel;
        private Text readerTitle;
        private Text readerBody;
        private readonly Image[,] cells = new Image[CategoryNames.Length, VisibleRows];
        private readonly Text[] categoryHeaders = new Text[CategoryNames.Length];
        private readonly int[] scrolls = new int[CategoryNames.Length];

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
            bool drop = GameInput.GetActionDown(InputManager.GameAction.Drop, KeyCode.X);
            if (ReaderOpen)
            {
                if (back || use || toggle) readerPanel.SetActive(false);
                return;
            }
            if (AskingDrop)
            {
                if (use) Drop();
                else if (back || toggle || drop) { askingDrop = false; Select(Selected); }
                return;
            }
            if (AskingDrug)
            {
                if (use && Act().StartsWith("used:") && drugMode) Close();
                else if (back || toggle) { pendingDrug = -1; Select(Selected); }
                return;
            }
            if (toggle)
            {
                Close();
                return;
            }
            if (GameInput.GetActionDown(InputManager.GameAction.Forward, KeyCode.W)) Scroll(-1);
            if (back) Scroll(1);
            if (GameInput.GetActionDown(InputManager.GameAction.TurnLeft, KeyCode.A)) ChangeCategory(-1);
            if (GameInput.GetActionDown(InputManager.GameAction.TurnRight, KeyCode.D)) ChangeCategory(1);
            if (use) Act();
            else if (drop) Drop();
        }

        private bool askingDrop;
        /// <summary>The question "Drop the RED?" is on.</summary>
        public bool AskingDrop => askingDrop;

        /// <summary>
        /// The drop key on the selected entry (Bruno, 2026-10-31: "give the player a warning and a chance to drop
        /// items"): the first press asks, the second (Interact) puts one on the floor at her feet. The room keeps it
        /// (FloorItem). Key items stay: she needs them.
        /// </summary>
        public string Drop()
        {
            if (entries.Count == 0) return "none";
            Entry e = entries[Selected];
            ItemDefinition item = e.Drug.HasValue ? Resources.Load<ItemDefinition>("Items/item_" + e.Drug.Value.ToString().ToLowerInvariant()) : e.Item;
            if (item == null || (!e.Drug.HasValue && item.IsCritical))
            {
                wordsText.text = "She needs it. It takes no slot.";
                return "keep:" + (item != null ? item.itemId : "none");
            }
            if (!askingDrop)
            {
                askingDrop = true;
                wordsText.text = $"Drop the {ItemDefinition.Spoken(e.Name)}? It stays on the floor here.";
                keysText.text = $"{Key(InputManager.GameAction.Interact)}  yes      {Key(InputManager.GameAction.Back)}  no";
                return "ask_drop:" + item.itemId;
            }
            askingDrop = false;
            GameObject lucy = inventory.gameObject;
            bool gone = e.Drug.HasValue ? drugs != null && drugs.Drop(e.Drug.Value) : inventory.Remove(item);
            if (!gone) return "none";
            FloorItem.Drop(lucy, item, 0);
            GlorifiedBody.DebugTools.RunTelemetry.Event("item_dropped", item.itemId);
            Rebuild();
            Select(Mathf.Min(Selected, Mathf.Max(0, entries.Count - 1)));
            wordsText.text = "On the floor.";
            return "dropped:" + item.itemId;
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

        /// <summary>The entries, category by category: the drugs and the carried objects, the keys, the logs.</summary>
        private void Rebuild()
        {
            entries.Clear();
            if (drugs != null)
            {
                foreach (DrugType type in DrugOrder)
                {
                    int count = drugs.Count(type);
                    if (count <= 0) continue;
                    ItemDefinition def = Resources.Load<ItemDefinition>("Items/item_" + type.ToString().ToLowerInvariant());
                    Entry e = FromItem(def, count);
                    e.Drug = type;
                    e.Category = Consumables;
                    if (def == null)
                    {
                        e.Name = type.ToString().ToUpperInvariant();
                        e.Words = "A drug.";
                        e.Colour = new Color(0.4f, 0.8f, 0.4f);
                    }
                    e.Label = $"{e.Name}  x{count}";
                    entries.Add(e);
                }
            }
            foreach (ItemDefinition item in inventory.Carried) entries.Add(FromItem(item, 1));
            for (int category = Keys; category <= Logs; category++)
                foreach (ItemDefinition item in inventory.Critical)
                    if (CategoryOf(item) == category) entries.Add(FromItem(item, 1));
            slotsText.text = $"SLOTS {inventory.UsedSlots} / {inventory.Capacity}      KEY ITEMS {inventory.Critical.Count} (no slot)";
            Draw();
        }

        private static int CategoryOf(ItemDefinition item)
        {
            if (item == null) return Consumables;
            return item.kind == ItemKind.Log || item.kind == ItemKind.AudioLog ? Logs : item.IsCritical ? Keys : Consumables;
        }

        /// <summary>The entries of one category, as indexes into the flat list.</summary>
        private List<int> InCategory(int category)
        {
            var list = new List<int>();
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Category == category) list.Add(i);
            return list;
        }

        /// <summary>Up or down inside the category of the selected entry. It wraps, and the list scrolls to keep it in view.</summary>
        private void Scroll(int step)
        {
            if (entries.Count == 0) return;
            List<int> list = InCategory(entries[Selected].Category);
            int at = list.IndexOf(Selected);
            Select(list[((at + step) % list.Count + list.Count) % list.Count]);
        }

        /// <summary>Left or right to the next category that holds anything, at the same row or its last row.</summary>
        private void ChangeCategory(int step)
        {
            if (entries.Count == 0) return;
            int category = entries[Selected].Category;
            int row = InCategory(category).IndexOf(Selected);
            for (int i = 1; i <= CategoryNames.Length; i++)
            {
                List<int> list = InCategory(((category + step * i) % CategoryNames.Length + CategoryNames.Length) % CategoryNames.Length);
                if (list.Count == 0) continue;
                Select(list[Mathf.Min(row, list.Count - 1)]);
                return;
            }
        }

        /// <summary>The three lists: each shows VisibleRows entries from its scroll, the selected one outlined.</summary>
        private void Draw()
        {
            for (int c = 0; c < CategoryNames.Length; c++)
            {
                List<int> list = InCategory(c);
                int at = entries.Count > 0 && Selected < entries.Count ? list.IndexOf(Selected) : -1;
                if (at >= 0)
                {
                    if (at < scrolls[c]) scrolls[c] = at;
                    if (at >= scrolls[c] + VisibleRows) scrolls[c] = at - VisibleRows + 1;
                }
                scrolls[c] = Mathf.Clamp(scrolls[c], 0, Mathf.Max(0, list.Count - VisibleRows));
                bool more = list.Count > VisibleRows;
                categoryHeaders[c].text = CategoryNames[c] + (more ? $"  {(scrolls[c] > 0 ? "^" : " ")}{(scrolls[c] + VisibleRows < list.Count ? "v" : " ")}" : "");
                categoryHeaders[c].color = at >= 0 ? new Color(1f, 0.95f, 0.6f) : new Color(0.7f, 0.75f, 0.75f);
                for (int r = 0; r < VisibleRows; r++)
                {
                    Image cell = cells[c, r];
                    int index = scrolls[c] + r < list.Count ? list[scrolls[c] + r] : -1;
                    bool empty = list.Count == 0 && r == 0;
                    cell.gameObject.SetActive(index >= 0 || empty);
                    Text label = cell.GetComponentInChildren<Text>();
                    Outline outline = cell.GetComponent<Outline>();
                    if (empty)
                    {
                        cell.color = new Color(0f, 0f, 0f, 0.2f);
                        label.text = "(nothing)";
                        label.color = Color.white;
                        outline.effectColor = new Color(0f, 0f, 0f, 0f);
                        continue;
                    }
                    if (index < 0) continue;
                    cell.color = entries[index].Colour * 0.8f + new Color(0f, 0f, 0f, 0.2f);
                    label.text = entries[index].Label;
                    Color c2 = entries[index].Colour;
                    label.color = c2.r * 0.3f + c2.g * 0.6f + c2.b * 0.1f > 0.65f ? new Color(0.05f, 0.05f, 0.06f) : Color.white; // dark words on a white capsule
                    label.GetComponent<Outline>().enabled = label.color == Color.white;
                    bool selected = index == Selected;
                    outline.effectColor = selected ? new Color(1f, 0.95f, 0.6f, 1f) : new Color(0f, 0f, 0f, 0.6f);
                    outline.effectDistance = selected ? new Vector2(4f, 4f) : new Vector2(1f, 1f);
                }
            }
        }

        /// <summary>The first row of a category on screen now (the tests).</summary>
        public int ScrollOf(int category) => scrolls[category];

        private static Entry FromItem(ItemDefinition item, int count)
        {
            if (item == null) return new Entry { Label = "?", Name = "?", Words = "", Colour = Color.grey, Count = count };
            string tag = item.kind == ItemKind.AudioLog ? "AUDIO LOG" : item.kind == ItemKind.Log ? "LOG" : item.IsCritical ? "KEY ITEM" : item.kind == ItemKind.Drug ? "CONSUMABLE" : "CARRIED";
            return new Entry
            {
                Label = item.ShownName,
                Name = item.ShownName,
                Words = $"{tag}. {item.ShownDescription}",
                Colour = item.colour,
                Item = item,
                Count = count,
                Category = CategoryOf(item)
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
                keysText.text = $"{Key(InputManager.GameAction.Inventory)}  close";
                Draw();
                return;
            }
            Selected = (index % entries.Count + entries.Count) % entries.Count;
            pendingDrug = -1;
            askingDrop = false;
            Draw();
            Entry e = entries[Selected];
            titleText.text = "Item...  " + e.Name;
            wordsText.text = e.Words;
            string action = e.Drug.HasValue ? "use" : e.Item != null && e.Item.kind == ItemKind.Log ? "read"
                : e.Item != null && e.Item.kind == ItemKind.AudioLog ? (TapePlaying(e.Item) ? "pause" : "play") : "look";
            string dropKey = e.Drug.HasValue || (e.Item != null && !e.Item.IsCritical) ? $"      {Key(InputManager.GameAction.Drop)}  drop" : "";
            keysText.text = $"{Key(InputManager.GameAction.Forward)} {Key(InputManager.GameAction.Back)}  scroll      {Key(InputManager.GameAction.TurnLeft)} {Key(InputManager.GameAction.TurnRight)}  category      {Key(InputManager.GameAction.Interact)}  {action}{dropKey}      {Key(InputManager.GameAction.Inventory)}  close";
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
                wordsText.text = why == "unknown" ? "She does not know what it does." : why == "mk_full" ? "That MK bar is full." : why == "short_empty" ? "The SHORT bar is empty."
                    : why == "second_wind_ready" ? "The second wind is ready." : "It does nothing now.";
                return "refused:" + why;
            }
            if (e.Item != null && e.Item.kind == ItemKind.AudioLog)
            {
                // Play or pause the tape (Bruno, 2026-10-31: a fight pauses it, she plays it again here).
                GlorifiedBody.Audio.TapePlayer tapes = GlorifiedBody.Audio.TapePlayer.Instance;
                if (tapes != null && !tapes.IsPlaying && GlorifiedBody.Audio.TapePlayer.InFight)
                {
                    wordsText.text = "A fight. The tape waits until it ends.";
                    return "tape_wait:" + e.Item.itemId;
                }
                bool plays = tapes != null && tapes.Toggle(e.Item);
                Select(Selected);
                wordsText.text = plays ? "It plays." : "Paused.";
                return (plays ? "tape_play:" : "tape_pause:") + e.Item.itemId;
            }
            if (e.Item != null && e.Item.kind == ItemKind.Log)
            {
                readerTitle.text = e.Item.displayName;
                readerBody.text = e.Item.logText;
                readerPanel.SetActive(true);
                ItemDefinition.MarkRead(e.Item);
                OnLogRead?.Invoke(e.Item);
                return "read:" + e.Item.itemId;
            }
            wordsText.text = e.Item != null && e.Item.kind == ItemKind.Key ? "Use it at the door it opens: face the door and press " + Key(InputManager.GameAction.Interact) + "."
                : e.Item != null && e.Item.kind == ItemKind.Map ? "The whole floor shows on the map. Press " + Key(InputManager.GameAction.Map) + " to see it."
                : e.Words;
            return "look";
        }

        private static bool TapePlaying(ItemDefinition item) =>
            GlorifiedBody.Audio.TapePlayer.Instance != null && GlorifiedBody.Audio.TapePlayer.Instance.IsPlaying && GlorifiedBody.Audio.TapePlayer.Instance.Tape == item;

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

            // Left: the title, and under it the three lists.
            GameObject titleBox = Panel(root.transform, "TitleBox", new Vector2(0.5f, 0f), new Vector2(-330f, 450f), new Vector2(1000f, 70f), new Color(0.08f, 0.1f, 0.12f, 0.92f));
            titleBox.AddComponent<Outline>().effectColor = new Color(0.85f, 0.88f, 0.9f, 0.9f);
            titleText = Label(titleBox.transform, "Title", 32, FontStyle.Bold, new Color(0.7f, 0.95f, 0.95f), TextAnchor.MiddleLeft, new Vector2(960f, 60f));
            GameObject rowBox = Panel(root.transform, "RowBox", new Vector2(0.5f, 0f), new Vector2(-330f, 240f), new Vector2(1000f, 340f), new Color(0.08f, 0.1f, 0.12f, 0.92f));
            rowBox.AddComponent<Outline>().effectColor = new Color(0.85f, 0.88f, 0.9f, 0.9f);
            for (int c = 0; c < CategoryNames.Length; c++)
            {
                float x = -330f + c * 330f;
                categoryHeaders[c] = Label(rowBox.transform, "Header_" + CategoryNames[c], 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, new Vector2(310f, 34f));
                categoryHeaders[c].rectTransform.anchoredPosition = new Vector2(x, 142f);
                for (int r = 0; r < VisibleRows; r++)
                {
                    GameObject cell = Panel(rowBox.transform, $"Cell_{c}_{r}", new Vector2(0.5f, 0.5f), new Vector2(x, 100f - r * 46f), new Vector2(310f, 40f), Color.grey);
                    cell.AddComponent<Outline>();
                    Text label = Label(cell.transform, "Label", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, new Vector2(300f, 36f));
                    label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                    cells[c, r] = cell.GetComponent<Image>();
                }
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
            slotsText.rectTransform.anchoredPosition = new Vector2(-330f, 510f);

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
