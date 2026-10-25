#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.UI;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// Fills the death screen content (Bruno, 2026-10-25: "put in code for the game over screen (i.e killed by
    /// assault_rifle_guard) so that we can code test the feature before building assets for it").
    ///   1. The writer's rows (game/death_screens.csv of the RFab repo, next to this one: enemy, picture,
    ///      voice_line, hint). The file is the source: a build writes its words over the asset's.
    ///   2. One PLACEHOLDER entry for each killer id of the test rooms whose weapon changes the hint
    ///      ("soldier_rifle", "soldier_gas", ...). It takes the voice line and the picture note of its class
    ///      row until the writer gives it its own row. A present entry keeps its words.
    /// The pictures and the voice clips come later (RFab). Batch:
    /// tools/unity.sh exec GlorifiedBody.Editor.DeathScreenSetup.Build
    /// </summary>
    public static class DeathScreenSetup
    {
        /// <summary>The writer's file, from the project root (the two repos sit side by side in ~/MEGA/code).</summary>
        public const string WriterCsv = "../reality-fabricator/the-glorified-body/game/death_screens.csv";

        private static readonly (string id, string classId, string note, string hint)[] Placeholders =
        {
            ("dummy_shooter", "", "PLACEHOLDER: the padded dummy, the gun on its arm", "PLACEHOLDER HINT: step back when the circle closes, then charge."),
            ("soldier_rifle", "soldier", "PLACEHOLDER: the soldier with the rifle, the laser on her", "PLACEHOLDER HINT: three rounds follow the laser, 0.1 s apart. One dodge to the side clears the burst."),
            ("soldier_gas", "soldier", "PLACEHOLDER: the green cloud, the gas mask in it", "PLACEHOLDER HINT: a green ring is gas. Leave it before the grenade lands, and stay out of the cloud."),
            ("soldier_grenades", "soldier", "PLACEHOLDER: the red ring on the floor, her feet in it", "PLACEHOLDER HINT: a red ring is a grenade. Leave the ring before it closes, or catch the grenade with mk_orzabal."),
            ("power_armour", "", "PLACEHOLDER: the power armour from the floor", "PLACEHOLDER HINT: it is too heavy to lift. Break its aim with mk_warspite, then burn it with mk_erebus."),
            ("power_armour_launchers", "power_armour", "PLACEHOLDER: the rocket's trail, the armour behind it", "PLACEHOLDER HINT: a rocket follows the laser. Dodge to the side, or catch it with mk_orzabal and send it back."),
        };

        [MenuItem("GlorifiedBody/Build Death Screen Content")]
        public static void Build()
        {
            DeathScreenContent content = AssetDatabase.LoadAssetAtPath<DeathScreenContent>(MenuLoopSetup.DeathContentPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<DeathScreenContent>();
                AssetDatabase.CreateAsset(content, MenuLoopSetup.DeathContentPath);
            }
            int rows = Fill(content);
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DeathScreenSetup] {MenuLoopSetup.DeathContentPath}: {rows} writer rows, {content.entries.Count} entries.");
        }

        /// <summary>The writer's rows, then the placeholders. Returns the number of writer rows read.</summary>
        public static int Fill(DeathScreenContent content)
        {
            int rows = 0;
            string path = Path.GetFullPath(WriterCsv);
            if (File.Exists(path))
            {
                List<string[]> table = ReadCsv(File.ReadAllText(path));
                for (int i = 1; i < table.Count; i++)
                {
                    string[] r = table[i];
                    if (r.Length < 4 || string.IsNullOrWhiteSpace(r[0])) continue;
                    DeathScreenContent.Entry e = Upsert(content, r[0].Trim());
                    e.pictureNote = r[1].Trim();
                    e.voiceText = r[2].Trim();
                    e.hint = r[3].Trim();
                    rows++;
                }
            }
            else
            {
                Debug.LogWarning($"[DeathScreenSetup] no writer file at {path}: placeholders only.");
            }

            foreach ((string id, string classId, string note, string hint) in Placeholders)
            {
                if (content.Exact(id) != null) continue;
                DeathScreenContent.Entry parent = string.IsNullOrEmpty(classId) ? null : content.Exact(classId);
                content.entries.Add(new DeathScreenContent.Entry
                {
                    killerId = id,
                    pictureNote = note,
                    hint = hint,
                    voiceText = parent != null ? parent.voiceText : ""
                });
            }
            content.fallback.hint = "PLACEHOLDER HINT: watch the red line and the ring. Step out before it closes.";
            content.fallback.pictureNote = "PLACEHOLDER: the room from the floor";
            return rows;
        }

        private static DeathScreenContent.Entry Upsert(DeathScreenContent content, string id)
        {
            DeathScreenContent.Entry e = content.Exact(id);
            if (e != null) return e;
            e = new DeathScreenContent.Entry { killerId = id };
            content.entries.Add(e);
            return e;
        }

        /// <summary>A CSV reader for the writer's files: commas, double quotes, "" inside quotes, CRLF or LF.</summary>
        public static List<string[]> ReadCsv(string text)
        {
            var rows = new List<string[]>();
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
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(cell.ToString());
                    cell.Clear();
                    if (row.Count > 1 || row[0].Length > 0) rows.Add(row.ToArray());
                    row.Clear();
                }
                else cell.Append(c);
            }
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row.ToArray());
            }
            return rows;
        }
    }
}
#endif
