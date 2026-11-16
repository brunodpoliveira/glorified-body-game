using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Systems;

namespace GlorifiedBody.DebugTools
{
    /// <summary>
    /// A manual test from one room of the demo (Bruno, 2026-11-15: "get the player dragged to the dining hall so i won't have
    /// to test the whole level"). It loads the demo, skips the opening (the wake, the frame), refills her as F7 does, and puts
    /// her at the point in that room. The rooms before it stay as they are at the start: nothing there is dead or open.
    /// Dev only: the TEST ROOMS column of the main menu.
    /// </summary>
    public sealed class DevRoomStart : MonoBehaviour
    {
        public const string World = "Demo";
        public const string WakeFlag = "demo_h1_wake";

        private string roomId;
        private Vector3 local;
        private float yaw;

        /// <summary>Loads the demo and puts her in the room, at the point in plan metres (the same as DiningHallTests).</summary>
        public static void Begin(string roomId, Vector3 planPoint, float yaw)
        {
            var go = new GameObject("[DevRoomStart]");
            DontDestroyOnLoad(go);
            DevRoomStart start = go.AddComponent<DevRoomStart>();
            start.roomId = roomId;
            start.local = planPoint;
            start.yaw = yaw;
            GameManager.Instance?.LoadLevel(World);
            start.StartCoroutine(start.Run());
        }

        private IEnumerator Run()
        {
            float t0 = Time.realtimeSinceStartup;
            while (!(SceneManager.GetActiveScene().name == World && GameManager.Instance != null && GameManager.Instance.IsPlaying))
            {
                if (Time.realtimeSinceStartup - t0 > 60f) { Fail("the demo did not load"); yield break; }
                yield return null;
            }
            yield return null;
            // The opening: skip the wake, let the frame go.
            t0 = Time.realtimeSinceStartup;
            while (!(StoryFlags.Has(WakeFlag) && !CutscenePlayer.IsPlaying && Time.timeScale > 0f))
            {
                if (CutscenePlayer.IsPlaying) CutscenePlayer.Skip();
                GlorifiedBody.Interaction.UprightRestraint.FreeAll();
                if (Time.realtimeSinceStartup - t0 > 20f) { Fail("the opening did not end"); yield break; }
                yield return null;
            }
            yield return null;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            GlorifiedBody.Rooms.Room room = GlorifiedBody.Rooms.Room.Of(roomId);
            if (lucy == null || room == null) { Fail(lucy == null ? "no Lucy" : "no room " + roomId); yield break; }
            // The frame leaves her with MK 0 and SHORT 90: the F7 refill (HP, MK, SHORT 0, two drugs of each).
            DebugCheatKeys cheats = FindFirstObjectByType<DebugCheatKeys>();
            if (cheats == null || !cheats.Refill()) lucy.GetComponent<PsychicStats>()?.RefillAll();
            lucy.GetComponent<TankController>().Teleport(room.transform.position + local, yaw);
            Debug.Log($"[DevRoomStart] she starts in {roomId} at {local}.");
            Destroy(gameObject);
        }

        private void Fail(string why)
        {
            Debug.LogWarning("[DevRoomStart] " + why + ": she stays where she is.");
            Destroy(gameObject);
        }
    }
}
