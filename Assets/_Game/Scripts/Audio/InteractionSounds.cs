using UnityEngine;

namespace GlorifiedBody.Audio
{
    /// <summary>
    /// The sounds of the E and F keys and the saves (the audio sweep: all silent): the result strings of the Interactor,
    /// the PsychicScanner and the SaveSystem map to a Sfx event. Placeholders.
    /// </summary>
    public static class InteractionSounds
    {
        /// <summary>An Interactor or PsychicScanner result ("taken:...", "read:...", "image:...") at her place.</summary>
        public static void OnResult(string what, Vector3 at)
        {
            if (string.IsNullOrEmpty(what)) return;
            Vector3 chest = at + Vector3.up * 1.2f;
            if (what.StartsWith("taken:")) Sfx.Play(Sfx.Pickup, chest, 0.8f);
            else if (what.StartsWith("read:")) Sfx.Ui(Sfx.LogOpen, 0.7f);
            else if (what.StartsWith("closed:")) Sfx.Ui(Sfx.LogClose, 0.7f);
            else if (what.StartsWith("full:")) Sfx.Ui(Sfx.Refuse, 0.6f);
            else if (what.StartsWith("left:")) Sfx.Ui(Sfx.UiBack, 0.5f);
            else if (what.StartsWith("ask:")) Sfx.Ui(Sfx.UiMove, 0.4f);
            else if (what.StartsWith("locked:")) Sfx.Play(Sfx.DoorLocked, chest, 0.9f);
            else if (what.StartsWith("image:") || what.StartsWith("video:")) Sfx.Ui(Sfx.Scan, 0.7f);
        }
    }
}
