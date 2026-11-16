using System.Collections;
using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A thing on the only way on that falls out of the world ends the game (Bruno, 2026-11-16: "the player can softlock
    /// themselves by letting the gurney drop out of the playable area. we can check this and give an unique game over (with an
    /// unique death screen and a hint about softlocking)"). When it falls this far under the floor where it stood, while she
    /// is on the side it must carry her from and the other way is still shut, she dies with its own killer id: the death
    /// screen shows the softlock and the hint. On the gurney of the Hold (DemoHold.Gurney).
    /// </summary>
    public sealed class LostProp : MonoBehaviour
    {
        public const string GurneyKillerId = "softlock_gurney";

        public string killerId = GurneyKillerId;
        [Tooltip("Lost when it is this many metres under the floor where it stood at the start.")]
        [Min(0.5f)] public float fallen = 2f;
        [Tooltip("The game ends this many seconds after: the player sees it fall.")]
        [Min(0f)] public float delay = 1.2f;
        [Tooltip("No game over when this flag is up: the other way is open (the Hold: EXTEND, the tube out).")]
        public string unlessFlag = "";
        [Tooltip("The game ends only while she is in one of these rooms (ids, commas): the side it must carry her from.")]
        public string strandedIn = "";

        public bool Lost { get; private set; }

        private float floorY;

        private void Awake() => floorY = transform.position.y;

        private void Update()
        {
            if (Lost || transform.position.y > floorY - fallen) return;
            Lost = true;
            RunTelemetry.Event("prop_lost", name);
            StartCoroutine(EndTheGame());
        }

        private IEnumerator EndTheGame()
        {
            yield return new WaitForSeconds(delay);
            GameObject her = GameObject.FindWithTag("Player");
            Health health = her != null ? her.GetComponentInChildren<Health>() : null;
            GameManager gm = GameManager.Instance;
            bool stranded = health != null && !health.IsDead && (gm == null || gm.IsPlaying)
                            && (string.IsNullOrEmpty(unlessFlag) || !StoryFlags.Has(unlessFlag)) && InStrandedRoom(her.transform.position);
            if (stranded)
            {
                RunTelemetry.Event("softlock", $"{name}:{killerId}");
                GlorifiedBody.UI.DeathMenuUI.LastKillerId = killerId;
                health.Kill();
            }
            gameObject.SetActive(false); // it fell into the dark: no more physics for it
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
