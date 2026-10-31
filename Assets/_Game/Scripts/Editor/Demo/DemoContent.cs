#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The writers' words for the greybox (round 18: "the cutscenes as subtitles"): the speaker lines of a cutscene script
    /// or a lines file in the writers' room (game/cutscenes, game/lines), read when a room is built. A line is
    /// "SPEAKER: words  (direction)"; the direction goes, a "..." line goes. No file: no lines, and the builder warns.
    /// </summary>
    public static class DemoScripts
    {
        public const string WriterGame = "../reality-fabricator/the-glorified-body/game";
        private static readonly Regex LineRx = new Regex(@"^\s*([A-Z][A-Z .'\-]*[A-Z]):\s+(.+?)\s*(\(.*\))?\s*$");

        public static List<(string speaker, string text)> Lines(string file)
        {
            var lines = new List<(string, string)>();
            string path = Path.Combine(WriterGame, file);
            if (!File.Exists(path))
            {
                Debug.LogWarning("[DemoScripts] no file " + path + ": the beat has no lines.");
                return lines;
            }
            string[] all = File.ReadAllLines(path);
            bool hasScript = System.Array.Exists(all, l => l.StartsWith("## The script"));
            bool inScript = !hasScript;
            foreach (string raw in all)
            {
                if (raw.StartsWith("## ")) { inScript = raw.StartsWith("## The script") || !hasScript; continue; }
                if (!inScript) continue;
                Match m = LineRx.Match(raw);
                if (!m.Success) continue;
                string text = m.Groups[2].Value.Trim().Trim('"');
                if (text == "..." || text.Length == 0) continue;
                lines.Add((m.Groups[1].Value.Trim(), text));
            }
            return lines;
        }

        /// <summary>The lines as steps: each one a subtitle with its speaker (a radio line: no voice yet), at reading pace.</summary>
        public static List<ScriptStep> Steps(string file, string idPrefix, int max = 99)
        {
            var steps = new List<ScriptStep>();
            int n = 0;
            foreach ((string speaker, string text) in Lines(file))
            {
                if (n >= max) break;
                steps.Add(ScriptStep.Radio(speaker, $"{idPrefix}_{n++:00}", text));
            }
            return steps;
        }
    }

    /// <summary>
    /// The items of the demo (the demo plan §5, round 9: "the logs are few"): the plan L1, the logs L2 to L9 with the
    /// plan's PROPOSED words, the card of the stairwell. L9 unlocks the feed (round 18). Assets in Resources/Items.
    /// </summary>
    public static class DemoItems
    {
        public const string Floor2 = "demo_2", Floor1 = "demo_1";

        public static ItemDefinition Plan()
        {
            ItemDefinition item = ItemAssets.Item("demo_plan", "STEVENS' PRINTOUT", ItemKind.Map, ItemAssets.PlanColour,
                "A plan of both floors. Sector C is crossed out in red: UNDER CONSTRUCTION. NO COVERAGE. A pen line runs from the ward to sector C.", "", DrugType.Nalcon);
            item.mapFloor = Floor2 + "," + Floor1;
            EditorUtility.SetDirty(item);
            return item;
        }

        public static ItemDefinition Log(string id, string name, string text, string readFlag = "")
        {
            ItemDefinition item = ItemAssets.Item(id, name, ItemKind.Log, ItemAssets.LogColour, name.ToLowerInvariant() + ".", text, DrugType.Nalcon);
            item.readFlag = readFlag;
            EditorUtility.SetDirty(item);
            return item;
        }

        public static ItemDefinition L2() => Log("l2_patient_chart", "THE PATIENT'S CHART", "CANDIDATE 0417. STAGE: PRE-TRANSLATION. FAMILY INFORMED. DO NOT DISCUSS OUTCOME WITH CANDIDATE.");
        public static ItemDefinition L3() => Log("l3_code_silver_card", "A LAMINATED CARD", "CODE SILVER. Non-security personnel: shelter in place. Security: respond. Patients in this facility can be confused and dangerous. Do not discuss patients outside the facility.");
        public static ItemDefinition L4() => Log("l4_whiteboard", "THE WHITEBOARD", "V3 SHOWCASE. RETIRE AFTER V4 TRANSFER. SCHEDULED.\n\nLUCY. NEXT OF KIN: KELLER, R. (CONSENT ON FILE).");
        public static ItemDefinition L5() => Log("l5_security_logbook", "THE SECURITY LOGBOOK", "01:52 SECTOR C CAMERAS OFF FOR MAINTENANCE. AUTH: STEVENS.");
        public static ItemDefinition L7() => Log("l7_session_note", "A SESSION NOTE", "SESSION 4,211. V3 HELD THE CUP FOR NINE SECONDS WITHOUT HELP. THE WHOLE TEAM CRIED. SHE SMILED, WE THINK. THIS IS WHY WE DO IT.");
        public static ItemDefinition L8() => Log("l8_prayer_card", "A PRAYER CARD", "Tonight we pray for the candidates in surgery, for the children of the programme, and for all who give the body so that humanity endures.");
        public static ItemDefinition L9() => Log("l9_care_chart", "HER CARE CHART", "V3. DAILY CARE. ORAL INTAKE: NONE. PCDH-Y: DONOR TISSUE PER PROTOCOL 7 (ALL DONORS CONSENTED). LAST DOSE: 6 DAYS AGO, HELD FOR THE V4 TRANSFER. EXPECT THE SUBJECT TO SEEK A SOURCE. NEVER ENTER ALONE.", Feeding.UnlockFlag);

        public static ItemDefinition StairCard()
        {
            return ItemAssets.Item("stair_card", "THE WARD SISTER'S CARD", ItemKind.Key, ItemAssets.KeyColour, "A white card on a lanyard. It opens the stairwell door.", "", DrugType.Nalcon);
        }
    }
}
#endif
