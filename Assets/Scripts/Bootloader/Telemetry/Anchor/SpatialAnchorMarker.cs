using Telemetry.SceneState;
using UnityEngine;

namespace Telemetry.Spatial
{
    public class SpatialAnchorMarker : MonoBehaviour
    {
        private const string LogPrefix = "[SpatialAnchorMarker]";

        [Header("References")]
        [SerializeField] private RoomStateProvider roomStateProvider;

        [Header("Anchor Naming")]
        [Tooltip("Opcjonalna rêczna nazwa. Jeœli puste, zostanie u¿yte Anchor_Room_{roomId}.")]
        [SerializeField] private string manualAnchorName = "";

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private string previewAnchorName;
        [SerializeField] private int previewRoomId;
        [SerializeField] private string previewSceneName;

        public int RoomId =>
            roomStateProvider != null
                ? roomStateProvider.CurrentRoomId
                : -1;

        public string SceneName => gameObject.scene.name;

        public string AnchorName =>
            !string.IsNullOrWhiteSpace(manualAnchorName)
                ? manualAnchorName
                : $"Anchor_Room_{RoomId}";

        public Vector3 Position => transform.position;

        public Quaternion Rotation => transform.rotation;

        private void Awake()
        {
            ResolveReferences();
            RefreshPreviewFields();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshPreviewFields();
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnValidate()
        {
            RefreshPreviewFields();
        }

        private void ResolveReferences()
        {
            if (roomStateProvider != null)
                return;

            roomStateProvider = GetComponentInParent<RoomStateProvider>();

            if (roomStateProvider == null)
                roomStateProvider = FindObjectOfType<RoomStateProvider>();
        }

        private void Register()
        {
            if (SpatialAnchorRegistry.Instance == null)
            {
                LogWarning("Cannot register anchor. SpatialAnchorRegistry.Instance is NULL.");
                return;
            }

            SpatialAnchorRegistry.Instance.RegisterAnchor(this);

            Log(
                $"Registered | " +
                $"Name={AnchorName} | " +
                $"Room={RoomId} | " +
                $"Scene={SceneName} | " +
                $"Position={Position}");
        }

        private void Unregister()
        {
            if (SpatialAnchorRegistry.Instance == null)
            {
                LogWarning("Cannot unregister anchor. SpatialAnchorRegistry.Instance is NULL.");
                return;
            }

            SpatialAnchorRegistry.Instance.UnregisterAnchor(this);

            Log(
                $"Unregistered from active anchors | " +
                $"Name={AnchorName} | " +
                $"Room={RoomId} | " +
                $"Scene={SceneName}");
        }

        private void RefreshPreviewFields()
        {
            previewAnchorName = AnchorName;
            previewRoomId = RoomId;
            previewSceneName = SceneName;
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
                return;

            Debug.Log($"{LogPrefix} {message}");
        }

        private void LogWarning(string message)
        {
            if (!enableDebugLogs)
                return;

            Debug.LogWarning($"{LogPrefix} {message}");
        }
    }
}