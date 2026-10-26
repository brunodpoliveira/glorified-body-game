using UnityEngine;

namespace GlorifiedBody.Subtitles
{
    /// <summary>
    /// The name a subtitle shows for a character (Bruno, 2026-10-25: "subtitle must always identify speaker"). The
    /// enemy factory sets it from the cast catalog (NURSE, LAB WORKER, SECURITY GUARD). Lucy is LUCY. With no
    /// Speaker, the object's own name stands in, in upper case.
    /// </summary>
    public sealed class Speaker : MonoBehaviour
    {
        public const string Lucy = "LUCY";

        [SerializeField] private string displayName = "";

        public string DisplayName
        {
            get => displayName;
            set => displayName = value ?? "";
        }

        public static string NameOf(GameObject who)
        {
            if (who == null) return SubtitleService.UnknownSpeaker;
            Speaker speaker = who.GetComponentInParent<Speaker>();
            if (speaker != null && !string.IsNullOrEmpty(speaker.displayName)) return speaker.displayName;
            if (who.CompareTag("Player") || who.transform.root.CompareTag("Player")) return Lucy;
            return who.name.Replace('_', ' ').ToUpperInvariant();
        }
    }
}
