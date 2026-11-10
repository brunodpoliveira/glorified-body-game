using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Destruction;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GlorifiedBody.Saving
{
    /// <summary>
    /// Saves, loads and checkpoints (Bruno, 2026-10-25: "only in save rooms"; checkpoints "only at specific save
    /// points"; "3 manual save slots, one for the auto checkpoints"). Slots 1 to 3 are hers, written at a SavePoint.
    /// Slot 0 is the checkpoint, written at a CheckpointPoint, and by a manual save and by a load too, so a death
    /// always restarts from the last place this game saved (PROPOSED). A death restarts from the checkpoint only when
    /// this game wrote or loaded it: a new game from the menu ignores an old one. The files are JSON in
    /// persistentDataPath/saves (tests and bots set Folder to their own place).
    ///
    /// A load: the file's breakage goes into RoomDamageLedger, the scene loads, and one frame after it (every Awake and
    /// Start done) Apply puts her at the spot with her bars and items, and the room as it was: the bodies where they
    /// lay with their gore, the blood on the floor (RoomGore), the taken items gone, the doors open.
    /// </summary>
    public static class SaveSystem
    {
        public const int ManualSlots = 3;
        public const int CheckpointSlot = 0;
        public const string MenuScene = "Main_Menu";

        private static string folder;
        private static SaveData pending;
        private static Runner runner;
        // The room as the scene built it (paths), so a save knows what is gone since.
        private static readonly HashSet<string> baselineFigures = new HashSet<string>();
        private static readonly HashSet<string> baselineItems = new HashSet<string>();
        // What a load took away: it counts as dead / taken at the next save.
        private static readonly HashSet<string> goneByLoad = new HashSet<string>();
        // The story flags as she came into the room: a restart of the room puts them back (Bruno, 2026-10-30: the
        // ward's red mark stayed up after a restart).
        private static readonly List<string> entryFlags = new List<string>();
        // She came into this room through a door: her state then, the room as she found it, the door. A RETRY with no
        // checkpoint gives these back (seam sweep, 2026-10-31: she came back blank, her items lost for good).
        private static PlayerState entryPlayer;
        private static RoomState entryRoom;
        private static string entryDoor;
        private static string currentRoom;
        // The rooms she was in when she came into this room (the map of a RETRY: the world scene, 2026-11-02).
        private static readonly List<string> entryVisited = new List<string>();

        public static string Folder
        {
            // Batch runs (tests, bots) never touch the player's own saves.
            get => folder ??= Path.Combine(Application.persistentDataPath, Application.isBatchMode ? "saves_batch" : "saves");
            set => folder = value;
        }

        /// <summary>This game wrote or loaded the checkpoint: a death restarts there.</summary>
        public static bool HasSessionCheckpoint { get; private set; }
        public static float PlaySeconds { get; set; }
        public static int Loads { get; private set; }
        public static int Saves { get; private set; }
        /// <summary>Loads applied to a room so far (tests wait for it).</summary>
        public static int Applies { get; private set; }
        /// <summary>Room crossings ended in a room so far (tests wait for it).</summary>
        public static int Arrivals { get; private set; }
        /// <summary>The last load's state applied to the room (tests).</summary>
        public static SaveData LastApplied { get; private set; }
        public static event Action<string> OnToast;

        public static string PathOf(int slot) => Path.Combine(Folder, slot == CheckpointSlot ? "checkpoint.json" : $"slot_{slot}.json");
        public static bool Exists(int slot) => File.Exists(PathOf(slot));

        /// <summary>The file of the slot, or null when it is missing, broken or of another version.</summary>
        public static SaveData Read(int slot)
        {
            try
            {
                if (!Exists(slot)) return null;
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathOf(slot)));
                return data != null && data.version == SaveData.CurrentVersion && !string.IsNullOrEmpty(data.scene) ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] slot {slot} unreadable: {e.Message}");
                return null;
            }
        }

        public static void Write(int slot, SaveData data)
        {
            Directory.CreateDirectory(Folder);
            string path = PathOf(slot);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path); // a crash mid-write leaves the old file whole
        }

        public static void Delete(int slot)
        {
            if (Exists(slot)) File.Delete(PathOf(slot));
        }

        /// <summary>The slot with the newest file (Continue), or -1.</summary>
        public static int Newest()
        {
            int best = -1;
            DateTime newest = DateTime.MinValue;
            for (int slot = 0; slot <= ManualSlots; slot++)
            {
                if (Read(slot) == null) continue;
                DateTime at = File.GetLastWriteTimeUtc(PathOf(slot));
                if (at > newest) { newest = at; best = slot; }
            }
            return best;
        }

        // ── Save ──────────────────────────────────────────────────────

        /// <summary>A save at a save point into slot 1 to 3; it is the checkpoint too.</summary>
        public static bool SaveManual(int slot, string spot)
        {
            if (slot < 1 || slot > ManualSlots) return false;
            SaveData data = Capture(spot);
            if (data == null) return false;
            Write(slot, data);
            Write(CheckpointSlot, data);
            HasSessionCheckpoint = true;
            Saves++;
            RunTelemetry.Event("save", $"slot {slot} at {spot}");
            GlorifiedBody.Audio.Sfx.Ui(GlorifiedBody.Audio.Sfx.Save, 0.8f);
            OnToast?.Invoke($"SAVED IN SLOT {slot}");
            return true;
        }

        /// <summary>A checkpoint point: the checkpoint slot only.</summary>
        public static bool SaveCheckpoint(string spot)
        {
            SaveData data = Capture(spot);
            if (data == null) return false;
            Write(CheckpointSlot, data);
            HasSessionCheckpoint = true;
            Saves++;
            RunTelemetry.Event("checkpoint", spot);
            GlorifiedBody.Audio.Sfx.Ui(GlorifiedBody.Audio.Sfx.Checkpoint, 0.7f);
            OnToast?.Invoke("CHECKPOINT");
            return true;
        }

        /// <summary>The game as it is now, with her at the spot: her, this room, and the rooms she left (WorldState).</summary>
        public static SaveData Capture(string spot)
        {
            GameObject lucy = GameObject.FindWithTag("Player");
            if (lucy == null) return null;
            Scene scene = SceneManager.GetActiveScene();
            return new SaveData
            {
                scene = scene.name,
                spot = spot ?? "",
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                playSeconds = PlaySeconds,
                ledger = RoomDamageLedger.Export(),
                flags = StoryFlags.All.ToList(),
                player = CapturePlayer(lucy),
                room = CaptureRoom(scene),
                rooms = WorldState.Export(scene.name),
                visited = GlorifiedBody.Rooms.RoomTracker.Visited.ToList(),
            };
        }

        /// <summary>Her state: the bars, the exp, the outfit, the items and the pills (a save, a room crossing).</summary>
        public static PlayerState CapturePlayer(GameObject lucy)
        {
            var p = new PlayerState();
            Health health = lucy.GetComponentInChildren<Health>();
            if (health != null) { p.health = health.CurrentHealth; p.maxHealth = health.MaxHealth; }
            PsychicStats stats = lucy.GetComponentInChildren<PsychicStats>();
            if (stats != null)
            {
                p.nalcon = stats.Nalcon;
                foreach (PowerKind kind in (PowerKind[])System.Enum.GetValues(typeof(PowerKind))) p.mk.Add(stats.Mk(kind));
                p.shortBar = stats.Short;
                p.kills = stats.Kills;
                p.exp = stats.Exp;
                p.people = stats.People;
                p.outfit = stats.Outfit != null ? stats.Outfit.outfitId : "";
            }
            ShortController running = lucy.GetComponentInChildren<ShortController>();
            if (running != null && running.IsActive) p.shortLeft = Mathf.Max(0.1f, running.Remaining);
            ItemInventory items = lucy.GetComponentInChildren<ItemInventory>();
            if (items != null)
            {
                foreach (ItemDefinition item in items.Critical) if (item != null) p.critical.Add(item.itemId);
                foreach (ItemDefinition item in items.Carried) if (item != null) p.carried.Add(item.itemId);
            }
            DrugInventory drugs = lucy.GetComponentInChildren<DrugInventory>();
            if (drugs != null) for (int i = 0; i < System.Enum.GetValues(typeof(DrugType)).Length; i++) p.drugs.Add(drugs.Count((DrugType)i));
            return p;
        }

        /// <summary>The room as it is: the dead (with their bodies and blood), the taken items, the open doors.</summary>
        public static RoomState CaptureRoom(Scene scene)
        {
            var room = new RoomState { scene = scene.name };
            var aliveFigures = new HashSet<string>();
            var deadBodies = new List<Health>();
            foreach (Health h in UnityEngine.Object.FindObjectsByType<Health>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (h.gameObject.scene != scene || IsHers(h)) continue;
                string path = ScenePaths.Of(h.transform);
                if (h.IsDead || goneByLoad.Contains(path)) room.dead.Add(path);
                else aliveFigures.Add(path);
                if (h.IsDead) deadBodies.Add(h);
            }
            RoomGore.Capture(scene, room, deadBodies);
            foreach (string path in baselineFigures)
                if (!aliveFigures.Contains(path) && !room.dead.Contains(path)) room.dead.Add(path); // destroyed since
            var presentItems = new HashSet<string>();
            // A hidden item counts too (a carrier's drop waits inactive until the carrier dies, ItemCarrier).
            foreach (ItemPickup pickup in UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (pickup.gameObject.scene == scene && !pickup.Taken) presentItems.Add(ScenePaths.Of(pickup.transform));
            foreach (string path in baselineItems)
                if (!presentItems.Contains(path)) room.taken.Add(path);
            foreach (LockedDoor door in UnityEngine.Object.FindObjectsByType<LockedDoor>(FindObjectsSortMode.None))
                if (door.gameObject.scene == scene && door.IsOpen) room.opened.Add(ScenePaths.Of(door.transform));
            foreach (FireDoor door in UnityEngine.Object.FindObjectsByType<FireDoor>(FindObjectsSortMode.None))
                if (door.gameObject.scene == scene && door.Current == FireDoor.State.Jammed) room.jammed.Add(ScenePaths.Of(door.transform));
            // What came in play stays too (Bruno, 2026-10-31: "fix both"): the items on the floor, the shield's scars.
            foreach (FloorItem item in UnityEngine.Object.FindObjectsByType<FloorItem>(FindObjectsSortMode.None))
            {
                ItemPickup pickup = item.GetComponent<ItemPickup>();
                if (item.gameObject.scene != scene || pickup == null || pickup.Taken) continue;
                room.floorItems.Add(new FloorItemState { itemId = item.itemId, position = item.transform.position, yaw = item.transform.eulerAngles.y });
            }
            foreach (GlorifiedBody.Combat.FloorScar scar in UnityEngine.Object.FindObjectsByType<GlorifiedBody.Combat.FloorScar>(FindObjectsSortMode.None))
                if (scar.gameObject.scene == scene) room.scars.Add(new ScarState { position = scar.transform.position, rotation = scar.transform.rotation });
            foreach (MovableProp prop in UnityEngine.Object.FindObjectsByType<MovableProp>(FindObjectsSortMode.None))
                if (prop.gameObject.scene == scene) room.props.Add(new PropState { path = ScenePaths.Of(prop.transform), position = prop.transform.position, rotation = prop.transform.rotation });
            room.propsSaved = true;
            return room;
        }

        // ── Load ──────────────────────────────────────────────────────

        /// <summary>Loads the slot: the scene of the save, then her and the room as saved. False when the slot is empty.</summary>
        public static bool Load(int slot)
        {
            SaveData data = Read(slot);
            if (data == null || GameManager.Instance == null) return false;
            // A save of a room that is not in the build (a renamed room) must not replace the checkpoint or hang in
            // the load (found 2026-10-26).
            if (!Application.CanStreamedLevelBeLoaded(data.scene))
            {
                Debug.LogWarning($"[SaveSystem] slot {slot}: the room '{data.scene}' is not in the build.");
                return false;
            }
            if (slot != CheckpointSlot) Write(CheckpointSlot, data); // a death after a load restarts from the load
            pending = data;
            HasSessionCheckpoint = true;
            PlaySeconds = data.playSeconds;
            StoryFlags.Set(data.flags);
            GlorifiedBody.Rooms.RoomTracker.SetVisited(data.visited);
            RoomDamageLedger.Import(data.ledger);
            WorldState.Clear();
            WorldState.Import(data.rooms);
            Loads++;
            RunTelemetry.Event("load", $"slot {slot}: {data.scene} at {data.spot}");
            EnsureRunner();
            GameManager.Instance.LoadLevel(data.scene);
            return true;
        }

        /// <summary>The death screen's restart: the checkpoint of this game, if any.</summary>
        public static bool RestartFromCheckpoint() => HasSessionCheckpoint && Load(CheckpointSlot);

        private static void Apply(SaveData data)
        {
            Scene scene = SceneManager.GetActiveScene();
            // The room first: each quiet death counts a kill, and her saved numbers overwrite that.
            int bodies = ApplyRoom(scene, data.room);
            GameObject lucy = GameObject.FindWithTag("Player");
            if (lucy != null)
            {
                ApplyPlayer(lucy, data.player);
                PlaceAt(lucy, data.spot);
            }
            LastApplied = data;
            Applies++;
            RunTelemetry.Event("load_applied", $"{data.scene}: {data.room.dead.Count} dead ({bodies} bodies), {data.room.stains.Count} stains, {data.room.limbs.Count} limbs, {data.room.taken.Count} taken, {data.room.opened.Count} open, {data.rooms.Count} other rooms");
        }

        /// <summary>A room as it was: the bodies where they lay (RoomGore), the other dead gone, the taken items gone, the doors open. Returns the bodies.</summary>
        public static int ApplyRoom(Scene scene, RoomState room)
        {
            if (room == null) return 0;
            foreach (FloorItemState f in room.floorItems)
            {
                ItemDefinition def = ItemDefinition.Find(f.itemId);
                if (def != null) FloorItem.Place(def, f.position, f.yaw);
            }
            foreach (ScarState scar in room.scars) GlorifiedBody.Combat.FloorShield.ScarAt(scar.position, scar.rotation);
            if (room.propsSaved)
                foreach (MovableProp prop in UnityEngine.Object.FindObjectsByType<MovableProp>(FindObjectsSortMode.None))
                {
                    if (prop.gameObject.scene != scene) continue;
                    string path = ScenePaths.Of(prop.transform);
                    PropState saved = room.props.Find(p => p.path == path);
                    if (saved != null) prop.Place(saved.position, saved.rotation);
                    else UnityEngine.Object.Destroy(prop.gameObject); // it broke
                }
            HashSet<string> bodies = RoomGore.Restore(scene, room);
            foreach (string path in room.dead)
            {
                if (bodies.Contains(path)) continue;
                GameObject figure = ScenePaths.Find(scene, path);
                if (figure == null) continue;
                if (figure.TryGetComponent(out ItemCarrier carrier)) carrier.DropNow(); // the item stays in the room
                figure.SetActive(false); // a dead one with no saved body (an older file, a destroyed figure) stays gone
                goneByLoad.Add(path);
            }
            foreach (string path in room.taken)
            {
                GameObject item = ScenePaths.Find(scene, path);
                if (item == null) continue;
                if (item.TryGetComponent(out ItemPickup taken)) taken.MarkTaken();
                item.SetActive(false);
                UnityEngine.Object.Destroy(item);
            }
            foreach (string path in room.opened)
            {
                GameObject door = ScenePaths.Find(scene, path);
                if (door != null && door.TryGetComponent(out LockedDoor locked)) locked.Open(instant: true);
            }
            foreach (FireDoor door in UnityEngine.Object.FindObjectsByType<FireDoor>(FindObjectsSortMode.None))
            {
                if (door.gameObject.scene != scene) continue;
                bool jam = room.jammed != null && room.jammed.Contains(ScenePaths.Of(door.transform));
                if (jam) door.Jam("load", instant: true);
                else door.Open("load", instant: true);
            }
            return bodies.Count;
        }

        /// <summary>Her state on her: the outfit, the HP, the bars, the exp, the items, the pills.</summary>
        public static void ApplyPlayer(GameObject lucy, PlayerState p)
        {
            if (lucy == null || p == null) return;
            Health health = lucy.GetComponentInChildren<Health>();
            PsychicStats stats = lucy.GetComponentInChildren<PsychicStats>();
            ItemInventory items = lucy.GetComponentInChildren<ItemInventory>();
            DrugInventory drugs = lucy.GetComponentInChildren<DrugInventory>();
            if (stats != null && !string.IsNullOrEmpty(p.outfit))
            {
                OutfitProfile outfit = Resources.Load<OutfitProfile>("Outfits/outfit_" + p.outfit);
                if (outfit != null && stats.Outfit != outfit) stats.SetOutfit(outfit);
            }
            if (health != null && p.maxHealth > 0f)
            {
                health.SetMaxHealth(p.maxHealth, fillToMax: false);
                health.SetHealth(p.health);
            }
            if (stats != null) stats.Restore(p.nalcon, p.shortBar, p.kills, p.exp, p.people, p.mk != null && p.mk.Count > 0 ? p.mk : null);
            if (items != null)
            {
                items.Clear();
                foreach (string id in p.critical.Concat(p.carried))
                {
                    ItemDefinition item = ItemDefinition.Find(id);
                    if (item != null) items.TryAdd(item);
                    else Debug.LogWarning($"[SaveSystem] no item '{id}'");
                }
            }
            if (drugs != null) for (int i = 0; i < System.Enum.GetValues(typeof(DrugType)).Length; i++) drugs.DebugSet((DrugType)i, i < p.drugs.Count ? p.drugs[i] : 0);
            ShortController shortController = lucy.GetComponentInChildren<ShortController>();
            if (shortController != null && p.shortLeft > 0f) shortController.GoOn(p.shortLeft);
        }

        /// <summary>
        /// She walks into another room of the world scene (2026-11-02): the retry point moves here. Her state, the world
        /// as she found it, the flags, the rooms she was in and the place where she came in: a RETRY with no checkpoint
        /// gives these back, as the door of a room scene did before.
        /// </summary>
        private static void OnRoomChanged(GlorifiedBody.Rooms.Room from, GlorifiedBody.Rooms.Room to)
        {
            if (from == null || to == null) return; // the first room of a load: the load set the retry point
            GameObject lucy = GameObject.FindWithTag("Player");
            if (lucy == null) return;
            entryFlags.Clear();
            entryFlags.AddRange(StoryFlags.All);
            entryPlayer = CapturePlayer(lucy);
            entryRoom = CaptureRoom(SceneManager.GetActiveScene());
            Vector3 p = lucy.transform.position;
            entryDoor = PoseSpot(p, lucy.transform.eulerAngles.y);
            entryVisited.Clear();
            entryVisited.AddRange(GlorifiedBody.Rooms.RoomTracker.Visited);
            RunTelemetry.Event("retry_point", to.id);
        }

        /// <summary>A place as a spot id: "@x;y;z;yaw" (PlaceAt reads it).</summary>
        public static string PoseSpot(Vector3 p, float yaw) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "@{0:0.###};{1:0.###};{2:0.###};{3:0.#}", p.x, p.y, p.z, yaw);

        private static void PlaceAt(GameObject lucy, string spot)
        {
            if (string.IsNullOrEmpty(spot)) return;
            if (spot[0] == '@')
            {
                string[] v = spot.Substring(1).Split(';');
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                var at = new Vector3(float.Parse(v[0], ci), float.Parse(v[1], ci), float.Parse(v[2], ci));
                float yaw = float.Parse(v[3], ci);
                TankController her = lucy.GetComponent<TankController>();
                if (her != null) her.Teleport(at, yaw);
                else lucy.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
                return;
            }
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!(behaviour is ISpot marker) || marker.SpotId != spot) continue;
                marker.MarkReached();
                Pose pose = marker.SpawnPose;
                TankController tank = lucy.GetComponent<TankController>();
                if (tank != null) tank.Teleport(pose.position, pose.rotation.eulerAngles.y);
                else lucy.transform.SetPositionAndRotation(pose.position, pose.rotation);
                return;
            }
            Debug.LogWarning($"[SaveSystem] no spot '{spot}' in the room");
        }

        private static bool IsHers(Health h) => h.CompareTag("Player") || h.transform.root.CompareTag("Player");

        // ── The session ───────────────────────────────────────────────

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            goneByLoad.Clear();
            if (scene.name == MenuScene)
            {
                // The menu ends the game: a new one starts with no checkpoint and no play time.
                HasSessionCheckpoint = false;
                PlaySeconds = 0f;
                pending = null;
                StoryFlags.Set(null);
                WorldState.Clear();
                currentRoom = null;
                entryFlags.Clear();
                entryPlayer = null;
                entryVisited.Clear();
                GlorifiedBody.Rooms.RoomTracker.SetVisited(null);
                return;
            }
            // The world scene: a door between two rooms is a room change, not a load (2026-11-02).
            GlorifiedBody.Rooms.RoomTracker.OnChanged -= OnRoomChanged;
            GlorifiedBody.Rooms.RoomTracker.OnChanged += OnRoomChanged;
            Baseline(scene);
            bool restart = scene.name == currentRoom && pending == null && !WorldState.Travelling;
            currentRoom = scene.name;
            if (restart)
            {
                StoryFlags.Set(entryFlags); // the room comes back as she found it, the story too
                GlorifiedBody.Rooms.RoomTracker.SetVisited(entryVisited);
            }
            else
            {
                entryFlags.Clear();
                entryFlags.AddRange(StoryFlags.All);
                entryPlayer = null;
                entryVisited.Clear();
                entryVisited.AddRange(GlorifiedBody.Rooms.RoomTracker.Visited);
            }
            if (restart && entryPlayer != null)
            {
                EnsureRunner();
                runner.StartCoroutine(ArriveNextFrame(scene, entryPlayer, entryRoom, entryDoor, again: true));
                return;
            }
            if (pending != null && pending.scene == scene.name)
            {
                SaveData data = pending;
                pending = null;
                EnsureRunner();
                runner.StartCoroutine(ApplyNextFrame(data));
            }
            else if (WorldState.TakeArrival(scene.name, out PlayerState player, out RoomState room, out string entry))
            {
                EnsureRunner();
                runner.StartCoroutine(ArriveNextFrame(scene, player, room, entry));
            }
        }

        /// <summary>A room crossing ends: the room as she left it (when she was here before), her state, her place at the entry.</summary>
        private static IEnumerator ArriveNextFrame(Scene scene, PlayerState player, RoomState room, string entry, bool again = false)
        {
            yield return null; // every Awake and Start of the room has run
            if (!again)
            {
                entryPlayer = player;
                entryRoom = room;
                entryDoor = entry;
            }
            int bodies = ApplyRoom(scene, room);
            GameObject lucy = GameObject.FindWithTag("Player");
            ApplyPlayer(lucy, player);
            if (lucy != null) PlaceAt(lucy, entry);
            if (again)
            {
                RunTelemetry.Event("retry_at_door", $"{scene.name}:{entry}");
                yield break;
            }
            Arrivals++;
            RunTelemetry.Event("arrived", $"{scene.name}:{entry} {(room != null ? $"again ({room.dead.Count} dead, {bodies} bodies, {room.taken.Count} taken)" : "first visit")}");
            WorldState.Arrived();
        }

        private static IEnumerator ApplyNextFrame(SaveData data)
        {
            yield return null; // every Awake and Start of the room has run
            Apply(data);
        }

        private static void Baseline(Scene scene)
        {
            baselineFigures.Clear();
            baselineItems.Clear();
            foreach (Health h in UnityEngine.Object.FindObjectsByType<Health>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (h.gameObject.scene == scene && !IsHers(h)) baselineFigures.Add(ScenePaths.Of(h.transform));
            foreach (ItemPickup pickup in UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (pickup.gameObject.scene == scene) baselineItems.Add(ScenePaths.Of(pickup.transform));
        }

        private static void EnsureRunner()
        {
            if (runner != null) return;
            var obj = new GameObject("[SaveSystem]");
            UnityEngine.Object.DontDestroyOnLoad(obj);
            runner = obj.AddComponent<Runner>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            folder = null;
            pending = null;
            runner = null;
            entryPlayer = null;
            entryRoom = null;
            entryVisited.Clear();
            HasSessionCheckpoint = false;
            PlaySeconds = 0f;
            Loads = 0;
            Saves = 0;
            Applies = 0;
            Arrivals = 0;
            LastApplied = null;
            OnToast = null;
            baselineFigures.Clear();
            baselineItems.Clear();
            goneByLoad.Clear();
            entryFlags.Clear();
            currentRoom = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartRunner() => EnsureRunner();

        /// <summary>Runs the apply coroutine and counts the play time (time in the game, not in the menus).</summary>
        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                if (GameManager.Instance != null && GameManager.Instance.IsPlaying) PlaySeconds += Time.unscaledDeltaTime;
            }
        }
    }
}
