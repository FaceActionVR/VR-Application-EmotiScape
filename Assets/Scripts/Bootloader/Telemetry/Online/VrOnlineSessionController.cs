using System;
using System.Collections;
using Telemetry.ExperimentFlow;
using Telemetry.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Telemetry.Online
{
    public enum VrOnlineState
    {
        Unknown,
        CheckingServer,
        ServerOffline,
        ServerRunning,
        MenuStandby,
        SessionQueued,
        AuthOrStatusError,
        Starting,
        Running,
        Failed
    }

    public class VrOnlineSessionController : MonoBehaviour
    {
        private const string LogPrefix = "[VR_ONLINE_SESSION]";

        [Header("References")]
        [SerializeField] private VrApiClient apiClient;
        [SerializeField] private TelemetrySessionManager sessionManager;
        [SerializeField] private ExperimentSceneFlowController sceneFlowController;
        [SerializeField] private VrRuntimeCommandPoller runtimeCommandPoller;
        [SerializeField] private VrHeartbeatSender heartbeatSender;

        [Header("Menu Polling")]
        [SerializeField] private bool pollOnlyInMainMenu = true;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private float menuStateIntervalSeconds = 2f;
        [SerializeField] private float retryServerLiveAfterErrorSeconds = 3f;
        [SerializeField] private bool startPollingOnEnable = true;

        [Header("Local Session")]
        [SerializeField] private string onlineSessionPrefix = "SERVER";

        [Header("Debug / Runtime State")]
        [SerializeField] private VrOnlineState state = VrOnlineState.Unknown;
        [SerializeField] private bool serverLiveConfirmed;
        [SerializeField] private int backendSessionId = -1;
        [SerializeField] private string backendUserIdentifier = "";
        [SerializeField] private string lastError = "";
        [SerializeField] private bool requestInProgress;
        [SerializeField] private bool isStarting;

        private Coroutine pollingCoroutine;

        public VrOnlineState State => state;
        public bool ServerLiveConfirmed => serverLiveConfirmed;
        public int BackendSessionId => backendSessionId;
        public string BackendUserIdentifier => backendUserIdentifier;
        public string LastError => lastError;

        public bool CanStartOnline =>
            serverLiveConfirmed &&
            state == VrOnlineState.SessionQueued &&
            backendSessionId > 0 &&
            !isStarting;

        public event Action OnStateChanged;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            SceneManager.sceneLoaded += HandleSceneLoaded;

            if (startPollingOnEnable)
                StartStatusPolling();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            StopStatusPolling();
        }


        private void ResolveReferences()
        {
            if (apiClient == null)
                apiClient = GetComponent<VrApiClient>();

            if (apiClient == null)
                apiClient = FindObjectOfType<VrApiClient>();

            if (sessionManager == null)
                sessionManager = TelemetrySessionManager.Instance;

            if (sceneFlowController == null)
                sceneFlowController = FindObjectOfType<ExperimentSceneFlowController>();

            if (runtimeCommandPoller == null)
                runtimeCommandPoller = GetComponent<VrRuntimeCommandPoller>();

            if (runtimeCommandPoller == null)
                runtimeCommandPoller = FindObjectOfType<VrRuntimeCommandPoller>();

            if (heartbeatSender == null)
                heartbeatSender = GetComponent<VrHeartbeatSender>();

            if (heartbeatSender == null)
                heartbeatSender = FindObjectOfType<VrHeartbeatSender>();
        }

        public void StartStatusPolling()
        {
            if (pollingCoroutine != null)
                return;

            pollingCoroutine = StartCoroutine(MenuPollingRoutine());
            Debug.Log($"{LogPrefix} Menu polling started.");
        }

        public void StopStatusPolling()
        {
            if (pollingCoroutine == null)
                return;

            StopCoroutine(pollingCoroutine);
            pollingCoroutine = null;

            Debug.Log($"{LogPrefix} Menu polling stopped.");
        }

        private IEnumerator MenuPollingRoutine()
        {
            while (true)
            {
                if (!ShouldPollInCurrentScene())
                {
                    yield return null;
                    continue;
                }

                if (!serverLiveConfirmed)
                {
                    yield return CheckServerLiveOnce();

                    if (!serverLiveConfirmed)
                    {
                        yield return new WaitForSeconds(retryServerLiveAfterErrorSeconds);
                        continue;
                    }
                }

                yield return GetMenuStateOnce();

                yield return new WaitForSeconds(menuStateIntervalSeconds);
            }
        }

        private bool ShouldPollInCurrentScene()
        {
            if (!pollOnlyInMainMenu)
                return true;

            return SceneManager.GetActiveScene().name == mainMenuSceneName;
        }

        private IEnumerator CheckServerLiveOnce()
        {
            if (requestInProgress)
                yield break;

            ResolveReferences();

            if (apiClient == null)
            {
                lastError = "VrApiClient not found.";
                serverLiveConfirmed = false;
                ClearBackendSession();
                SetState(VrOnlineState.Failed);
                yield break;
            }

            requestInProgress = true;
            SetState(VrOnlineState.CheckingServer);

            bool liveOk = false;
            string result = "";

            yield return apiClient.CheckServerLive((success, responseOrError) =>
            {
                liveOk = success;
                result = responseOrError;
            });

            if (!liveOk)
            {
                lastError = result;
                serverLiveConfirmed = false;
                ClearBackendSession();
                SetState(VrOnlineState.ServerOffline);
                requestInProgress = false;
                yield break;
            }

            lastError = "";
            serverLiveConfirmed = true;
            SetState(VrOnlineState.ServerRunning);

            requestInProgress = false;
        }

        private IEnumerator GetMenuStateOnce()
        {
            if (requestInProgress)
                yield break;

            ResolveReferences();

            if (apiClient == null)
            {
                lastError = "VrApiClient not found.";
                serverLiveConfirmed = false;
                ClearBackendSession();
                SetState(VrOnlineState.Failed);
                yield break;
            }

            requestInProgress = true;

            bool ok = false;
            VrMenuStateResponse response = null;
            string error = "";

            yield return apiClient.GetMenuState((success, menuResponse, errorMessage) =>
            {
                ok = success;
                response = menuResponse;
                error = errorMessage;
            });

            if (!ok || response == null)
            {
                lastError = error;
                serverLiveConfirmed = false;
                ClearBackendSession();
                SetState(VrOnlineState.AuthOrStatusError);
                requestInProgress = false;
                yield break;
            }

            ApplyMenuState(response);

            requestInProgress = false;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != mainMenuSceneName)
                return;

            ResolveReferences();

            Debug.Log(
                $"{LogPrefix} MainMenu loaded. " +
                $"state={state}, backendSessionId={backendSessionId}, " +
                $"recording={(sessionManager != null && sessionManager.IsRecording)}");

            if (state == VrOnlineState.Running &&
                (sessionManager == null || !sessionManager.IsRecording))
            {
                ClearBackendSession();

                lastError = "";
                isStarting = false;

                SetState(VrOnlineState.MenuStandby);

                Debug.Log(
                    $"{LogPrefix} Previous online session cleared after returning to MainMenu.");
            }

            StartStatusPolling();
        }

        private void ApplyMenuState(VrMenuStateResponse response)
        {
            switch (response.state)
            {
                case "SESSION_QUEUED":
                    backendSessionId = response.session_id;
                    backendUserIdentifier = response.user_identifier;
                    lastError = "";

                    SetState(VrOnlineState.SessionQueued);

                    HandleMenuCommands(response.commands);
                    break;

                case "STANDBY":
                case "NO_SESSION":
                    ClearBackendSession();
                    lastError = "";
                    SetState(VrOnlineState.MenuStandby);
                    break;

                default:
                    lastError = $"Unknown menu_state value: {response.state}";
                    serverLiveConfirmed = false;
                    ClearBackendSession();
                    SetState(VrOnlineState.AuthOrStatusError);
                    break;
            }
        }

        private void HandleMenuCommands(VrCommandDto[] commands)
        {
            if (commands == null || commands.Length == 0)
                return;

            foreach (VrCommandDto command in commands)
            {
                if (command == null)
                    continue;

                Debug.Log(
                    $"{LogPrefix} Menu command received. " +
                    $"command_id={command.command_id}, type={command.command_type}");

                switch (command.command_type)
                {
                    case "START_SESSION":
                        if (!isStarting)
                            StartCoroutine(StartOnlineRoutine(command.command_id));
                        break;

                    case "END_SESSION":
                    case "TELEPORT_TO_ROOM":
                    case "BACK_TO_HUB":
                        StartCoroutine(RejectMenuCommand(
                    command.command_id,
                            $"{LogPrefix} Command {command.command_type} ignored in MainMenu. " +
                            "It should be handled during runtime."));
                        break;

                    default:
                        Debug.LogWarning($"{LogPrefix} Unknown menu command type: {command.command_type}");
                        break;
                }
            }
        }

        private IEnumerator RejectMenuCommand(int commandId, string reason)
        {
            if (commandId <= 0)
            {
                Debug.LogWarning($"{LogPrefix} Cannot reject menu command. Invalid command_id={commandId}");
                yield break;
            }

            if (backendSessionId <= 0)
            {
                Debug.LogWarning(
                    $"{LogPrefix} Cannot reject menu command. Invalid backendSessionId={backendSessionId}. " +
                    $"command_id={commandId}, reason={reason}");
                yield break;
            }

            Debug.LogWarning(
                $"{LogPrefix} Rejecting menu command. " +
                $"command_id={commandId}, reason={reason}");

            bool rejectOk = false;
            string error = "";

            yield return apiClient.RejectCommand(
                backendSessionId,
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
                    $"{LogPrefix} Reject command failed. " +
                    $"command_id={commandId}, error={error}");
            }
            else
            {
                Debug.Log(
                    $"{LogPrefix} Reject command OK. " +
                    $"command_id={commandId}");
            }
        }

        public void StartOnlineFromButton()
        {
            if (isStarting)
                return;

            StartCoroutine(StartOnlineRoutine(-1));
        }

        private IEnumerator StartOnlineRoutine(int startCommandId)
        {
            if (!CanStartOnline)
            {

                Debug.LogWarning(
                    $"{LogPrefix} Cannot start online. " +
                    $"state={state}, serverLive={serverLiveConfirmed}, backendSessionId={backendSessionId}");
                yield break;
            }

            isStarting = true;
            SetState(VrOnlineState.Starting);

            StopStatusPolling();

            bool confirmOk = false;
            string confirmError = "";

            ResolveReferences();

            if (sceneFlowController == null)
            {
                string reason = "START_SESSION rejected. ExperimentSceneFlowController not found before ConfirmStart.";

                Debug.LogWarning($"{LogPrefix} {reason}");

                if (startCommandId > 0)
                    yield return RejectStartCommand(startCommandId, reason);

                isStarting = false;
                SetState(VrOnlineState.Failed);
                StartStatusPolling();
                yield break;
            }

            if (!sceneFlowController.CanLoadMainHub(out string hubReason))
            {
                string reason = $"START_SESSION rejected. Cannot load MainHub before ConfirmStart. {hubReason}";

                Debug.LogWarning($"{LogPrefix} {reason}");

                if (startCommandId > 0)
                    yield return RejectStartCommand(startCommandId, reason);

                isStarting = false;
                SetState(VrOnlineState.Failed);
                StartStatusPolling();
                yield break;
            }

            yield return apiClient.ConfirmStart(
                backendSessionId,
                (success, responseOrError) =>
                {
                    confirmOk = success;
                    confirmError = responseOrError;
                });

            if (!confirmOk)
            {
                if (startCommandId > 0 && backendSessionId > 0)
                {
                    yield return apiClient.RejectCommand(
                        backendSessionId,
                        startCommandId,
                        $"START_SESSION rejected. ConfirmStart failed: {confirmError}",
                        (success, responseOrError) =>
                        {
                            if (!success)
                                Debug.LogWarning($"{LogPrefix} START_SESSION reject failed: {responseOrError}");
                        });
                }

                lastError = confirmError;
                SetState(VrOnlineState.Failed);
                isStarting = false;
                StartStatusPolling();
                yield break;
            }

            StartLocalTelemetrySession();

            if (runtimeCommandPoller != null)
                runtimeCommandPoller.StartPolling(backendSessionId);

            if (sceneFlowController == null)
            {
                string reason = "START_SESSION rejected. ExperimentSceneFlowController not found. MainHub was not loaded.";

                Debug.LogWarning($"{LogPrefix} {reason}");

                if (startCommandId > 0)
                    yield return RejectStartCommand(startCommandId, reason);

                SetState(VrOnlineState.Failed);
                isStarting = false;
                StartStatusPolling();
                yield break;
            }

            yield return sceneFlowController.LoadMainHubNormalRoutine();

            if (SceneManager.GetActiveScene().name == mainMenuSceneName)
            {
                string reason =
                    "START_SESSION rejected. MainHub load was requested, but active scene is still MainMenu. " +
                    "Check Main Hub Scene Name and Build Settings.";

                Debug.LogWarning($"{LogPrefix} {reason}");

                if (startCommandId > 0)
                    yield return RejectStartCommand(startCommandId, reason);

                SetState(VrOnlineState.Failed);
                isStarting = false;
                StartStatusPolling();
                yield break;
            }

            ResolveReferences();

            if (heartbeatSender != null)
            {
                Debug.Log(
                    $"{LogPrefix} HUB loaded. Starting heartbeat. " +
                    $"session_id={backendSessionId}, scene={SceneManager.GetActiveScene().name}");

                heartbeatSender.StartHeartbeat(backendSessionId);
            }
            else
            {
                Debug.LogWarning(
                    $"{LogPrefix} HUB loaded, but VrHeartbeatSender was not found.");
            }

            isStarting = false;
            SetState(VrOnlineState.Running);

            if (startCommandId > 0)
                yield return AckStartCommand(startCommandId);
        }

        private IEnumerator RejectStartCommand(int commandId, string reason)
        {
            if (apiClient == null)
            {
                Debug.LogWarning($"{LogPrefix} Cannot reject START_SESSION. VrApiClient is null.");
                yield break;
            }

            if (backendSessionId <= 0)
            {
                Debug.LogWarning($"{LogPrefix} Cannot reject START_SESSION. Invalid backendSessionId={backendSessionId}");
                yield break;
            }

            bool rejectOk = false;
            string error = "";

            yield return apiClient.RejectCommand(
                backendSessionId,
                commandId,
                reason,
                (success, responseOrError) =>
                {
                    rejectOk = success;
                    error = responseOrError;
                });

            if (!rejectOk)
                Debug.LogWarning($"{LogPrefix} START_SESSION REJECT failed. command_id={commandId}, error={error}");
            else
                Debug.LogWarning($"{LogPrefix} START_SESSION REJECT OK. command_id={commandId}, reason={reason}");
        }

        private void StartLocalTelemetrySession()
        {
            ResolveReferences();

            if (sessionManager == null)
            {
                Debug.LogWarning($"{LogPrefix} TelemetrySessionManager not found. Cannot start local session.");
                return;
            }

            string userId = string.IsNullOrWhiteSpace(backendUserIdentifier)
                ? $"USER_{backendSessionId}"
                : backendUserIdentifier;

            String localSessionId = TelemetrySessionIdFactory.GenerateSessionId(userId);

            sessionManager.StartSession(localSessionId, userId);

            Debug.Log(
                $"{LogPrefix} Local telemetry started. " +
                $"localSessionId={localSessionId}, userId={userId}, backendSessionId={backendSessionId}");
        }

        private IEnumerator AckStartCommand(int commandId)
        {
            bool ackOk = false;
            string error = "";

            yield return apiClient.AckCommand(
                backendSessionId,
                commandId,
                (success, responseOrError) =>
                {
                    ackOk = success;
                    error = responseOrError;
                });

            if (!ackOk)
                Debug.LogWarning($"{LogPrefix} START_SESSION ACK failed. command_id={commandId}, error={error}");
            else
                Debug.Log($"{LogPrefix} START_SESSION ACK OK. command_id={commandId}");
        }

        private void ClearBackendSession()
        {
            backendSessionId = -1;
            backendUserIdentifier = "";
        }

        private void SetState(VrOnlineState newState)
        {
            if (state == newState)
                return;

            state = newState;
            Debug.Log($"{LogPrefix} State changed: {state}");
            OnStateChanged?.Invoke();
        }

        public string GetBackendLine()
        {
            if (!serverLiveConfirmed)
            {
                switch (state)
                {
                    case VrOnlineState.CheckingServer:
                        return "backend: <color=#FFD166>Checking...</color>";

                    case VrOnlineState.ServerOffline:
                        return "backend: <color=#FF3333>Offline</color>";

                    case VrOnlineState.AuthOrStatusError:
                        return "backend: <color=#FF3333>Error</color>";

                    case VrOnlineState.Failed:
                        return "backend: <color=#FF3333>Failed</color>";

                    default:
                        return "backend: <color=#AAAAAA>Unknown</color>";
                }
            }

            return "backend: <color=#6DEF00>Running</color>";
        }

        public string GetSessionLine()
        {
            switch (state)
            {
                case VrOnlineState.SessionQueued:
                    return $"session: <color=#6DEF00>{backendSessionId}</color>";

                case VrOnlineState.MenuStandby:
                    return "session: <color=#FFD166>standby</color>";

                case VrOnlineState.Starting:
                    return $"session: <color=#FFD166>starting {backendSessionId}</color>";

                case VrOnlineState.Running:
                    return $"session: <color=#6DEF00>running {backendSessionId}</color>";

                case VrOnlineState.AuthOrStatusError:
                    return "session: <color=#FF3333>error</color>";

                default:
                    if (backendSessionId > 0)
                        return $"session: {backendSessionId}";

                    return "session: waiting";
            }
        }

        public string GetUserLine()
        {
            if (string.IsNullOrWhiteSpace(backendUserIdentifier))
                return "user: -";

            string shortUser = backendUserIdentifier;

            if (shortUser.Length > 16)
                shortUser = shortUser.Substring(0, 16) + "...";

            return $"user: {shortUser}";
        }
    }
}