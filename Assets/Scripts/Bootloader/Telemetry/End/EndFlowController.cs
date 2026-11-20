using System.Collections;
using System;
using Telemetry.Export;
using Telemetry.Session;
using Telemetry.Upload;
using Telemetry.Online;
using UnityEngine;
using UnityEngine.SceneManagement;
using Telemetry.Interactions;

namespace Telemetry.ExperimentFlow
{
    public enum EndUploadState
    {
        None,
        Uploading,
        Succeeded,
        Failed
    }

    public class EndFlowController : MonoBehaviour
    {
        private const string LogPrefix = "[EXPERIMENT_END_FLOW]";

        [Header("References")]
        [SerializeField] private VrApiClient apiClient;
        [SerializeField] private VrHeartbeatSender heartbeatSender;
        [SerializeField] private VrRuntimeCommandPoller runtimeCommandPoller;

        [SerializeField] private TelemetrySessionManager sessionManager;
        [SerializeField] private SessionZipExporter zipExporter;
        [SerializeField] private SessionUploadClient uploadClient;

        [SerializeField]
        private InteractionEventLogger interactionEventLogger;

        [Header("Scene Flow")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Upload Behaviour")]
        [Tooltip("Je¿eli true, czekamy na upload przed przejœciem do menu.")]
        [SerializeField] private bool waitForUploadBeforeMenu = true;

        [Tooltip("Je¿eli upload siê nie powiedzie, po ilu sekundach i tak wróciæ do menu.")]
        [SerializeField] private float uploadFailReturnDelaySeconds = 2f;

        [Tooltip("Maksymalny czas oczekiwania na upload. Po tym czasie wracamy do menu mimo braku odpowiedzi.")]
        [SerializeField] private float uploadTimeoutSeconds = 20f;

        [Header("Debug")]
        [SerializeField] private bool isDebug = true;
        [SerializeField] private bool endFlowRunning;
        [SerializeField] private bool uploadFinished;
        [SerializeField] private bool uploadSucceeded;
        [SerializeField] private string lastZipPath;
        [SerializeField] private string lastUploadError;

        public EndUploadState UploadState { get; private set; } = EndUploadState.None;
        public string LastUploadError => lastUploadError;
        public string LastZipPath => lastZipPath;

        public event Action<EndUploadState> OnUploadStateChanged;
        private Coroutine endRoutine;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeUpload();
        }

        private void OnDisable()
        {
            UnsubscribeUpload();
        }

        private void ResolveReferences()
        {
            if (sessionManager == null)
                sessionManager = TelemetrySessionManager.Instance;

            if (zipExporter == null)
                zipExporter = FindObjectOfType<SessionZipExporter>();

            if (uploadClient == null)
                uploadClient = FindObjectOfType<SessionUploadClient>();

            if (apiClient == null)
                apiClient = FindObjectOfType<VrApiClient>();

            if (heartbeatSender == null)
                heartbeatSender = FindObjectOfType<VrHeartbeatSender>();

            if (runtimeCommandPoller == null)
                runtimeCommandPoller = FindObjectOfType<VrRuntimeCommandPoller>();

            if (interactionEventLogger == null)
                interactionEventLogger = FindObjectOfType<InteractionEventLogger>();
        }

        private void SubscribeUpload()
        {
            if (uploadClient == null)
                return;

            uploadClient.OnUploadSucceeded += HandleUploadSucceeded;
            uploadClient.OnUploadFailed += HandleUploadFailed;
        }

        private void UnsubscribeUpload()
        {
            if (uploadClient == null)
                return;

            uploadClient.OnUploadSucceeded -= HandleUploadSucceeded;
            uploadClient.OnUploadFailed -= HandleUploadFailed;
        }

        public void EndExperimentAndReturnToMenu()
        {
            if (endFlowRunning)
            {
                Log("End flow already running. Ignored duplicate request.");
                return;
            }

            endRoutine = StartCoroutine(EndExperimentRoutine());
        }

        public void EndOnlineExperimentAndReturnToMenu(int backendSessionId)
        {
            if (endFlowRunning)
            {
                Log("Online end flow already running. Ignored duplicate request.");
                return;
            }

            if (backendSessionId <= 0)
            {
                LogWarning($"Invalid backendSessionId={backendSessionId}. Falling back to local end flow.");
                EndExperimentAndReturnToMenu();
                return;
            }

            endRoutine = StartCoroutine(EndOnlineExperimentRoutine(backendSessionId));
        }

