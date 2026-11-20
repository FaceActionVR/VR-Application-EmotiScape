using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Telemetry.Metadata;
using Telemetry.Session;
using Telemetry.Writers;
using UnityEngine;


namespace Telemetry.Export
{
    public class SessionZipExporter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TelemetrySessionManager sessionManager;
        [SerializeField] private SessionPathProvider pathProvider;

        [Header("ZIP Settings")]
        [SerializeField] private string zipPrefix = "session";
        [SerializeField] private bool overwriteExistingZip = true;

        [Header("Expected Files")]
        [SerializeField] private FaceTrackingCsvWriter faceWriter;
        [SerializeField] private PhysicsTrackingCsvWriter physicsWriter;
        [SerializeField] private EventLogCsvWriter eventsWriter;
        [SerializeField] private SessionMetadataBuilder metadataBuilder;

        [Header("Debug")]
        [SerializeField] private string lastZipPath;
        [SerializeField] private bool lastExportSucceeded;

        public event Action<string> OnZipCreated;
        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeSession();
        }

        private void OnDisable()
        {
            UnsubscribeSession();
        }

        private void ResolveReferences()
        {
            if (sessionManager == null)
                sessionManager = TelemetrySessionManager.Instance;

            if (pathProvider == null && sessionManager != null)
                pathProvider = sessionManager.GetComponent<SessionPathProvider>();

            if (faceWriter == null)
                faceWriter = FindObjectOfType<FaceTrackingCsvWriter>();

            if (physicsWriter == null)
                physicsWriter = FindObjectOfType<PhysicsTrackingCsvWriter>();

            if (eventsWriter == null)
                eventsWriter = FindObjectOfType<EventLogCsvWriter>();

            if (metadataBuilder == null)
                metadataBuilder = FindObjectOfType<SessionMetadataBuilder>();
        }

        private void SubscribeSession()
        {
            if (sessionManager == null)
                return;

            sessionManager.OnSessionStopped += HandleSessionStopped;
        }

        private void UnsubscribeSession()
        {
            if (sessionManager == null)
                return;

            sessionManager.OnSessionStopped -= HandleSessionStopped;
        }

        private void HandleSessionStopped()
        {
            ExportCurrentSession();
        }

        public void ExportCurrentSession()
        {
            lastExportSucceeded = false;

            if (pathProvider == null)
            {
                Debug.LogError("[SessionZipExporter] Missing SessionPathProvider.");
                return;
            }

            string sessionFolder = pathProvider.CurrentSessionFolder;

            if (string.IsNullOrWhiteSpace(sessionFolder) || !Directory.Exists(sessionFolder))
            {
                Debug.LogError($"[SessionZipExporter] Invalid session folder: {sessionFolder}");
                return;
            }

            string sessionId = sessionManager != null
                ? sessionManager.CurrentSessionId
                : "unknown";

            string zipFileName = $"{zipPrefix}_{SanitizeFileName(sessionId)}.zip";
            string zipPath = Path.Combine(Directory.GetParent(sessionFolder).FullName, zipFileName);

            if (File.Exists(zipPath))
            {
                if (overwriteExistingZip)
                    File.Delete(zipPath);
                else
                {
                    Debug.LogWarning($"[SessionZipExporter] ZIP already exists: {zipPath}");
                    return;
                }
            }

            try
            {
                using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (string fileName in GetExpectedFiles())
                    {
                       AddFileIfExists(
                           archive,
                           sessionFolder,
                           fileName);
                    }
                }

                lastZipPath = zipPath;
                lastExportSucceeded = true;

                Debug.Log($"[SessionZipExporter] ZIP created: {zipPath}");

                OnZipCreated?.Invoke(lastZipPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SessionZipExporter] Failed to create ZIP:\n{e}");
            }
        }

        private void AddFileIfExists(
            ZipArchive archive,
            string sessionFolder,
            string fileName)
        {
            string filePath = Path.Combine(sessionFolder, fileName);

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[SessionZipExporter] Missing file, skipped: {fileName}");
                return;
            }

            archive.CreateEntryFromFile(filePath, fileName);
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');

            return value;
        }

        private IEnumerable<string> GetExpectedFiles()
        {
            if (faceWriter != null)
                yield return faceWriter.FileName;

            if (physicsWriter != null)
                yield return physicsWriter.FileName;

            if (eventsWriter != null)
                yield return eventsWriter.FileName;

            if (metadataBuilder != null)
                yield return metadataBuilder.FileName;
        }
    }
}