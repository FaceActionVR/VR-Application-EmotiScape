using System.Collections;
using System;
using System.IO;
using Telemetry.Export;
using UnityEngine;
using UnityEngine.Networking;


namespace Telemetry.Upload
{
    public class SessionUploadClient : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SessionZipExporter zipExporter;

        [Header("Upload Settings")]
        [SerializeField] private bool uploadAutomatically = true;

        [Tooltip("Pe³ny endpoint backendu przyjmuj¹cy ZIP, np. http://192.168.50.209:8000")]
        [SerializeField] private string serwerUrl = "http://192.168.50.209:8000";
        [Tooltip("Pe³ny endpoint backendu przyjmuj¹cy ZIP, np. /api/upload_session_zip")]
        [SerializeField] private string uploadApi = "/api/upload";

        [Tooltip("Nazwa pola multipart/form-data po stronie backendu.")]
        [SerializeField] private string fileFormFieldName = "file";

        public event Action<string> OnUploadSucceeded;
        public event Action<string, string> OnUploadFailed;

        [Header("Debug")]
        [SerializeField] private bool isUploading;
        [SerializeField] private bool lastUploadSucceeded;
        [SerializeField] private string lastUploadedZipPath;
        [SerializeField] private long lastResponseCode;
        [TextArea]
        [SerializeField] private string lastResponseText;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (zipExporter != null)
                zipExporter.OnZipCreated += HandleZipCreated;
        }

        private void OnDisable()
        {
            if (zipExporter != null)
                zipExporter.OnZipCreated -= HandleZipCreated;
        }

        private void ResolveReferences()
        {
            if (zipExporter == null)
                zipExporter = FindObjectOfType<SessionZipExporter>();
        }

        private void HandleZipCreated(string zipPath)
        {
            if (!uploadAutomatically)
                return;

            UploadZip(zipPath);
        }

        public void UploadZip(string zipPath)
        {
            if (isUploading)
            {
                Debug.LogWarning("[SessionUploadClient] Upload already in progress.");
                return;
            }

            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            {
                Debug.LogError($"[SessionUploadClient] ZIP file does not exist: {zipPath}");
                return;
            }

            StartCoroutine(UploadZipCoroutine(zipPath));
        }

        private IEnumerator UploadZipCoroutine(string zipPath)
        {
            isUploading = true;
            lastUploadSucceeded = false;
            lastUploadedZipPath = zipPath;
            lastResponseCode = 0;
            lastResponseText = "";

            byte[] fileBytes;

            string currentSessionId = "";
            if (Telemetry.Session.TelemetrySessionManager.Instance != null)
            {
                currentSessionId = Telemetry.Session.TelemetrySessionManager.Instance.CurrentSessionId;
            }

            if (string.IsNullOrWhiteSpace(currentSessionId))
            {
                currentSessionId = Path.GetFileNameWithoutExtension(zipPath);

                if (currentSessionId.StartsWith("session_"))
                    currentSessionId = currentSessionId.Substring("session_".Length);

                Debug.LogWarning($"[SessionUploadClient] Session ID was empty. Fallback session_id={currentSessionId}");
            }

            try
            {
                fileBytes = File.ReadAllBytes(zipPath);
            }
            catch (System.Exception e)
            {
                isUploading = false;
                Debug.LogError($"[SessionUploadClient] Failed to read ZIP:\n{e}");
                yield break;
            }

            WWWForm form = new WWWForm();
            form.AddField("session_id", currentSessionId);
            form.AddBinaryData(
                fileFormFieldName,
                fileBytes,
                Path.GetFileName(zipPath),
                "application/zip"
            );

            using UnityWebRequest request = UnityWebRequest.Post(serwerUrl + uploadApi, form);

            Debug.Log($"[SessionUploadClient] Uploading ZIP to {serwerUrl + uploadApi}: {zipPath}");

            yield return request.SendWebRequest();

            lastResponseCode = request.responseCode;
            lastResponseText = request.downloadHandler != null
                ? request.downloadHandler.text
                : "";

            if (request.result == UnityWebRequest.Result.Success)
            {
                lastUploadSucceeded = true;
                Debug.Log($"[SessionUploadClient] Upload succeeded. Response: {lastResponseText}");
                OnUploadSucceeded?.Invoke(lastUploadedZipPath);
            }
            else
            {
                lastUploadSucceeded = false;

                Debug.LogError(
                    $"[SessionUploadClient] Upload failed. " +
                    $"Code: {request.responseCode}, Error: {request.error}, Response: {lastResponseText}"
                );

                OnUploadFailed?.Invoke(lastUploadedZipPath, request.error);
            }

            isUploading = false;
        }
    }
}