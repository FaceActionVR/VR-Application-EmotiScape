using System;
using System.Collections.Generic;
using UnityEngine;

namespace Telemetry.Spatial
{
    public class SpatialAnchorRegistry : MonoBehaviour
    {
        private const string LogPrefix = "[SpatialAnchorRegistry]";

        public static SpatialAnchorRegistry Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private int activeAnchorCount;
        [SerializeField] private int sessionAnchorCount;

        private readonly List<SpatialAnchorMarker> registeredAnchors = new List<SpatialAnchorMarker>();
        private readonly Dictionary<string, SpatialAnchorSnapshot> sessionAnchors =
            new Dictionary<string, SpatialAnchorSnapshot>();

        public IReadOnlyList<SpatialAnchorMarker> RegisteredAnchors => registeredAnchors;
        public IReadOnlyCollection<SpatialAnchorSnapshot> SessionAnchors => sessionAnchors.Values;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"{LogPrefix} Duplicate instance found. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void RegisterAnchor(SpatialAnchorMarker anchor)
        {
            if (anchor == null)
                return;

            if (!registeredAnchors.Contains(anchor))
                registeredAnchors.Add(anchor);

            AddOrUpdateSessionSnapshot(anchor);

            RefreshDebugCounts();

            Log(
                $"Anchor registered | " +
                $"Name={anchor.AnchorName} | " +
                $"Room={anchor.RoomId} | " +
                $"Scene={anchor.SceneName} | " +
                $"Active={registeredAnchors.Count} | " +
                $"Session={sessionAnchors.Count}");
        }

        public void UnregisterAnchor(SpatialAnchorMarker anchor)
        {
            if (anchor == null)
                return;

            registeredAnchors.Remove(anchor);

            // Wa¿ne:
            // Nie usuwamy anchora z sessionAnchors.
            // sessionAnchors to pamiêæ wszystkich anchorów widzianych podczas sesji.
            // Dziêki temu session_meta.json mo¿e zawieraæ anchory z poprzednich scen.
            RefreshDebugCounts();

            Log(
                $"Anchor unregistered from active list only | " +
                $"Name={anchor.AnchorName} | " +
                $"Room={anchor.RoomId} | " +
                $"Scene={anchor.SceneName} | " +
                $"Active={registeredAnchors.Count} | " +
                $"Session={sessionAnchors.Count}");
        }

        public void ClearActiveAnchors()
        {
            registeredAnchors.Clear();
            RefreshDebugCounts();

            Log("Active anchors cleared.");
        }

        public void ClearSessionAnchors()
        {
            sessionAnchors.Clear();
            RefreshDebugCounts();

            Log("Session anchor snapshots cleared.");
        }

        public void ClearAll()
        {
            registeredAnchors.Clear();
            sessionAnchors.Clear();
            RefreshDebugCounts();

            Log("All anchors cleared.");
        }

        private void AddOrUpdateSessionSnapshot(SpatialAnchorMarker anchor)
        {
            string key = BuildKey(anchor);

            sessionAnchors[key] = new SpatialAnchorSnapshot
            {
                room_id = anchor.RoomId,
                anchor_name = anchor.AnchorName,
                position = anchor.Position,
                rotation = anchor.Rotation,
                scene_name = anchor.SceneName
            };
        }

        private string BuildKey(SpatialAnchorMarker anchor)
        {
            string sceneName = string.IsNullOrWhiteSpace(anchor.SceneName)
                ? "unknown_scene"
                : anchor.SceneName;

            string anchorName = string.IsNullOrWhiteSpace(anchor.AnchorName)
                ? "unknown_anchor"
                : anchor.AnchorName;

            return $"{sceneName}|{anchor.RoomId}|{anchorName}";
        }

        private void RefreshDebugCounts()
        {
            activeAnchorCount = registeredAnchors.Count;
            sessionAnchorCount = sessionAnchors.Count;
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
                return;

            Debug.Log($"{LogPrefix} {message}");
        }
    }

    [Serializable]
    public class SpatialAnchorSnapshot
    {
        public int room_id;
        public string anchor_name;
        public Vector3 position;
        public Quaternion rotation;
        public string scene_name;
    }
}