        private IEnumerator EndOnlineExperimentRoutine(int backendSessionId)
        {
            endFlowRunning = true;
            uploadFinished = false;
            uploadSucceeded = false;
            lastZipPath = "";
            lastUploadError = "";

            SetUploadState(EndUploadState.None);

            Log(
                $"Online end experiment requested. " +
                $"backendSessionId={backendSessionId}");

            ResolveReferences();

            if (heartbeatSender != null)
                heartbeatSender.StopHeartbeat();

            if (sessionManager == null)
                sessionManager = TelemetrySessionManager.Instance;

            if (sessionManager == null)
            {
                lastUploadError = "TelemetrySessionManager not found.";
                SetUploadState(EndUploadState.Failed);

                LogWarning(
                    "TelemetrySessionManager not found. " +
                    "Returning to MainMenu.");

                LoadMainMenu();
                yield break;
            }

            if (!sessionManager.IsRecording)
            {
                LogWarning(
                    "No active telemetry session. " +
                    "Returning to MainMenu.");

                LoadMainMenu();
                yield break;
            }

            if (zipExporter == null)
                zipExporter = FindObjectOfType<SessionZipExporter>();

            if (zipExporter == null)
            {
                lastUploadError = "SessionZipExporter not found.";
                SetUploadState(EndUploadState.Failed);

                LogWarning(
                    "SessionZipExporter not found. " +
                    "Returning to MainMenu.");

                LoadMainMenu();
                yield break;
            }

            string producedZipPath = "";
            bool zipCreated = false;

            void HandleZipCreated(string zipPath)
            {
                producedZipPath = zipPath;
                zipCreated = true;
            }

            zipExporter.OnZipCreated += HandleZipCreated;

            Log("Stopping telemetry session.");

            if (interactionEventLogger != null)
            {
                interactionEventLogger.FlushActiveManipulations();
            }

            sessionManager.StopSession();

            float zipWait = 0f;

            while (!zipCreated && zipWait < 10f)
            {
                zipWait += Time.deltaTime;
                yield return null;
            }

            zipExporter.OnZipCreated -= HandleZipCreated;

            if (!zipCreated ||
                string.IsNullOrWhiteSpace(producedZipPath))
            {
                lastUploadError =
                    "Nie uda³o siê utworzyæ archiwum sesji.";

                SetUploadState(EndUploadState.Failed);

                LogWarning(
                    "ZIP was not created. " +
                    "Returning to MainMenu.");

                LoadMainMenu();
                yield break;
            }

            lastZipPath = producedZipPath;

            Log($"ZIP ready: {producedZipPath}");

            if (apiClient == null)
            {
                lastUploadError = "VrApiClient not found.";
                SetUploadState(EndUploadState.Failed);

                LogWarning(
                    "VrApiClient not found. " +
                    "Returning to MainMenu.");

                LoadMainMenu();
                yield break;
            }

            // WA¯NE:
            // ustawiamy Uploading PRZED wejœciem do MainMenu,
            // ¿eby nowy overlay od razu wiedzia³, ¿e ma siê pokazaæ.
            SetUploadState(EndUploadState.Uploading);

            Log(
                "ZIP ready. Returning to MainMenu. " +
                "Network upload will continue in background.");

            LoadMainMenu();

            yield return null;

            StartCoroutine(
                BackgroundEndAndUploadRoutine(
                    backendSessionId,
                    producedZipPath));

            yield break;
        }

