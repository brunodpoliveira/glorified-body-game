using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Destruction;
using GlorifiedBody.Editor.Destruction;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>
    /// The breakable cover's editor side and its rules (2026-10-24): the box cut gives closed convex pieces that
    /// fill the box, the same seed gives the same pieces, the stored piece assets match, the Debris layer exists,
    /// the effect settings have their sounds, and the room ledger keeps a broken room when she leaves and comes
    /// back but drops it on a restart (Bruno: "broken room stays broken").
    /// </summary>
    public class BreakableAssetsTests
    {
        [Test]
        public void BoxFracture_PiecesFillTheBox_AreClosed_AndFaceOut()
        {
            var size = new Vector3(1.6f, 0.05f, 0.8f);
            List<BoxFracture.Piece> pieces = BoxFracture.Split(size, 8, 11, "test");
            Assert.AreEqual(8, pieces.Count, "eight pieces");
            float volume = 0f;
            foreach (BoxFracture.Piece p in pieces)
            {
                volume += p.volume;
                Mesh mesh = p.mesh;
                Assert.AreEqual(2, mesh.subMeshCount, "outside and inside");
                Assert.Greater(mesh.GetTriangles(1).Length, 0, $"{mesh.name} has a cut face");
                Vector3[] v = mesh.vertices;
                Vector3[] n = mesh.normals;
                for (int s = 0; s < 2; s++)
                {
                    int[] t = mesh.GetTriangles(s);
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                        if (face.sqrMagnitude < 1e-12f) continue;
                        Assert.Greater(Vector3.Dot(face, n[t[i]]), 0f, $"{mesh.name} triangle {i / 3} faces out");
                        // Convex: every vertex lies behind every face plane.
                        foreach (Vector3 q in v) Assert.LessOrEqual(Vector3.Dot(q - v[t[i]], face.normalized), 1e-4f, $"{mesh.name} is convex");
                    }
                }
            }
            Assert.AreEqual(size.x * size.y * size.z, volume, size.x * size.y * size.z * 0.01f, "the pieces fill the box");
            List<BoxFracture.Piece> again = BoxFracture.Split(size, 8, 11, "test");
            for (int i = 0; i < pieces.Count; i++) Assert.AreEqual(pieces[i].centre, again[i].centre, "the same seed, the same pieces");
            foreach (BoxFracture.Piece p in pieces) Object.DestroyImmediate(p.mesh);
            foreach (BoxFracture.Piece p in again) Object.DestroyImmediate(p.mesh);
        }

        [Test]
        public void TheAssets_Layer_Pieces_AndSounds_AreInPlace()
        {
            Assert.AreEqual(BreakableAssets.DebrisLayerIndex, LayerMask.NameToLayer(Breakables.DebrisLayerName), "the Debris layer in slot 19");
            foreach (string design in new[] { "table_top", "barricade_panel", "lamp_panel", "plaster_patch" })
            {
                Object[] stored = AssetDatabase.LoadAllAssetsAtPath($"{BreakableAssets.FragmentsFolder}/{design}.asset");
                Assert.Greater(stored.Length, 3, $"{design}: the pieces are stored");
            }
            var fx = AssetDatabase.LoadAssetAtPath<DestructionFxSettings>(BreakableAssets.FxPath);
            Assert.IsNotNull(fx, "the effect settings");
            Assert.IsNotNull(fx.woodBreak);
            Assert.IsNotNull(fx.glassBreak);
            Assert.IsNotNull(fx.plasterBreak);
            Assert.IsNotNull(fx.sparks);
            Assert.IsNotNull(fx.dust);
            Assert.IsNotNull(fx.glow);
        }

        [Test]
        public void Ledger_KeepsTheRoom_WhenSheLeaves_DropsItOnARestart()
        {
            RoomDamageLedger.Enter(RoomDamageLedger.MenuScene);
            Assert.AreEqual(0, RoomDamageLedger.BrokenIn("RoomA"));
            RoomDamageLedger.Enter("RoomA");
            var obj = new GameObject("Table_X");
            try
            {
                // The ledger keys by the object's scene: an object made in EditMode sits in the open scene.
                string scene = obj.scene.name;
                RoomDamageLedger.Enter(scene);
                var b = obj.AddComponent<Breakable>();
                RoomDamageLedger.NoteBroken(b);
                RoomDamageLedger.Enter(scene); // a restart: the room as she found it
                Assert.IsNull(RoomDamageLedger.Find(b), "a restart drops the damage of the visit");
                RoomDamageLedger.NoteBroken(b);
                RoomDamageLedger.Enter("Elsewhere"); // she leaves
                Assert.IsNotNull(RoomDamageLedger.Find(b), "the room keeps what broke");
                Assert.IsTrue(RoomDamageLedger.Find(b).broken);
                RoomDamageLedger.Enter(scene); // she comes back: still broken
                Assert.IsTrue(RoomDamageLedger.Find(b).broken, "still broken when she comes back");
                RoomDamageLedger.Enter(RoomDamageLedger.MenuScene);
                Assert.IsNull(RoomDamageLedger.Find(b), "the main menu ends the session");
            }
            finally
            {
                Object.DestroyImmediate(obj);
                RoomDamageLedger.Clear();
            }
        }
    }
}
