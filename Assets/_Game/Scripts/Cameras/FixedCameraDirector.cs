using System;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Cameras
{
    /// <summary>
    /// Owns Camera.main in a room. Every frame it checks which zones contain the player, picks the winning zone
    /// (priority, then the zone entered last), and cuts the camera to that zone's FixedCamera. No zone = the
    /// room's default camera. A cut is instant, as in Galerians: the plate changes, the input does not.
    /// </summary>
    public sealed class FixedCameraDirector : MonoBehaviour
    {
        public static FixedCameraDirector Instance { get; private set; }

        private static readonly List<CameraZone> zones = new List<CameraZone>();

        [SerializeField] private FixedCamera defaultCamera;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private string playerTag = "Player";

        private readonly List<CameraZone> activeZones = new List<CameraZone>();
        private Transform player;
        private Quaternion baseRotation;

        public FixedCamera Current { get; private set; }
        public string CurrentName => Current != null ? Current.cameraName : "none";
        public FixedCamera DefaultCamera => defaultCamera;
        public IReadOnlyList<CameraZone> ActiveZones => activeZones;

        public event Action<FixedCamera> OnCameraChanged;

        public static void RegisterZone(CameraZone zone)
        {
            if (zone != null && !zones.Contains(zone)) zones.Add(zone);
        }

        public static void UnregisterZone(CameraZone zone)
        {
            zones.Remove(zone);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            zones.Clear();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            UpdateZones();
            Resolve(force: true);
        }

        public void SetDefaultCamera(FixedCamera camera)
        {
            defaultCamera = camera;
        }

        /// <summary>Checks the zones and applies the camera again, even when nothing changed.</summary>
        public void ForceResolve()
        {
            UpdateZones();
            Resolve(force: true);
        }

        /// <summary>
        /// Holds one camera whatever the zones say (the look test bot shoots every camera). Null releases it.
        /// </summary>
        public void Override(FixedCamera camera)
        {
            overrideCamera = camera;
            Resolve(force: true);
        }

        private FixedCamera overrideCamera;

        private void Update()
        {
            if (UpdateZones()) Resolve(force: false);
        }

        /// <summary>Keeps activeZones equal to the zones that contain the player. Returns true on a change.</summary>
        private bool UpdateZones()
        {
            if (player == null)
            {
                GameObject playerObject = GameObject.FindWithTag(playerTag);
                player = playerObject != null ? playerObject.transform : null;
                if (player == null) return false;
            }

            Vector3 position = player.position;
            bool changed = false;
            for (int i = activeZones.Count - 1; i >= 0; i--)
            {
                CameraZone zone = activeZones[i];
                if (zone == null || !zone.isActiveAndEnabled || !zone.Contains(position))
                {
                    activeZones.RemoveAt(i);
                    changed = true;
                }
            }

            for (int i = 0; i < zones.Count; i++)
            {
                CameraZone zone = zones[i];
                if (zone == null || !zone.isActiveAndEnabled || activeZones.Contains(zone)) continue;
                if (zone.Contains(position))
                {
                    activeZones.Add(zone);
                    changed = true;
                }
            }

            return changed;
        }

        private void Resolve(bool force)
        {
            CameraZone best = null;
            for (int i = 0; i < activeZones.Count; i++)
            {
                CameraZone zone = activeZones[i];
                if (zone == null || zone.fixedCamera == null) continue;
                if (best == null || zone.priority >= best.priority) best = zone;
            }

            FixedCamera wanted = overrideCamera != null ? overrideCamera : best != null ? best.fixedCamera : defaultCamera;
            if (!force && wanted == Current) return;

            Current = wanted;
            Apply();
            RunTelemetry.Event("camera_cut", CurrentName);
            OnCameraChanged?.Invoke(Current);
        }

        private void Apply()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null || Current == null) return;
            targetCamera.transform.SetPositionAndRotation(Current.transform.position, Current.transform.rotation);
            targetCamera.fieldOfView = Current.fieldOfView;
            baseRotation = Current.transform.rotation;
            GlorifiedBody.Rendering.ShadowDistance.Set(Current.shadowDistance);
        }

        private void LateUpdate()
        {
            if (Current == null || !Current.followPlayerYaw || targetCamera == null || player == null) return;
            Vector3 to = player.position - targetCamera.transform.position;
            Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
            Quaternion clamped = Quaternion.RotateTowards(baseRotation, look, Current.followMaxYaw);
            targetCamera.transform.rotation = Quaternion.RotateTowards(targetCamera.transform.rotation, clamped, Current.followSpeed * Time.deltaTime);
        }
    }
}
