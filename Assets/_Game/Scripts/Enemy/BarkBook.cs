using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GlorifiedBody.Enemy
{
    /// <summary>The situations the code asks the bark book for. The names are the writer's (game/barks.csv, column situation).</summary>
    public static class BarkSituation
    {
        public const string SpotsLucy = "spots Lucy";
        public const string Fires = "fires";
        public const string Hit = "hit";
        public const string Shock = "shock";
        public const string PartnerDown = "partner down";
        public const string SeesFeed = "sees the feed";
        public const string Retreats = "retreats, fear at the limit";
        public const string Cower = "cower";
        public const string Begs = "begs";
        public const string BacksAway = "backs away slowly";
        public const string StartsCare = "starts the care";
        public const string GoesToBody = "goes to a body";
        public const string Drags = "drags a body";
        public const string Cpr = "CPR";
        public const string TalksToGhost = "talks to Ghost";
        public const string TooHeavy = "too heavy";
        public const string ArmourEnter = "armour_enter";
        public const string HearsLucy = "hears Lucy";
        // The overconfident guard (round 134). NOT ASKED YET: the baton behaviour waits for Bruno.
        public const string Baton = "approaches with the baton";
        public const string Chases = "chases her";
        public const string DrawsGun = "draws the gun";
        // The fire team (FireTeam, TeamReinforcement).
        public const string TeamContact = "team: contact";
        public const string TeamMoving = "team: moving";
        public const string TeamCovering = "team: covering";
        public const string TeamSuppressing = "team: suppressing";
        public const string TeamManDown = "team: man down";
        public const string TeamTwoLeft = "team: two left";
        public const string TeamOneLeft = "team: one left";
        public const string TeamLostHer = "team: lost her";
        public const string TeamSearchMoving = "team: search, moving";
        public const string TeamSearchCovering = "team: search, covering";
        public const string TeamSearchClear = "team: search, clear";
        public const string TeamSecond = "team: second team";
        // mk_edgar (Domination). The enemy column holds a group, not a class: "fighter", "civilian", "any", "voice".
        public const string EdgarKneelFighter = "edgar: kneel fighter";
        public const string EdgarKneelCivilian = "edgar: kneel civilian";
        public const string EdgarSeen = "edgar: seen";
        public const string EdgarTaken = "edgar: taken";
        public const string EdgarSent = "edgar: sent";
        public const string EdgarLetGo = "edgar: letgo";
        public const string EdgarVoiceTake = "edgar: voice take";
        public const string EdgarVoiceSent = "edgar: voice sent";
        public const string EdgarVoiceSnap = "edgar: voice snap";
    }

    /// <summary>One row of the writer's barks.csv.</summary>
    public sealed class BarkRow
    {
        public string enemy, personality, situation, line, direction;
        /// <summary>Its direction says "plays first": it plays before the others of its situation, once for each person.</summary>
        public bool first;
        /// <summary>The things of a "PLACE:" direction. The row plays only in a room that has one of them. Empty: any room.</summary>
        public string[] places = new string[0];
        /// <summary>The place of the row among the rows of its enemy and situation (0 first), for the scripted lines.</summary>
        public int index;
    }

    /// <summary>
    /// The writer's barks (2026-11-13, rounds 133 to 135, dialogue/takes/barks_v2/GAME_NOTES.md): game/barks.csv, with the
    /// columns enemy, personality, situation, line, direction. BarkBookSetup.Build copies the writer's file to
    /// Resources/Barks/barks.csv (the player build has no access to the writers' folder). A person (BarkVoice) asks for a
    /// situation: the rows of its enemy and its personality, else the rows of its enemy with an empty personality (the
    /// fallback). A lab worker with no row also takes the scientist's rows (the game has only Figure_LabWorker). A nurse
    /// has no rows for some situations: she takes the nearest situation of her own (NurseAlias).
    /// The code asks only for the pairs in Asked. BarkBookTests checks that each pair has rows for every personality.
    /// </summary>
    public static class BarkBook
    {
        public const string ResourcePath = "Barks/barks";
        public const string Header = "enemy,personality,situation,line,direction";

        private static List<BarkRow> rows;
        private static Dictionary<string, List<BarkRow>> byKey;
        private static string source = "";

        public static IReadOnlyList<BarkRow> Rows { get { Load(); return rows; } }
        /// <summary>Where the rows came from (Resources or a test's text).</summary>
        public static string Source { get { Load(); return source; } }

        /// <summary>The personalities a person of the enemy can have (round 133). A guard turns broken when he panics (round 134).</summary>
        public static string[] PersonalitiesOf(string enemy)
        {
            switch (enemy)
            {
                case "guard": return new[] { "overconfident", "sarcastic", "professional", "broken" };
                case "soldier": return new[] { "professional", "callous", "sadistic" };
                case "armour": return new[] { "professional", "confident", "cocky" };
                case "nurse":
                case "labworker": return new[] { "confused", "afraid", "bargaining" };
                default: return new[] { "" };
            }
        }

        /// <summary>The personalities the code asks the situation for: "talks to Ghost" only for one who bargains (round 133).</summary>
        public static string[] PersonalitiesAsked(string enemy, string situation)
            => situation == BarkSituation.TalksToGhost ? new[] { "bargaining" } : PersonalitiesOf(enemy);

        /// <summary>
        /// Every pair (enemy, situation) the code asks for. Edit it with the call sites: BarkBook.Say plays nothing for a pair
        /// that is not here. The nurse's pairs go through NurseAlias.
        /// </summary>
        public static readonly (string enemy, string situation)[] Asked =
        {
            // The guard (SoldierBrain, CorpseCare, EnemyMorale).
            ("guard", BarkSituation.SpotsLucy), ("guard", BarkSituation.Fires), ("guard", BarkSituation.Hit), ("guard", BarkSituation.Shock),
            ("guard", BarkSituation.PartnerDown), ("guard", BarkSituation.SeesFeed), ("guard", BarkSituation.Retreats), ("guard", BarkSituation.Cower),
            ("guard", BarkSituation.StartsCare), ("guard", BarkSituation.Drags), ("guard", BarkSituation.Cpr),
            // The soldier (SoldierBrain, CorpseCare, EnemyMorale, FireTeam, TeamReinforcement).
            ("soldier", BarkSituation.SpotsLucy), ("soldier", BarkSituation.Shock), ("soldier", BarkSituation.PartnerDown), ("soldier", BarkSituation.SeesFeed),
            ("soldier", BarkSituation.StartsCare), ("soldier", BarkSituation.Drags), ("soldier", BarkSituation.Cpr),
            ("soldier", BarkSituation.TeamContact), ("soldier", BarkSituation.TeamMoving), ("soldier", BarkSituation.TeamCovering),
            ("soldier", BarkSituation.TeamSuppressing), ("soldier", BarkSituation.TeamManDown), ("soldier", BarkSituation.TeamTwoLeft),
            ("soldier", BarkSituation.TeamOneLeft), ("soldier", BarkSituation.TeamLostHer), ("soldier", BarkSituation.TeamSearchMoving),
            ("soldier", BarkSituation.TeamSearchCovering), ("soldier", BarkSituation.TeamSearchClear), ("soldier", BarkSituation.TeamSecond),
            // The power armour (SoldierBrain, EnemyMorale, HeavyArrival, PsychicPowers, FireTeam: he joins the calls of a team in his room).
            ("armour", BarkSituation.SpotsLucy), ("armour", BarkSituation.Fires), ("armour", BarkSituation.Hit), ("armour", BarkSituation.Shock),
            ("armour", BarkSituation.SeesFeed), ("armour", BarkSituation.TooHeavy), ("armour", BarkSituation.ArmourEnter),
            ("armour", BarkSituation.TeamContact), ("armour", BarkSituation.TeamManDown), ("armour", BarkSituation.TeamOneLeft), ("armour", BarkSituation.TeamLostHer),
            // The lab worker, armed (SoldierBrain) and unarmed (CivilianBrain).
            ("labworker", BarkSituation.SpotsLucy), ("labworker", BarkSituation.Fires), ("labworker", BarkSituation.Hit), ("labworker", BarkSituation.Shock),
            ("labworker", BarkSituation.SeesFeed), ("labworker", BarkSituation.Retreats), ("labworker", BarkSituation.Cower), ("labworker", BarkSituation.Begs),
            ("labworker", BarkSituation.StartsCare), ("labworker", BarkSituation.Drags), ("labworker", BarkSituation.Cpr),
            // The nurse (CivilianBrain, CorpseCare, EnemyMorale).
            ("nurse", BarkSituation.SpotsLucy), ("nurse", BarkSituation.Shock), ("nurse", BarkSituation.SeesFeed), ("nurse", BarkSituation.Retreats),
            ("nurse", BarkSituation.Cower), ("nurse", BarkSituation.Begs), ("nurse", BarkSituation.StartsCare), ("nurse", BarkSituation.Drags),
            ("nurse", BarkSituation.Cpr),
            // A bargaining nurse or lab worker talks to Ghost at the first sight (round 133). The lab worker takes the scientist's rows.
            ("nurse", BarkSituation.TalksToGhost), ("labworker", BarkSituation.TalksToGhost),
            // The patient in her bed (PatientBrain).
            ("patient", BarkSituation.HearsLucy),
            // mk_edgar (Domination): the groups of the enemy column.
            ("fighter", BarkSituation.EdgarKneelFighter), ("civilian", BarkSituation.EdgarKneelCivilian), ("fighter", BarkSituation.EdgarSeen),
            ("any", BarkSituation.EdgarTaken), ("any", BarkSituation.EdgarSent), ("any", BarkSituation.EdgarLetGo),
            ("voice", BarkSituation.EdgarVoiceTake), ("voice", BarkSituation.EdgarVoiceSent), ("voice", BarkSituation.EdgarVoiceSnap),
        };

        private static HashSet<string> asked;

        public static bool IsAsked(string enemy, string situation)
        {
            if (asked == null)
            {
                asked = new HashSet<string>();
                foreach ((string e, string s) in Asked) asked.Add(e + "|" + s);
            }
            return asked.Contains(enemy + "|" + situation);
        }

        /// <summary>The nurse has no rows for these situations: she says the rows of the nearest situation of her own (PROPOSED).</summary>
        public static string NurseAlias(string situation)
        {
            switch (situation)
            {
                case BarkSituation.SpotsLucy:
                case BarkSituation.Retreats:
                case BarkSituation.Cower:
                case BarkSituation.Begs: return BarkSituation.BacksAway;
                case BarkSituation.StartsCare: return BarkSituation.GoesToBody;
                default: return situation;
            }
        }

        // ── Load ───────────────────────────────────────────────────────

        private static void Load()
        {
            if (rows != null) return;
            TextAsset text = Resources.Load<TextAsset>(ResourcePath);
            if (text == null)
            {
                Debug.LogWarning("[BarkBook] no Resources/" + ResourcePath + ".csv: run BarkBookSetup.Build. No barks play.");
                Use(new List<BarkRow>(), "none");
                return;
            }
            Use(Parse(text.text), "Resources/" + ResourcePath);
        }

        /// <summary>Uses the rows of this text (a test reads the writer's file).</summary>
        public static void LoadText(string csv, string from)
        {
            Use(Parse(csv), from);
        }

        private static void Use(List<BarkRow> list, string from)
        {
            rows = list;
            source = from;
            byKey = new Dictionary<string, List<BarkRow>>();
            foreach (BarkRow row in rows)
            {
                string key = Key(row.enemy, row.personality, row.situation);
                if (!byKey.TryGetValue(key, out List<BarkRow> bucket)) byKey[key] = bucket = new List<BarkRow>();
                bucket.Add(row);
            }
            // The index of a row among the rows of its enemy and situation, over every personality (the scripted lines).
            var counts = new Dictionary<string, int>();
            foreach (BarkRow row in rows)
            {
                string key = row.enemy + "|" + row.situation;
                counts.TryGetValue(key, out int n);
                row.index = n;
                counts[key] = n + 1;
            }
        }

        private static string Key(string enemy, string personality, string situation) => enemy + "|" + personality + "|" + situation;

        public static List<BarkRow> Parse(string csv)
        {
            var list = new List<BarkRow>();
            List<string[]> table = ReadCsv(csv);
            for (int i = 1; i < table.Count; i++) // row 0 is the header
            {
                string[] cells = table[i];
                if (cells.Length < 4 || string.IsNullOrWhiteSpace(cells[0])) continue;
                var row = new BarkRow
                {
                    enemy = cells[0].Trim(),
                    personality = cells[1].Trim(),
                    situation = cells[2].Trim(),
                    line = cells[3].Trim(),
                    direction = cells.Length > 4 ? cells[4].Trim() : "",
                };
                if (string.IsNullOrEmpty(row.line)) continue;
                row.first = row.direction.Contains("plays first");
                row.places = PlacesOf(row.direction);
                list.Add(row);
            }
            return list;
        }

        /// <summary>"PLACE: linen, cart; take t2.21" gives { "linen", "cart" }.</summary>
        private static string[] PlacesOf(string direction)
        {
            int at = direction.IndexOf("PLACE:", System.StringComparison.Ordinal);
            if (at < 0) return new string[0];
            string rest = direction.Substring(at + 6);
            int end = rest.IndexOf(';');
            if (end >= 0) rest = rest.Substring(0, end);
            var places = new List<string>();
            foreach (string part in rest.Split(','))
            {
                string p = Simple(part);
                if (p.Length > 0) places.Add(p);
            }
            return places.ToArray();
        }

        /// <summary>Lower case letters only, a final s dropped: "Nurses' station" and "NurseStation" meet.</summary>
        public static string Simple(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text.ToLowerInvariant()) if (c >= 'a' && c <= 'z') sb.Append(c);
            string s = sb.ToString();
            return s.Length > 3 && s.EndsWith("s") ? s.Substring(0, s.Length - 1) : s;
        }

        /// <summary>RFC 4180: commas and new lines inside quotes, a doubled quote is a quote.</summary>
        public static List<string[]> ReadCsv(string text)
        {
            var result = new List<string[]>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else cell.Append(c);
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(cell.ToString());
                    cell.Clear();
                    result.Add(row.ToArray());
                    row.Clear();
                }
                else cell.Append(c);
            }
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                result.Add(row.ToArray());
            }
            return result;
        }

        // ── Look-up ───────────────────────────────────────────────────

        /// <summary>
        /// The rows for a person: its personality's, else its enemy's with an empty personality. A nurse goes through NurseAlias.
        /// A lab worker with none takes the scientist's (the game has no scientist figure). Empty when nothing fits.
        /// </summary>
        public static List<BarkRow> RowsFor(string enemy, string personality, string situation)
        {
            Load();
            if (enemy == "nurse") situation = NurseAlias(situation);
            List<BarkRow> found = Exact(enemy, personality, situation);
            if (found.Count == 0 && enemy == "labworker") found = Exact("scientist", personality, situation);
            return found;
        }

        private static List<BarkRow> Exact(string enemy, string personality, string situation)
        {
            if (!string.IsNullOrEmpty(personality) && byKey.TryGetValue(Key(enemy, personality, situation), out List<BarkRow> own) && own.Count > 0) return own;
            return byKey.TryGetValue(Key(enemy, "", situation), out List<BarkRow> fallback) ? fallback : new List<BarkRow>();
        }

        public static bool Has(string enemy, string personality, string situation) => RowsFor(enemy, personality, situation).Count > 0;

        /// <summary>A scripted line (a beat of one room): the row at the index among the rows of the enemy and the situation. "" when missing.</summary>
        public static string Scripted(string enemy, string situation, int index = 0)
        {
            Load();
            foreach (BarkRow row in rows)
                if (row.enemy == enemy && row.situation == situation && row.index == index) return row.line;
            return "";
        }

        /// <summary>The lines of a group with ids for the voice clips (the core's voice: "edgar_voice_take_1" ...).</summary>
        public static (string id, string text)[] Set(string enemy, string situation, string idPrefix)
        {
            Load();
            var set = new List<(string, string)>();
            foreach (BarkRow row in rows)
                if (row.enemy == enemy && row.situation == situation) set.Add((idPrefix + (row.index + 1), row.line));
            return set.ToArray();
        }

        // ── Say ───────────────────────────────────────────────────────

        /// <summary>The person says a line of the situation (no repeat, "plays first" first). False when it has none.</summary>
        public static bool Say(GameObject who, string situation, float seconds = 1.6f)
        {
            string line = Line(who, situation);
            if (string.IsNullOrEmpty(line)) return false;
            Bark.Say(who, line, seconds);
            return true;
        }

        /// <summary>The next line of the situation for the person, or null.</summary>
        public static string Line(GameObject who, string situation)
        {
            BarkVoice voice = BarkVoice.Of(who);
            return voice != null ? voice.Next(situation) : null;
        }

        /// <summary>The person has a line for the situation (for the choice of a speaker).</summary>
        public static bool CanSay(GameObject who, string situation)
        {
            BarkVoice voice = BarkVoice.Of(who);
            return voice != null && IsAsked(voice.Enemy, situation) && Has(voice.Enemy, voice.Personality, situation);
        }

        /// <summary>A line of a group (mk_edgar's "fighter", "civilian", "any"), said as the person, no repeat for the person.</summary>
        public static bool SayAs(GameObject who, string group, string situation, float seconds = 1.6f)
        {
            if (who == null) return false;
            BarkVoice voice = BarkVoice.Of(who);
            if (voice == null) voice = who.GetComponent<BarkVoice>();
            if (voice == null) voice = who.AddComponent<BarkVoice>();
            string line = voice.NextOf(group, "", situation);
            if (string.IsNullOrEmpty(line)) return false;
            Bark.Say(who, line, seconds);
            return true;
        }

        /// <summary>Drops the rows: the next use loads Resources again (a test that loaded its own text).</summary>
        public static void Unload() => ResetOnLoad();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            rows = null;
            byKey = null;
            asked = null;
        }
    }
}
