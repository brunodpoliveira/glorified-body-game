using System;
using UnityEngine;
using UnityEngine.Video;

namespace GlorifiedBody.Story
{
    /// <summary>
    /// A cutscene (design §1.3: "Nano Banana Pro stills with voice acting on top, some animated cuts on Seedance"): its
    /// shots in order. A shot is a still with a slow pan and zoom, or a video clip. It carries one spoken line (the
    /// speaker's name, the voice id, the words): the line plays Resources/Voice/&lt;id&gt; when it is recorded, and its
    /// subtitle shows for the shot. A shot lasts its seconds, or its clip or its line when longer. Its own music, or
    /// silence. Its done flag goes up at the end (a save keeps it).
    /// </summary>
    [CreateAssetMenu(fileName = "cutscene", menuName = "GlorifiedBody/Cutscene")]
    public sealed class CutsceneAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Shot
        {
            public Texture2D still;
            public VideoClip video;
            [Min(0.5f)] public float seconds = 4f;
            [Tooltip("The still's view at the start and at the end: x, y the corner of the view, z its zoom (1 = all).")]
            public Vector3 panFrom = new Vector3(0f, 0f, 1f);
            public Vector3 panTo = new Vector3(0.04f, 0.02f, 1.08f);
            public string speaker = "";
            public string lineId = "";
            [TextArea] public string text = "";
        }

        public string cutsceneId = "";
        public Shot[] shots = new Shot[0];
        public AudioClip music;
        [Tooltip("Raised at the end, or when skipped (empty: \"cutscene_\" + the id).")]
        public string doneFlag = "";

        public string DoneFlag => string.IsNullOrEmpty(doneFlag) ? "cutscene_" + cutsceneId : doneFlag;
    }
}
