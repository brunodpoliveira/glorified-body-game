using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Saving
{
    /// <summary>
    /// One save file (JsonUtility): where she is, her bars and her items, what changed in the room, the story flags.
    /// Version goes up when the shape changes; a file of another version does not load (the slot shows it).
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string scene = "";
        /// <summary>The save point or the checkpoint she saved at: she starts there.</summary>
        public string spot = "";
        /// <summary>When it was written, local time, "yyyy-MM-dd HH:mm".</summary>
        public string savedAt = "";
        public float playSeconds;
        public PlayerState player = new PlayerState();
        public RoomState room = new RoomState();
        public LedgerState ledger = new LedgerState();
        public List<string> flags = new List<string>();
    }

    [Serializable]
    public sealed class PlayerState
    {
        public float health;
        public float maxHealth;
        public float nalcon;
        public float shortBar;
        public int kills;
        /// <summary>Her exp (2026-10-26); the level comes from it. An older file has none: level 1.</summary>
        public int exp;
        public string outfit = "";
        public List<string> critical = new List<string>();
        public List<string> carried = new List<string>();
        public List<int> drugs = new List<int>();
    }

    /// <summary>
    /// The room of the save: its figures that are dead, the items taken, the doors open. Each is named by its path in
    /// the scene; the room builders give the names, so a rebuild keeps them.
    /// </summary>
    [Serializable]
    public sealed class RoomState
    {
        public string scene = "";
        public List<string> dead = new List<string>();
        public List<string> taken = new List<string>();
        public List<string> opened = new List<string>();
        /// <summary>The bodies of the dead, as they lie (Bruno, 2026-10-25: "stay on the floor"). A dead one with no body here is hidden.</summary>
        public List<BodyState> bodies = new List<BodyState>();
        /// <summary>The floor blood and the pools under the bodies (Bruno: "gore should be saved as well").</summary>
        public List<StainState> stains = new List<StainState>();
        /// <summary>The cut limbs on the floor.</summary>
        public List<LimbState> limbs = new List<LimbState>();
    }

    /// <summary>A dead body: the root, the humanoid bones (local), and the gore on it.</summary>
    [Serializable]
    public sealed class BodyState
    {
        public string path = "";
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        /// <summary>It lay still (a frozen ragdoll or the end of the death clip). Else a ragdoll falls from the pose.</summary>
        public bool settled = true;
        public Vector3 hipsLocal;
        public List<BonePose> bones = new List<BonePose>();
        /// <summary>The bones that are stumps (HumanBodyBones names): Head for a burst head, an arm for a cut.</summary>
        public List<string> stumps = new List<string>();
        /// <summary>The shield it dropped, where it lies.</summary>
        public bool shield;
        public Vector3 shieldPosition;
        public Quaternion shieldRotation = Quaternion.identity;
    }

    [Serializable]
    public sealed class BonePose
    {
        public string bone = "";
        public Quaternion local = Quaternion.identity;
    }

    [Serializable]
    public sealed class StainState
    {
        /// <summary>True for the pool under a body, false for a floor stain.</summary>
        public bool pool;
        public string material = "";
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = Vector3.one;
    }

    [Serializable]
    public sealed class LimbState
    {
        /// <summary>The body it came from and its bone (HumanBodyBones name): the piece is cut again from that body.</summary>
        public string body = "";
        public string bone = "";
        public Vector3 position;
        /// <summary>The turn of the cut bone in the piece (SeveredLimb.BoneTurn), not the piece's own turn.</summary>
        public Quaternion boneTurn = Quaternion.identity;
    }

    /// <summary>The breakage of every room she saw (RoomDamageLedger): a broken room stays broken after a load.</summary>
    [Serializable]
    public sealed class LedgerState
    {
        public List<LedgerRoom> rooms = new List<LedgerRoom>();
    }

    [Serializable]
    public sealed class LedgerRoom
    {
        public string room = "";
        public List<LedgerBreakable> breakables = new List<LedgerBreakable>();
        public List<LedgerPose> greebles = new List<LedgerPose>();
    }

    [Serializable]
    public sealed class LedgerBreakable
    {
        public string id = "";
        public bool broken;
        public float health;
        public List<LedgerPose> pieces = new List<LedgerPose>();
    }

    /// <summary>A pose, or none (has false): a piece that is gone.</summary>
    [Serializable]
    public sealed class LedgerPose
    {
        public string id = "";
        public bool has;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
    }
}