        private IEnumerator BackgroundEndAndUploadRoutine(
            int backendSessionId,
            string zipPath)
        {
            Log(
                $"Background end/upload started. " +
                $"backendSessionId={backendSessionId}, zip={zipPath}");

            if (apiClient == null)
            {
                lastUploadError = "VrApiClient not found.";
                uploadFinished = true;
                uploadSucceeded = false;

                SetUploadState(EndUploadState.Failed);
                yield break;
            }

            // Najpierw informujemy backend o zakoñczeniu sesji.
            bool backendEndOk = false;
            string backendEndResponse = "";

            yield return apiClient.EndSession(
                backendSessionId,
                (success, responseOrError) =>
                {
                    backendEndOk = success;
                    backendEndResponse = responseOrError;
                });

            if (backendEndOk)
            {
                Log($"Background EndSession OK: {backendEndResponse}");
            }
            else
            {
                LogWarning(
                    $"Background EndSession failed: " +
                    $"{backendEndResponse}. " +
                    "ZIP upload will still be attempted.");
            }

            bool uploadOk = false;
            long responseCode = 0;
            string uploadResponseOrError = "";

            Log(
                $"Background ZIP upload started. " +
                $"backendSessionId={backendSessionId}, zip={zipPath}");

            yield return apiClient.UploadZip(
                backendSessionId,
                zipPath,
                (success, code, responseOrError) =>
                {
                    uploadOk = success;
                    responseCode = code;
                    uploadResponseOrError = responseOrError;
                });

            uploadFinished = true;
            uploadSucceeded = uploadOk;
            lastUploadError = uploadOk ? "" : uploadResponseOrError;

            if (uploadOk)
            {
                lastUploadError = "";
                Log($"Background upload succeeded. Code={responseCode}");
                SetUploadState(EndUploadState.Succeeded);
            }
            else
            {
                LogWarning(
                    $"Background upload failed. " +
                    $"Code={responseCode}, Error={uploadResponseOrError}");

                SetUploadState(EndUploadState.Failed);
            }
        }

        private IEnumerator EndExperimentRoutine()
        {
            endFlowRunning = true;
            uploadFinished = false;
            uploadSucceeded = false;
            lastZipPath = "";
            lastUploadError = "";

            Log("End experiment requested.");

            ResolveReferences();

            if (sessionManager == null)
            {
                LogWarning("TelemetrySessionManager not found. Returning to main menu without stopping session.");
                LoadMainMenu();
                yield break;
            }

            if (sessionManager.IsRecording)
            {
                Log("Stopping telemetry session.");

                if (interactionEventLogger != null)
                {
                    interactionEventLogger.FlushActiveManipulations();
                }

                sessionManager.StopSession();

                // StopSession odpala:
                // OnSessionStopping -> zamkniêcie CSV + metadata
                // OnSessionStopped -> ZIP + upload
            }
            else
            {
                LogWarning("No active telemetry session. Returning to main menu.");
                LoadMainMenu();
                yield break;
            }

            if (!waitForUploadBeforeMenu)
            {
                Log("Not waiting for upload. Returning to main menu.");
                LoadMainMenu();
                yield break;
            }

            Log("Waiting for upload result.");

            float elapsed = 0f;

            while (!uploadFinished && elapsed < uploadTimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!uploadFinished)
            {
                LogWarning($"Upload timeout after {uploadTimeoutSeconds:F1}s. Returning to main menu.");
                LoadMainMenu();
                yield break;
            }

            if (uploadSucceeded)
            {
                Log("Upload succeeded. Returning to main menu.");
                LoadMainMenu();
                yield break;
            }

            LogWarning($"Upload failed: {lastUploadError}. Returning to main menu after delay.");

            if (uploadFailReturnDelaySeconds > 0f)
                yield return new WaitForSeconds(uploadFailReturnDelaySeconds);

            LoadMainMenu();
        }

        private void HandleUploadSucceeded(string zipPath)
        {
            uploadFinished = true;
            uploadSucceeded = true;
            lastZipPath = zipPath;

            Log($"Upload succeeded. Zip: {zipPath}");
        }

        private void HandleUploadFailed(string zipPath, string error)
        {
            uploadFinished = true;
            uploadSucceeded = false;
            lastZipPath = zipPath;
            lastUploadError = error;

            LogWarning($"Upload failed. Zip: {zipPath}, Error: {error}");
        }

        private void LoadMainMenu()
        {
            Log($"Loading main menu scene: {mainMenuSceneName}");

            endFlowRunning = false;

            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void Log(string message)
        {
            if (!isDebug)
                return;

            Debug.Log($"{LogPrefix} {message}");
        }

        private void LogWarning(string message)
        {
            if (!isDebug)
                return;

            Debug.LogWarning($"{LogPrefix} {message}");
        }

        private void SetUploadState(EndUploadState newState)
        {
            UploadState = newState;
            OnUploadStateChanged?.Invoke(newState);

            Debug.Log($"Upload state changed: {newState}");
        }

        public void ClearCompletedUploadState()
        {
            if (UploadState == EndUploadState.Succeeded)
                SetUploadState(EndUploadState.None);
        }
    }
}