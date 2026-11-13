using System.IO;
using GlorifiedBody.Editor.Demo;
using GlorifiedBody.Enemy;
using UnityEditor;
using UnityEngine;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The writer's barks in the game (2026-11-13, rounds 133 to 135). The writer's file is game/barks.csv in the writers'
    /// folder (DemoScripts.WriterGame). Build copies it to Resources/Barks/barks.csv, which BarkBook reads in play (a player
    /// build has no access to the writers' folder). Run it after the writer changes the file:
    ///   tools/unity.sh exec GlorifiedBody.Editor.BarkBookSetup.Build
    /// DemoWorld.BuildAll runs it too. The room builders take their scripted lines with Scripted.
    /// </summary>
    public static class BarkBookSetup
    {
        public const string ResourceFile = "Assets/_Game/Resources/Barks/barks.csv";
        public static string WriterFile => Path.GetFullPath(DemoScripts.WriterGame + "/barks.csv");

        public static void Build()
        {
            string from = WriterFile;
            if (!File.Exists(from))
            {
                Debug.LogWarning("[BarkBookSetup] no writer file " + from + ": the game keeps " + ResourceFile);
                return;
            }
            string text = File.ReadAllText(from);
            if (!text.StartsWith(BarkBook.Header)) throw new System.Exception("[BarkBookSetup] the header of " + from + " is not '" + BarkBook.Header + "'");
            Directory.CreateDirectory(Path.GetDirectoryName(ResourceFile));
            bool same = File.Exists(ResourceFile) && File.ReadAllText(ResourceFile) == text;
            if (!same) File.WriteAllText(ResourceFile, text);
            AssetDatabase.ImportAsset(ResourceFile);
            BarkBook.LoadText(text, from);
            Debug.Log($"[BarkBookSetup] {BarkBook.Rows.Count} rows from {from} {(same ? "(no change)" : "copied")} to {ResourceFile}");
        }

        /// <summary>
        /// A scripted line for a room builder: the row at the index among the rows of the enemy and the situation, from the
        /// writer's file. Missing: the fallback, with a warning (the writer must add the row).
        /// </summary>
        public static string Scripted(string enemy, string situation, int index = 0, string fallback = "")
        {
            if (File.Exists(WriterFile) && BarkBook.Source != WriterFile) BarkBook.LoadText(File.ReadAllText(WriterFile), WriterFile);
            string line = BarkBook.Scripted(enemy, situation, index);
            if (!string.IsNullOrEmpty(line)) return line;
            Debug.LogWarning($"[BarkBookSetup] no row {enemy} / {situation} / {index} in barks.csv: the builder uses '{fallback}'");
            return fallback;
        }
    }
}
