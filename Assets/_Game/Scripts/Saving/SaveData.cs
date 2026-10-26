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
