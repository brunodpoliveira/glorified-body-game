using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The voice of one person (rounds 133 and 134): its enemy (the barks.csv class: guard, soldier, armour, labworker,
    /// nurse, patient) and its personality, kept for the rest of the night. Two changes only: a guard turns broken when he
    /// panics (SoldierBrain.Retreat), and the staff (nurse, lab worker) turn afraid when Lucy kills or hurts someone in
    /// their sight (EnemyMorale). EnemySpawn gives each figure its voice at the spawn; a figure with none gets it at its
    /// first line. Each situation is a deck: the "plays first" row first, then the others at random, no repeat until all
    /// played. A row with a PLACE plays only in a room that has the thing.
    /// The spawn weights and the share of confused staff are PROPOSED (GAME_NOTES.md): fewer confused staff as the demo goes
    /// on, because the truth about her spreads among the survivors.
    /// </summary>
    public sealed class BarkVoice : MonoBehaviour
    {
        public const string Broken = "broken", Afraid = "afraid", Confused = "confused", Bargaining = "bargaining";

        [SerializeField] private string enemy = "";
        [SerializeField] private string personality = "";
        private bool armed;
        private bool ready;
        private readonly Dictionary<string, List<BarkRow>> decks = new Dictionary<string, List<BarkRow>>();
        private readonly HashSet<string> firstDone = new HashSet<string>();
        private readonly Dictionary<string, string> lastLine = new Dictionary<string, string>();
        private readonly Dictionary<string, float> nextAt = new Dictionary<string, float>();

        public string Enemy { get { Ready(); return enemy; } }
        public string Personality { get { Ready(); return personality; } }
        /// <summary>It holds a gun or a knife: the "plays first" rows of the cower are the drop of the weapon.</summary>
        public bool Armed { get { Ready(); return armed; } }
        public bool IsStaff => Enemy == "nurse" || Enemy == "labworker";

        /// <summary>The voice of the person (on the root of its brain). Null for a thing that is not a person.</summary>
        public static BarkVoice Of(GameObject who)
        {
            if (who == null) return null;
            BarkVoice voice = who.GetComponentInParent<BarkVoice>();
            if (voice != null) return voice;
            // No ?? on Unity objects: a missing component can be a fake null in the editor.
            Component brain = who.GetComponentInParent<SoldierBrain>();
            if (brain == null) brain = who.GetComponentInParent<CivilianBrain>();
            if (brain == null) brain = who.GetComponentInParent<PatientBrain>();
            if (brain == null) return null;
            voice = brain.gameObject.AddComponent<BarkVoice>();
            voice.Ready();
            return voice;
        }

        /// <summary>Sets the voice by hand (a test, a figure with no brain).</summary>
        public void Init(string enemyClass, string withPersonality, bool isArmed)
        {
            ready = true;
            enemy = enemyClass;
            personality = withPersonality;
            armed = isArmed;
            decks.Clear();
            firstDone.Clear();
        }

        /// <summary>A test or a scripted beat sets the personality.</summary>
        public void SetPersonality(string value)
        {
            Ready();
            if (personality == value) return;
            RunTelemetry.Event("bark_personality", $"{name}:{personality}->{value}");
            personality = value;
            decks.Clear();
        }

        /// <summary>A guard panics: broken for the rest of the night (round 134).</summary>
        public void Panic()
        {
            if (Enemy == "guard") SetPersonality(Broken);
        }

        /// <summary>The staff saw her kill or hurt someone: afraid for the rest of the night (round 134).</summary>
        public void Frighten()
        {
            if (IsStaff && Personality != Afraid) SetPersonality(Afraid);
        }

        private void Ready()
        {
            if (ready) return;
            ready = true;
            SoldierBrain soldier = GetComponent<SoldierBrain>();
            CivilianBrain civilian = GetComponent<CivilianBrain>();
            EnemyMorale morale = GetComponent<EnemyMorale>();
            if (string.IsNullOrEmpty(enemy))
            {
                if (GetComponent<PatientBrain>() != null) enemy = "patient";
                else if (civilian != null) enemy = civilian.IsNurse ? "nurse" : "labworker";
                else if (soldier != null)
                {
                    EnemyMorale.Rank rank = morale != null ? morale.RankOf : EnemyMorale.Rank.Soldier;
                    enemy = rank == EnemyMorale.Rank.Guard ? "guard" : rank == EnemyMorale.Rank.Heavy ? "armour"
                        : rank == EnemyMorale.Rank.ArmedCivilian ? "labworker" : "soldier";
                }
            }
            armed = soldier != null;
            if (string.IsNullOrEmpty(personality)) personality = Pick(enemy, transform.position);
        }

        // ── The personality at the spawn (PROPOSED weights) ───────────

        public static string Pick(string enemy, Vector3 at)
        {
            float r = Random.value;
            switch (enemy)
            {
                case "guard": return r < 0.4f ? "professional" : r < 0.7f ? "overconfident" : "sarcastic";
                case "soldier": return r < 0.5f ? "professional" : r < 0.8f ? "callous" : "sadistic";
                case "armour": return r < 1f / 3f ? "professional" : r < 2f / 3f ? "confident" : "cocky";
                case "nurse":
                case "labworker":
                    (float confused, float bargaining) = StaffShares(at);
                    return r < confused ? Confused : r < confused + bargaining ? Bargaining : Afraid;
                default: return "";
            }
        }

        /// <summary>
        /// The share of confused and bargaining staff at the spawn (GAME_NOTES.md, PROPOSED): 60 % confused in the Hold and on
        /// the ward, 30 % on the ground floor, 10 % after she was in the Dining Hall. Bargaining only in the Hold and on the ward,
        /// 20 % (PROPOSED). A scene with no rooms (a test room): the ward's shares.
        /// </summary>
        public static (float confused, float bargaining) StaffShares(Vector3 at)
        {
            if (GlorifiedBody.Rooms.RoomTracker.WasIn(DiningHall)) return (0.1f, 0f);
            GlorifiedBody.Rooms.Room room = GlorifiedBody.Rooms.Room.Any ? GlorifiedBody.Rooms.Room.At(at) : null;
            if (room != null && room.floor == GroundFloor) return (0.3f, 0f);
            return (0.6f, 0.2f);
        }

        public const string DiningHall = "G18", GroundFloor = "demo_1";

        // ── The decks ─────────────────────────────────────────────────

        /// <summary>The next line of the situation, or null (a pair the code does not ask for, or no row).</summary>
        public string Next(string situation)
        {
            Ready();
            if (!BarkBook.IsAsked(enemy, situation)) return null;
            return NextOf(enemy, personality, situation);
        }

        /// <summary>The next line of any enemy and situation for this person (mk_edgar's groups).</summary>
        public string NextOf(string group, string withPersonality, string situation)
        {
            Ready();
            string key = group + "|" + withPersonality + "|" + situation;
            List<BarkRow> all = BarkBook.RowsFor(group, withPersonality, situation);
            if (all.Count == 0) return null;
            List<BarkRow> fit = all.FindAll(Fits);
            if (fit.Count == 0) return null;
            if (!firstDone.Contains(key))
            {
                firstDone.Add(key);
                BarkRow first = armed ? fit.Find(r => r.first) : null;
                if (first != null) return Remember(key, first.line);
            }
            if (!decks.TryGetValue(key, out List<BarkRow> deck) || deck.Count == 0)
            {
                deck = fit.FindAll(r => !r.first);
                if (deck.Count == 0) deck = new List<BarkRow>(fit);
                decks[key] = deck;
            }
            int pick = Random.Range(0, deck.Count);
            if (deck.Count > 1 && lastLine.TryGetValue(key, out string last) && deck[pick].line == last) pick = (pick + 1) % deck.Count;
            BarkRow row = deck[pick];
            deck.RemoveAt(pick);
            return Remember(key, row.line);
        }

        private string Remember(string key, string line)
        {
            lastLine[key] = line;
            return line;
        }

        /// <summary>A row with no PLACE fits anywhere; a row with one fits in a room that has the thing.</summary>
        private bool Fits(BarkRow row)
        {
            if (row.places.Length == 0) return true;
            if (!GlorifiedBody.Rooms.Room.Any) return false;
            GlorifiedBody.Rooms.Room room = GlorifiedBody.Rooms.Room.At(transform.position);
            if (room == null) return false;
            string things = ThingsOf(room);
            foreach (string place in row.places) if (things.Contains(place)) return true;
            return false;
        }

        private static readonly Dictionary<GlorifiedBody.Rooms.Room, string> roomThings = new Dictionary<GlorifiedBody.Rooms.Room, string>();

        /// <summary>The room's title and the names of the objects in it, in the Simple form, once for each room.</summary>
        private static string ThingsOf(GlorifiedBody.Rooms.Room room)
        {
            if (roomThings.TryGetValue(room, out string things)) return things;
            var sb = new System.Text.StringBuilder(BarkBook.Simple(room.title)).Append('|');
            foreach (Transform t in room.GetComponentsInChildren<Transform>(true)) sb.Append(BarkBook.Simple(t.name)).Append('|');
            things = sb.ToString();
            roomThings[room] = things;
            return things;
        }

        /// <summary>Says a line of the situation, but not again within the gap (a hit, a shot), and only with the chance.</summary>
        public bool SayNow(string situation, float gap, float chance = 1f, float seconds = 1.6f)
        {
            if (nextAt.TryGetValue(situation, out float at) && Time.time < at) return false;
            nextAt[situation] = Time.time + gap;
            if (Random.value > chance) return false;
            return BarkBook.Say(gameObject, situation, seconds);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => roomThings.Clear();
    }
}
