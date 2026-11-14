#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The writer's in-world text in DEMO_PLAN.md (round 138 made the rewrite v2 CANON, round 142 the EMERGENCY STOP): the
    /// tables of §7 (the signs, by the start of the "Where" cell), §8 (the logs, by the id "L13") and §9 (the scans, by the
    /// id "C5"). The room builders take the words from here when a room is built, with the old words as the fallback and a
    /// warning. A cell's "&lt;br&gt;" is a new line. "(by hand)" is a direction, not words on the sign. A last parenthesis that
    /// names a round, CANON or a source (the verse) is the writer's note, not words. DemoPlanTests checks every key.
    /// </summary>
    public static class DemoPlan
    {
        public static string PlanFile => Path.GetFullPath(Path.Combine(DemoScripts.WriterGame, "..", "DEMO_PLAN.md"));

        private static string loadedFrom;
        private static List<string[]> signs, logs, scans;

        /// <summary>The rows of the table under the "## n." heading: the cells of each row, trimmed (no header, no rule).</summary>
        public static List<string[]> Table(string[] lines, string heading)
        {
            var rows = new List<string[]>();
            bool inSection = false, header = true;
            foreach (string raw in lines)
            {
                if (raw.StartsWith("## ")) { inSection = raw.StartsWith("## " + heading); header = true; continue; }
                if (!inSection || !raw.StartsWith("|")) continue;
                string[] cells = raw.Trim().Trim('|').Split('|');
                for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim();
                if (header) { header = false; continue; } // the column names
                if (cells.Length > 0 && Regex.IsMatch(cells[0], @"^-+$")) continue; // the rule under them
                rows.Add(cells);
            }
            return rows;
        }

        private static bool Load()
        {
            string path = PlanFile;
            if (!File.Exists(path)) return false;
            if (loadedFrom == path && signs != null) return true;
            string[] lines = File.ReadAllLines(path);
            signs = Table(lines, "7.");
            logs = Table(lines, "8.");
            scans = Table(lines, "9.");
            loadedFrom = path;
            return true;
        }

        /// <summary>Forget the file (a test, or after the writer changes it).</summary>
        public static void Unload() => signs = logs = scans = null;

        /// <summary>The words of a cell: new lines for "&lt;br&gt;", no "(by hand)", no last note in parentheses.</summary>
        public static string Words(string cell)
        {
            string s = Regex.Replace(cell, @"\s*\((?:[^()]*\b(?:CANON|PROPOSED|round|Corinthians|translation)\b[^()]*)\)\s*$", "");
            s = Regex.Replace(s, @"\(by hand\)\s*", "");
            s = Regex.Replace(s, @"\s*<br>\s*", "\n");
            return s.Trim();
        }

        /// <summary>The sign of §7 whose "Where" starts with where. Missing: the fallback, with a warning.</summary>
        public static string Sign(string where, string fallback)
        {
            string[] row = Load() ? signs.Find(r => r.Length >= 2 && r[0].StartsWith(where)) : null;
            if (row != null) return Words(row[1]);
            Debug.LogWarning($"[DemoPlan] no sign '{where}' in §7 of {PlanFile}: the builder uses '{fallback}'");
            return fallback;
        }

        /// <summary>The text of the log of §8 (the id "L13"). Missing: the fallback, with a warning.</summary>
        public static string Log(string id, string fallback)
        {
            string[] row = Load() ? logs.Find(r => r.Length >= 4 && r[0] == id) : null;
            if (row != null) return Words(row[3]);
            Debug.LogWarning($"[DemoPlan] no log {id} in §8 of {PlanFile}: the builder uses the old text");
            return fallback;
        }

        /// <summary>
        /// The caption of the scan of §9 (the id "C5"): the quoted words, or "" when the caption is "None". Missing: the
        /// fallback, with a warning.
        /// </summary>
        public static string Caption(string id, string fallback)
        {
            string[] row = Load() ? scans.Find(r => r.Length >= 6 && r[0] == id) : null;
            if (row == null)
            {
                Debug.LogWarning($"[DemoPlan] no scan {id} in §9 of {PlanFile}: the builder uses '{fallback}'");
                return fallback;
            }
            Match quoted = Regex.Match(row[5], "\"([^\"]+)\"");
            return quoted.Success ? "\"" + quoted.Groups[1].Value + "\"" : row[5].StartsWith("None") ? "" : Words(row[5]);
        }

        /// <summary>
        /// The card of a scan until its picture exists: "PLACEHOLDER (C5): " and what it shows (§9), then its caption.
        /// Missing: the fallback, with a warning.
        /// </summary>
        public static string ScanCard(string id, string fallback)
        {
            string[] row = Load() ? scans.Find(r => r.Length >= 6 && r[0] == id) : null;
            if (row == null)
            {
                Debug.LogWarning($"[DemoPlan] no scan {id} in §9 of {PlanFile}: the builder uses '{fallback}'");
                return fallback;
            }
            string caption = Caption(id, "");
            return $"PLACEHOLDER ({id}): {Words(row[4]).TrimEnd('.')}." + (caption.Length > 0 ? " " + caption : "");
        }

        /// <summary>Every key the code asks for exists (DemoPlanTests).</summary>
        public static bool HasSign(string where) => Load() && signs.Exists(r => r.Length >= 2 && r[0].StartsWith(where));
        public static bool HasLog(string id) => Load() && logs.Exists(r => r.Length >= 4 && r[0] == id);
        public static bool HasScan(string id) => Load() && scans.Exists(r => r.Length >= 6 && r[0] == id);
    }
}
#endif
