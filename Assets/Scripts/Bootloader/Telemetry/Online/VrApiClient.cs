using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Telemetry.Online
{
    public class VrApiClient : MonoBehaviour
    {
        private const string LogPrefix = "[VR_API_CLIENT]";

        [Header("Server")]
        [Tooltip("Adres backendu Flask. Na goglach NIE u¿ywaj localhost, tylko IP komputera w sieci LAN.")]
        [SerializeField] private string baseUrl = "http://192.168.50.209:5000";

        [Header("Basic Auth")]
        [SerializeField] private string username = "user";
        [SerializeField] private string password = "veqso6-fubrot-zeqwUh";

        [Header("Request Settings")]
        [SerializeField] private int timeoutSeconds = 10;

        [Header("Upload")]
        [SerializeField] private int uploadTimeoutSeconds = 300;
        [SerializeField] private string uploadFileFormFieldName = "file";

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;

        public string BaseUrl => baseUrl;

        // =========================================================
        // PUBLICZNY ENDPOINT - BEZ BASIC AUTH
        // GET /api/server_live
        // =========================================================

        public IEnumerator CheckServerLive(Action<bool, string> onCompleted)
        {
            string url = BuildUrl("/api/server_live");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = uploadTimeoutSeconds;

                yield return request.SendWebRequest();

                if (!IsRequestSuccess(request))
                {
                    string error = BuildError(request);
                    LogWarning($"Server live failed: {error}");
                    onCompleted?.Invoke(false, error);
                    yield break;
                }

                string response = request.downloadHandler.text;
                Log($"Server live OK: {response}");
                onCompleted?.Invoke(true, response);
            }
        }

        // =========================================================
        // BASIC AUTH
        // GET /api/check_status
        // =========================================================

        public IEnumerator GetMenuState(Action<bool, VrMenuStateResponse, string> onCompleted)
        {
            string url = BuildUrl("/api/vr/menu_state");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = timeoutSeconds;
                AddBasicAuth(request);

                yield return request.SendWebRequest();

                if (!IsRequestSuccess(request))
                {
                    string error = BuildError(request);
                    LogWarning($"Menu state failed: {error}");
                    onCompleted?.Invoke(false, null, error);
                    yield break;
                }

                string json = request.downloadHandler.text;
                VrMenuStateResponse response;

                try
                {
                    response = JsonUtility.FromJson<VrMenuStateResponse>(json);
                }
                catch (Exception e)
                {
                    string error = $"Cannot parse menu_state response. Error={e.Message}, Raw={json}";
                    LogWarning(error);
                    onCompleted?.Invoke(false, null, error);
                    yield break;
                }

                Log($"Menu state OK: {json}");
                onCompleted?.Invoke(true, response, null);
            }
        }

        // =========================================================
        // BASIC AUTH
        // POST /api/confirm_start
        // Body: { "session_id": 123 }
        // =========================================================

        public IEnumerator ConfirmStart(int backendSessionId, Action<bool, string> onCompleted)
        {
            string url = BuildUrl("/api/confirm_start");

            ConfirmStartRequest body = new ConfirmStartRequest
            {
                session_id = backendSessionId
            };

            string json = JsonUtility.ToJson(body);

            using (UnityWebRequest request = CreateJsonPost(url, json))
            {
                AddBasicAuth(request);

                yield return request.SendWebRequest();

                if (!IsRequestSuccess(request))
                {
                    string error = BuildError(request);
                    LogWarning($"Confirm start failed: {error}");
                    onCompleted?.Invoke(false, error);
                    yield break;
                }

                string response = request.downloadHandler.text;
                Log($"Confirm start OK: {response}");
                onCompleted?.Invoke(true, response);
            }
        }

        // =========================================================
        // BASIC AUTH
        // POST /api/vr/sessions/{id}/heartbeat
        // =========================================================

        public IEnumerator SendHeartbeat(
            int backendSessionId,
            HeartbeatRequest body,
            Action<bool, string> onCompleted)
        {
            string url = BuildUrl($"/api/vr/sessions/{backendSessionId}/heartbeat");
            string json = JsonUtility.ToJson(body);

            using (UnityWebRequest request = CreateJsonPost(url, json))
            {
                AddBasicAuth(request);

                yield return request.SendWebRequest();

                if (!IsRequestSuccess(request))
                {
                    string error = BuildError(request);
                    LogWarning($"Heartbeat failed: {error}");
                    onCompleted?.Invoke(false, error);
                    yield break;
                }

                string response = request.downloadHandler.text;
                onCompleted?.Invoke(true, response);
            }
        }

        // =========================================================
        // BASIC AUTH
        // GET /api/vr/sessions/{id}/commands
        // =========================================================

        public IEnumerator GetCommands(
            int backendSessionId,
            Action<bool, VrCommandListResponse, string> onCompleted)
        {
            string url = BuildUrl($"/api/vr/sessions/{backendSessionId}/commands");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = timeoutSeconds;
                AddBasicAuth(request);

                yield return request.SendWebRequest();

                if (!IsRequestSuccess(request))
                {
                    string error = BuildError(request);
                    LogWarning($"Get commands failed: {error}");
                    onCompleted?.Invoke(false, null, error);
                    yield break;
                }

                string rawJson = request.downloadHandler.text;
                string wrappedJson = "{\"commands\":" + rawJson + "}";

                VrCommandListResponse response;

                try
                {
                    response = JsonUtility.FromJson<VrCommandListResponse>(wrappedJson);
                }
                catch (Exception e)
                {
                    string error = $"Cannot parse commands response. Error={e.Message}, Raw={rawJson}";
                    LogWarning(error);
                    onCompleted?.Invoke(false, null, error);
                    yield break;
                }

                onCompleted?.Invoke(true, response, null);
            }
        }

        // =========================================================
        // BASIC AUTH
        // POST /api/vr/sessions/{id}/commands/{command_id}/ack
        // Body: none
        // =========================================================

        public IEnumerator AckCommand(
            int backendSessionId,
            int commandId,
            Action<bool, string> onCompleted)
        {
            string url = BuildUrl($"/api/vr/sessions/{backendSessionId}/commands/{commandId}/ack");

            using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, ""))
            {
                request.timeout = timeoutSeconds;
                AddBasicAuth(request);

                yield return request.SendWebRequest();

                if (!IsRequestSuccess(request))
                {
                    string error = BuildError(request);
                    LogWarning($"Ack command failed: {error}");
                    onCompleted?.Invoke(false, error);
                    yield break;
                }

                string response = request.downloadHandler.text;
                Log($"Ack command OK. CommandId={commandId}, Response={response}");
                onCompleted?.Invoke(true, response);
            }
        }

        public IEnumerator RejectCommand(
    int backendSessionId,
    int commandId,
    string reason,
    Action<bool, string> onCompleted)
        {
            if (backendSessionId <= 0)
            {
                string error = $"Invalid backendSessionId={backendSessionId}";
                LogWarning(error);
                onCompleted?.Invoke(false, error);
                yield break;
            }

            if (commandId <= 0)
            {
                string error = $"Invalid commandId={commandId}";
                LogWarning(error);
                onCompleted?.Invoke(false, error);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(reason))
                reason = "No rejection reason provided by VR.";

            string url = BuildUrl($"/api/vr/sessions/{backendSessionId}/commands/{commandId}/reject");

            RejectCommandRequest body = new RejectCommandRequest
            {
                reason = reason
            };

            string json = JsonUtility.ToJson(body);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.timeout = timeoutSeconds;
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();

                request.SetRequestHeader("Content-Type", "application/json");
                AddBasicAuth(request);

                yield return request.SendWebRequest();

                if (!IsRequestSuccess(request))
                {
                    string error = BuildError(request);
                    LogWarning($"RejectCommand failed. command_id={commandId}, error={error}");
                    onCompleted?.Invoke(false, error);
                    yield break;
                }

                string responseText = request.downloadHandler.text;

                Log($"RejectCommand OK. command_id={commandId}, response={responseText}");
                onCompleted?.Invoke(true, responseText);
            }
        }

        // =========================================================
        // BASIC AUTH
        // POST /api/vr/sessions/{id}/end
        // Body: none
        // =========================================================

        public IEnumerator EndSession(
            int backendSessionId,
            Action<bool, string> onCompleted)
        {
            string url = BuildUrl($"/api/vr/sessions/{backendSessionId}/end");

            using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, ""))
            {
                request.timeout = timeoutSeconds;
                AddBasicAuth(request);

                yield return request.SendWebRequest();

                if (!IsRequestSuccess(request))
                {
                    string error = BuildError(request);
                    LogWarning($"End session failed: {error}");
                    onCompleted?.Invoke(false, error);
                    yield break;
                }

                string response = request.downloadHandler.text;
                Log($"End session OK: {response}");
                onCompleted?.Invoke(true, response);
            }
        }

        // =========================================================
        // BASIC AUTH
        // POST /api/upload
        //
        // multipart/form-data:
        // session_id = backendSessionId
        // file = ZIP
        // =========================================================

        public IEnumerator UploadZip(
            int backendSessionId,
            string zipPath,
            Action<bool, long, string> onCompleted)
        {
            if (backendSessionId <= 0)
            {
                string error = $"Invalid backendSessionId={backendSessionId}";
                LogWarning(error);
                onCompleted?.Invoke(false, 0, error);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(zipPath))
            {
                string error = "ZIP path is empty.";
                LogWarning(error);
                onCompleted?.Invoke(false, 0, error);
                yield break;
            }

            if (!File.Exists(zipPath))
            {
                string error = $"ZIP file does not exist: {zipPath}";
                LogWarning(error);
                onCompleted?.Invoke(false, 0, error);
                yield break;
            }

            byte[] fileBytes;

            try
            {
                fileBytes = File.ReadAllBytes(zipPath);
            }
            catch (Exception e)
            {
                string error = $"Cannot read ZIP file. Path={zipPath}, Error={e.Message}";
                LogWarning(error);
                onCompleted?.Invoke(false, 0, error);
                yield break;
            }

            WWWForm form = new WWWForm();

            form.AddField("session_id", backendSessionId.ToString());

            form.AddBinaryData(
                uploadFileFormFieldName,
                fileBytes,
                Path.GetFileName(zipPath),
                "application/zip"
            );

            string url = BuildUrl("/api/upload");

            using (UnityWebRequest request = UnityWebRequest.Post(url, form))
            {
                request.timeout = timeoutSeconds;
                AddBasicAuth(request);

                Log($"Uploading ZIP. backendSessionId={backendSessionId}, path={zipPath}, bytes={fileBytes.Length}");

                yield return request.SendWebRequest();

                long responseCode = request.responseCode;

                if (!IsRequestSuccess(request))
                {
                    string error = BuildError(request);

                    if (responseCode == 409)
                    {
                        LogWarning($"Upload ZIP conflict 409. Do not retry. {error}");
                    }
                    else
                    {
                        LogWarning($"Upload ZIP failed. {error}");
                    }

                    onCompleted?.Invoke(false, responseCode, error);
                    yield break;
                }

                string response = request.downloadHandler.text;
                Log($"Upload ZIP OK. Code={responseCode}, Response={response}");
                onCompleted?.Invoke(true, responseCode, response);
            }
        }

        // =========================================================
        // PRIVATE HELPERS
        // =========================================================

        private string BuildUrl(string path)
        {
            string cleanBase = baseUrl.TrimEnd('/');
            string cleanPath = path.StartsWith("/") ? path : "/" + path;
            return cleanBase + cleanPath;
        }

        private UnityWebRequest CreateJsonPost(string url, string json)
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");

            return request;
        }

        private void AddBasicAuth(UnityWebRequest request)
        {
            string raw = $"{username}:{password}";
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
            request.SetRequestHeader("Authorization", $"Basic {encoded}");
        }

        private bool IsRequestSuccess(UnityWebRequest request)
        {
#if UNITY_2020_2_OR_NEWER
            return request.result == UnityWebRequest.Result.Success;
#else
            return !request.isNetworkError && !request.isHttpError;
#endif
        }

        private string BuildError(UnityWebRequest request)
        {
            string body = request.downloadHandler != null
                ? request.downloadHandler.text
                : "";

            return $"Code={request.responseCode}, Error={request.error}, Body={body}";
        }

        private void Log(string message)
        {
            if (!debugLogs)
                return;

            Debug.Log($"{LogPrefix} {message}");
        }

        private void LogWarning(string message)
        {
            if (!debugLogs)
                return;

            Debug.LogWarning($"{LogPrefix} {message}");
        }

