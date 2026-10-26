using UnityEngine;

namespace GlorifiedBody.Audio
{
    /// <summary>
    /// The ears of the game are on Lucy, not on the fixed camera (the audio sweep, 2026-10-26: a camera cut moved the
    /// listener metres away and could swap left and right). The listener sits at her head and turns with the active
    /// camera, so the left of the screen is the left ear. A room with no Lucy (a menu) keeps the camera's listener.
    /// </summary>
    public sealed class ListenerRig : MonoBehaviour
    {
        public const float HeadHeight = 1.6f;
        private const float CheckSeconds = 0.5f;

        private AudioListener listener;
        private Transform lucy;
        private float nextCheck;

        public AudioListener Listener => listener;
        public bool OnLucy => listener != null && listener.enabled && lucy != null;

        private void Awake()
        {
            listener = gameObject.AddComponent<AudioListener>();
            listener.enabled = false;
        }

        /// <summary>A new room: find Lucy again and take the ears from the cameras.</summary>
        public void Rebind()
        {
            // At once, in the load: two listeners for a frame log a warning each frame.
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            lucy = player != null ? player.transform : null;
            SetEars();
            nextCheck = Time.unscaledTime + CheckSeconds;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + CheckSeconds;
                if (lucy == null)
                {
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    lucy = player != null ? player.transform : null;
                }
                SetEars();
            }
            if (lucy == null) return;
            Camera cam = Camera.main;
            float yaw = cam != null ? cam.transform.eulerAngles.y : lucy.eulerAngles.y;
            transform.SetPositionAndRotation(lucy.position + Vector3.up * HeadHeight, Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>One listener: hers when she is in the room, else the camera's.</summary>
        private void SetEars()
        {
            bool mine = lucy != null;
            foreach (AudioListener other in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (other == listener) continue;
                if (mine && other.enabled) other.enabled = false;
            }
            if (listener.enabled != mine) listener.enabled = mine;
            if (!mine)
            {
                // A menu: give the ears back to a camera if none has them.
                bool any = false;
                foreach (AudioListener other in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (other != listener && other.enabled) any = true;
                if (!any && Camera.main != null && Camera.main.TryGetComponent(out AudioListener camEars)) camEars.enabled = true;
            }
        }
    }
}
