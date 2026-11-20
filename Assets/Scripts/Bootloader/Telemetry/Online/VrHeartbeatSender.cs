using System.Collections;
using Telemetry.SceneState;
using Telemetry.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Telemetry.Online
{
    public class VrHeartbeatSender : MonoBehaviour
    {
        private const string LogPrefix = "[VR_HEARTBEAT]";

        [Header("References")]
        [SerializeField] private VrApiClient apiClient;

        [Header("Settings")]
        [SerializeField] private float intervalSeconds = 2f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;
        [SerializeField] private bool isSending;
        [SerializeField] private int backendSessionId = -1;

        private Coroutine heartbeatCoroutine;

        private void Awake()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (apiClient == null)
                apiClient = GetComponent<VrApiClient>();

            if (apiClient == null)
                apiClient = FindObjectOfType<VrApiClient>();
        }

        public void StartHeartbeat(int sessionId)
        {
            StopHeartbeat();

            if (sessionId <= 0)
            {
                Debug.LogWarning($"{LogPrefix} Cannot start heartbeat. Invalid sessionId={sessionId}");
                return;
            }

            ResolveReferences();

            if (apiClient == null)
            {
                Debug.LogWarning($"{LogPrefix} Cannot start heartbeat. VrApiClient not found.");
                return;
            }

            backendSessionId = sessionId;
            isSending = true;
            heartbeatCoroutine = StartCoroutine(HeartbeatRoutine());

            Debug.Log($"{LogPrefix} Started. session_id={backendSessionId}");
        }

        public void StopHeartbeat()
        {
            if (heartbeatCoroutine != null)
            {
                StopCoroutine(heartbeatCoroutine);
                heartbeatCoroutine = null;
            }

            isSending = false;
            backendSessionId = -1;

            if (debugLogs)
                Debug.Log($"{LogPrefix} Stopped.");
        }

        private IEnumerator HeartbeatRoutine()
        {
            while (backendSessionId > 0)
            {
                ResolveReferences();

                if (apiClient == null)
                {
                    Debug.LogWarning($"{LogPrefix} VrApiClient not found.");
                    yield return new WaitForSeconds(intervalSeconds);
                    continue;
                }

                int sessionIdForRequest = backendSessionId;
                HeartbeatRequest requestBody = BuildHeartbeatRequest();

                bool requestOk = false;
                string responseOrError = "";

                yield return apiClient.SendHeartbeat(
                    sessionIdForRequest,
                    requestBody,
                    (success, result) =>
                    {
                        requestOk = success;
                        responseOrError = result;
                    });

                if (!requestOk)
                {
                    Debug.LogWarning(
                        $"{LogPrefix} Failed. session_id={sessionIdForRequest}, error={responseOrError}");
                }
                else if (debugLogs)
                {
                    Debug.Log(
                        $"{LogPrefix} OK. session_id={sessionIdForRequest}, scene={requestBody.current_scene}, room={requestBody.current_room_id}");
                }

                yield return new WaitForSeconds(intervalSeconds);
            }

            isSending = false;
        }

        private HeartbeatRequest BuildHeartbeatRequest()
        {
            TelemetrySessionManager manager = TelemetrySessionManager.Instance;

            float timestamp = Time.time;
            bool recording = false;

            if (manager != null)
            {
                recording = manager.IsRecording;

                if (manager.Clock != null && manager.Clock.IsRunning)
                    timestamp = (float)manager.Clock.CurrentTimeSeconds;
            }

            int currentRoomId = -1;

            RoomStateProvider roomStateProvider = FindObjectOfType<RoomStateProvider>();
            if (roomStateProvider != null)
                currentRoomId = roomStateProvider.CurrentRoomId;

            return new HeartbeatRequest
            {
                timestamp = timestamp,
                current_room_id = currentRoomId,
                current_scene = SceneManager.GetActiveScene().name,
                recording = recording,
                tracking = new HeartbeatTracking
                {
                    face_tracking_available = false,
                    eye_tracking_available = false,
                    physics_tracking_active = recording
                }
            };
        }
    }
}