#if UNITY_EDITOR
        [ContextMenu("DEV Test Server Live")]
        private void DevTestServerLive()
        {
            StartCoroutine(CheckServerLive((success, result) =>
            {
                Debug.Log($"[VR_API_CLIENT_TEST] server_live success={success}, result={result}");
            }));
        }

        [ContextMenu("DEV Test Check Status")]
        private void DevTestCheckStatus()
        {
            StartCoroutine(GetMenuState((success, response, error) =>
            {
                if (!success)
                {
                    Debug.LogWarning($"[VR_API_CLIENT_TEST] menu_state failed: {error}");
                    return;
                }

                int commandCount = response.commands != null ? response.commands.Length : 0;

                Debug.Log(
                    $"[VR_API_CLIENT_TEST] menu_state={response.state}, " +
                    $"session_id={response.session_id}, " +
                    $"user={response.user_identifier}, " +
                    $"commands={commandCount}");
            }));
        }
#endif
    }

    // =========================================================
    // DTO - CONFIRM START
    // =========================================================

    [Serializable]
    public class ConfirmStartRequest
    {
        public int session_id;
    }

    // =========================================================
    // DTO - HEARTBEAT
    // =========================================================

    [Serializable]
    public class HeartbeatRequest
    {
        public float timestamp;
        public int current_room_id;
        public string current_scene;
        public bool recording;
        public HeartbeatTracking tracking;
    }

    [Serializable]
    public class HeartbeatTracking
    {
        public bool face_tracking_available;
        public bool eye_tracking_available;
        public bool physics_tracking_active;
    }

    // =========================================================
    // DTO - COMMANDS
    // =========================================================

    [Serializable]
    public class VrCommandListResponse
    {
        public VrCommandDto[] commands;
    }

    [Serializable]
    public class VrCommandDto
    {
        public int command_id;
        public string command_type;

        // Backend zwraca payload jako obiekt.
        // JsonUtility nie obs³uguje wygodnie dowolnego JSON object.
        // Na razie payload nie jest potrzebny dla END_SESSION.
        public string payload_json;
    }

    [Serializable]
    public class VrMenuStateResponse
    {
        public string state;
        public int session_id;
        public string user_identifier;
        public VrCommandDto[] commands;
    }

    [Serializable]
    public class RejectCommandRequest
    {
        public string reason;
    }
}