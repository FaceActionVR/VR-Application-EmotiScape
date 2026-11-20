using System;
using System.Collections;
using System.Collections.Generic;
using Telemetry.ExperimentFlow;
using Telemetry.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace Telemetry.Online
{
    public class VrRuntimeCommandPoller : MonoBehaviour
    {
        private const string LogPrefix = "[VR_RUNTIME_COMMANDS]";

        [Header("References")]
        [SerializeField] private VrApiClient apiClient;
        [SerializeField] private EndFlowController endFlowController;
        [SerializeField] private ExperimentSceneFlowController sceneFlowController;

        [Header("Polling")]
        [SerializeField] private float pollIntervalSeconds = 2f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;
        [SerializeField] private bool isPolling;
        [SerializeField] private int backendSessionId = -1;

        private readonly HashSet<int> consumedOrRunningCommands = new HashSet<int>();
        private Coroutine pollingCoroutine;

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

            if (endFlowController == null)
                endFlowController = FindObjectOfType<EndFlowController>();

            if (sceneFlowController == null)
                sceneFlowController = FindObjectOfType<ExperimentSceneFlowController>();
        }

        public void StartPolling(int sessionId)
        {
            StopPolling();

            if (sessionId <= 0)
            {
                Debug.LogWarning($"{LogPrefix} Cannot start polling. Invalid sessionId={sessionId}");
                return;
            }

            ResolveReferences();

            if(apiClient == null)
            {
                Debug.LogWarning($"{LogPrefix} Cannot start polling. VrApiClient not found.");
                return;
            }

            backendSessionId = sessionId;
            consumedOrRunningCommands.Clear();
            pollingCoroutine = StartCoroutine(PollingRoutine());
            isPolling = true;

            Debug.Log($"{LogPrefix} Runtime command polling started. session_id={backendSessionId}");
        }

        public void StopPolling()
        {
            if (pollingCoroutine != null)
            {
                StopCoroutine(pollingCoroutine);
                pollingCoroutine = null;
            }

            isPolling = false;
            backendSessionId = -1;
        }

        private IEnumerator PollingRoutine()
        {
            while (backendSessionId > 0)
            {
                ResolveReferences();

                if (apiClient == null)
                {
                    Debug.LogWarning($"{LogPrefix} VrApiClient not found.");
                    yield return new WaitForSeconds(pollIntervalSeconds);
                    continue;
                }

                int sessionIdForRequest = backendSessionId;

                bool requestOk = false;
                VrCommandListResponse response = null;
                string error = "";

                yield return apiClient.GetCommands(
                    backendSessionId,
                    (success, commandResponse, errorMessage) =>
                    {
                        requestOk = success;
                        response = commandResponse;
                        error = errorMessage;
                    });

                if (!requestOk)
                {
                    Debug.LogWarning($"{LogPrefix} GetCommands failed: {error}");
                    yield return new WaitForSeconds(pollIntervalSeconds);
                    continue;
                }

                if (response != null && response.commands != null)
                {
                    foreach (VrCommandDto command in response.commands)
                    {
                        yield return ExecuteRuntimeCommand(command);
                    }
                }

                yield return new WaitForSeconds(pollIntervalSeconds);
            }

            isPolling = false;
            pollingCoroutine = null;
        }

        private IEnumerator ExecuteRuntimeCommand(VrCommandDto command)
        {
            if (command == null)
                yield break;

            int commandId = command.command_id;
            int sessionIdForCommand = backendSessionId;

            if (commandId <= 0)
            {
                Debug.LogWarning($"{LogPrefix} Command ignored. Invalid command_id.");
                yield break;
            }

            if (sessionIdForCommand <= 0)
            {
                Debug.LogWarning(
                    $"{LogPrefix} Command ignored. Invalid backendSessionId={sessionIdForCommand}, command_id={commandId}");
                yield break;
            }

            if (consumedOrRunningCommands.Contains(commandId))
                yield break;

            consumedOrRunningCommands.Add(commandId);

            if (!IsRuntimeSessionActive())
            {
                string reason =
                    $"Command rejected because runtime session is not active. " +
                    $"command_type={command.command_type}";

                Debug.LogWarning($"{LogPrefix} {reason}. command_id={commandId}");

                bool rejectOk = false;

                yield return RejectCommand(
                    sessionIdForCommand,
                    commandId,
                    reason,
                    success => rejectOk = success);

                if (!rejectOk)
                    consumedOrRunningCommands.Remove(commandId);

                yield break;
            }

            Debug.Log(
                $"{LogPrefix} Command received. " +
                $"command_id={commandId}, type={command.command_type}, payload={command.payload_json}");

            bool executedSuccessfully = false;
            string rejectionReason = "";
            bool shouldStopPollingAfterAck = false;

            switch (command.command_type)
            {
                case "END_SESSION":
                    yield return ExecuteEndSessionCommand(sessionIdForCommand, result =>
                    {
                        executedSuccessfully = result.success;
                        rejectionReason = result.reason;
                    });

                    if (executedSuccessfully)
                        shouldStopPollingAfterAck = true;

                    break;

                case "BACK_TO_HUB":
                    yield return ExecuteBackToHubCommand(result =>
                    {
                        executedSuccessfully = result.success;
                        rejectionReason = result.reason;
                    });
                    break;

                case "TELEPORT_TO_ROOM":
                    yield return ExecuteTeleportToRoomCommand(command, result =>
                    {
                        executedSuccessfully = result.success;
                        rejectionReason = result.reason;
                    });
                    break;

                case "START_SESSION":
                    executedSuccessfully = false;
                    rejectionReason =
                        "START_SESSION rejected during runtime. START_SESSION is valid only in MainMenu before session starts.";
                    break;

                default:
                    executedSuccessfully = false;
                    rejectionReason = $"Unknown runtime command type: {command.command_type}";
                    break;
            }

            bool finalResponseOk = false;


            if (executedSuccessfully)
            {
                yield return AckCommand(
                    sessionIdForCommand,
                    commandId,
                    success => finalResponseOk = success);
            }
            else
            {
                yield return RejectCommand(
                    sessionIdForCommand,
                    commandId,
                    rejectionReason,
                    success => finalResponseOk = success);
            }

            if (!finalResponseOk)
            {
                consumedOrRunningCommands.Remove(commandId);
                yield break;
            }

            if (shouldStopPollingAfterAck)
            {
                Debug.Log($"{LogPrefix} END_SESSION ACK/REJECT finished. Stopping runtime polling.");
                backendSessionId = -1;
                isPolling = false;
                pollingCoroutine = null;
            }
        }

        private bool IsRuntimeSessionActive()
        {
            TelemetrySessionManager manager = TelemetrySessionManager.Instance;

            return manager != null && manager.IsRecording;
        }
        private IEnumerator ExecuteEndSessionCommand(
            int sessionIdForEnd,
            Action<CommandExecutionResult> onCompleted)
        {
            ResolveReferences();

            Debug.LogWarning($"{LogPrefix} END_SESSION executed by operator command.");

            if (endFlowController == null)
            {
                onCompleted?.Invoke(CommandExecutionResult.Failed(
                    "END_SESSION rejected. EndFlowController not found."));
                yield break;
            }

            if (sessionIdForEnd <= 0)
            {
                onCompleted?.Invoke(CommandExecutionResult.Failed(
                    $"END_SESSION rejected. Invalid sessionIdForEnd={sessionIdForEnd}."));
                yield break;
            }

            endFlowController.EndOnlineExperimentAndReturnToMenu(sessionIdForEnd);

            onCompleted?.Invoke(CommandExecutionResult.Success());

            yield return null;
        }

        private IEnumerator ExecuteBackToHubCommand(Action<CommandExecutionResult> onCompleted)
        {
            ResolveReferences();

            if (sceneFlowController == null)
            {
                onCompleted?.Invoke(CommandExecutionResult.Failed(
                    "BACK_TO_HUB rejected. ExperimentSceneFlowController not found."));
                yield break;
            }

            if (!sceneFlowController.CanLoadMainHub(out string reason))
            {
                onCompleted?.Invoke(CommandExecutionResult.Failed(
                    $"BACK_TO_HUB rejected. {reason}"));
                yield break;
            }

            yield return sceneFlowController.BackToHubForcedRoutine();

            onCompleted?.Invoke(CommandExecutionResult.Success());
        }

        private IEnumerator ExecuteTeleportToRoomCommand(
            VrCommandDto command,
            Action<CommandExecutionResult> onCompleted)
        {
            ResolveReferences();

            if (sceneFlowController == null)
            {
                onCompleted?.Invoke(CommandExecutionResult.Failed(
                    "TELEPORT_TO_ROOM rejected. ExperimentSceneFlowController not found."));
                yield break;
            }

            if (command == null)
            {
                onCompleted?.Invoke(CommandExecutionResult.Failed(
                    "TELEPORT_TO_ROOM rejected. Command is null."));
                yield break;
            }

            TeleportToRoomPayload payload = ParseTeleportPayload(command.payload_json);

            if (payload == null)
            {
                onCompleted?.Invoke(CommandExecutionResult.Failed(
                    $"TELEPORT_TO_ROOM rejected. Invalid payload_json: {command.payload_json}"));
                yield break;
            }

            if (payload.room_id <= 0 && string.IsNullOrWhiteSpace(payload.scene_name))
            {
                onCompleted?.Invoke(CommandExecutionResult.Failed(
                    $"TELEPORT_TO_ROOM rejected. Payload must contain room_id or scene_name. payload_json={command.payload_json}"));
                yield break;
            }

            if (!sceneFlowController.TryResolveTeleportScene(payload, out string sceneName, out string reason))
            {
                onCompleted?.Invoke(CommandExecutionResult.Failed(
                    $"TELEPORT_TO_ROOM rejected. {reason}"));
                yield break;
            }

            Debug.LogWarning(
                $"{LogPrefix} TELEPORT_TO_ROOM accepted. Forced teleport to scene={sceneName}, room_id={payload.room_id}");

            yield return sceneFlowController.TeleportToRoomForcedRoutine(payload);

            onCompleted?.Invoke(CommandExecutionResult.Success());
        }

        private TeleportToRoomPayload ParseTeleportPayload(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
                return null;

            try
            {
                return JsonUtility.FromJson<TeleportToRoomPayload>(payloadJson);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogPrefix} Cannot parse TELEPORT_TO_ROOM payload_json. Error={e.Message}, Raw={payloadJson}");
                return null;
            }
        }

        private IEnumerator AckCommand(
            int sessionId,
            int commandId,
            Action<bool> onCompleted)
        {
            if (apiClient == null)
            {
                Debug.LogWarning($"{LogPrefix} Cannot ACK. VrApiClient is null.");
                onCompleted?.Invoke(false);
                yield break;
            }

            if (sessionId <= 0)
            {
                Debug.LogWarning($"{LogPrefix} Cannot ACK. Invalid sessionId={sessionId}, command_id={commandId}");
                onCompleted?.Invoke(false);
                yield break;
            }

            bool ackOk = false;
            string error = "";

            yield return apiClient.AckCommand(
                sessionId,
                commandId,
                (success, responseOrError) =>
                {
                    ackOk = success;
                    error = responseOrError;
                });

            if (!ackOk)
            {
                Debug.LogWarning($"{LogPrefix} ACK failed. session_id={sessionId}, command_id={commandId}, error={error}");
                onCompleted?.Invoke(false);
            }
            else
            {
                Debug.Log($"{LogPrefix} ACK OK. session_id={sessionId}, command_id={commandId}");
                onCompleted?.Invoke(true);
            }
        }

        private IEnumerator RejectCommand(
            int sessionId,
            int commandId,
            string reason,
            Action<bool> onCompleted)
        {
            if (apiClient == null)
            {
                Debug.LogWarning($"{LogPrefix} Cannot REJECT. VrApiClient is null.");
                onCompleted?.Invoke(false);
                yield break;
            }

            if (sessionId <= 0)
            {
                Debug.LogWarning(
                    $"{LogPrefix} Cannot REJECT. Invalid sessionId={sessionId}. " +
                    $"command_id={commandId}, reason={reason}");

                onCompleted?.Invoke(false);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(reason))
                reason = "No rejection reason provided by VR.";

            bool rejectOk = false;
            string error = "";

            yield return apiClient.RejectCommand(
                sessionId,
                commandId,
                reason,
                (success, responseOrError) =>
                {
                    rejectOk = success;
                    error = responseOrError;
                });

            if (!rejectOk)
            {
                Debug.LogWarning(
                    $"{LogPrefix} REJECT failed. " +
                    $"session_id={sessionId}, command_id={commandId}, error={error}");

                onCompleted?.Invoke(false);
            }
            else
            {
                Debug.LogWarning(
                    $"{LogPrefix} REJECT OK. " +
                    $"session_id={sessionId}, command_id={commandId}, reason={reason}");

                onCompleted?.Invoke(true);
            }
        }

        private void Log(string message)
        {
            if (!debugLogs)
                return;

            Debug.Log($"{LogPrefix} {message}");
        }
        private struct CommandExecutionResult
        {
            public bool success;
            public string reason;

            public static CommandExecutionResult Success()
            {
                return new CommandExecutionResult
                {
                    success = true,
                    reason = ""
                };
            }

            public static CommandExecutionResult Failed(string reason)
            {
                return new CommandExecutionResult
                {
                    success = false,
                    reason = reason
                };
            }
        }
    }
}
