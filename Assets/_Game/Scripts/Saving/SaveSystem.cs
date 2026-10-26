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

        /// <summary>The game as it is now, with her at the spot.</summary>
        public static SaveData Capture(string spot)
        {
            GameObject lucy = GameObject.FindWithTag("Player");
            if (lucy == null) return null;
            Scene scene = SceneManager.GetActiveScene();
            var data = new SaveData
            {
                scene = scene.name,
                spot = spot ?? "",
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                playSeconds = PlaySeconds,
                ledger = RoomDamageLedger.Export(),
                flags = StoryFlags.All.ToList(),
            };
            PlayerState p = data.player;
            Health health = lucy.GetComponentInChildren<Health>();
            if (health != null) { p.health = health.CurrentHealth; p.maxHealth = health.MaxHealth; }
            PsychicStats stats = lucy.GetComponentInChildren<PsychicStats>();
            if (stats != null)
            {
                p.nalcon = stats.Nalcon;
                p.shortBar = stats.Short;
                p.kills = stats.Kills;
                p.exp = stats.Exp;
                p.outfit = stats.Outfit != null ? stats.Outfit.outfitId : "";
            }
            ItemInventory items = lucy.GetComponentInChildren<ItemInventory>();
            if (items != null)
            {
                foreach (ItemDefinition item in items.Critical) if (item != null) p.critical.Add(item.itemId);
                foreach (ItemDefinition item in items.Carried) if (item != null) p.carried.Add(item.itemId);
            }
            DrugInventory drugs = lucy.GetComponentInChildren<DrugInventory>();
            if (drugs != null) for (int i = 0; i < 3; i++) p.drugs.Add(drugs.Count((DrugType)i));

            RoomState room = data.room;
            room.scene = scene.name;
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
            foreach (ItemPickup pickup in UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None))
                if (pickup.gameObject.scene == scene) presentItems.Add(ScenePaths.Of(pickup.transform));
            foreach (string path in baselineItems)
                if (!presentItems.Contains(path)) room.taken.Add(path);
            foreach (LockedDoor door in UnityEngine.Object.FindObjectsByType<LockedDoor>(FindObjectsSortMode.None))
                if (door.gameObject.scene == scene && door.IsOpen) room.opened.Add(ScenePaths.Of(door.transform));
            return data;
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
            RoomDamageLedger.Import(data.ledger);
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
            // The bodies first: each quiet death counts a kill, and her saved numbers below overwrite that.
            HashSet<string> bodies = RoomGore.Restore(scene, data.room);
            foreach (string path in data.room.dead)
            {
                if (bodies.Contains(path)) continue;
                GameObject figure = ScenePaths.Find(scene, path);
                if (figure == null) continue;
                figure.SetActive(false); // a dead one with no saved body (an older file, a destroyed figure) stays gone
                goneByLoad.Add(path);
            }
            GameObject lucy = GameObject.FindWithTag("Player");
            if (lucy != null)
            {
                Health health = lucy.GetComponentInChildren<Health>();
                PsychicStats stats = lucy.GetComponentInChildren<PsychicStats>();
                ItemInventory items = lucy.GetComponentInChildren<ItemInventory>();
                DrugInventory drugs = lucy.GetComponentInChildren<DrugInventory>();
                PlayerState p = data.player;
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
                if (stats != null) stats.Restore(p.nalcon, p.shortBar, p.kills, p.exp);
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
                if (drugs != null) for (int i = 0; i < 3 && i < p.drugs.Count; i++) drugs.DebugSet((DrugType)i, p.drugs[i]);
                PlaceAt(lucy, data.spot);
            }
            foreach (string path in data.room.taken)
            {
                GameObject item = ScenePaths.Find(scene, path);
                if (item == null) continue;
                item.SetActive(false);
                UnityEngine.Object.Destroy(item);
            }
            foreach (string path in data.room.opened)
            {
                GameObject door = ScenePaths.Find(scene, path);
                if (door != null && door.TryGetComponent(out LockedDoor locked)) locked.Open(instant: true);
            }
            LastApplied = data;
            Applies++;
            RunTelemetry.Event("load_applied", $"{data.scene}: {data.room.dead.Count} dead ({bodies.Count} bodies), {data.room.stains.Count} stains, {data.room.limbs.Count} limbs, {data.room.taken.Count} taken, {data.room.opened.Count} open");
        }

        private static void PlaceAt(GameObject lucy, string spot)
        {
            if (string.IsNullOrEmpty(spot)) return;
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
                return;
            }
            Baseline(scene);
            if (pending != null && pending.scene == scene.name)
            {
                SaveData data = pending;
                pending = null;
                EnsureRunner();
                runner.StartCoroutine(ApplyNextFrame(data));
            }
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
            HasSessionCheckpoint = false;
            PlaySeconds = 0f;
            Loads = 0;
            Saves = 0;
            Applies = 0;
            LastApplied = null;
            OnToast = null;
            baselineFigures.Clear();
            baselineItems.Clear();
            goneByLoad.Clear();
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
