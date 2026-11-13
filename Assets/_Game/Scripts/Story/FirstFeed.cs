using System.Collections;
using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Story
{
    /// <summary>
    /// Her first feed is cutscene 02b (round 118, CANON: "when the player does it for the first time, a video plays showing
    /// her feasting on a corpse and wounds healing"; the writer's 02b version 5). Two ways in:
    ///   1. After the video log L36 the feed is unlocked. The first "Feed on the body?" she takes plays the cutscene in
    ///      place of the feed in the game, and she heals to full (02b, note 5: it is a whole man).
    ///   2. The urge in the airlock H5 (StepKind.Feast): with the feed still locked, she kneels at the nearest dead body,
    ///      the cutscene plays, she heals to full, and the feed unlocks.
    /// A skip in the FMV menu does the same (02b, note 1). The done flag is in the saves. One object in the Hold holds it.
    /// </summary>
    public sealed class FirstFeed : MonoBehaviour
    {
        public const string DoneFlag = "hold_first_feed";
        public const string TapeFlag = "hold_tape_seen";

        public CutsceneAsset cutscene;

        public static int Played { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Played = 0;

        private void OnEnable() => Feeding.OnFeedStarted += OnFeed;
        private void OnDisable() => Feeding.OnFeedStarted -= OnFeed;

        private void OnFeed(Feeding feeding, FeedableBody body)
        {
            if (StoryFlags.Has(DoneFlag) || !StoryFlags.Has(TapeFlag) || cutscene == null) return;
            StartCoroutine(PlayOver(feeding.gameObject, body));
        }

        private IEnumerator PlayOver(GameObject her, FeedableBody body)
        {
            StoryFlags.Raise(DoneFlag);
            yield return Cut(cutscene, her, body);
        }

        /// <summary>The urge (StepKind.Feast): the nearest dead body, the cutscene, the heal, the unlock.</summary>
        public static IEnumerator Feast(CutsceneAsset asset, GameObject her)
        {
            if (her == null) yield break;
            FeedableBody body = Nearest(her.transform.position);
            StoryFlags.Raise(DoneFlag);
            if (body != null)
            {
                Vector3 to = body.Point - her.transform.position;
                to.y = 0f;
                Vector3 at = body.Point - (to.sqrMagnitude > 0.01f ? to.normalized : her.transform.forward) * 0.9f;
                TankController tank = her.GetComponent<TankController>();
                if (tank != null && to.sqrMagnitude > 0.01f) tank.Teleport(at, Quaternion.LookRotation(to).eulerAngles.y);
            }
            yield return Cut(asset, her, body);
            StoryFlags.Raise(Feeding.UnlockFlag);
        }

        private static IEnumerator Cut(CutsceneAsset asset, GameObject her, FeedableBody body)
        {
            if (asset != null && CutscenePlayer.Play(asset))
                while (CutscenePlayer.IsPlaying) yield return null;
            Health health = her != null ? her.GetComponentInChildren<Health>() : null;
            if (health != null && !health.IsDead) health.Heal(health.MaxHealth); // a whole man (02b, note 5)
            if (body != null) body.Take(body.Total);
            Played++;
            RunTelemetry.Event("first_feed", body != null ? body.name : "none");
        }

        /// <summary>The nearest dead body that still gives something.</summary>
        public static FeedableBody Nearest(Vector3 from)
        {
            FeedableBody best = null;
            float bestDistance = float.MaxValue;
            foreach (FeedableBody body in FindObjectsByType<FeedableBody>(FindObjectsSortMode.None))
            {
                if (body == null || !body.IsDead) continue;
                float d = (body.transform.position - from).sqrMagnitude;
                if (d < bestDistance) { best = body; bestDistance = d; }
            }
            return best;
        }
    }
}
