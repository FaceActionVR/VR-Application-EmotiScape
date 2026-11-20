using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Telemetry.SceneState;
using Telemetry.Session;
using Telemetry.Writers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Telemetry.ExperimentFlow
{
    public class ExperimentSceneFlowController : MonoBehaviour
    {
        private const string LogPrefix = "[EXPERIMENT_SCENE_FLOW]";

        [Header("Scenes")]
        [SerializeField] private string mainHubSceneName = "MainHub";

        [SerializeField] private List<RoomSceneMapping> roomScenes = new List<RoomSceneMapping>();

        [Header("Telemetry")]
        [SerializeField] private EventLogCsvWriter eventsWriter;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;

        private void Awake()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (eventsWriter == null)
                eventsWriter = FindObjectOfType<EventLogCsvWriter>();
        }

        public IEnumerator BackToHubForcedRoutine()
        {
            ResolveReferences();

            Debug.LogWarning(
                $"{LogPrefix} BACK_TO_HUB executed. " +
                $"Forced scene change to MainHub by operator command. Scene={mainHubSceneName}");

            LogOperatorSceneEvent(
                "OPERATOR_BACK_TO_HUB",
                mainHubSceneName,
                -1,
                mainHubSceneName,
                "{\"source\":\"operator_command\",\"forced\":true}"
            );

            yield return LoadSceneRoutine(mainHubSceneName);
        }

        public IEnumerator TeleportToRoomForcedRoutine(TeleportToRoomPayload payload)
        {
            ResolveReferences();

            if (payload == null)
            {
                Debug.LogWarning($"{LogPrefix} TELEPORT_TO_ROOM failed. Payload is null.");
                yield break;
            }

            string sceneName = ResolveRoomSceneName(payload);

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning(
                    $"{LogPrefix} TELEPORT_TO_ROOM failed. " +
                    $"Cannot resolve scene. room_id={payload.room_id}, scene_name={payload.scene_name}");
                yield break;
            }

            Debug.LogWarning(
                $"{LogPrefix} TELEPORT_TO_ROOM executed. " +
                $"Forced teleport to room_id={payload.room_id}, scene={sceneName} by operator command.");

            string extraJson =
                "{" +
                "\"source\":\"operator_command\"," +
                "\"forced\":true," +
                $"\"room_id\":{payload.room_id}," +
                $"\"scene_name\":\"{EscapeJson(sceneName)}\"" +
                "}";

            LogOperatorSceneEvent(
                "OPERATOR_TELEPORT_TO_ROOM",
                sceneName,
                payload.room_id,
                sceneName,
                extraJson
            );

            yield return LoadSceneRoutine(sceneName);
        }

        public IEnumerator LoadMainHubNormalRoutine()
        {
            yield return LoadSceneRoutine(mainHubSceneName);
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning($"{LogPrefix} Cannot load scene. Scene name is empty.");
                yield break;
            }

            Debug.Log($"{LogPrefix} Loading scene: {sceneName}");

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

            if (operation == null)
            {
                Debug.LogWarning($"{LogPrefix} LoadSceneAsync returned null for scene: {sceneName}");
                yield break;
            }

            while (!operation.isDone)
                yield return null;
        }

        public bool CanLoadMainHub(out string reason)
        {
            if (string.IsNullOrWhiteSpace(mainHubSceneName))
            {
                reason = "MainHub scene name is empty.";
                return false;
            }

            reason = "";
            return true;
        }

        public bool TryResolveTeleportScene(TeleportToRoomPayload payload, out string sceneName, out string reason)
        {
            sceneName = "";
            reason = "";

            if (payload == null)
            {
                reason = "Payload is null.";
                return false;
            }

            sceneName = ResolveRoomSceneName(payload);

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                reason = $"Cannot resolve scene. room_id={payload.room_id}, scene_name={payload.scene_name}";
                return false;
            }

            return true;
        }

        private string ResolveRoomSceneName(TeleportToRoomPayload payload)
        {
            if (!string.IsNullOrWhiteSpace(payload.scene_name))
                return payload.scene_name;

            foreach (RoomSceneMapping mapping in roomScenes)
            {
                if (mapping != null && mapping.roomId == payload.room_id)
                    return mapping.sceneName;
            }

            return "";
        }

        private void LogOperatorSceneEvent(
            string eventType,
            string targetObject,
            int targetRoomId,
            string targetScene,
            string extraJson)
        {
            ResolveReferences();

            if (eventsWriter == null)
            {
                Log($"Event writer not found. Skipping telemetry event: {eventType}");
                return;
            }

            double timestamp = GetSessionTime();

            int currentRoomId = -1;
            RoomStateProvider roomStateProvider = FindObjectOfType<RoomStateProvider>();

            if (roomStateProvider != null)
                currentRoomId = roomStateProvider.CurrentRoomId;

            eventsWriter.LogEvent(
                timestamp,
                timestamp,
                eventType,
                targetObject,
                0.0,
                currentRoomId,
                extraJson
            );
        }

        private double GetSessionTime()
        {
            TelemetrySessionManager manager = TelemetrySessionManager.Instance;

            if (manager != null &&
                manager.Clock != null &&
                manager.Clock.IsRunning)
            {
                return manager.Clock.CurrentTimeSeconds;
            }

            return Time.timeAsDouble;
        }

        private string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private void Log(string message)
        {
            if (!debugLogs)
                return;

            Debug.Log($"{LogPrefix} {message}");
        }
    }

    [Serializable]
    public class RoomSceneMapping
    {
        public int roomId;
        public string sceneName;
    }

    [Serializable]
    public class TeleportToRoomPayload
    {
        public int room_id;
        public string scene_name;
    }
}