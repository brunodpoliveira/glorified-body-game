using System.IO;
using GlorifiedBody.Editor.Demo;
using UnityEditor;
using UnityEngine;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The writer's police bulletin in the game (round 138): game/lines/T11_bulletin.md is copied to
    /// Resources/Writer/T11_bulletin.txt, which Story.Bulletin reads (a player build has no access to the writers' folder).
    /// Run it after the writer changes the file: tools/unity.sh exec GlorifiedBody.Editor.BulletinSetup.Build
    /// DemoWorld.BuildAll runs it too.
    /// </summary>
    public static class BulletinSetup
    {
        public const string ResourceFile = "Assets/_Game/Resources/Writer/T11_bulletin.txt";
        public static string WriterFile => Path.GetFullPath(DemoScripts.WriterGame + "/lines/T11_bulletin.md");

        public static void Build()
        {
            if (!File.Exists(WriterFile))
            {
                Debug.LogWarning("[BulletinSetup] no writer file " + WriterFile + ": the game keeps " + ResourceFile);
                return;
            }
            string text = File.ReadAllText(WriterFile);
            Directory.CreateDirectory(Path.GetDirectoryName(ResourceFile));
            bool same = File.Exists(ResourceFile) && File.ReadAllText(ResourceFile) == text;
            if (!same) File.WriteAllText(ResourceFile, text);
            AssetDatabase.ImportAsset(ResourceFile);
            Debug.Log($"[BulletinSetup] {WriterFile} {(same ? "(no change)" : "copied")} to {ResourceFile}");
        }
    }
}
