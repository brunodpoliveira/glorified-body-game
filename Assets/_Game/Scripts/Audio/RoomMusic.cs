using UnityEngine;

namespace GlorifiedBody.Audio
{
    /// <summary>
    /// The music key of a room: rooms of one sector share a key, and so a track (Resources/Audio/Music/&lt;key&gt;/).
    /// In its own file since 2026-10-31: inside MusicDirector.cs the scenes kept no link to its script, and a room could
    /// load it as a missing script (OUTFIT TEST read the scene name as its key).
    /// </summary>
    public sealed class RoomMusic : MonoBehaviour
    {
        [SerializeField] private string key = "";

        public string Key
        {
            get => key;
            set => key = value ?? "";
        }
    }
}
