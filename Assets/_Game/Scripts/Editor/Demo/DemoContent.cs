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
        public const string WriterGame = "../the-glorified-body/game";
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

        public const string FmvFolder = "Assets/_Game/Cutscenes";
        public const string FmvCardPath = FmvFolder + "/fmv_placeholder.png";

        /// <summary>
        /// A story cutscene of the demo as FMV (Bruno, 2026-11-02: "the two initial cutscenes (the stevens release and the
        /// scientist thing) will be fmv cutscenes"): Resources/Cutscenes/cutscene_&lt;id&gt;.asset. The clip
        /// Assets/_Game/Cutscenes/&lt;id&gt;.webm (VP8: the Linux editor has no H.264) is the whole cutscene when it exists.
        /// Until then a dark card holds the place, one shot for each line of the writers' script, the line as its subtitle.
        /// </summary>
        public static CutsceneAsset Fmv(string file, string cutsceneId, string idPrefix)
        {
            Directory.CreateDirectory(ScriptTestSetup.CutscenesFolder);
            string path = ScriptTestSetup.CutscenesFolder + "/cutscene_" + cutsceneId + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<CutsceneAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CutsceneAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.cutsceneId = cutsceneId;
            asset.music = null;
            var clip = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(FmvFolder + "/" + cutsceneId + ".webm");
            var shots = new List<CutsceneAsset.Shot>();
            if (clip != null) shots.Add(new CutsceneAsset.Shot { video = clip, seconds = 1f });
            else
            {
                Texture2D card = Card();
                int n = 0;
                foreach ((string speaker, string text) in Lines(file))
                    shots.Add(new CutsceneAsset.Shot
                    {
                        still = card, seconds = Mathf.Clamp(1.2f + text.Length * 0.055f, 2f, 6f),
                        panFrom = new Vector3(0f, 0f, 1f), panTo = new Vector3(0f, 0f, 1f),
                        speaker = speaker, lineId = $"{idPrefix}_{n++:00}", text = text,
                    });
            }
            asset.shots = shots.ToArray();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>The dark card of an FMV slot with no clip yet: a grey frame on black (no words: the subtitles carry them).</summary>
        private static Texture2D Card()
        {
            var card = AssetDatabase.LoadAssetAtPath<Texture2D>(FmvCardPath);
            if (card != null) return card;
            Directory.CreateDirectory(FmvFolder);
            const int w = 640, h = 360;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool frame = (x > 24 && x < w - 24 && (y == 24 || y == h - 25)) || (y > 24 && y < h - 24 && (x == 24 || x == w - 25));
                    byte v = frame ? (byte)70 : (byte)12;
                    px[y * w + x] = new Color32(v, v, (byte)(v + 3), 255);
                }
            tex.SetPixels32(px);
            File.WriteAllBytes(FmvCardPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(FmvCardPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(FmvCardPath);
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
    /// plan's PROPOSED words, the card of the stairwell. Cutscene 02b opens the feed and gives L9 (Bruno, 2026-11-05). Assets in Resources/Items.
    /// </summary>
    public static class DemoItems
    {
        public const string Floor2 = "demo_2", Floor1 = "demo_1", FloorRoad = "demo_road", FloorBrain = "demo_brain";

        /// <summary>The sunglasses of the coda (the writer's words): the readout after the Crown, and no camera reads her face.</summary>
        public static ItemDefinition Sunglasses() => ItemAssets.Item(DemoCoda.SunglassesId, "SUNGLASSES", ItemKind.Critical, ItemAssets.KeyColour,
            "Dark glasses, heavier than they look. A readout runs along the inside of the lens, and cameras cannot read the face behind them.", "", DrugType.Nalcon);

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
        public static ItemDefinition L4() => Log("l4_whiteboard", "THE WHITEBOARD", "V3 SHOWCASE. RETIRE AFTER V4 TRANSFER. SCHEDULED.\n\nDE HOMEN-CRISTO, LUCIA. NEXT OF KIN: DE HOMEN-CRISTO, H. (MOTHER. CONSENT ON FILE.)\n\nFOURTH FACULTY: NOT FOR SHOWCASE.");
        public static ItemDefinition L5() => Log("l5_security_logbook", "THE SECURITY LOGBOOK", "01:52 SECTOR C CAMERAS OFF FOR MAINTENANCE. AUTH: STEVENS.");
        public static ItemDefinition L7() => Log("l7_session_note", "A SESSION NOTE", "SESSION 4,211. V3 HELD THE CUP FOR NINE SECONDS WITHOUT HELP. THE WHOLE TEAM CRIED. SHE SMILED, WE THINK. THIS IS WHY WE DO IT.");
        public static ItemDefinition L8() => Log("l8_prayer_card", "A PRAYER CARD", "Tonight we pray for the candidates in surgery, for the children of the programme, and for all who give the body so that humanity endures.");
        /// <summary>
        /// L19, the incident review: an audio log on the counter of W9 (DEMO_PLAN §8, round 44: the backup of the feed;
        /// the writer's game/lines/T14_incident_review.md, PROPOSED). It plays when taken, and at its end the feed unlocks.
        /// The clip is a placeholder (tools/audio_log_placeholders.py); the line times are its times.
        /// </summary>
        public static ItemDefinition L19() => AudioLog("l19_incident_review", "A DICTAPHONE", "REVIEWER", GlorifiedBody.Player.Feeding.UnlockFlag,
            new[] { 0.6f, 4.9f, 13.1f, 17.1f, 26.4f, 31.5f, 35.1f, 46.0f },
            new[]
            {
                "Incident review. V4 series, unit two. Recorded for the file.",
                "Summary. The unit was held at the standard interval for PCDH-Y. The replacement batch did not arrive on the day.",
                "Logistics reports a hold at the hub. Six days.",
                "On day six the nursing team entered to administer the replacement. The unit was in deficit. It did not wait for the injection.",
                "The unit fed on a member of staff.",
                "The team withdrew. The unit did not pursue.",
                "Exams on day seven. Levels within the normal range. Accelerated healing of every wound the unit sustained in the incident, confirmed on imaging. The unit is calm.",
                "Recommendation. Do not hold a batch. Never enter alone. End of review.",
            });

        /// <summary>L20, the calibration note: an audio log on the bench of W11 (round 49; game/lines/L20_calibration.md, PROPOSED).</summary>
        public static ItemDefinition L20() => AudioLog("l20_calibration", "A RECORDER", "TECHNICIAN", "",
            new[] { 0.6f, 3.4f, 6.2f, 8.6f, 12.9f },
            new[]
            {
                "Mapping. Day eleven. For the file.",
                "Presentation stable. Separation holds under load.",
                "Recommendation. Retain for v4 transfer.",
                "Note. Affect flatter than day one.",
                "Acceptable. End of note.",
            });

        private static ItemDefinition AudioLog(string id, string name, string speaker, string endFlag, float[] at, string[] lines)
        {
            ItemDefinition item = ItemAssets.Item(id, name, ItemKind.AudioLog, ItemAssets.TapeColour, "A recording. It plays while she walks. A fight pauses it.",
                string.Join("\n\n", lines), DrugType.Nalcon);
            item.audioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Game/Audio/Logs/{id}.wav");
            if (item.audioClip == null) Debug.LogWarning($"[DemoItems] no clip for {id}: run tools/audio_log_placeholders.py");
            item.tapeLines = new List<TapeLine>();
            for (int i = 0; i < lines.Length; i++) item.tapeLines.Add(new TapeLine { at = at[i], speaker = speaker, text = lines[i] });
            item.readFlag = endFlag; // an audio log raises it at its end (TapePlayer)
            UnityEditor.EditorUtility.SetDirty(item);
            return item;
        }

        public static ItemDefinition L9() => Log("l9_care_chart", "HER CARE CHART", "V3. DAILY CARE. ORAL INTAKE: NONE. PCDH-Y: DONOR TISSUE PER PROTOCOL 7 (ALL DONORS CONSENTED). LAST DOSE: 6 DAYS AGO, HELD FOR THE V4 TRANSFER. EXPECT THE SUBJECT TO SEEK A SOURCE. NEVER ENTER ALONE.");

        public static ItemDefinition StairCard()
        {
            return ItemAssets.Item("stair_card", "THE WARD SISTER'S CARD", ItemKind.Key, ItemAssets.KeyColour, "A white card on a lanyard. It opens the stairwell door.", "", DrugType.Nalcon);
        }
    }
}
#endif
