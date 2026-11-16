using System.Collections;
using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A thing on the only way on that falls out of the world (Bruno, 2026-11-16: "the player can softlock themselves by letting
    /// the gurney drop out of the playable area"). When it falls this far under the floor where it stood, while she is on the
    /// side it must carry her from and the other way is still shut, a cutscene plays, and then the thing is back where it stood
    /// at the start and she stands at herSpot. No death (Bruno, the same night: "the player in the actual demo won't die if they
    /// softlock, but will get a special cutscene"; his pick: "the gurney comes back"). Her kills and items stay. On the gurney
    /// of the Hold (DemoHold.Gurney).
    /// </summary>
    public sealed class LostProp : MonoBehaviour
    {
        [Tooltip("Lost when it is this many metres under the floor where it stood at the start.")]
        [Min(0.5f)] public float fallen = 2f;
        [Tooltip("The cutscene starts this many seconds after: the player sees it fall.")]
        [Min(0f)] public float delay = 1.2f;
        [Tooltip("Nothing happens when this flag is up: the other way is open (the Hold: EXTEND, the tube out).")]
        public string unlessFlag = "";
        [Tooltip("Only while she is in one of these rooms (ids, commas): the side it must carry her from.")]
        public string strandedIn = "";
        [Tooltip("Played in place of the death (a PLACEHOLDER until the writer makes it).")]
        public CutsceneAsset cutscene;
        [Tooltip("Where she stands after the cutscene.")]
        public Transform herSpot;

        public bool Lost { get; private set; }
        public int Returns { get; private set; }

        private float floorY;
        private Vector3 startAt;
        private Quaternion startTurn;

        private void Awake()
        {
            floorY = transform.position.y;
            startAt = transform.position;
            startTurn = transform.rotation;
        }

        private void Update()
        {
            if (Lost || transform.position.y > floorY - fallen) return;
            Lost = true;
            RunTelemetry.Event("prop_lost", name);
            StartCoroutine(Recover());
        }

        private IEnumerator Recover()
        {
            yield return new WaitForSeconds(delay);
            GameObject her = GameObject.FindWithTag("Player");
            Health health = her != null ? her.GetComponentInChildren<Health>() : null;
            bool stranded = health != null && !health.IsDead && (string.IsNullOrEmpty(unlessFlag) || !StoryFlags.Has(unlessFlag)) && InStrandedRoom(her.transform.position);
            if (!stranded)
            {
                gameObject.SetActive(false); // it fell into the dark and she needs it no more: no more physics for it
                yield break;
            }
            RunTelemetry.Event("softlock", name);
            if (cutscene != null && CutscenePlayer.Play(cutscene))
                while (CutscenePlayer.IsPlaying) yield return null;
            // Back where it stood, at rest, out of any hold; she stands at her spot.
            var lift = GetComponent<GlorifiedBody.Combat.TelekinesisLift>();
            if (lift != null) Destroy(lift);
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = startAt;
                body.rotation = startTurn;
            }
            transform.SetPositionAndRotation(startAt, startTurn);
            Physics.SyncTransforms();
            if (herSpot != null) her.GetComponent<GlorifiedBody.Player.TankController>()?.Teleport(herSpot.position, herSpot.eulerAngles.y);
            Returns++;
            Lost = false;
        }

        private bool InStrandedRoom(Vector3 at)
        {
            if (string.IsNullOrEmpty(strandedIn)) return true;
            GlorifiedBody.Rooms.Room room = GlorifiedBody.Rooms.Room.At(at + Vector3.up * 0.1f);
            if (room == null) return false;
            foreach (string id in strandedIn.Split(','))
                if (id.Trim() == room.id) return true;
            return false;
        }
    }
}
