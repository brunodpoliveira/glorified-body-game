using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace GlorifiedBody.Subtitles
{
    /// <summary>
    /// The subtitle track of a cutscene (Bruno, 2026-10-25: subtitles "both in-game and in cutscenes"). Each clip is
    /// one line with its speaker; the line shows for the clip's time on the same subtitle overlay as the game lines.
    /// </summary>
    [TrackColor(1f, 0.9f, 0.1f)]
    [TrackClipType(typeof(SubtitleClip))]
    public sealed class SubtitleTrack : TrackAsset
    {
    }

    /// <summary>Shows the clip's line while the clip plays, and hides it after.</summary>
    public sealed class SubtitleBehaviour : PlayableBehaviour
    {
        public string Key;
        public string Speaker;
        public string Line;

        public override void OnBehaviourPlay(Playable playable, FrameData info) => SubtitleService.Show(Key, Speaker, Line);

        public override void OnBehaviourPause(Playable playable, FrameData info) => SubtitleService.Hide(Key);

        public override void OnPlayableDestroy(Playable playable) => SubtitleService.Hide(Key);
    }
}
