using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace GlorifiedBody.Subtitles
{
    /// <summary>One subtitle line on a SubtitleTrack.</summary>
    [Serializable]
    public sealed class SubtitleClip : PlayableAsset, ITimelineClipAsset
    {
        public string speaker = "";
        [TextArea] public string line = "";

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<SubtitleBehaviour>.Create(graph);
            SubtitleBehaviour behaviour = playable.GetBehaviour();
            behaviour.Key = "timeline:" + GetInstanceID();
            behaviour.Speaker = speaker;
            behaviour.Line = line;
            return playable;
        }
    }
}
