using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Saving
{
    /// <summary>
    /// The game across its rooms (the design sweep, 2026-10-26, rank 1: "travel from room to room, and the world state
    /// of every room"). A room is a scene, and Lucy is an object of each scene, so a crossing carries her over: the
    /// exit takes her state (bars, items, pills, exp, kills, outfit) and the state of the room she leaves (the dead
    /// and their bodies and blood, the taken items, the open doors), the next room loads, and SaveSystem puts the room
    /// back as she left it (if she was there before) and her at the entry. Broken cover keeps its own ledger
    /// (RoomDamageLedger). A save keeps every room she left; the main menu clears them (a new game).
    /// </summary>
    public static class WorldState
    {
        public const float FadeOutSeconds = 0.25f;
        public const float FadeInSeconds = 0.3f;

        private static readonly Dictionary<string, RoomState> rooms = new Dictionary<string, RoomState>();
        private static PlayerState carried;
        private static string arrivalScene;
        private static string arrivalEntry;

        public static bool Travelling { get; private set; }
        public static int Crossings { get; private set; }
        public static IReadOnlyDictionary<string, RoomState> Rooms => rooms;
        public static bool Visited(string scene) => rooms.ContainsKey(scene);

        /// <summary>Leaves this room for the scene, to come in at the entry. False when the scene is not in the build.</summary>
        public static bool Travel(string scene, string entry)
        {
            if (Travelling || string.IsNullOrEmpty(scene)) return false;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogWarning($"[WorldState] the room '{scene}' is not in the build.");
                return false;
            }
            GameObject lucy = GameObject.FindWithTag("Player");
            Scene here = SceneManager.GetActiveScene();
            rooms[here.name] = SaveSystem.CaptureRoom(here);
            carried = lucy != null ? SaveSystem.CapturePlayer(lucy) : null;
            arrivalScene = scene;
            arrivalEntry = entry ?? "";
            Travelling = true;
            Crossings++;
            RunTelemetry.Event("travel", $"{here.name} -> {scene}:{entry}");
            ScreenFade.Out(FadeOutSeconds, () =>
            {
                if (GameManager.Instance != null) GameManager.Instance.LoadLevel(scene);
                else SceneManager.LoadScene(scene);
            });
            return true;
        }

        /// <summary>The crossing that ends in this scene, once: her state, the room as she left it (null: first visit), the entry.</summary>
        internal static bool TakeArrival(string scene, out PlayerState player, out RoomState room, out string entry)
        {
            player = null;
            room = null;
            entry = null;
            if (!Travelling || arrivalScene != scene) return false;
            player = carried;
            rooms.TryGetValue(scene, out room);
            entry = arrivalEntry;
            carried = null;
            arrivalScene = null;
            return true;
        }

        /// <summary>The crossing is done (her place set): the screen comes back.</summary>
        internal static void Arrived()
        {
            Travelling = false;
            ScreenFade.In(FadeInSeconds);
        }

        /// <summary>The rooms she left, for a save (not the current one: the save holds it apart).</summary>
        internal static List<RoomState> Export(string currentScene)
        {
            var list = new List<RoomState>();
            foreach (KeyValuePair<string, RoomState> pair in rooms)
                if (pair.Key != currentScene) list.Add(pair.Value);
            return list;
        }

        internal static void Import(List<RoomState> saved)
        {
            rooms.Clear();
            if (saved == null) return;
            foreach (RoomState room in saved)
                if (room != null && !string.IsNullOrEmpty(room.scene)) rooms[room.scene] = room;
        }

        /// <summary>A new game: no room visited, no crossing on its way.</summary>
        internal static void Clear()
        {
            rooms.Clear();
            carried = null;
            arrivalScene = null;
            arrivalEntry = null;
            if (Travelling) ScreenFade.In(0f);
            Travelling = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            rooms.Clear();
            carried = null;
            arrivalScene = null;
            arrivalEntry = null;
            Travelling = false;
            Crossings = 0;
        }
    }
}
